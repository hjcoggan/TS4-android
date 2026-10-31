# 01: Shader pack prepared at setup

**What to build:** The Launcher's setup ends with a complete shader pack in the cache folder, as `.scratch/shader-setup/spec.md` ("Prepare shaders") describes, so a fresh install's Native renderer never skips a draw for a missing shader.

- **Archive reader (Launcher.Core):** every single-part entry of the prepared game folder's `.arc` archives, decoded with the vendored managed LZX decoder, kept when it is a valid container (magic `0x102A1100`/`0x102A1101`, header size + microcode size = entry length), named `ps_`/`vs_` + FNV-1a-64 as the game's dump names it, deduplicated. Layout: spec, "Implementation Decisions". If the vendored decoder cannot read the frames, stop and ask the owner before adding libmspack.
- **Module shaders (game exe):** a headless mode next to `--ts4_prepare_pipelines` that loads Loader and Swing as play does (TU1 applied), writes every valid container in their read-only data into the shader top-up folder under its dump name, prints a result line and exits. If the runtime cannot load the Modules without running guest code, record what blocks it in Comments and ask; do not reverse-engineer around it.
- **Conversion and pack:** copy WWE All Stars' converter interface, `XenosRecompConverter`, `ShaderPackFile` (build and check) and `ShaderPreparation` (ADR-0004), adapted to Top Spin 4's names. Converter id from the XenosRecomp patch series at build time, as the game computes it. XenosRecomp ships in the release's `xenosrecomp/` folder, where the game already looks for it.
- **Setup:** a "Preparing shaders" band in setup's overall progress after the disc and TU1 bands; "prepared" requires a usable pack; cancel and resume; a failure is shown and Play stays available (Native falls back to Classic as today without a pack). A stale pack (other converter id) is rebuilt before the next Play without a click; Repair rebuilds it. The space check counts the pack. Launcher.Cli runs the same step with text progress.

**Blocked by:** None

**Status:** ready-for-agent

- [ ] Launcher.Tests: the archive reader on synthetic archives (compressed entry, stored entry, skipped multi-part entry, non-shader entry, damaged archive with its name in the message)
- [ ] Launcher.Tests: shader preparation with a fake converter (pack written, progress to 1, cancel, converter failure message); setup flow with the shaders band (resume at it, failure at it, stale pack rebuilt)
- [ ] On the owner's prepared files: the reader finds 1007 unique containers in the archives, and the Module mode about 90 more
- [ ] End-to-end: fresh install folder → `topspin4-setup` → Native scene run boot to a match (`tests/scenes/exhibition-match.toml`): zero `no_shader` draw skips in the session report (live conversions allowed only for `vs_5780e914afa75799` and `ps_0c62d19530927dac`)
- [ ] One smoke run passes (`scripts/smoke.ps1`) and `pwsh scripts/build.ps1 -Action Check` passes

## Comments
