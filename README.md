# Top Spin 4 port

Static recompilation of the Xbox 360 version of Top Spin 4 (2K Czech, 2011) to native code,
for Mac and Android, following the method of the Virtua Tennis 3 port (`../../Virtua Tennis 3/vt3-port`).

Planned toolchain: XenonRecomp (PowerPC to C++) and XenosRecomp (Xenos shaders), with a new
runtime for the Xbox kernel calls, a Vulkan renderer and the Android/Mac platform layers.

Game data is not part of this repository. Put your own disc dump in `../dump/` (see docs/dumping.md).
