// D3D hook bodies, built into each Module's DLL. cmake/d3d_hooks.cmake writes one source per
// Module from symbols/d3d-functions.csv: it defines TS4_D3D_MODULE, includes this header and
// expands one line per table row, TS4_D3D_HOOK(name, address) for a renderer row and
// TS4_D3D_COUNT(name, address) for a census row. A row's name picks its body below. Every
// hook counts its calls (sink.h). Adapted from WWE All Stars' src/d3d_census.cpp (ADR-0004);
// argument registers are WWE's, checked against both copies (docs/research/native-renderer.md).
//
// Included by exactly one source per DLL, so the state below belongs to that Module's copy.

#pragma once

#include <rex/hook.h>

#include "d3d/sink.h"

namespace topspin4::d3d::hooks {

inline uint32_t LoadBE32(const uint8_t* p) {
  return uint32_t(p[0]) << 24 | uint32_t(p[1]) << 16 | uint32_t(p[2]) << 8 | p[3];
}

// A user-memory draw from BeginVertices or BeginIndexedVertices, waiting for the game to
// write its vertices; taken by whatever ends the draw.
struct PendingVertices {
  uint32_t device = 0;
  uint32_t primitive = 0;
  uint32_t count = 0;
  uint32_t stride = 0;
  uint32_t vertices = 0;
  uint32_t indices = 0;
  uint32_t index_count = 0;
  bool index32 = false;
  int32_t base_vertex = 0;
};
inline PendingVertices g_pending_vertices;

// Inside a Create*Shader call on this thread. It registers the object it builds before the
// object holds the shader's microcode (WWE ticket 29), so the register hook leaves those calls
// to the Create hook, which reads the caller's container instead.
inline thread_local bool g_creating_shader = false;

inline void FinishPendingVertices(Module module, const uint8_t* base) {
  const PendingVertices pending = g_pending_vertices;
  g_pending_vertices = {};
  if (pending.vertices && GetSink().draw_vertices) {
    GetSink().draw_vertices(module, base, pending.device, pending.primitive, pending.count,
                            pending.stride, pending.vertices, pending.indices,
                            pending.index_count, pending.index32, pending.base_vertex);
  }
}

}  // namespace topspin4::d3d::hooks

#define TS4_D3D_FN(name) ::topspin4::d3d::Fn::k##name

#define TS4_D3D_HOOK(name, address) TS4_D3D_HOOK_##name(name, address)
#define TS4_D3D_COUNT(name, address) TS4_D3D_HOOK_COUNTED(name, address)

// Expands the address first, so sub_ and __imp__sub_ paste onto its digits.
#define TS4_D3D_SUB(address) TS4_D3D_SUB_(address)
#define TS4_D3D_SUB_(address) sub_##address
#define TS4_D3D_IMP(address) TS4_D3D_IMP_(address)
#define TS4_D3D_IMP_(address) __imp__sub_##address
// DECLARE_REX_FUNC pastes its argument unexpanded, so it gets the finished sub_ token.
#define TS4_D3D_DECLARE(address) TS4_D3D_DECLARE_(sub_##address)
#define TS4_D3D_DECLARE_(sub) DECLARE_REX_FUNC(sub)

// A hook whose body runs before and after the original: TS4_D3D_BEGIN(...) { before }
// TS4_D3D_CALL; { after } TS4_D3D_END.
#define TS4_D3D_BEGIN(name, address)                       \
  TS4_D3D_DECLARE(address);                                \
  REX_HOOK_RAW(TS4_D3D_SUB(address)) {                     \
    [[maybe_unused]] constexpr auto module = TS4_D3D_MODULE; \
    constexpr auto fn = TS4_D3D_FN(name);                  \
    ::topspin4::d3d::Count(module, fn);                    \
    [[maybe_unused]] auto& sink = ::topspin4::d3d::GetSink();
// The original, between the census's draw-attribution callbacks (set only under the census).
#define TS4_D3D_CALL(address)                                                   \
  do {                                                                          \
    if (sink.call_entry) sink.call_entry(module, fn, base, uint32_t(ctx.lr));   \
    TS4_D3D_IMP(address)(ctx, base);                                            \
    if (sink.call_exit) sink.call_exit(module, fn, base);                       \
  } while (0)
#define TS4_D3D_END }

#define TS4_D3D_HOOK_COUNTED(name, address) \
  TS4_D3D_BEGIN(name, address)              \
  TS4_D3D_CALL(address);                    \
  TS4_D3D_END

// D3DDevice_Present: r3 = device, r4 = front buffer texture. The sink sees it before the
// original swaps.
#define TS4_D3D_HOOK_Present(name, address)                              \
  TS4_D3D_BEGIN(name, address)                                           \
  if (sink.present) sink.present(module, base, ctx.r3.u32, ctx.r4.u32); \
  TS4_D3D_CALL(address);                                                 \
  TS4_D3D_END

// D3DDevice_SetRenderTarget: r4 = index, r5 = surface.
#define TS4_D3D_HOOK_SetRenderTarget(name, address)                               \
  TS4_D3D_BEGIN(name, address)                                                    \
  const uint32_t index = ctx.r4.u32;                                              \
  const uint32_t surface = ctx.r5.u32;                                            \
  TS4_D3D_CALL(address);                                                          \
  if (sink.set_render_target) sink.set_render_target(module, base, index, surface); \
  TS4_D3D_END

// D3DDevice_Clear: r4 = flags, r6..r9 = rect, r10 = float4 colour pointer, f1 = depth,
// stencil on the caller's stack.
#define TS4_D3D_HOOK_Clear(name, address)                                              \
  TS4_D3D_BEGIN(name, address)                                                         \
  const uint32_t device = ctx.r3.u32;                                                  \
  const uint32_t flags = ctx.r4.u32;                                                   \
  const int32_t rect[4] = {ctx.r6.s32, ctx.r7.s32, ctx.r8.s32, ctx.r9.s32};            \
  const uint32_t color = ctx.r10.u32;                                                  \
  const float depth = float(ctx.f1.f64);                                               \
  const uint32_t stencil = ::topspin4::d3d::hooks::LoadBE32(base + ctx.r1.u32 + 0x5C); \
  TS4_D3D_CALL(address);                                                               \
  if (sink.clear) sink.clear(module, base, device, flags, rect, color, depth, stencil); \
  TS4_D3D_END

// D3DDevice_Resolve: r4 = flags, r5 = source rect, r6 = destination texture, r7 = destination
// point, r8 = level (the XDK's order; Loader's callers pass it so). WWE All Stars' older XDK
// took the destination in r8.
#define TS4_D3D_HOOK_Resolve(name, address)                                      \
  TS4_D3D_BEGIN(name, address)                                                   \
  const uint32_t device = ctx.r3.u32;                                            \
  const uint32_t flags = ctx.r4.u32;                                             \
  const uint32_t destination = ctx.r6.u32;                                       \
  const uint32_t level = ctx.r8.u32;                                             \
  TS4_D3D_CALL(address);                                                         \
  if (sink.resolve) sink.resolve(module, base, device, flags, destination, level); \
  TS4_D3D_END

// D3DDevice_SetStreamSource: r4 = stream, r5 = vertex buffer, r6 = offset, r7 = stride.
#define TS4_D3D_HOOK_SetStreamSource(name, address)                           \
  TS4_D3D_BEGIN(name, address)                                                \
  const uint32_t stream = ctx.r4.u32;                                         \
  const uint32_t stride = ctx.r7.u32;                                         \
  TS4_D3D_CALL(address);                                                      \
  if (sink.set_stream_source) sink.set_stream_source(module, stream, stride); \
  TS4_D3D_END

// D3DDevice_DrawIndexedVertices: r4 = primitive, r5 = base vertex, r6 = start index,
// r7 = index count.
#define TS4_D3D_HOOK_DrawIndexedVertices(name, address)                                   \
  TS4_D3D_BEGIN(name, address)                                                            \
  const uint32_t device = ctx.r3.u32;                                                     \
  const uint32_t primitive = ctx.r4.u32;                                                  \
  const int32_t base_vertex = ctx.r5.s32;                                                 \
  const uint32_t start_index = ctx.r6.u32;                                                \
  const uint32_t index_count = ctx.r7.u32;                                                \
  TS4_D3D_CALL(address);                                                                  \
  if (sink.draw_indexed)                                                                  \
    sink.draw_indexed(module, base, device, primitive, base_vertex, start_index, index_count); \
  TS4_D3D_END

// D3DDevice_DrawVertices: r4 = primitive, r5 = start vertex, r6 = vertex count.
#define TS4_D3D_HOOK_DrawVertices(name, address)                                         \
  TS4_D3D_BEGIN(name, address)                                                           \
  const uint32_t device = ctx.r3.u32;                                                    \
  const uint32_t primitive = ctx.r4.u32;                                                 \
  const uint32_t start_vertex = ctx.r5.u32;                                              \
  const uint32_t vertex_count = ctx.r6.u32;                                              \
  TS4_D3D_CALL(address);                                                                 \
  if (sink.draw_non_indexed)                                                             \
    sink.draw_non_indexed(module, base, device, primitive, start_vertex, vertex_count);  \
  TS4_D3D_END

// Shader constants from a constant buffer: r4 = the buffer's range table, r5 = the data base.
// D3D sends them to the GPU (inline, or as LOAD_ALU_CONSTANT) without touching the device's
// constant mirror, which is where the Native renderer reads constants.
#define TS4_D3D_HOOK_SetShaderConstantsFromBuffer(name, address)                       \
  TS4_D3D_BEGIN(name, address)                                                         \
  const uint32_t device = ctx.r3.u32;                                                  \
  const uint32_t table = ctx.r4.u32;                                                   \
  const uint32_t data = ctx.r5.u32;                                                    \
  TS4_D3D_CALL(address);                                                               \
  if (sink.constants_from_buffer) sink.constants_from_buffer(module, base, device, table, data); \
  TS4_D3D_END

// Swing's DrawVertices, specialised by LTCG to start at vertex 0: r4 = primitive, r5 = vertex
// count.
#define TS4_D3D_HOOK_DrawVerticesFromZero(name, address)                              \
  TS4_D3D_BEGIN(name, address)                                                        \
  const uint32_t device = ctx.r3.u32;                                                 \
  const uint32_t primitive = ctx.r4.u32;                                              \
  const uint32_t vertex_count = ctx.r5.u32;                                           \
  TS4_D3D_CALL(address);                                                              \
  if (sink.draw_non_indexed)                                                          \
    sink.draw_non_indexed(module, base, device, primitive, 0, vertex_count);          \
  TS4_D3D_END

// D3DDevice_BeginVertices: r4 = primitive, r5 = vertex count, r6 = stride; returns the guest
// pointer the game copies the vertices into. A draw still pending was ended inline.
#define TS4_D3D_HOOK_BeginVertices(name, address)                                     \
  TS4_D3D_BEGIN(name, address)                                                        \
  ::topspin4::d3d::hooks::FinishPendingVertices(module, base);                        \
  const ::topspin4::d3d::hooks::PendingVertices pending{ctx.r3.u32, ctx.r4.u32,       \
                                                        ctx.r5.u32, ctx.r6.u32};      \
  TS4_D3D_CALL(address);                                                              \
  ::topspin4::d3d::hooks::g_pending_vertices = pending;                               \
  ::topspin4::d3d::hooks::g_pending_vertices.vertices = ctx.r3.u32;                   \
  TS4_D3D_END

// BeginIndexedVertices: r5 = base vertex, r6 = vertex count, r7 = index count, r8 = flags
// (4: 32-bit indices), r9 = stride; returns indices through r10 and vertices through stack
// argument 9.
#define TS4_D3D_HOOK_BeginIndexedVertices(name, address)                                 \
  TS4_D3D_BEGIN(name, address)                                                           \
  ::topspin4::d3d::hooks::FinishPendingVertices(module, base);                           \
  ::topspin4::d3d::hooks::PendingVertices pending;                                       \
  pending.device = ctx.r3.u32;                                                           \
  pending.primitive = ctx.r4.u32;                                                        \
  pending.base_vertex = ctx.r5.s32;                                                      \
  pending.count = ctx.r6.u32;                                                            \
  pending.index_count = ctx.r7.u32;                                                      \
  pending.index32 = (ctx.r8.u32 & 4) != 0;                                               \
  pending.stride = ctx.r9.u32;                                                           \
  const uint32_t out_indices = ctx.r10.u32;                                              \
  const uint32_t out_vertices = ::topspin4::d3d::hooks::LoadBE32(base + ctx.r1.u32 + 0x54); \
  TS4_D3D_CALL(address);                                                                 \
  if (ctx.r3.u32 == 0 && out_vertices && out_indices) {                                  \
    pending.vertices = ::topspin4::d3d::hooks::LoadBE32(base + out_vertices);            \
    pending.indices = ::topspin4::d3d::hooks::LoadBE32(base + out_indices);              \
    ::topspin4::d3d::hooks::g_pending_vertices = pending;                                \
  }                                                                                      \
  TS4_D3D_END

// What ends a user-memory draw: EndVertices, the *UP draws (they commit inline), and in
// Swing the game function its draws call right after their inline end.
#define TS4_D3D_HOOK_ENDS_DRAW(name, address)                 \
  TS4_D3D_BEGIN(name, address)                                \
  TS4_D3D_CALL(address);                                      \
  ::topspin4::d3d::hooks::FinishPendingVertices(module, base); \
  TS4_D3D_END
#define TS4_D3D_HOOK_EndVertices(name, address) TS4_D3D_HOOK_ENDS_DRAW(name, address)
#define TS4_D3D_HOOK_DrawVerticesUP(name, address) TS4_D3D_HOOK_ENDS_DRAW(name, address)
#define TS4_D3D_HOOK_DrawIndexedVerticesUP(name, address) TS4_D3D_HOOK_ENDS_DRAW(name, address)
// Taken on entry: this function restores device state.
#define TS4_D3D_HOOK_FinishVertices(name, address)            \
  TS4_D3D_BEGIN(name, address)                                \
  ::topspin4::d3d::hooks::FinishPendingVertices(module, base); \
  TS4_D3D_CALL(address);                                      \
  TS4_D3D_END

// Create*Shader: r3 = the caller's container; returns the object.
#define TS4_D3D_HOOK_CREATE_SHADER(name, address, is_pixel)                             \
  TS4_D3D_BEGIN(name, address)                                                          \
  const uint32_t container = ctx.r3.u32;                                                \
  ::topspin4::d3d::hooks::g_creating_shader = true;                                     \
  TS4_D3D_CALL(address);                                                                \
  ::topspin4::d3d::hooks::g_creating_shader = false;                                    \
  if (sink.shader_created) sink.shader_created(module, base, container, ctx.r3.u32, is_pixel); \
  TS4_D3D_END
#define TS4_D3D_HOOK_CreateVertexShader(name, address) \
  TS4_D3D_HOOK_CREATE_SHADER(name, address, false)
#define TS4_D3D_HOOK_CreatePixelShader(name, address) \
  TS4_D3D_HOOK_CREATE_SHADER(name, address, true)

// The register functions: r3 = object.
#define TS4_D3D_HOOK_REGISTER_SHADER(name, address, is_pixel)                             \
  TS4_D3D_BEGIN(name, address)                                                            \
  const uint32_t object = ctx.r3.u32;                                                     \
  TS4_D3D_CALL(address);                                                                  \
  if (sink.shader_registered && !::topspin4::d3d::hooks::g_creating_shader)               \
    sink.shader_registered(module, base, object, is_pixel);                               \
  TS4_D3D_END
#define TS4_D3D_HOOK_RegisterVertexShader(name, address) \
  TS4_D3D_HOOK_REGISTER_SHADER(name, address, false)
#define TS4_D3D_HOOK_RegisterPixelShader(name, address) \
  TS4_D3D_HOOK_REGISTER_SHADER(name, address, true)

// D3D internal: copies one register group from the device's register mirror into the command
// buffer (r3 = device, r5 = first register, r6 = mirror).
#define TS4_D3D_HOOK_MirrorCommit(name, address)                                         \
  TS4_D3D_BEGIN(name, address)                                                           \
  if (sink.mirror_commit)                                                                \
    sink.mirror_commit(module, ctx.r3.u32, ctx.r4.u32, ctx.r5.u32, ctx.r6.u32);          \
  TS4_D3D_CALL(address);                                                                 \
  TS4_D3D_END
