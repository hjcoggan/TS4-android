# Native renderer debugging

What to do when the Native renderer (`src/native`, ticket 26 on) draws something differently
from Classic. The renderer, its hooks and every finding so far are in
`docs/research/native-renderer.md`. Every run here is unattended; the scripts pass `--headless
--window_hidden=true`.

## The loop

1. **Measure on the same frame.** `python scripts/scene_runner.py <scene> --native 1 --dual 1`.
   The dual run draws every frame with both renderers (`--ts4_native_dual_compare`) and judges
   Native against Classic on the identical frame, so a match's AI or load times cannot hide or
   fake a difference. The plain `--native` verdict compares different runs and is decisive only
   for deterministic scenes (boot to the main menu). A dual verdict is red or green; take that
   over any reading of the images.
2. **Find the draw.** Capture the frame on each renderer with
   `pwsh scripts/rdoc.ps1 capture -Frame N [-InputScript …] [-ExtraArguments '--ts4_renderer=native']`.
   On the Native capture, run `query … 'history <rt> X Y'` on a wrong pixel. It lists every
   draw that wrote the pixel, with the shader's output. `query … 'draws --indices N'` finds the
   same draw in the Classic capture by index count. Classic draws a tiled pass once per EDRAM
   tile, so its matches come in twos.
3. **Compare the draw's inputs, one class at a time.** `query … 'draw EID'` on both sides, then
   `'texture ID'` for each bound texture. For constants, `--ts4_native_trace_frame=N
   --ts4_native_trace_ps=<decimal hash>` dumps Native's vertex and pixel constants for draws
   with that pixel shader. `python scripts/shader_lookup.py <hash>` gives the shader's HLSL and
   the register of every constant it reads. Line those up with Classic's `xe_uniform_float_constants`,
   which pack only the used registers in ascending order.
4. **When every input matches,** the difference is in how a vertex or texture is decoded. Read
   the draw's `layout` line in the Native trace (the format of each location) against the raw
   stream dwords.

## Causes found so far

- **D3D argument order differs from WWE All Stars'.** Top Spin 4's XDK is 2.0.20209, and LTCG
  specialises functions per Module. Resolve takes its destination in r6; Swing's DrawVertices
  has no start vertex. `python scripts/d3d_callsites.py` (also run by `build.ps1 -Action Check`)
  flags a hook reading an argument its callers don't pass. Its allow-list of constant arguments
  (`CONSTANT_OK`) hides a register that is legitimately constant: when a hook starts reading
  such a register for something else, recheck by hand.
- **Vertex formats the shader decodes itself:** DEC3N normals (`0x2A2187`), 16-bit halves that
  stay swapped after the 32-bit vertex swap (texcoords, positions, blend weights). See
  `kDeclTypes` and `HalfSwapBit` in `src/native/guest_decode.h` and `patches/xenosrecomp/`.
- **Texture formats Vulkan lacks** (`k_CTX1`): `src/native/texture_decode.h`. A skipped
  `no_texture` with "format N not supported" in the log names the format.
- **State the device mirror doesn't hold:** constants set from a buffer
  (`SetShaderConstantsFromBuffer`). Native reads draw state from the guest device, so anything
  D3D writes only to the ring needs a hook that copies it into the mirror.
- **Shaders missing from the pack:** `no_shader` skips. Dump on a Classic run
  (`--ts4_shader_dump_dir=<abs path>`), then `pwsh scripts/convert-shaders.ps1`.

- **Fixed-function state the converted shaders hard-code.** The alpha test was always
  greater-or-equal (`0005-alpha-test-function`). A pass that differs while every input matches
  may be reading state from a register the shader ignores; compare `RB_COLORCONTROL` and the
  Classic shader's `xe_uniform_system_constants`.
- **Per-texture state in the fetch constant.** Dword 4 holds an LOD bias Classic applies (Swing
  sharpens foliage with -1 and -2). When a draw's textures, samplers and constants match but its
  output alpha or detail does not, decode the fetch constants in the Native trace (`slot N
  fetch` lines) against `xe_gpu_texture_fetch_t`.
- **Classic's defaults are part of the picture.** Classic filters at 4x anisotropy
  (`anisotropic_override` 3). `query … 'draw EID'` prints each sampler's filter and anisotropy.
- **Same-frame captures.** `pwsh scripts/rdoc.ps1 capture … -ExtraArguments
  '--ts4_renderer=native','--ts4_native_dual_compare=true'` puts both renderers' draws in one
  capture, Native's first. `'mesh EID 4000'` dumps post-VS positions for matching vertex sets.

## After a fix

A XenosRecomp change is a patch (`pwsh scripts/new-patch.ps1 <name> -Tool xenosrecomp`): it
changes the converter id, so rebuild it (`pwsh scripts/build-xenosrecomp.ps1`) and convert the
pack again. Then rerun the scene in dual mode once.
