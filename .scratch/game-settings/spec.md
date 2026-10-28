# Game settings: Settings page, Launcher and Settings file

Status: ready-for-agent

Research: `docs/research/game-settings.md`. Settled in a grilling session on 2026-10-28. Finishes the UI part of pc-port-1-0 ticket 10 (settings system) and feeds ticket 30 (launcher UI).

## Problem Statement

The port now has many player settings: the renderer, four image Enhancements (Resolution scale, MSAA, anisotropic filtering, CAS sharpening), keyboard-and-mouse mode, 16 pad bindings and 24 key bindings, with more to come. A player can only change them by editing `topspin4.settings.toml` by hand or passing command-line flags. That is no use on a couch with a controller or on a Steam Deck in Game Mode. The player can't see which settings apply at once and which wait for the next start, and has no quick way to say "make it look as good as it can". The only on-screen UI is the SDK's F6 developer overlay, which lists every developer cvar and can't be driven with a pad.

Setup is just as rough: disc and Title Update 1 preparation is a console tool. And if a setting stops the game from starting, the only way out is to find and edit the Settings file.

The Settings file itself has no version, so the first rename or removal of a setting will leave old files misread.

## Solution

Three surfaces over one list of settings, with the Settings file as the one source of truth:

- **Settings page**: an in-game screen, usable with a controller, and the main way players change settings. It opens from a **PC SETTINGS** row in the game's own OPTIONS menu, from **F4**, or with a **pad combo** the game never uses. It looks like part of Top Spin 4, using the game's fonts and colours. It can be opened at any time, mid-match included, and the game ignores the pad while it is open. Every row says whether its change applies now or at the next start, or that it is set by a Launch option. A Graphics **Preset** row offers Original and Enhanced and shows Custom when the values match neither. Controls (pad and key remapping) are on this page.
- **Launcher**: a small separate program that prepares the disc and TU1, starts the game, changes the few settings that only make sense before start (renderer, GPU), and offers **Safe start**. It reads the game's own description of its settings, so nothing is defined twice. Once setup is done, starting the game skips it.
- **Settings file**: stays where it is, gains a version number and migrations, and still yields to Launch options (command line and environment), so scripts, headless runs and agents work as before.

Safe start runs one session on all defaults without changing the Settings file. The F6 developer overlay stays, in developer builds only.

On first start every setting is at its default, the original behaviour (ADR-0001). Original is itself a Preset.

## User Stories

1. As a player, I want to change settings with my controller from inside the game, so that I don't have to leave the couch or edit a file.
2. As a player, I want a PC SETTINGS entry in Top Spin 4's own OPTIONS menu, so that I find the port's settings where I'd look for any setting.
3. As a player, I want that entry to look like the game's other menu entries, so that it feels part of Top Spin 4.
4. As a keyboard player, I want F4 to open the Settings page, so that I can reach it from anywhere.
5. As a controller player, I want a pad combo that opens the Settings page during a match, so that I don't have to quit to the menus.
6. As a player, I want the pad combo to be one the game never uses, so that opening the page never triggers a shot or a menu action.
7. As a player, I want the game to ignore my pad while the Settings page is open, so that navigating the page doesn't move my player or select menu items.
8. As a player, I want the game to read my pad again as soon as I close the page, so that I can carry on playing at once.
9. As a player, I want the Settings page to use the game's fonts and colours, so that it doesn't look like a debug tool.
10. As a player, I want to move through the page with the D-pad or left stick and change values with left and right, so that it works like a console options screen.
11. As a player, I want a short description of the highlighted setting, so that I know what it does before I change it.
12. As a player, I want a live setting to change the moment I change it, so that I see the effect behind the page.
13. As a player, I want image settings (Resolution scale, MSAA, anisotropic filtering, sharpening) to change while I watch, so that the game itself is the preview.
14. As a player, I want a setting that can't change while playing marked "Applies the next time the game starts", so that I'm not left guessing whether it worked.
15. As a player, I want a pending next-start change kept and shown as pending, so that I can see what will change when I restart.
16. As a player, I want every change saved straight away, so that I don't lose it if the game closes or crashes.
17. As a player, I want a Graphics Preset row with Original and Enhanced, so that I can switch everything to the best look or back to the original in one step.
18. As a player, I want Original to be the default Preset, so that the game looks like the 360 version until I choose otherwise.
19. As a player, I want the Preset row to show Custom once I change a setting it covers, so that I know I've moved off a Preset.
20. As a player, I want choosing a Preset to set all its settings at once, so that I don't have to change each one.
21. As a player on the Classic renderer, I want Native-only settings shown as unavailable with the reason, so that I know why they do nothing.
22. As a player, I want to remap pad buttons on the Settings page, so that I can change controls without leaving the game.
23. As a keyboard-and-mouse player, I want to remap keys on the Settings page, so that I can fix a layout that doesn't suit me.
24. As a player, I want a way to reset Controls to the original layout, so that I can undo a bad remap.
25. As a player, I want a setting given as a Launch option shown as "Set by launch options: a change here lasts until the game closes", so that I understand why my saved value isn't used.
26. As a Steam Deck player, I want the page to fit and stay readable on the Deck screen, so that I can use it in Game Mode.
27. As a player starting for the first time, I want the Launcher to walk me through preparing the disc and TU1, so that I can get playing without a console tool.
28. As a player, I want the Launcher to be fully usable with a controller, so that setup works on a Deck in Game Mode.
29. As a player who has finished setup, I want starting the game to go straight into the game, so that a launcher doesn't stand in my way every time.
30. As a player, I want to pick the renderer and GPU in the Launcher, so that I can change them before the game starts, including after a GPU failure.
31. As a player whose game failed to start on the GPU, I want the Launcher to tell me to switch renderer, so that I have a way forward.
32. As a player, I want the Launcher and the Settings page to show the same settings with the same values, so that I never see two different answers.
33. As a player, I want a Safe start button in the Launcher, so that I can get into the game when a setting stops it from starting.
34. As a player, I want Safe start to leave my Settings file untouched, so that I don't lose my settings to get past one bad one.
35. As a player in a Safe start session, I want to be told that it is a Safe start, so that I know why my settings aren't in use.
36. As a player, I want my settings kept when I update the port, even if settings were renamed or removed, so that an update doesn't reset or misread them.
37. As a player, I want a setting value the port no longer allows replaced with its default and logged, so that a stale or mistyped value can't break the game.
38. As a player, I want a Settings file that is not valid TOML set aside and replaced with defaults, so that a damaged file can't stop the game from starting.
39. As a player, I want one Settings file location in my user folder, so that I always know where my settings are.
40. As a player, I don't want the F6 developer overlay in a release build, so that there is only one settings screen and I can't stumble into developer cvars.
41. As a developer, I want the F6 overlay in developer builds, so that I can still reach every cvar.
42. As a developer or agent, I want Launch options to win over the Settings file and never be saved, so that scripted, headless and per-run user-folder runs stay isolated from the player's file.
43. As a developer or agent, I want a scripted `set` of a setting to go through the same path as the Settings page, so that scene runs test what players use.
44. As a developer, I want `--safe` as a command-line flag, so that I can reproduce a Safe start session without the Launcher.
45. As a developer, I want the game to print its list of settings (keys, kinds, choices, defaults, when they apply, which renderers they work on, help text), so that the Launcher and tests read it instead of keeping their own copy.
46. As a developer, I want every Settings page row, mark and Preset result to come from a model with no UI or GPU, so that I can unit-test it.
47. As a developer, I want a new setting added to the registry to appear on the Settings page and in the Launcher's export without UI code, so that adding settings stays cheap.
48. As a developer, I want each setting's "applies now or next start" taken from the setting itself, so that the page's marks can't disagree with what the code does.
49. As a developer, I want all-default scene runs to still match the Oracle with the page and the OPTIONS row present, so that the menu entry doesn't change the original behaviour.
50. As a developer, I want the session report to list Safe start and the active Preset, so that a bug report shows how the session was set up.

## Implementation Decisions

- **One registry stays the single list of settings.** The existing player-settings registry (key, kind, default, choices, help text, renderer availability) gains what the page needs: a section (Graphics, Controls, Gameplay and so on), a display label and order, and when the setting applies, derived from the cvar's lifecycle (hot-reload means it applies now; requires-restart means next start). The marks are never written by hand.
- **Settings page model (new, pure).** It takes the registry, the values loaded from the Settings file, the set of settings pinned by Launch options and whether this is a Safe start, and gives back:
  - the page's sections and rows, each with its current value, its choices, whether it is available on the current renderer, and its mark (applies now, applies at next start and pending, or set by launch options);
  - the Preset row, with Custom detected when the covered settings match no Preset;
  - the result of a change: which cvar to set, whether to save it, and which rows change (for a Preset, all its settings).

  The ImGui page only draws this model and turns pad and key input into model actions. Copied from WWE All Stars' page model (ADR-0004) and adapted.
- **Presets.** Original (every covered setting at its default) and Enhanced (Auto Resolution scale, 16x anisotropic filtering, 4x MSAA, sharpening off). On Classic, the Native-only parts of a Preset are shown as unavailable and don't make the row read Custom. The first start is always Original; no Preset is suggested.
- **Settings page UI.** Dear ImGui through the SDK's existing overlay drawer, so it covers Classic and Native the same way. Styled with the game's fonts and colours. It reads the pad itself through the input hold that patch 0014 added (the game reads an idle pad while the page is open), because the SDK's ImGui drawer is fed only keyboard and mouse. Copied from WWE and adapted.
- **Opening the page.** F4 (freed by patch 0014). A pad combo the game never reads, chosen during implementation; candidates are checked against the game's input use first. Clicking both sticks, WWE's choice, is ruled out because Top Spin 4 uses those inputs. A PC SETTINGS row in the OPTIONS menu, below.
- **The OPTIONS row.** Top Spin 4's front end is PAM's own Lua-driven UI system ("RGui"), not Scaleform: its screens are scene files in compressed `.arc` archives, and TU1 replaces some of them by name. The row is added the way WWE added its own, adapted to RGui. Patch or redirect the OPTIONS scene data as it loads, answer the row's label through the string-table lookup, and catch the row's activation (a scene push or a script command) to open the Settings page. First try reusing the Xbox 3D entry, which is pointless on PC: force its accessibility check true, relabel it, and open the page when its screen would be pushed. If OPTIONS has no such row, add a new one to the scene data. Either way no game data is committed (ADR-0006); any change is made at runtime from the player's own files. Addresses found so far are in the addendum to `docs/research/game-settings.md`; confirm them before use.
- **Settings file schema.** A top-level `version` key, starting at 1, plus an ordered list of migrations that run on load before values are applied, starting empty. A file with no version is treated as version 1. A file newer than the build is read as well as possible and left unchanged on disk. Saving one key keeps rewriting only that line, through a temporary file and a rename (as today).
- **Precedence is unchanged.** Compiled-in default, then the developer config beside the exe, then the Settings file, then `REX_*` environment variables and command-line flags. Launch options are never written to the Settings file.
- **Safe start.** A `--safe` flag. Every player setting takes its default for the session, the Settings file is neither applied nor written, and the session report and log say it is a Safe start. Changes made on the page during a Safe start last only until the game closes, and their rows say so. The Launcher's Safe start button starts the game with `--safe`. Nothing offers Safe start automatically.
- **Settings export.** The game can print the registry (key, kind, choices, default, when it applies, renderers, section, label, help) in a stable machine-readable form, from a command-line flag that prints and exits without needing game data. The Launcher and tests read this export; the Launcher keeps no list of its own.
- **Launcher.** Copy WWE All Stars' Avalonia launcher (ADR-0004) and cut it down to: first-run disc and TU1 preparation (the existing Launcher.Core logic behind the console tool), Play, the before-start settings (renderer, GPU) written to the same Settings file with the same line-preserving rules, Safe start, and showing the game's GPU failure exit code as "switch renderer". It is fully usable with a controller. After setup, the game exe starts straight into the game without it.
- **F6 developer overlay.** Compiled into developer builds only; release builds leave it out.
- **Session report.** Records Safe start and the active Preset (or Custom) next to the existing list of enhancements.
- **Settings file location.** Stays in the per-user folder only. No portable mode.

## Testing Decisions

A good test checks what the player or the Launcher sees (rows, marks, values, saved file contents, arguments), given settings, file and Launch options. It doesn't check how the model computes them.

- **Seam 1: the settings model (host unit tests, no GPU).** Extend the existing settings tests. Cases:
  - all defaults give Original on the Preset row and default rows;
  - each setting's mark follows its cvar lifecycle;
  - a next-start change is saved and shown as pending without being applied;
  - a pinned setting shows "set by launch options" and is not saved when changed;
  - choosing Enhanced sets its settings, and changing one of them gives Custom;
  - on Classic, Native-only rows are unavailable and don't cause Custom;
  - versioning: a file with no version loads as 1, a migration renames a key and keeps its value, and a file newer than the build is not rewritten;
  - a Safe start ignores the file, writes nothing, and changes last only for the session;
  - invalid values and a corrupt file still recover (existing cases);
  - the settings export includes every registry setting with its fields, and reading it back gives the same registry.
- **Seam 2: Launcher.Core (existing Launcher.Tests).** Reading the settings export; writing a before-start setting into an existing Settings file without disturbing other lines; the Play and Safe start arguments; mapping the GPU failure exit code to the switch-renderer message.
- **Seam 3: scene runs (existing scene runner, headless).**
  - The OPTIONS row is present with its label, and selecting it opens the Settings page.
  - F4 and the pad combo open it mid-match.
  - While the page is open, pad input doesn't reach the game.
  - A live change lands, and the Settings file in the run's own user folder holds it afterwards.
  - With `--safe`, the file is unchanged after the run.
  - The existing all-default dual-mode scenes still match the Oracle.

  The page needs a way to be driven by script (open, move, change), through the existing script input. That way scene runs test the real page.
- Prior art: the existing settings, pad-binding and script-input host tests, Launcher.Tests, the scene runner tests, and the image-quality tickets that proved live changes with a scripted `set`.

## Out of Scope

- Settings pages drawn natively in the game's own RGui system. They are a post-1.0 goal; 1.0 ships the native OPTIONS row opening the ImGui page.
- A "Restart now" button for next-start settings.
- Portable mode (settings beside the exe).
- Offering Safe start automatically after failed starts.
- Suggesting a Preset for the player's GPU or the Steam Deck on first start.
- More Presets than Original and Enhanced.
- Remapping in the Launcher.
- New Enhancements; this spec only exposes existing and future settings.
- Mod settings (ADR-0001: mods come after 1.0).

## Further Notes

- The OPTIONS row is the riskiest part. The RE pass couldn't decompress the scene archives, so neither the row layout nor the Xbox 3D shortcut is confirmed. Do the RE before the page wiring that depends on it, and keep F4 and the pad combo working on their own so the page ships even if the row slips.
- An Avalonia launcher under Proton is unverified, and so is the exact wording of Valve's launcher guidance cited in the research.
- ADR-0007 records why the Settings page stays ImGui and opens from a native OPTIONS row, with native RGui pages deferred.
