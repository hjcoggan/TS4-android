# 01: Disc art inventory

Type: research

**What to build:** A research note, `docs/research/disc-art.md`, listing the art on the Reference build's disc that the Launcher could show and how to decode it: the images inside the `nxeart` package (it starts with `PIRS`, an STFS package), the title image resource in `default.xex`, and the images in `Media/XUI/Startup.xzp` and `Common.xzp` (they start with `XUIZ`). For each image it records the source, purpose (background, box art, banner, logo, icon), size, format, and the decoding steps, and recommends which one fills each slot in the spec (background, box art, logo, title icon). It cites format sources (Xenia's STFS and XEX code, other STFS/XUI tools) and checks each recommendation by decoding the image once into `analysis/` (git-ignored). No image goes into the repo. Spec: `.scratch/launcher-look/spec.md`.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] The note lists every image found in the three sources, with size and format
- [ ] Each of the four slots has a recommended image, decoded once into `analysis/` as evidence
- [ ] The decoding steps for each source are concrete enough to implement without further research
