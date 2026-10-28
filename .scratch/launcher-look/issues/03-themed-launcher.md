# 03: Themed Launcher

**What to build:** The Launcher's pages draw the disc art. Home: the background under a dark scrim, the logo in place of the title text, the box art beside PLAY. Setup and Settings: the background more subdued, the logo small in the header. The window and taskbar icon is the title icon, and the Direct shortcut points its icon at the `.ico` in the art folder. Accent colours are retuned to the art. Each image that is missing falls back to the original look's resource, with the same layout. A small footer line says this is a fan recompilation, not affiliated with 2K Sports or 2K Czech, and that the art comes from the player's disc. Text keeps 4.5:1 contrast against what lies under it. Spec: `.scratch/launcher-look/spec.md` (Solution, "Theme reads the art", "Readability").

**Blocked by:** 02 (Disc art module); `.scratch/launcher-ux/issues/06-starting-the-game.md` (the Direct shortcut) and `07-controller-and-accessibility-pass.md` (the final page layout)

**Status:** ready-for-agent

- [ ] Launcher.Tests: with a full art folder the theme resources point at the art; with an empty or partial one each missing image falls back to the original resource
- [ ] Launcher.Tests: the shortcut writer sets the icon to the art folder's `.ico` when it exists, and to the exe otherwise
- [ ] The published Launcher holds no game art: no image in `out/launcher/publish-app` or the repo comes from the disc
- [ ] `pwsh scripts/build.ps1 -Action Check` passes
