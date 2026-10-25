"""Make a program started under RenderDoc capture its next frame, over RenderDoc's target control
(run inside qrenderdoc by scripts/rdoc.ps1 trigger). Used for Xenia Edge, which the port's
--ts4_renderdoc_frame cannot reach; pressing F12 would go to whichever window has focus.

TS4_RDOC is JSON: {"out": dir}. Writes <out>/trigger.txt with the new capture's path, or why not.
"""
import json
import os
import time
import traceback

import renderdoc as rd

request = json.loads(os.environ["TS4_RDOC"])
os.makedirs(request["out"], exist_ok=True)
log = open(os.path.join(request["out"], "trigger.txt"), "w", encoding="utf-8")
try:
    ident = rd.EnumerateRemoteTargets("localhost", 0)
    if not ident:
        raise RuntimeError("no program running under RenderDoc")
    control = rd.CreateTargetControl("localhost", ident, "topspin4-rdoc-trigger", True)
    if control is None or not control.Connected():
        raise RuntimeError(f"cannot connect to RenderDoc target {ident}")
    log.write(f"target {control.GetTarget()} pid {control.GetPID()}\n")
    control.TriggerCapture(1)
    deadline = time.monotonic() + 60
    path = None
    while time.monotonic() < deadline and path is None:
        message = control.ReceiveMessage(None)
        if message.type == rd.TargetControlMessageType.NewCapture:
            path = message.newCapture.path
        elif message.type == rd.TargetControlMessageType.Disconnected:
            raise RuntimeError("the target went away before the capture arrived")
    control.Shutdown()
    log.write(f"capture {path}\n" if path else "no capture within 60 s\n")
except Exception:
    log.write(traceback.format_exc())
log.close()
os._exit(0)
