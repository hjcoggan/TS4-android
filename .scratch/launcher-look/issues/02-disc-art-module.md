# 02: Disc art module

**What to build:** The Disc art module in Launcher.Core. Given a disc `.iso` or the prepared disc folder, it decodes the images ticket 01 recommends and writes them as PNG to an `art` folder in the per-user folder, with a multi-size `.ico` from the title icon and a small index naming what was found and the disc it came from. When the setup page accepts a disc, the art is read from the `.iso`; a failure is logged and never fails the disc row. Spec: `.scratch/launcher-look/spec.md` (Implementation Decisions "Disc art module", "Setup hook"); format details in `docs/research/disc-art.md`.

**Blocked by:** 01 (Disc art inventory); `.scratch/launcher-ux/issues/04-setup-page-check-on-pick.md` (the disc row's accept step)

**Status:** ready-for-agent

- [ ] Launcher.Tests: fixture packages generated in the tests (no game art) give the PNGs, the `.ico` and the index; a package without the image gives no file and no error; a damaged package gives a logged reason and no file
- [ ] Launcher.Tests: accepting a disc on the setup page writes the art; an art failure leaves the disc row accepted
- [ ] Run once against the owner's `.iso`: the art folder holds the four images, each matching ticket 01's evidence
- [ ] `pwsh scripts/build.ps1 -Action Check` passes
