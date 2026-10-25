"""Questions to a RenderDoc capture, run inside qrenderdoc (scripts/rdoc.ps1 query starts it).

qrenderdoc passes no arguments to a script, so scripts/rdoc.ps1 hands them over in the
TS4_RDOC environment variable as JSON: {"capture": path, "out": dir, "commands": [[...], ...]}.
Every command runs against one replay, so ask several questions per run: a replay of a frame
capture takes a minute or more. Results go to <out>/query.txt (and PNGs beside it).

Commands:
  draws [--indices N,N]       every draw: event, index count, pixel shader, its textures
  draw EID                    one draw: viewport, scissor, targets, textures with view format,
                              swizzle and mips, samplers, and every constant block of both
                              stages; the pixel shader's SPIR-V goes to ps-EID.txt
  texture ID [--all-mips]     texel samples per mip and face, and PNGs of them
  rt EID                      the colour targets as they are after the draw, as PNGs
  pixel ID X Y [MIP] [SLICE]  one texel's value
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
            f"lod {sd.minLOD}..{sd.maxLOD} bias {sd.mipBias}")
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
        if stage == rd.ShaderStage.Pixel:
            target = ctrl.GetDisassemblyTargets(True)[0]
            text = ctrl.DisassembleShader(pipe.GetGraphicsPipelineObject(), refl, target)
            path = os.path.join(OUT, f"ps-{eid}.txt")
            open(path, "w", encoding="utf-8").write(text)
            say(f"  pixel shader SPIR-V: {path}")


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


COMMANDS = {"draws": cmd_draws, "draw": cmd_draw, "texture": cmd_texture, "rt": cmd_rt,
            "pixel": cmd_pixel}

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
