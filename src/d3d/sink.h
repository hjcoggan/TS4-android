// The D3D sink: how the D3D hooks reach the host. Top Spin 4 links two copies of the XDK's
// Direct3D 9, one in Loader and one in Swing (docs/research/native-renderer.md), so each
// hook is built into its own Module's DLL (src/d3d/hooks.h, generated from
// symbols/d3d-functions.csv by cmake/d3d_hooks.cmake). The DLLs cannot link against the exe,
// which already depends on them, so both sides link this small shared library instead: the
// hooks count their calls here and call the sink's functions, which the exe fills in before
// any guest code runs. A function left null is not called.
//
// Each hook runs on the guest thread that called D3D, after the original function, so the
// guest device's register mirror is up to date. base is guest memory, device the guest
// D3DDevice.

#pragma once

#include <cstdint>

#include "d3d_functions.h"  // generated: TS4_D3D_FUNCTIONS(X), one X(name) per name in the table

#if defined(_WIN32)
#if defined(TOPSPIN4_D3D_SINK_BUILD)
#define TS4_D3D_SINK_API __declspec(dllexport)
#else
#define TS4_D3D_SINK_API __declspec(dllimport)
#endif
#else
#define TS4_D3D_SINK_API __attribute__((visibility("default")))
#endif

namespace topspin4::d3d {

enum class Module : uint8_t { kLoader, kSwing, kCount };

#define TS4_D3D_ENUM(name) k##name,
enum class Fn : uint16_t { TS4_D3D_FUNCTIONS(TS4_D3D_ENUM) kCount };
#undef TS4_D3D_ENUM

inline const char* ModuleName(Module module) {
  return module == Module::kLoader ? "Loader" : "Swing";
}

inline const char* FnName(Fn fn) {
#define TS4_D3D_NAME(name) #name,
  static constexpr const char* kNames[] = {TS4_D3D_FUNCTIONS(TS4_D3D_NAME)};
#undef TS4_D3D_NAME
  return kNames[size_t(fn)];
}

struct Sink {
  // D3DDevice_Present: front_buffer is the texture object shown. Called before the original.
  void (*present)(Module, const uint8_t* base, uint32_t device, uint32_t front_buffer);
  // D3DDevice_SetRenderTarget: index and surface object.
  void (*set_render_target)(Module, const uint8_t* base, uint32_t index, uint32_t surface);
  // D3DDevice_Clear: flags (0xF colour, 0x10 depth, 0x20 stencil), rect in pixels, colour as
  // a guest float4 pointer (may be 0), depth and stencil.
  void (*clear)(Module, const uint8_t* base, uint32_t device, uint32_t flags,
                const int32_t rect[4], uint32_t color, float depth, uint32_t stencil);
  // D3DDevice_Resolve: flags (0..3 colour target, 4 depth-stencil) and the destination
  // texture object (0 for a clear-only resolve).
  void (*resolve)(Module, const uint8_t* base, uint32_t device, uint32_t flags,
                  uint32_t destination);
  // D3DDevice_SetStreamSource: the stride, which the device does not keep.
  void (*set_stream_source)(Module, uint32_t stream, uint32_t stride);
  // D3DDevice_DrawIndexedVertices: indices from the device's index buffer.
  void (*draw_indexed)(Module, const uint8_t* base, uint32_t device, uint32_t primitive,
                       int32_t base_vertex, uint32_t start_index, uint32_t index_count);
  // D3DDevice_DrawVertices: vertex_count vertices from start_vertex over the bound streams.
  void (*draw_non_indexed)(Module, const uint8_t* base, uint32_t device, uint32_t primitive,
                           uint32_t start_vertex, uint32_t vertex_count);
  // A user-memory draw (BeginVertices, BeginIndexedVertices, the *UP draws), once the game
  // has written its vertices. Indices, when present, address the vertex allocation with
  // base_vertex added.
  void (*draw_vertices)(Module, const uint8_t* base, uint32_t device, uint32_t primitive,
                        uint32_t vertex_count, uint32_t stride, uint32_t vertices,
                        uint32_t indices, uint32_t index_count, bool index32,
                        int32_t base_vertex);
  // Create*Shader: the caller's container and the object made from it.
  void (*shader_created)(Module, const uint8_t* base, uint32_t container, uint32_t object,
                         bool is_pixel_shader);
  // The XDK's shader register functions, on every shader object.
  void (*shader_registered)(Module, const uint8_t* base, uint32_t object, bool is_pixel_shader);
  // The register-mirror commit (r3 device, r4, r5 first register, r6 mirror address).
  void (*mirror_commit)(Module, uint32_t device, uint32_t r4, uint32_t first_register,
                        uint32_t mirror);
};

// The one sink, shared by the exe and both Module DLLs.
TS4_D3D_SINK_API Sink& GetSink();

// Census: calls of each hooked function per Module since start (relaxed counters).
TS4_D3D_SINK_API void Count(Module module, Fn fn);
TS4_D3D_SINK_API uint64_t Calls(Module module, Fn fn);

}  // namespace topspin4::d3d
