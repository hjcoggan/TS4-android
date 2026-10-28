# 06: Starting the game: Play session, Direct shortcut, not set up

**What to build:** Three ways into the game, each with a clear way back.

- **Play session:** PLAY and Safe start (`--safe`) start the game, hide the Launcher, and show the home screen again when the game exits. The exit is classified as normal, GPU failure ("switch renderer") or crash, with "Open crash report" after a crash, copying WWE All Stars' exit classification (ADR-0004). After a failure or crash Safe start is offered prominently.
- **Direct shortcut:** "Create shortcut" on the home screen creates or replaces "Top Spin 4" shortcuts on the Desktop and in the Start menu that run the game exe with no arguments from its own folder, using the saved Settings file.
- **Not set up:** when the game is started without command-line game folders and the prepared disc or TU1 isn't found (using ticket 05's lookup), it starts the Launcher beside it with an argument naming the reason and exits; the Launcher opens on the setup page showing that reason. With no Launcher beside it, the game shows a message box saying setup is needed.

Spec: User Stories 35–38, 40–45; Implementation Decisions "Game session from the Launcher", "Direct shortcut", "Not set up".

**Blocked by:** 01 (Home screen tracer), 05 (Game files location)

**Status:** ready-for-agent

- [ ] Launcher.Tests: home status after a normal exit, a GPU failure exit and a crash exit; Safe start prominent only after the last two; the crash report path is offered after a crash
- [ ] Launcher.Tests: the shortcut writer gets the game exe as target, no arguments and its folder as working folder, for Desktop and Start menu; a second call replaces rather than adds
- [ ] Host test: the not-set-up decision returns "open the Launcher with reason" when the disc or TU1 is missing, and nothing when both are found or folders are given on the command line
- [ ] Headless smoke run with developer folders on the command line is unchanged
- [ ] `pwsh scripts/build.ps1 -Action Check` passes
