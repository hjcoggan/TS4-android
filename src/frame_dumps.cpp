// Frame dumps, exit after a frame and the session report for scene runs (see frame_dumps.h).

#include "frame_dumps.h"
#include "crash_report.h"
#include "relaunch.h"

#include <chrono>
#include <cstdio>
#include <cstdlib>
#include <concepts>
#include <cstring>
#include <filesystem>
#include <thread>

#include <rex/cvar.h>
#include <rex/logging.h>
#include <rex/runtime.h>
#include <rex/system/external_frame.h>
#include <rex/system/guest_swaps.h>
#include <rex/ui/presenter.h>

#if defined(_WIN32)
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>

#include "thirdparty/renderdoc_app.h"
#endif

REXCVAR_DEFINE_STRING(ts4_dump_frames, "", "TopSpin4",
                      "Scene runs: guest frames to save as PNGs into ts4_dump_dir, "
                      "comma-separated (src/frame_dumps.h)");
REXCVAR_DEFINE_STRING(ts4_dump_dir, "", "TopSpin4",
                      "Scene runs: the directory ts4_dump_frames writes frame-NNNNNN.png to");
REXCVAR_DEFINE_STRING(ts4_dump_presented, "", "TopSpin4",
                      "Scene runs: also save each dumped frame as the presenter shows it in an "
                      "output of this size (WxH, e.g. 2560x1440): "
                      "frame-NNNNNN-presented.png next to the dump, with the presenter's "
                      "upscaling and sharpening. Needs the presenter's CapturePresentedOutput");
REXCVAR_DEFINE_INT32(ts4_exit_after_frame, -1, "TopSpin4",
                     "Scene runs: after this guest frame is shown (and dumped), write the "
                     "session report and exit");
REXCVAR_DEFINE_STRING(ts4_session_report, "", "TopSpin4",
                      "Scene runs: the session report (JSON) written on ts4_exit_after_frame");
REXCVAR_DEFINE_STRING(ts4_frame_times_csv, "", "TopSpin4",
                      "Write the host time of every frame shown (frame,ms) to this CSV, for "
                      "scripts/frame_pacing.py");
REXCVAR_DEFINE_STRING(ts4_frame_hash_csv, "", "TopSpin4",
                      "Write a sampled hash of every frame shown (frame,ms,hash) to this CSV: "
                      "when the picture changes, for movie cadence");
REXCVAR_DEFINE_BOOL(ts4_fps_log, false, "TopSpin4",
                    "Log the guest frame rate (VdSwap calls) once a second, for comparison "
                    "with the Oracle's scripts/edge.ps1 -FrameLog");
REXCVAR_DEFINE_INT32(ts4_renderdoc_frame, -1, "TopSpin4",
                     "With RenderDoc attached (renderdoccmd capture): capture this guest frame, "
                     "everything the GPU does between the swap before it and its own");

namespace topspin4::frame_dumps {

namespace {

// fopen through the wide path on Windows: a narrow one goes through the ANSI code page, and the
// folders come from the command line in UTF-8.
std::string Utf8Text(const std::filesystem::path& path) {
  const std::u8string text = path.u8string();
  return std::string(text.begin(), text.end());
}

std::filesystem::path Utf8Path(const std::string& text) {
  return std::filesystem::path(std::u8string(text.begin(), text.end()));
}

FILE* OpenFile(const std::filesystem::path& path, const char* mode) {
#if defined(_WIN32)
  const std::wstring wide_mode(mode, mode + std::strlen(mode));
  return _wfopen(path.c_str(), wide_mode.c_str());
#else
  return std::fopen(path.c_str(), mode);  // narrow path ok
#endif
}

struct Dump {
  uint64_t frame;
  std::string file;
  uint32_t width, height;
  uint8_t max_luma;  // brightest pixel, so a black frame shows without opening it
  // --ts4_dump_presented: the same frame as the presenter shows it; empty when not saved.
  std::string presented_file;
  uint32_t presented_width = 0, presented_height = 0;
};

rex::Runtime* g_runtime = nullptr;
std::set<uint64_t> g_frames;
OutputSize g_presented_size;  // 0x0 when --ts4_dump_presented is not set
std::filesystem::path g_dir;
int64_t g_exit_after = -1;
std::filesystem::path g_report;
std::string (*g_session_end)() = nullptr;
std::chrono::steady_clock::time_point g_start;
FILE* g_frame_times = nullptr;  // --ts4_frame_times_csv
FILE* g_frame_hashes = nullptr;  // --ts4_frame_hash_csv
std::vector<Dump> g_dumps;  // GPU thread only, as is every function below but Install
int64_t g_renderdoc_frame = -1;
#if defined(_WIN32)
RENDERDOC_API_1_1_2* g_renderdoc = nullptr;
#endif

// Start a RenderDoc capture as the frame before the wanted one is shown, end it with the wanted
// one: the capture then holds exactly that guest frame's GPU work, on the GPU thread that
// submits it.
void CaptureWithRenderDoc(uint64_t frame) {
#if defined(_WIN32)
  if (!g_renderdoc) {
    return;
  }
  if (int64_t(frame) + 1 == g_renderdoc_frame) {
    g_renderdoc->StartFrameCapture(nullptr, nullptr);
  } else if (int64_t(frame) == g_renderdoc_frame) {
    const bool captured = g_renderdoc->EndFrameCapture(nullptr, nullptr) == 1;
    REXLOG_INFO("frame dumps: RenderDoc capture of frame {} {}", frame,
                captured ? "written" : "failed");
  }
#else
  (void)frame;
#endif
}

std::string JsonString(const std::string& text) {
  std::string out = "\"";
  for (char c : text) {
    if (c == '"' || c == '\\') out += '\\';
    out += c;
  }
  return out + "\"";
}

// Whether the SDK's presenter can paint its guest output for an output of a given size: the
// presented-output API (see frame_dumps.h) is a rexglue patch that may not be in the tree yet.
// A concept, so a presenter without it is not a compile error.
template <typename P>
concept PaintsOutput = requires(P& presenter, rex::ui::RawImage& image) {
  { presenter.CapturePresentedOutput(image, uint32_t(0), uint32_t(0)) } -> std::same_as<bool>;
};

template <typename P>
bool CapturePresentedFrom(P& presenter, rex::ui::RawImage& image, OutputSize size) {
  if constexpr (PaintsOutput<P>) {
    return presenter.CapturePresentedOutput(image, size.width, size.height);
  } else {
    (void)presenter, (void)image, (void)size;
    return false;
  }
}

// The image as a PNG at path; false (and a warning) when it cannot be written. max_luma gets the
// brightest pixel.
bool WritePng(const rex::ui::RawImage& image, const std::filesystem::path& path,
              uint8_t& max_luma) {
  std::vector<uint8_t> rgb(size_t(image.width) * image.height * 3);
  max_luma = 0;
  for (uint32_t y = 0; y < image.height; ++y) {
    for (uint32_t x = 0; x < image.width; ++x) {
      const uint8_t* in = &image.data[y * image.stride + x * 4];
      uint8_t* out = &rgb[(size_t(y) * image.width + x) * 3];
      out[0] = in[0], out[1] = in[1], out[2] = in[2];
      max_luma = std::max(max_luma, uint8_t((299 * in[0] + 587 * in[1] + 114 * in[2]) / 1000));
    }
  }
  FILE* file = OpenFile(path, "wb");
  if (!file) {
    REXLOG_WARN("frame dumps: cannot write {}", Utf8Text(path));
    return false;
  }
  const std::vector<uint8_t> png = EncodePngRgb(rgb.data(), image.width, image.height);
  const bool written = std::fwrite(png.data(), 1, png.size(), file) == png.size();
  if (std::fclose(file) != 0 || !written) {
    REXLOG_WARN("frame dumps: writing {} failed", Utf8Text(path));
    return false;
  }
  return true;
}

// --ts4_dump_presented: the frame as the presenter shows it at the output size, beside the dump.
// Best effort: a failure costs this file only, and the report says which dumps have one.
void SavePresented(rex::ui::Presenter& presenter, uint64_t frame, Dump& dump) {
  rex::ui::RawImage image;
  if (!CapturePresentedFrom(presenter, image, g_presented_size)) {
    REXLOG_WARN("frame dumps: frame {} has no presented image at {}x{}", frame,
                g_presented_size.width, g_presented_size.height);
    return;
  }
  const std::filesystem::path path = g_dir / PresentedFileName(frame);
  uint8_t max_luma = 0;
  if (!WritePng(image, path, max_luma)) {
    return;
  }
  REXLOG_INFO("frame dumps: frame {} presented ({}x{}, max luma {}) to {}", frame, image.width,
              image.height, max_luma, Utf8Text(path));
  dump.presented_file = Utf8Text(path.filename());
  dump.presented_width = image.width;
  dump.presented_height = image.height;
}

void SaveDump(uint64_t frame) {
  auto* graphics = g_runtime ? g_runtime->graphics_system() : nullptr;
  rex::ui::Presenter* presenter = graphics ? graphics->presenter() : nullptr;
  rex::ui::RawImage image;
  if (!presenter || !presenter->CaptureGuestOutput(image)) {
    REXLOG_WARN("frame dumps: frame {} not saved: no presenter output", frame);
    return;
  }
  if (rex::system::external_frame::IsEnabled()) {
    // The swap shows the Native frame with its own number unless the GPU ran too far behind
    // (rex/system/swap_choice.h); then this image is of another frame than Native's own dump.
    const uint64_t shown = rex::system::external_frame::LastSwappedSequence();
    if (shown != frame) {
      REXLOG_WARN("frame dumps: frame {} shows Native's frame {}, not its own", frame, shown);
    }
  }
  const std::filesystem::path path = g_dir / DumpFileName(frame);
  uint8_t max_luma = 0;
  if (!WritePng(image, path, max_luma)) {
    return;
  }
  REXLOG_INFO("frame dumps: frame {} ({}x{}, max luma {}) to {}", frame, image.width,
              image.height, max_luma, Utf8Text(path));
  Dump dump{frame, Utf8Text(path.filename()), image.width, image.height, max_luma};
  if (g_presented_size.width) {
    SavePresented(*presenter, frame, dump);
  }
  g_dumps.push_back(std::move(dump));
}

void WriteReport(uint64_t frame) {
  const std::string extra = g_session_end ? g_session_end() : std::string();
  if (g_report.empty()) {
    return;
  }
  const auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(
      std::chrono::steady_clock::now() - g_start);
  std::string json = fmt::format(
      "{{\n \"exit_after_frame\": {},\n \"guest_swaps\": {},\n \"elapsed_ms\": {},\n"
      " \"dumps_requested\": {},\n \"dumps\": [",
      frame, rex::system::GuestSwapCount(), elapsed.count(), g_frames.size());
  for (size_t i = 0; i < g_dumps.size(); ++i) {
    const Dump& d = g_dumps[i];
    json += fmt::format("{}\n  {{\"frame\": {}, \"file\": {}, \"width\": {}, \"height\": {}, "
                        "\"max_luma\": {}",
                        i ? "," : "", d.frame, JsonString(d.file), d.width, d.height, d.max_luma);
    if (!d.presented_file.empty()) {
      json += fmt::format(", \"presented_file\": {}, \"presented_width\": {}, "
                          "\"presented_height\": {}",
                          JsonString(d.presented_file), d.presented_width, d.presented_height);
    }
    json += "}";
  }
  json += "\n ]";
  if (!extra.empty()) {
    json += ",\n " + extra;
  }
  json += "\n}\n";
  FILE* file = OpenFile(g_report, "wb");
  const bool written = file && std::fwrite(json.data(), 1, json.size(), file) == json.size();
  if (!file || std::fclose(file) != 0 || !written) {
    REXLOG_WARN("frame dumps: cannot write the session report {}", Utf8Text(g_report));
  }
}

// Once a second, the guest swaps since the last line over the time since it. Its own thread,
// so a stalled guest still logs (0 fps) instead of going quiet.
void LogFrameRate() {
  using Clock = std::chrono::steady_clock;
  auto last_time = Clock::now();
  uint64_t last_swaps = rex::system::GuestSwapCount();
  for (auto next = last_time + std::chrono::seconds(1);; next += std::chrono::seconds(1)) {
    std::this_thread::sleep_until(next);
    const auto now = Clock::now();
    const uint64_t swaps = rex::system::GuestSwapCount();
    const double seconds = std::chrono::duration<double>(now - last_time).count();
    REXLOG_INFO("fps: {:.1f} (guest swaps {})", (swaps - last_swaps) / seconds, swaps);
    last_time = now;
    last_swaps = swaps;
  }
}

void OnFrameShown(uint64_t frame) {
  topspin4::relaunch::OnFrameShown(frame);
  if (g_frame_times) {
    const double ms = std::chrono::duration<double, std::milli>(
                          std::chrono::steady_clock::now() - g_start)
                          .count();
    std::fprintf(g_frame_times, "%llu,%.3f\n", static_cast<unsigned long long>(frame), ms);
    // A run killed at its deadline still leaves the times up to the last second.
    if (frame % 60 == 0) std::fflush(g_frame_times);
  }
  if (g_frame_hashes) {
    auto* graphics = g_runtime ? g_runtime->graphics_system() : nullptr;
    rex::ui::Presenter* presenter = graphics ? graphics->presenter() : nullptr;
    rex::ui::RawImage image;
    const double ms = std::chrono::duration<double, std::milli>(
                          std::chrono::steady_clock::now() - g_start)
                          .count();
    if (presenter && presenter->CaptureGuestOutput(image)) {
      std::fprintf(g_frame_hashes, "%llu,%.3f,%016llx\n", static_cast<unsigned long long>(frame),
                   ms,
                   static_cast<unsigned long long>(SampledFrameHash(
                       image.data.data(), image.width, image.height, image.stride)));
      if (frame % 60 == 0) std::fflush(g_frame_hashes);
    }
  }
  CaptureWithRenderDoc(frame);
  if (g_frames.contains(frame)) {
    SaveDump(frame);
  }
  if (int64_t(frame) == g_exit_after) {
    REXLOG_INFO("frame dumps: exit after frame {}", frame);
    WriteReport(frame);
    rex::FlushLogging();
    std::_Exit(0);
  }
  topspin4::crash::CrashIfAskedAtFrame(frame);
}

}  // namespace

void SetSessionEnd(std::string (*session_end)()) { g_session_end = session_end; }

void Install(rex::Runtime* runtime) {
  g_runtime = runtime;
  g_start = std::chrono::steady_clock::now();
  if (!ParseFrameList(REXCVAR_GET(ts4_dump_frames), g_frames)) {
    REXLOG_ERROR("frame dumps: bad --ts4_dump_frames list '{}'", REXCVAR_GET(ts4_dump_frames));
  }
  g_dir = Utf8Path(REXCVAR_GET(ts4_dump_dir));
  g_exit_after = REXCVAR_GET(ts4_exit_after_frame);
  g_report = Utf8Path(REXCVAR_GET(ts4_session_report));
  g_renderdoc_frame = REXCVAR_GET(ts4_renderdoc_frame);
#if defined(_WIN32)
  if (g_renderdoc_frame >= 1) {
    // RenderDoc is loaded into the process only when the game is started under it.
    HMODULE module = GetModuleHandleA("renderdoc.dll");
    auto get_api = module ? reinterpret_cast<pRENDERDOC_GetAPI>(
                                GetProcAddress(module, "RENDERDOC_GetAPI"))
                          : nullptr;
    if (!get_api ||
        get_api(eRENDERDOC_API_Version_1_1_2, reinterpret_cast<void**>(&g_renderdoc)) != 1) {
      REXLOG_ERROR("frame dumps: --ts4_renderdoc_frame needs the game started under RenderDoc "
                   "(scripts/rdoc.ps1 capture)");
      g_renderdoc = nullptr;
    }
  }
#endif
  if (!ParseOutputSize(REXCVAR_GET(ts4_dump_presented), g_presented_size)) {
    REXLOG_ERROR("frame dumps: bad --ts4_dump_presented size '{}' (want WxH, e.g. 2560x1440)",
                 REXCVAR_GET(ts4_dump_presented));
  }
  if (g_presented_size.width && !PaintsOutput<rex::ui::Presenter>) {
    REXLOG_ERROR("frame dumps: --ts4_dump_presented needs rex::ui::Presenter::"
                 "CapturePresentedOutput, which this build's SDK lacks; only the guest output is "
                 "dumped");
  }
  if (!g_frames.empty()) {
    if (g_dir.empty()) {
      REXLOG_ERROR("frame dumps: --ts4_dump_frames needs --ts4_dump_dir");
      g_frames.clear();
    } else {
      std::error_code error;
      std::filesystem::create_directories(g_dir, error);
      if (error) {
        REXLOG_ERROR("frame dumps: cannot create {}: {}", Utf8Text(g_dir), error.message());
      }
    }
  }
  if (REXCVAR_GET(ts4_fps_log)) {
    std::thread(LogFrameRate).detach();
  }
  if (const std::string path = REXCVAR_GET(ts4_frame_times_csv); !path.empty()) {
    g_frame_times = OpenFile(Utf8Path(path), "w");
    if (g_frame_times) {
      std::fputs("frame,ms\n", g_frame_times);
    } else {
      REXLOG_ERROR("frame dumps: cannot write {}", path);
    }
  }
  if (const std::string path = REXCVAR_GET(ts4_frame_hash_csv); !path.empty()) {
    g_frame_hashes = OpenFile(Utf8Path(path), "w");
    if (g_frame_hashes) {
      std::fputs("frame,ms,hash\n", g_frame_hashes);
    } else {
      REXLOG_ERROR("frame dumps: cannot write {}", path);
    }
  }
  if (g_frames.empty() && g_exit_after < 0 && g_renderdoc_frame < 1 && !g_frame_times &&
      !g_frame_hashes &&
      topspin4::crash::CrashFrameAsked() < 0 && !topspin4::relaunch::WaitingForFirstFrame()) {
    return;
  }
  rex::system::SetFrameShownObserver(&OnFrameShown);
}

}  // namespace topspin4::frame_dumps
