#include "d3d/host.h"

#include <algorithm>
#include <filesystem>
#include <memory>
#include <mutex>
#include <string>
#include <unordered_map>
#include <unordered_set>
#include <vector>

#include <fmt/format.h>
#include <rex/cvar.h>
#include <rex/logging.h>

#include "d3d/census.h"
#include "d3d/sink.h"
#include "native/host_paths.h"
#include "native/native_renderer.h"
#include "native/shader_library.h"
#include "native/shader_topup.h"
#include "native/topup_converter.h"

REXCVAR_DEFINE_STRING(ts4_shader_dump_dir, "", "TopSpin4",
                      "Save each unique shader container the game creates or registers to this "
                      "directory, for offline conversion (scripts/convert-shaders.ps1)");

// The shader bookkeeping is WWE All Stars' (src/d3d_census.cpp, ADR-0004); the object layout
// is the same in both of Top Spin 4's copies (docs/research/native-renderer.md).
namespace topspin4::d3d {
namespace {

uint32_t LoadBE32(const uint8_t* p) {
  return uint32_t(p[0]) << 24 | uint32_t(p[1]) << 16 | uint32_t(p[2]) << 8 | p[3];
}

// Shader containers start with flags 0x102A11xx, then the virtual (header and constant
// tables) and physical (microcode) sizes; on disc the two parts are contiguous. Shader
// objects keep a copy of the header and a pointer to the microcode, so the container is
// rebuilt from both (ShaderRegistered).
bool ValidContainerHeader(const uint8_t* header) {
  const uint32_t flags = LoadBE32(header);
  const uint32_t size = LoadBE32(header + 4) + LoadBE32(header + 8);
  return (flags & 0xFFFFFF00) == 0x102A1100 && size != 0 && size <= (1u << 20);
}

uint64_t Fnv1a(const uint8_t* data, size_t size) {
  uint64_t hash = 14695981039346656037ull;
  for (size_t i = 0; i < size; ++i) {
    hash = (hash ^ data[i]) * 1099511628211ull;
  }
  return hash;
}

void DumpShaderContainer(const std::vector<uint8_t>& container, bool is_pixel_shader) {
  const std::string dir = REXCVAR_GET(ts4_shader_dump_dir);
  if (dir.empty()) {
    return;
  }
  const uint64_t hash = Fnv1a(container.data(), container.size());
  static std::mutex mutex;
  static std::unordered_set<uint64_t> seen;
  std::lock_guard lock(mutex);
  if (!seen.insert(hash).second) {
    return;
  }
  const std::filesystem::path folder = native::TextPath(dir);
  std::error_code error;
  std::filesystem::create_directories(folder, error);
  const auto path = folder / fmt::format("{}_{:016x}.bin", is_pixel_shader ? "ps" : "vs", hash);
  if (FILE* file = native::OpenFile(path, "wb")) {
    std::fwrite(container.data(), 1, container.size(), file);
    std::fclose(file);
  }
}

// Objects built by Create*Shader, registered from the caller's container (ShaderCreated),
// with a hash of the header copy the object holds. D3D registers them again later; the
// register hook's rebuild of these objects is not the shader (WWE ticket 29), so it must not
// replace them. Once the game frees an object and its memory holds another shader, the header
// differs and the register hook takes the new shader as usual.
std::mutex g_created_mutex;
std::unordered_map<uint32_t, uint64_t> g_created_headers;

uint64_t HeaderHash(const uint8_t* header) {
  return Fnv1a(header, std::min<uint32_t>(LoadBE32(header + 4), 1u << 16));
}

// Header copy and microcode pointer offsets in the shader objects.
constexpr uint32_t kVertexHeader = 0x368, kVertexMicrocode = 0x20;
constexpr uint32_t kPixelHeader = 0x28, kPixelMicrocode = 0x18;

void ShaderRegistered(Module, const uint8_t* base, uint32_t object, bool is_pixel_shader) {
  const uint32_t header_offset = is_pixel_shader ? kPixelHeader : kVertexHeader;
  const uint32_t microcode_offset = is_pixel_shader ? kPixelMicrocode : kVertexMicrocode;
  {
    std::lock_guard lock(g_created_mutex);
    const auto created = g_created_headers.find(object);
    if (created != g_created_headers.end()) {
      if (ValidContainerHeader(base + object + header_offset) &&
          HeaderHash(base + object + header_offset) == created->second) {
        return;  // the same Create*Shader shader, registered again
      }
      g_created_headers.erase(created);
    }
  }
  const uint8_t* header = base + object + header_offset;
  if (!ValidContainerHeader(header)) {
    REXLOG_WARN("shader register: {} object {:08X} has no container header (flags {:08X})",
                is_pixel_shader ? "pixel" : "vertex", object, LoadBE32(header));
    return;
  }
  const uint32_t header_size = LoadBE32(header + 4);
  const uint32_t microcode_size = LoadBE32(header + 8);
  const uint32_t microcode = LoadBE32(base + object + microcode_offset);
  std::vector<uint8_t> container(header, header + header_size);
  container.insert(container.end(), base + microcode, base + microcode + microcode_size);
  DumpShaderContainer(container, is_pixel_shader);
  native::shader_library::OnShaderCreated(container.data(), object, is_pixel_shader);
}

// Create*Shader: the caller's container is the shader itself, so it replaces what the register
// hook recorded. They are also the only path for shaders the game compiles at runtime, which
// a pack converted from disc lacks: a miss goes to the shader top-up folder.
std::unique_ptr<native::ShaderTopUp> g_topup;

void ShaderCreated(Module, const uint8_t* base, uint32_t container, uint32_t object,
                   bool is_pixel_shader) {
  const uint8_t* header = base + container;
  if (!object || !ValidContainerHeader(header)) {
    return;
  }
  const uint32_t size = LoadBE32(header + 4) + LoadBE32(header + 8);
  const std::vector<uint8_t> bytes(header, header + size);
  DumpShaderContainer(bytes, is_pixel_shader);
  const bool packed =
      native::shader_library::OnShaderCreated(bytes.data(), object, is_pixel_shader);
  if (g_topup && g_topup->Offer(bytes.data(), bytes.size(), is_pixel_shader, packed)) {
    REXLOG_INFO("shader top-up: wrote {}",
                native::ShaderTopUp::FileName(bytes.data(), bytes.size(), is_pixel_shader));
  }
  // Converted in the background and used once ready; a file an earlier session wrote is
  // converted too.
  if (!packed && native::IsEnabled()) {
    native::topup_converter::Offer(bytes.data(), bytes.size(), is_pixel_shader);
  }
  const uint8_t* object_header = base + object + (is_pixel_shader ? kPixelHeader : kVertexHeader);
  if (ValidContainerHeader(object_header)) {
    std::lock_guard lock(g_created_mutex);
    g_created_headers[object] = HeaderHash(object_header);
  }
}

void Present(Module module, const uint8_t* base, uint32_t device, uint32_t front_buffer) {
  if (census::Enabled()) {
    census::OnPresent(module, device);
  }
  native::OnPresent(base, device, front_buffer);
}

void SetRenderTarget(Module, const uint8_t* base, uint32_t index, uint32_t surface) {
  native::OnSetRenderTarget(base, index, surface);
}

void Clear(Module, const uint8_t* base, uint32_t device, uint32_t flags, const int32_t rect[4],
           uint32_t color, float depth, uint32_t stencil) {
  native::OnClear(base, device, flags, rect, color, depth, stencil);
}

void Resolve(Module, const uint8_t* base, uint32_t device, uint32_t flags, uint32_t destination,
             uint32_t level) {
  native::OnResolve(base, device, flags, destination, level);
}

void SetStreamSource(Module, uint32_t stream, uint32_t stride) {
  native::OnSetStreamSource(stream, stride);
}

void DrawIndexed(Module, const uint8_t* base, uint32_t device, uint32_t primitive,
                 int32_t base_vertex, uint32_t start_index, uint32_t index_count) {
  native::OnDrawIndexed(base, device, primitive, base_vertex, start_index, index_count);
}

void DrawNonIndexed(Module, const uint8_t* base, uint32_t device, uint32_t primitive,
                    uint32_t start_vertex, uint32_t vertex_count) {
  native::OnDrawNonIndexed(base, device, primitive, start_vertex, vertex_count);
}

void DrawVertices(Module, const uint8_t* base, uint32_t device, uint32_t primitive,
                  uint32_t vertex_count, uint32_t stride, uint32_t vertices, uint32_t indices,
                  uint32_t index_count, bool index32, int32_t base_vertex) {
  native::OnDrawVertices(base, device, primitive, vertex_count, stride, vertices, indices,
                         index_count, index32, base_vertex);
}

}  // namespace

void InstallHost(const std::filesystem::path& cache_root) {
  Sink& sink = GetSink();
  if (census::Enabled()) {
    sink.present = &Present;
    sink.mirror_commit = &census::OnMirrorCommit;
    sink.call_entry = &census::OnCallEntry;
    sink.call_exit = &census::OnCallExit;
  }
  // Shaders are tracked on Classic too, for --ts4_shader_dump_dir and the top-up folder.
  sink.shader_created = &ShaderCreated;
  sink.shader_registered = &ShaderRegistered;
  g_topup = std::make_unique<native::ShaderTopUp>(cache_root);
  if (!native::IsEnabled()) {
    return;
  }
  sink.present = &Present;
  sink.set_render_target = &SetRenderTarget;
  sink.clear = &Clear;
  sink.resolve = &Resolve;
  sink.set_stream_source = &SetStreamSource;
  sink.draw_indexed = &DrawIndexed;
  sink.draw_non_indexed = &DrawNonIndexed;
  sink.draw_vertices = &DrawVertices;
}

void OnSessionEnd() {
  census::LogFinal();
  native::OnExit();
}

}  // namespace topspin4::d3d
