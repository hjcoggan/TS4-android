// Frame dumps, exit after a frame and the session report for scene runs (see frame_dumps.h).

#include "frame_dumps.h"

#include <chrono>
#include <cstdio>
#include <cstdlib>
#include <filesystem>

#include <rex/cvar.h>
#include <rex/logging.h>
#include <rex/runtime.h>
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
REXCVAR_DEFINE_INT32(ts4_exit_after_frame, -1, "TopSpin4",
                     "Scene runs: after this guest frame is shown (and dumped), write the "
                     "session report and exit");
REXCVAR_DEFINE_STRING(ts4_session_report, "", "TopSpin4",
                      "Scene runs: the session report (JSON) written on ts4_exit_after_frame");
REXCVAR_DEFINE_INT32(ts4_renderdoc_frame, -1, "TopSpin4",
                     "With RenderDoc attached (renderdoccmd capture): capture this guest frame, "
                     "everything the GPU does between the swap before it and its own");

namespace topspin4::frame_dumps {

namespace {

struct Dump {
  uint64_t frame;
  std::string file;
  uint32_t width, height;
  uint8_t max_luma;  // brightest pixel, so a black frame shows without opening it
};

rex::Runtime* g_runtime = nullptr;
std::set<uint64_t> g_frames;
std::filesystem::path g_dir;
int64_t g_exit_after = -1;
std::filesystem::path g_report;
std::chrono::steady_clock::time_point g_start;
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

void SaveDump(uint64_t frame) {
  auto* graphics = g_runtime ? g_runtime->graphics_system() : nullptr;
  rex::ui::Presenter* presenter = graphics ? graphics->presenter() : nullptr;
  rex::ui::RawImage image;
  if (!presenter || !presenter->CaptureGuestOutput(image)) {
    REXLOG_WARN("frame dumps: frame {} not saved: no presenter output", frame);
    return;
  }
  std::vector<uint8_t> rgb(size_t(image.width) * image.height * 3);
  uint8_t max_luma = 0;
  for (uint32_t y = 0; y < image.height; ++y) {
    for (uint32_t x = 0; x < image.width; ++x) {
      const uint8_t* in = &image.data[y * image.stride + x * 4];
      uint8_t* out = &rgb[(size_t(y) * image.width + x) * 3];
      out[0] = in[0], out[1] = in[1], out[2] = in[2];
      max_luma = std::max(max_luma, uint8_t((299 * in[0] + 587 * in[1] + 114 * in[2]) / 1000));
    }
  }
  const std::filesystem::path path = g_dir / DumpFileName(frame);
  FILE* file = std::fopen(path.string().c_str(), "wb");
  if (!file) {
    REXLOG_WARN("frame dumps: cannot write {}", path.string());
    return;
  }
  const std::vector<uint8_t> png = EncodePngRgb(rgb.data(), image.width, image.height);
  const bool written = std::fwrite(png.data(), 1, png.size(), file) == png.size();
  if (std::fclose(file) != 0 || !written) {
    REXLOG_WARN("frame dumps: writing {} failed", path.string());
    return;
  }
  REXLOG_INFO("frame dumps: frame {} ({}x{}, max luma {}) to {}", frame, image.width,
              image.height, max_luma, path.string());
  g_dumps.push_back({frame, path.filename().string(), image.width, image.height, max_luma});
}

void WriteReport(uint64_t frame) {
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
                        "\"max_luma\": {}}}",
                        i ? "," : "", d.frame, JsonString(d.file), d.width, d.height, d.max_luma);
  }
  json += "\n ]\n}\n";
  FILE* file = std::fopen(g_report.string().c_str(), "wb");
  const bool written = file && std::fwrite(json.data(), 1, json.size(), file) == json.size();
  if (!file || std::fclose(file) != 0 || !written) {
    REXLOG_WARN("frame dumps: cannot write the session report {}", g_report.string());
  }
}

void OnFrameShown(uint64_t frame) {
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
}

}  // namespace

void Install(rex::Runtime* runtime) {
  g_runtime = runtime;
  g_start = std::chrono::steady_clock::now();
  if (!ParseFrameList(REXCVAR_GET(ts4_dump_frames), g_frames)) {
    REXLOG_ERROR("frame dumps: bad --ts4_dump_frames list '{}'", REXCVAR_GET(ts4_dump_frames));
  }
  g_dir = REXCVAR_GET(ts4_dump_dir);
  g_exit_after = REXCVAR_GET(ts4_exit_after_frame);
  g_report = REXCVAR_GET(ts4_session_report);
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
  if (!g_frames.empty()) {
    if (g_dir.empty()) {
      REXLOG_ERROR("frame dumps: --ts4_dump_frames needs --ts4_dump_dir");
      g_frames.clear();
    } else {
      std::error_code error;
      std::filesystem::create_directories(g_dir, error);
      if (error) {
        REXLOG_ERROR("frame dumps: cannot create {}: {}", g_dir.string(), error.message());
      }
    }
  }
  if (g_frames.empty() && g_exit_after < 0 && g_renderdoc_frame < 1) {
    return;
  }
  rex::system::SetFrameShownObserver(&OnFrameShown);
}

}  // namespace topspin4::frame_dumps
