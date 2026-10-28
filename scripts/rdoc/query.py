"""Questions to a RenderDoc capture, run inside qrenderdoc (scripts/rdoc.ps1 query starts it).

qrenderdoc passes no arguments to a script, so scripts/rdoc.ps1 hands them over in the
TS4_RDOC environment variable as JSON: {"capture": path, "out": dir, "commands": [[...], ...]}.
Every command runs against one replay, so ask several questions per run: a replay of a frame
capture takes a minute or more. Results go to <out>/query.txt (and PNGs beside it).

Commands:
  draws [--indices N,N]       every draw: event, index count, pixel shader, its textures
  draw EID                    one draw: viewport, scissor, targets, textures with view format,
                              swizzle and mips, samplers, and every constant block of both
                              stages; the shaders' SPIR-V goes to vs-EID.txt and ps-EID.txt
  texture ID [--all-mips]     texel samples per mip and face, and PNGs of them
  rt EID                      the colour targets as they are after the draw, as PNGs
  present                     the swapchain images written in the capture, as PNGs: the shown
                              frame after the presenter's effect (CAS), which the dumps lack
  pixel ID X Y [MIP] [SLICE]  one texel's value
  history ID X Y              every event that wrote texel (X, Y): shader output and result
  actions FROM TO             every action between two events (passes, copies, dispatches with
                              the compute stage's textures and buffer ranges): follows a resolve
                              from render target through the EDRAM buffer to guest memory
  buffer ID OFFSET LEN EID    a buffer range (hex offset and length) after an event: how many
                              bytes are nonzero, and the first dwords
  constants EID vs|ps FIRST [COUNT]
                              float4 registers FIRST.. of a draw's guest constants, read
                              through Native's constant pointers (Native captures only)
  calls EID                   the API calls recorded since the previous action up to action EID
  durations [FROM TO]         GPU time per action on replay (EventGPUDuration): the frame's
                              sum, and the actions FROM..TO (default: the 10 longest)
"""
import json
import math
import os
import traceback

import renderdoc as rd

request = json.loads(os.environ["TS4_RDOC"])
OUT = request["out"]
os.makedirs(OUT, exist_ok=True)
log = open(os.path.join(OUT, "query.txt"), "w", encoding="utf-8")


def say(text):
    log.write(text + "\n")
    log.flush()


def fmt_tex(t):
    return f"{int(t.resourceId)}:{t.width}x{t.height}:{t.format.Name()}" + (
        f":{t.arraysize}sl" if t.arraysize > 1 else "") + (f":{t.mips}mip" if t.mips > 1 else "")


def walk(actions, out):
    for a in actions:
        if a.children:
            walk(a.children, out)
        if a.flags & rd.ActionFlags.Drawcall:
            out.append(a)
    return out


def cmd_draws(ctrl, textures, args):
    indices = None
    if args[:1] == ["--indices"]:
        indices = {int(n) for n in args[1].split(",")}
    for a in walk(ctrl.GetRootActions(), []):
        if indices and a.numIndices not in indices:
            continue
        ctrl.SetFrameEvent(a.eventId, False)
        pipe = ctrl.GetPipelineState()
        texs = []
        for u in pipe.GetReadOnlyResources(rd.ShaderStage.Pixel, False):
            t = textures.get(u.descriptor.resource)
            if t:
                texs.append(f"{u.access.index}={fmt_tex(t)}")
        say(f"eid {a.eventId} idx {a.numIndices} inst {a.numInstances} "
            f"ps {int(pipe.GetShader(rd.ShaderStage.Pixel))} tex {' '.join(texs)}")


def dump_var(v, indent):
    n = v.rows * v.columns
    if v.members:
        say(f"{indent}{v.name}:")
        for m in v.members:
            dump_var(m, indent + "  ")
    elif n:
        floats = [v.value.f32v[i] for i in range(n)]
        uints = [v.value.u32v[i] for i in range(n)]
        # Bit patterns that read as denormals or NaN are integers (masks, packed fields).
        if any(math.isnan(f) or (f != 0 and abs(f) < 1e-30) for f in floats):
            say(f"{indent}{v.name} = " + " ".join(f"{u:08X}" for u in uints))
        else:
            say(f"{indent}{v.name} = " + " ".join(f"{f:.5g}" for f in floats))


def cmd_draw(ctrl, textures, args):
    eid = int(args[0])
    ctrl.SetFrameEvent(eid, False)
    pipe = ctrl.GetPipelineState()
    vp, sc = pipe.GetViewport(0), pipe.GetScissor(0)
    say(f"== draw {eid}: viewport {vp.x},{vp.y} {vp.width}x{vp.height}, "
        f"scissor {sc.x},{sc.y} {sc.width}x{sc.height}")
    outs = [int(getattr(o, "descriptor", o).resource) for o in pipe.GetOutputTargets()]
    say(f"targets {outs or 'none (FSI writes EDRAM from the shader)'}")
    vk = ctrl.GetVulkanPipelineState()
    raster, ds = vk.rasterizer, vk.depthStencil
    say(f"depth test {'on' if ds.depthTestEnable else 'off'} {ds.depthFunction} write "
        f"{'on' if ds.depthWriteEnable else 'off'}, cull {raster.cullMode} front "
        f"{'ccw' if raster.frontCCW else 'cw'}, depth bias "
        f"{'on' if raster.depthBiasEnable else 'off'}: constant {raster.depthBias} "
        f"slope {raster.slopeScaledDepthBias} clamp {raster.depthBiasClamp}")
    vps = vk.viewportScissor.viewportScissors
    if vps:
        say(f"depth range {vps[0].vp.minDepth}..{vps[0].vp.maxDepth}")
    for u in pipe.GetReadOnlyResources(rd.ShaderStage.Pixel, False):
        d = u.descriptor
        t = textures.get(d.resource)
        if not t:
            say(f"  slot {u.access.index}: null")
            continue
        s = d.swizzle
        say(f"  slot {u.access.index}: {fmt_tex(t)} view {d.format.Name()} swizzle "
            f"{s.red},{s.green},{s.blue},{s.alpha} mips {d.firstMip}+{d.numMips} "
            f"slices {d.firstSlice}+{d.numSlices}")
    for i, s in enumerate(pipe.GetSamplers(rd.ShaderStage.Pixel, False)):
        sd = s.sampler
        say(f"  sampler {i}: address {sd.addressU},{sd.addressV},{sd.addressW} "
            f"lod {sd.minLOD}..{sd.maxLOD} bias {sd.mipBias} filter {sd.filter.minify}/"
            f"{sd.filter.magnify}/{sd.filter.mip} aniso {sd.maxAnisotropy}")
    for stage in (rd.ShaderStage.Vertex, rd.ShaderStage.Pixel):
        refl = pipe.GetShaderReflection(stage)
        if refl is None:
            continue
        say(f"-- {stage} shader {int(pipe.GetShader(stage))}")
        for i, cb in enumerate(refl.constantBlocks):
            d = pipe.GetConstantBlock(stage, i, 0).descriptor
            variables = ctrl.GetCBufferVariableContents(
                pipe.GetGraphicsPipelineObject(), refl.resourceId, stage,
                pipe.GetShaderEntryPoint(stage), i, d.resource, d.byteOffset, d.byteSize)
            say(f"  block {cb.name} ({d.byteSize} bytes)")
            for v in variables:
                dump_var(v, "    ")
        target = ctrl.GetDisassemblyTargets(True)[0]
        text = ctrl.DisassembleShader(pipe.GetGraphicsPipelineObject(), refl, target)
        name = "ps" if stage == rd.ShaderStage.Pixel else "vs"
        path = os.path.join(OUT, f"{name}-{eid}.txt")
        open(path, "w", encoding="utf-8").write(text)
        say(f"  {'pixel' if name == 'ps' else 'vertex'} shader SPIR-V: {path}")


def save_texture(ctrl, rid, mip, slice_, path):
    s = rd.TextureSave()
    s.resourceId = rid
    s.mip = mip
    s.slice.sliceIndex = slice_
    s.destType = rd.FileType.PNG
    s.alpha = rd.AlphaMapping.Discard
    ctrl.SaveTexture(s, path)


def find_texture(textures, ident):
    for rid, t in textures.items():
        if int(rid) == ident:
            return rid, t
    raise KeyError(f"no texture {ident}")


def cmd_texture(ctrl, textures, args):
    rid, t = find_texture(textures, int(args[0]))
    mips = range(t.mips) if "--all-mips" in args else range(1)
    say(f"== texture {fmt_tex(t)}")
    for mip in mips:
        w, h = max(1, t.width >> mip), max(1, t.height >> mip)
        for face in range(t.arraysize):
            vals = []
            for fx, fy in ((0.25, 0.25), (0.5, 0.5), (0.75, 0.75)):
                v = ctrl.PickPixel(rid, int(w * fx), int(h * fy), rd.Subresource(mip, face, 0),
                                   rd.CompType.Typeless)
                vals.append("(" + " ".join(f"{c:.3g}" for c in v.floatValue[:4]) + ")")
            say(f"  mip {mip} ({w}x{h}) slice {face}: {' '.join(vals)}")
            save_texture(ctrl, rid, mip, face,
                         os.path.join(OUT, f"tex-{int(rid)}-mip{mip}-slice{face}.png"))


def cmd_rt(ctrl, textures, args):
    eid = int(args[0])
    ctrl.SetFrameEvent(eid, False)
    for o in ctrl.GetPipelineState().GetOutputTargets():
        rid = getattr(o, "descriptor", o).resource
        if rid == rd.ResourceId.Null():
            continue
        path = os.path.join(OUT, f"rt-{eid}-{int(rid)}.png")
        save_texture(ctrl, rid, 0, 0, path)
        say(f"rt after {eid}: {fmt_tex(textures[rid])} -> {path}")


def cmd_pixel(ctrl, textures, args):
    rid, t = find_texture(textures, int(args[0]))
    x, y = int(args[1]), int(args[2])
    mip = int(args[3]) if len(args) > 3 else 0
    slice_ = int(args[4]) if len(args) > 4 else 0
    v = ctrl.PickPixel(rid, x, y, rd.Subresource(mip, slice_, 0), rd.CompType.Typeless)
    bits = " ".join(f"{u:08X}" for u in v.uintValue[:4])
    say(f"pixel {int(rid)} ({x},{y}) mip {mip} slice {slice_}: "
        + " ".join(f"{c:.5g}" for c in v.floatValue[:4]) + f"  bits {bits}")


def cmd_history(ctrl, textures, args):
    """Every event that wrote texel (X, Y) of a texture: the shader's output and the texel after,
    so a wrong pixel can be traced to the draw that made it."""
    rid, t = find_texture(textures, int(args[0]))
    x, y = int(args[1]), int(args[2])
    ctrl.SetFrameEvent(walk(ctrl.GetRootActions(), [])[-1].eventId, False)
    mods = ctrl.PixelHistory(rid, x, y, rd.Subresource(0, 0, 0), rd.CompType.Typeless)
    say(f"history {fmt_tex(t)} ({x},{y}): {len(mods)} modifications")
    for m in mods:
        flags = []
        if m.backfaceCulled: flags.append("culled")
        if m.depthTestFailed: flags.append("depth-failed")
        if m.scissorClipped: flags.append("scissored")
        if m.shaderDiscarded: flags.append("discarded")
        out = " ".join(f"{c:.4g}" for c in m.shaderOut.col.floatValue[:4])
        post = " ".join(f"{c:.4g}" for c in m.postMod.col.floatValue[:4])
        say(f"  eid {m.eventId} prim {m.primitiveID} out ({out}) after ({post}) {' '.join(flags)}")


def all_actions(actions, out):
    for a in actions:
        out.append(a)
        if a.children:
            all_actions(a.children, out)
    return out


def cmd_actions(ctrl, textures, args):
    first, last = int(args[0]), int(args[1])
    buffers = {b.resourceId: b for b in ctrl.GetBuffers()}
    sdfile = ctrl.GetStructuredFile()
    for a in all_actions(ctrl.GetRootActions(), []):
        if not first <= a.eventId <= last:
            continue
        line = f"eid {a.eventId} {a.GetName(sdfile)}"
        if a.flags & rd.ActionFlags.Dispatch:
            ctrl.SetFrameEvent(a.eventId, False)
            pipe = ctrl.GetPipelineState()
            res = []
            for kind, used in (("ro", pipe.GetReadOnlyResources(rd.ShaderStage.Compute, False)),
                               ("rw", pipe.GetReadWriteResources(rd.ShaderStage.Compute, False))):
                for u in used:
                    d = u.descriptor
                    t = textures.get(d.resource)
                    b = buffers.get(d.resource)
                    if t:
                        res.append(f"{kind}{u.access.index}={fmt_tex(t)}")
                    elif b:
                        res.append(f"{kind}{u.access.index}=buf{int(d.resource)}"
                                   f"@{d.byteOffset:X}+{d.byteSize:X}")
            line += f" cs {int(pipe.GetShader(rd.ShaderStage.Compute))} " + " ".join(res)
        say(line)


def cmd_calls(ctrl, textures, args):
    """calls EID: every API call recorded since the previous action up to action EID (the state
    set for a draw: binds, push constants, dynamic state, barriers), in order."""
    eid = int(args[0])
    sdfile = ctrl.GetStructuredFile()
    for a in all_actions(ctrl.GetRootActions(), []):
        if a.eventId != eid:
            continue
        for e in a.events:
            say(f"  eid {e.eventId} {sdfile.chunks[e.chunkIndex].name}")
        return
    say(f"calls: no action {eid}")


def cmd_buffer(ctrl, textures, args):
    """buffer ID OFFSET LENGTH EID: nonzero bytes and the first dwords, as of after EID."""
    rid = next(b.resourceId for b in ctrl.GetBuffers() if int(b.resourceId) == int(args[0]))
    offset, length, eid = int(args[1], 16), int(args[2], 16), int(args[3])
    ctrl.SetFrameEvent(eid, True)
    data = bytes(ctrl.GetBufferData(rid, offset, length))
    nonzero = sum(1 for x in data if x)
    words = " ".join(data[i:i + 4].hex() for i in range(0, min(len(data), 32), 4))
    say(f"buffer {args[0]} @{offset:X}+{length:X} after eid {eid}: {nonzero} of {len(data)} "
        f"bytes nonzero; first {words}")


def cmd_constants(ctrl, textures, args):
    """constants EID vs|ps FIRST [COUNT]: float4 registers FIRST.. of a draw's guest constants.
    Native's push constants hold buffer addresses (vertex, pixel, shared), which a draw's block
    shows as raw pointers; this follows the stage's pointer to the buffer that holds it."""
    import struct
    eid, stage = int(args[0]), args[1]
    stages = {"vs": rd.ShaderStage.Vertex, "ps": rd.ShaderStage.Pixel}
    if stage not in stages:
        say(f"constants: stage must be vs or ps, not {stage!r}")
        return
    shader_stage = stages[stage]
    first, count = int(args[2]), int(args[3]) if len(args) > 3 else 1
    ctrl.SetFrameEvent(eid, True)
    pipe = ctrl.GetPipelineState()
    refl = pipe.GetShaderReflection(shader_stage)
    if refl is None:  # a depth-only pass binds no pixel shader
        say(f"draw {eid}: no {stage} shader bound")
        return
    pointers = None
    for i, cb in enumerate(refl.constantBlocks):
        d = pipe.GetConstantBlock(shader_stage, i, 0).descriptor
        variables = ctrl.GetCBufferVariableContents(
            pipe.GetGraphicsPipelineObject(), refl.resourceId, shader_stage,
            pipe.GetShaderEntryPoint(shader_stage), i, d.resource, d.byteOffset,
            d.byteSize)
        if len(variables) >= 2 and variables[0].name == "_child0":
            pointers = [v.value.u64v[0] for v in variables]
    if pointers is None:
        say(f"draw {eid}: no constant pointers in the {stage} shader's blocks")
        return
    address = pointers[0 if stage == "vs" else 1] + first * 16
    for b in ctrl.GetBuffers():
        if b.gpuAddress and b.gpuAddress <= address < b.gpuAddress + b.length:
            data = bytes(ctrl.GetBufferData(b.resourceId, address - b.gpuAddress, count * 16))
            for r in range(len(data) // 16):
                v = struct.unpack_from("<4f", data, r * 16)
                say(f"  {stage} c{first + r} = " + " ".join(f"{x:.7g}" for x in v))
            return
    say(f"draw {eid}: no buffer holds address {address:X}")


def cmd_mesh(ctrl, textures, args):
    """mesh EID [N]: the first N (8) vertices' clip position after the vertex shader, and its
    screen x, y and depth (viewport 0."""
    import struct
    eid, count = int(args[0]), int(args[1]) if len(args) > 1 else 8
    ctrl.SetFrameEvent(eid, True)
    vp = ctrl.GetPipelineState().GetViewport(0)
    mesh = ctrl.GetPostVSData(0, 0, rd.MeshDataStage.VSOut)
    stride = mesh.vertexByteStride
    data = bytes(ctrl.GetBufferData(mesh.vertexResourceId, mesh.vertexByteOffset,
                                    stride * count))
    say(f"== mesh {eid}: {mesh.numIndices} indices, output stride {stride}")
    if count > 8:  # every vertex to mesh-EID.csv: clip x y z w
        with open(os.path.join(OUT, f"mesh-{eid}.csv"), "w") as f:
            for i in range(len(data) // stride if stride else 0):
                f.write("%f,%f,%f,%f\n" % struct.unpack_from("<4f", data, i * stride))
        count = 8
    for i in range(min(count, len(data) // stride if stride else 0)):
        x, y, z, w = struct.unpack_from("<4f", data, i * stride)
        sx = vp.x + (x / w * 0.5 + 0.5) * vp.width if w else 0.0
        sy = vp.y + (y / w * 0.5 + 0.5) * vp.height if w else 0.0
        say(f"  v{i}: clip {x:.5f} {y:.5f} {z:.6f} {w:.5f}  screen {sx:.2f} {sy:.2f} "
            f"depth {z / w if w else 0:.7f}")


def cmd_present(ctrl, textures, args):
    """present: every swapchain image the capture wrote to, as it is after the last event, as
    PNGs: the frame the window shows, after the presenter's effect (CAS, FSR). The frame dumps
    (--ts4_dump_frames) are the presenter's input, before the effect."""
    last = all_actions(ctrl.GetRootActions(), [])[-1].eventId
    ctrl.SetFrameEvent(last, True)
    for rid, t in textures.items():
        if not t.creationFlags & rd.TextureCategory.SwapBuffer:
            continue
        used = [f"{u.eventId}:{str(u.usage).rsplit('.', 1)[-1]}" for u in ctrl.GetUsage(rid)]
        say(f"swapchain image {fmt_tex(t)}: " + ", ".join(used))
        if any(not u.endswith((":Present", ":Barrier")) for u in used):
            path = os.path.join(OUT, f"present-{int(rid)}.png")
            save_texture(ctrl, rid, 0, 0, path)
            say(f"  -> {path}")


def cmd_durations(ctrl, textures, args):
    """durations [FROM TO]: GPU time of each action (RenderDoc's EventGPUDuration, measured on
    replay), the sum over the frame, and the actions FROM..TO (by default the 10 longest) with
    their names, so one pass (the presenter's CAS) can be timed against the frame."""
    counter = rd.GPUCounter.EventGPUDuration
    if counter not in ctrl.EnumerateCounters():
        say("durations: this replay has no EventGPUDuration counter")
        return
    desc = ctrl.DescribeCounter(counter)
    scale = {rd.CounterUnit.Seconds: 1000.0}.get(desc.unit)
    if scale is None:
        say(f"durations: EventGPUDuration is in {desc.unit}, not seconds")
        return

    def value(r):
        if desc.resultType == rd.CompType.Float:
            return r.value.d if desc.resultByteWidth == 8 else r.value.f
        return r.value.u64 if desc.resultByteWidth == 8 else r.value.u32

    ms = {r.eventId: value(r) * scale for r in ctrl.FetchCounters([counter])}
    sdfile = ctrl.GetStructuredFile()
    actions = all_actions(ctrl.GetRootActions(), [])
    say(f"frame: {len(ms)} timed events, {sum(ms.values()):.4f} ms GPU in all")
    if args:
        first, last = int(args[0]), int(args[1])
        chosen = [a for a in actions if first <= a.eventId <= last]
        say(f"events {first}..{last}: {sum(ms.get(a.eventId, 0.0) for a in chosen):.4f} ms")
    else:
        chosen = sorted((a for a in actions if a.eventId in ms),
                        key=lambda a: -ms[a.eventId])[:10]
    for a in chosen:
        t = ms.get(a.eventId)
        say(f"  eid {a.eventId} {'%.4f ms' % t if t is not None else '-'} {a.GetName(sdfile)}")


COMMANDS = {"draws": cmd_draws, "draw": cmd_draw, "texture": cmd_texture, "rt": cmd_rt,
            "history": cmd_history, "mesh": cmd_mesh, "present": cmd_present,
            "pixel": cmd_pixel, "actions": cmd_actions, "buffer": cmd_buffer,
            "constants": cmd_constants, "durations": cmd_durations, "calls": cmd_calls}

def succeeded(result):
    return getattr(result, "code", result) == rd.ResultCode.Succeeded


try:
    cap = rd.OpenCaptureFile()
    if not succeeded(cap.OpenFile(request["capture"], "", None)):
        raise RuntimeError(f"cannot open {request['capture']}")
    status, ctrl = cap.OpenCapture(rd.ReplayOptions(), None)
    if not succeeded(status):
        raise RuntimeError(f"cannot replay {request['capture']}: {status}")
    textures = {t.resourceId: t for t in ctrl.GetTextures()}
    for command in request["commands"]:
        name, args = command[0], command[1:]
        say(f"### {' '.join(command)}")
        try:
            COMMANDS[name](ctrl, textures, args)
        except Exception:
            say(traceback.format_exc())
    ctrl.Shutdown()
    cap.Shutdown()
except Exception:
    say(traceback.format_exc())
log.close()
os._exit(0)
