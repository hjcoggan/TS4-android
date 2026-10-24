# TU1 is the only supported build

The port supports only the 360 disc (media `60819732`) with Title Update 1 applied, and it refuses to run without TU1. TU1 patches all three Modules, and Swing is effectively relinked (198 of 221 pages differ), so addresses from the base build do not carry over. Supporting both builds would double the manifests, seeds and hooks, which isn't worth it for a build nobody should prefer. PS3 and Wii are permanently out of scope: PS3 tooling (ps3recomp) is too immature, and the Wii release is a different game.
