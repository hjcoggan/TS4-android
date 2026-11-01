# Launcher UX: first-run setup and Play

Research date: 2026-10-28. The owner said the launcher should "be like best practice, very
intuitive for users; currently I don't find it intuitive." This note maps what the launcher
does today, collects what primary sources say about first-run setup and play flows, compares
comparable recomp launchers, and ranks the changes by impact. It recommends; it decides nothing
and designs no tickets. No code was changed and neither the game nor the launcher was run, so
everything about the window comes from reading its source.

Where settings live (launcher versus in-game page) is already covered by
[game-settings.md](game-settings.md) and is not repeated here. That note's conclusion stands:
the launcher stays small, and after setup the game exe starts straight into the game
(game-settings.md §3.1, option D).

Heads read:

- this repo at `c5b1c00` (branch `game-settings`);
- WWE All Stars at `D:\Projects\WWE_ALLSTARS-RECOMP` (`387e216`), written `WWE:`, whose launcher
  ours was copied from (ADR-0004);
- upstream repos at the commits under Sources, cited with a prefix: `UR:` Unleashed Recompiled,
  `MR:` MarathonRecomp, `Z64:` Zelda64Recomp, `SOH:` Ship of Harkinian, `DOL:` Dolphin, `RP:`
  RPCS3, `AV:` Avalonia. A
  citation `UR:UnleashedRecomp/ui/installer_wizard.cpp:1160-1173` is the base URL under Sources
  plus `path#L1160-L1173`.

**[inferred]** marks my own reasoning. **[unverified]** marks a claim I could not check against
a primary source or a run. Every other claim has a URL or a `file:line` beside it.

## Summary and recommendation

The owner's feedback: "what is unintuitive is that I did not see any parameters possible to
set there, etc, I don't know, it felt weird." §0 traces exactly why no settings showed. In
short:

- **On the owner's machine the launcher almost certainly showed no settings at all.** The disc
  and TU1 are already prepared (`%LOCALAPPDATA%\TopSpin4Recomp\gamedata` and `updatedata` both
  hold the `.topspin4-prepared` marker), so the launcher opens straight on its Play screen. The
  published launcher, `out/launcher/publish-app/topspin4-launcher.exe`
  (`scripts/build.ps1:48-53`), is not beside `topspin4.exe`
  (`out/build/win-amd64-relwithdebinfo/`). With no game exe beside it, the launcher can't read
  the game's settings list and **skips the settings rows without a word**
  (`MainWindow.axaml.cs:149-150`). What remains is the title, "Everything is ready.", PLAY and
  Safe start **[inferred from the files on disk; which exe the owner opened is not known]**.
- **Even when it works, it shows one setting.** By design the launcher lists only settings that
  apply at next start in Graphics (`SettingsSchema.cs:57-61`). Today that is only the renderer;
  resolution scale, MSAA, filtering and sharpening apply live and are in-game only
  (`src/settings/settings_cvars.cpp:11, 25-34`). The row is a plain button reading "Renderer:
  classic" under PLAY, with no "Settings" heading. Nothing says that more settings exist in
  game, or how to open them (F4, Back + Start, `src/settings/open_combo.h:1`).
- **The main screen doesn't say what you can do.** There is no Settings, Help, About or Exit
  entry. Every launcher surveyed has a visible Settings or Config entry on its main screen:
  Zelda64 ("Settings"), WWE ("SETTINGS", with "You can also change them in game on the PC
  SETTINGS page (F4)"), Dolphin ("Config", "Graphics", "Controllers") and RPCS3 (Config and
  Pads in the toolbar) (§0.3).

The setup flow behind it is sound: it checks the right things, resumes a stopped setup, never
writes to the player's files, and is driven by a pad. Its friction is in the presentation:
one centred page, a file the player may not know (Title Update 1) asked for before it is
explained, checks only after SET UP, and errors in one red line away from the input at fault.

The ranked changes, each tied to a friction point (§0, §1) and a source (§2, §3):

1. **Make settings visible and honest on the main screen.** Give the Play screen a "Settings"
   entry, a peer of PLAY, that opens a panel with the before-start rows under a heading. Add
   one line saying the rest are in game: "More settings (resolution, anti-aliasing, controls)
   are in game: press F4, or hold Back + Start." When the settings list can't be read, say so
   and say why, instead of showing nothing (F0a-F0d). Sources: Zelda64, WWE, Dolphin, RPCS3
   (§0.3); Nielsen 1, visibility of system status, and 6, recognition rather than recall
   (§2.4). Effort: small to medium.
2. **Find the game exe reliably and say when it's missing.** Check for `topspin4.exe` when the
   window opens, not when PLAY is pressed. Make the dev build put the launcher beside the game
   exe (or pass `--game-exe`), so the owner sees what players will see (F8). Effort: small.
3. **Give the main screen a clear hierarchy.** One primary action (PLAY), a short row of
   labelled secondary actions (Settings, Help, Exit), a status line, and Safe start moved out
   of the main column (§0.4). Effort: small to medium.
4. **Check each file the moment it is chosen and show the result on its own row.** Today a
   picked or auto-found file is shown only as a raw path, and nothing is checked until SET UP
   (§1.2 F1, F2). Unleashed and Zelda64 check on pick and mark the slot done (§3.1, §3.3);
   NN/g's "safeguard against likely mistakes" and Microsoft's in-place errors say the same
   (§2.3). A row like "Top Spin 4 disc ✓ (Xbox 360, supported edition)" or "This is a disc of
   another game" is the biggest single gain. Effort: medium; the checks exist
   (`DiscSetup.Check`, `TitleUpdateSetup.Check`), they need splitting from extraction.
5. **Explain Title Update 1 before asking for it, and say where it comes from.** The only
   explanation of what TU1 is and where to find it appears inside error messages
   (`TitleUpdateSetup.cs:31-34`), so a first-time player meets it only after failing (F3). WWE
   players asked exactly this ("Where can I find the title update?",
   `WWE:docs/research/community-feedback.md:66`). Unleashed shows a short tutorial before its
   picker opens and links a dumping guide (§3.1). Microsoft: a wizard page should give "enough
   information so that referring to documentation … is unnecessary" (§2.1). Effort: small.
6. **Put each error next to the input that caused it, in plain words, with one next action,
   and move focus to that input.** Today all setup errors share one red line under both
   panels (`MainWindow.axaml:43`), they carry hex title and media IDs and version numbers
   (`DiscSetup.cs:56-61, 76-79`), and after a failure focus returns to SET UP, inviting a retry
   of the same bad file (`MainWindow.axaml.cs:236`) (F4, F5). Microsoft and NN/g both say:
   problem, cause, solution; near the source; codes only for diagnosis (§2.3). Effort: small
   to medium.
7. **Remove the dead ends.** Two messages tell the player to "choose a cache folder", but the
   window has no way to do that, only the `--cache` command-line flag
   (`Preparation.cs:79-80`, `PreparedFolder.cs:70-72`, `Program.cs:8`) (F6). The space needed
   (about 6.2 GB) is not shown before setup starts (F7). Unleashed has a space page with
   required and available sizes (§3.1). Effort: small each.
8. **Give the setup screen a clear order and one obvious next step.** Number the two inputs
   ("1 Your disc", "2 Title Update 1"), show the step state, and keep one primary button
   whose label says what happens ("Set up the game" or Zelda64's swap from "Select ROM" to
   "Start game", §3.3). Use one word for the task throughout; today the button says SET UP,
   the messages say "preparation", and the cancel button says Stop (F9). Effort: small.

Further changes, lower impact: report crashes and other exits the way WWE does (F12); make the
renderer row a labelled choice with plain names instead of a cycling button whose text is the
raw value `classic` (F10, part of change 1); spatial pad navigation
with `XYFocus` and a consistent B = back (F13, F14); an Exit button reachable by pad; left-aligned
body text at 18 px or more (F15); one progress bar across both steps (F16); drag and drop.
The gap table (§4) lists them all with effort.

Questions only the owner can answer are in §5. The first ones: which launcher exe did you open,
and did it show a "Renderer" row at all? Which settings did you expect to find there?

## 0. Why settings weren't visible

### 0.1 What gates the settings rows

The rows appear only when all four of these hold, in this order:

1. **Setup is complete.** The Play view, which holds the rows, is shown only in the `Ready`
   phase (`MainWindow.axaml.cs:191-195`, `MainWindow.axaml:55-68`). On a first run nothing about
   settings is visible until the disc and TU1 have been prepared, which takes minutes. Nothing on
   the Setup view says settings will come later.
2. **`topspin4.exe` is beside the launcher**, or the launcher was started with
   `--game-exe` (`MainWindow.axaml.cs:149`, `LaunchArguments.cs:10-14`, `Program.cs:19`). If not,
   `LoadBeforeStartSettings` returns **without any message** (`MainWindow.axaml.cs:150`).
3. **The game prints its settings list** within 20 s (`SettingsExport.cs:15-56`). On failure a
   red line shows the error text (`MainWindow.axaml.cs:155-160`), for example "The game did not
   print its settings in time."
4. **A setting is next-start, in Graphics, and a choice or bool**
   (`SettingsSchema.cs:57-61`). In the current registry only `ts4_renderer` qualifies
   (`src/settings/registry.h:125-129`; it is `kRequiresRestart`, the four image settings are
   `kHotReload`, `src/settings/settings_cvars.cpp:11, 25-34`). Controls bindings are next-start
   too, but are left out on purpose ("remapping is the Settings page's job",
   `SettingsSchema.cs:54-56`). The spec sets this scope: the launcher "changes the few settings
   that only make sense before start (renderer, GPU)" (`.scratch/game-settings/spec.md:20`).

### 0.2 What the owner most likely saw

- The prepared data on this machine is complete: both folders hold `.topspin4-prepared`
  (checked on disk, `%LOCALAPPDATA%\TopSpin4Recomp\gamedata` and `updatedata`). So the launcher
  opens on the Play view (`SetupFlow.cs:21`).
- `scripts/build.ps1 -Action Launcher` publishes the window to
  `out/launcher/publish-app/topspin4-launcher.exe` (`scripts/build.ps1:48-53`). The game exe is
  in `out/build/win-amd64-relwithdebinfo/topspin4.exe`. The comment says the launcher "sits beside
  topspin4.exe" (`build.ps1:48`), but no step puts it there.
- So gate 2 fails, and the window shows only: "TOP SPIN 4", "RECOMP", "Everything is ready.",
  PLAY, a "Safe start" button with its note, and the hint bar. No settings, no error, nothing to
  say why. Pressing PLAY would then say "topspin4.exe was not found next to the Launcher. Put the
  Launcher in the game's folder." (`MainWindow.axaml.cs:111-114`) **[inferred: I did not run the
  launcher and don't know which copy the owner opened]**.
- Had the exe been beside it, the owner would have seen one extra button, "Renderer: classic",
  with the registry's help under it, appearing a moment after the window opened, below PLAY
  (`MainWindow.axaml.cs:164-186`).

Friction points:

- **F0a. Silent failure.** A missing game exe hides the settings with no message. Nielsen 1:
  "The design should always keep users informed about what is going on"
  ([10 heuristics](https://www.nngroup.com/articles/ten-usability-heuristics/)).
- **F0b. No entry point.** No "Settings" label, heading or button anywhere; the one row is
  styled exactly like Safe start (`MainWindow.axaml.cs:171-172`, class `secondary`). A
  player can't recognise "settings live here" (Nielsen 6, recognition rather than recall).
- **F0c. One setting, with nothing pointing to the rest.** The in-game page opens with F4,
  Back + Start, or the PC SETTINGS row (`.scratch/game-settings/spec.md:19`,
  `src/settings/open_combo.h:1`), but the launcher never says so. WWE's settings panel says
  "Used when the game starts. You can also change them in game on the PC SETTINGS page (F4)."
  (`WWE:launcher/src/Launcher.App/MainWindow.axaml:271-272`).
- **F0d. Late, shifting layout.** The row is added after the export returns, up to 20 s later,
  below PLAY, so the screen changes after it first appears (`SettingsExport.cs:28`,
  `MainWindow.axaml.cs:196-200`).

### 0.3 How other launchers expose options from the main screen

| Launcher | Settings entry on the main screen | Source |
|---|---|---|
| Zelda64Recomp | "Settings" is a menu item beside "Start game", "Setup controls", "Mods", "Exit" | `Z64:assets/launcher.rml:42-61` |
| WWE All Stars (our source) | "SETTINGS" button on the home screen opens a side panel titled SETTINGS with "Reset to defaults" and "Back", the startup rows, and the line pointing to the in-game page | `WWE:…/MainWindow.axaml:183, 256-282`; `MainWindow.axaml.cs:89, 1732` |
| Dolphin | Toolbar: "Open", "Play", "Stop", …, "Config", "Graphics", "Controllers" | `DOL:Source/Core/DolphinQt/ToolBar.cpp:115-131` |
| RPCS3 | Toolbar: open, refresh, fullscreen, stop, start, config, controls ("Configure graphics", "Configure controls" …) | `RP:rpcs3/rpcs3qt/main_window.ui:544-550, 634-701` |
| Unleashed / Marathon | No launcher after install; options are an in-game menu from the title screen | game-settings.md §2.1-2.2 |
| Ship of Harkinian | No launcher; F1 opens an ImGui menu bar in game | game-settings.md §2.4 |

Every launcher that has a main screen shows its settings as a named, always-visible entry next
to Play. None makes the player find settings as unlabelled rows that may or may not load. The
ports with no launcher put settings in game, which ours also does; the gap is that our
launcher doesn't say so.

### 0.4 Layout, hierarchy, and "it felt weird"

Reading `MainWindow.axaml` and `Theme.axaml`, these are the likely causes of "weird" beyond
the missing settings **[inferred; not checked against a screenshot]**:

- **No visible list of what you can do.** The Play screen has one action (PLAY) and one
  recovery action (Safe start). Dolphin, RPCS3, Zelda64 and WWE all show four to ten labelled
  actions at a glance (§0.3). XAG 112 asks for persistent ways back and a reachable Quit
  ([XAG 112](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/112)).
- **The title dominates; the content floats.** An 84 px "TOP SPIN 4" and a "RECOMP" kicker sit
  above a column centred both ways in a 1100 × 680 window (`MainWindow.axaml:5, 10-14`). With
  only "Everything is ready." and PLAY under it, the screen reads as a splash screen, not a
  launcher. Apple: a splash screen should be shown "just long enough for people to absorb the
  information at a glance"
  ([HIG Onboarding](https://developer.apple.com/design/human-interface-guidelines/onboarding)).
- **No grouping.** Status, PLAY, settings and Safe start are siblings in one centred
  `StackPanel` (`MainWindow.axaml:55-68`), with no headings and no difference in style between
  a setting and a recovery action. Microsoft's "page integrity": "the contents of a page should
  belong together"
  ([Win32 UX, Wizards](https://learn.microsoft.com/en-us/windows/win32/uxguide/win-wizards)).
- **A recovery action on the main path.** Safe start is shown on every visit at the same weight
  as the settings (F11). Nielsen 8: "Interfaces should not contain information that is
  irrelevant or rarely needed."
- **All text centred.** Paragraphs are centred (`MainWindow.axaml:18, 56, 65`); XAG 101 asks for
  left-aligned body text (§2.5).
- **A hint bar that doesn't match the screen.** "B: stop" is shown where B does nothing
  (`MainWindow.axaml:72-73`, `MainWindow.axaml.cs:284-286`).
- **No status.** "Everything is ready." never changes to "Game running" or how the last session
  ended; after PLAY the window simply disappears (`MainWindow.axaml.cs:127`).

A layout that addresses these, in the shape every surveyed launcher shares **[inferred]**: the
title smaller and to one side; PLAY as the one primary button with a status line under it; a
row of labelled secondary buttons, Settings, Help (where to get the files, where the in-game
page is), Open logs, Exit; Safe start under "Trouble starting?" or offered after a failed start.
WWE's home screen is already built this way (`WWE:…/MainWindow.axaml:168-227`) and can be
copied under ADR-0004.

## 1. Current flow walkthrough

### 1.1 Screens, states and labels

The launcher is one window, `topspin4-launcher.exe` (`Launcher.App.csproj:7`), 1100 × 680,
minimum 720 × 480 (`MainWindow.axaml:5`). One `ScrollViewer` holds a centred, 760-wide stack
(`MainWindow.axaml:9-11`) with the title "TOP SPIN 4" and the kicker "RECOMP"
(`:12-14`), then one of two views. A hint bar is always shown at the bottom: "D-pad or stick:
move      A: select      B: stop" (`:72-73`).

The state model is `SetupFlow` with four phases, `NeedsInputs`, `Preparing`, `Failed` and
`Ready` (`SetupFlow.cs:3-12`). The window shows the Setup view for the first three and the
Play view for `Ready` (`MainWindow.axaml.cs:191-195`).

**Setup view** (`MainWindow.axaml:17-52`):

| Element | Label or text | Source |
|---|---|---|
| Intro | "To play, you need a disc image (.iso) of your own Top Spin 4 Xbox 360 disc and its Title Update 1. Setup checks both and copies the game files once. It never changes your disc image or update file." | `:18-19` |
| Disc panel | heading "Disc image"; path or "No disc image chosen"; buttons "Choose disc image…" and, when more than one was found, "Next one found" | `:21-30`, `.cs:204, 206` |
| TU1 panel | heading "Title Update 1"; path or "No Title Update 1 chosen"; "Choose Title Update 1…", "Next one found" | `:32-41`, `.cs:205, 207` |
| Message | one red text line for any setup error or the stop note | `:43`, `.cs:214-215` |
| Progress | bar, a step line such as "Extracting the disc  42%, 7 min left", button "Stop" | `:45-49`, `SetupFlow.cs:97-113` |
| Commit | big yellow "SET UP", disabled until every needed input is chosen | `:51`, `.cs:211-212`, `SetupFlow.cs:36-39` |

A panel is hidden when the cache already holds that half, so a stopped or half-finished setup
asks only for what is missing (`.cs:202-203`, `Preparation.cs:37-41`).

**Play view** (`MainWindow.axaml:55-68`):

| Element | Label or text | Source |
|---|---|---|
| Status | "Everything is ready." (replaced by launch or exit messages) | `:56-57` |
| Commit | big yellow "PLAY" | `:58` |
| Before-start settings | one button per setting, text "Renderer: classic", and the registry's help text under it | `.cs:164-186` |
| Settings message | red line for export or save failures | `:61`, `.cs:157, 176-177` |
| Safe start | button "Safe start" and "If the game won't start: runs once with the default settings and leaves your settings file unchanged." | `:63-67` |

There is no Back, Exit, About, Open logs, Repair or "re-run setup" control; the window is closed
with the title bar's close button (`MainWindow.axaml`, whole file).

### 1.2 The first-time path, with friction

1. **Open the launcher.** It searches the launcher's folder, `import\`, Downloads and Desktop
   (`InputFinder.cs:11-21`) for any `*.iso` and any file named `TU_*`
   (`InputFinder.cs:24-29`), and silently puts the first of each in the panels
   (`MainWindow.axaml.cs:59-66`). Focus goes to SET UP if both were found, else to the first
   Choose button (`.cs:232-240`).
   - **F1. Auto-fill is silent and by name only.** Any ISO in Downloads (another game, a Linux
     installer) is shown as if chosen, as a full path in small muted text. The player is not
     told it was found for them, nor whether it is right. Ship of Harkinian also auto-searches
     but asks "It appears to be: … Use this rom?" for each find (§3.4).
2. **Choose the disc.** The picker is titled "Choose your Top Spin 4 disc image" and filters
   `*.iso` (`.cs:42, 75-86`). Only an `.iso` is accepted; an extracted folder or a Games on
   Demand package is not (`DiscSetup.cs:12-30`).
   - **F2. Nothing is checked until SET UP.** Picking a wrong file looks exactly like picking
     the right one.
3. **Choose Title Update 1.** The picker is titled "Choose Title Update 1" and shows all files
   (`.cs:43, 81-82`), because the package has no extension.
   - **F3. TU1 is never explained up front.** The intro names it but doesn't say what it is or
     where it comes from. The explanation ("the free update Top Spin 4 got on Xbox 360 … copy
     it from your own console (the TU_1A5822P_000000C000000.0000000000083 file in the console's
     Content or Cache folder)") is the `WhatTitleUpdate1Is` constant, used only inside errors
     (`TitleUpdateSetup.cs:16-34`).
4. **Press SET UP.** The flow checks the disc, then TU1, then extracts and unpacks
   (`Preparation.cs:53-84`). Progress shows "Checking the disc image", "Checking Title Update
   1", "Extracting the disc  42%, 7 min left", "Unpacking Title Update 1  …"
   (`SetupFlow.cs:97-113`). Stop leaves the partial files and says "Stopped. The next
   preparation resumes where this one left off." (`SetupFlow.cs:80-84`).
   - **F7. Space needed is not shown first.** The disc is 5,737,767,977 bytes
     (`.scratch/pc-port-1-0/issues/12-disc-preparation.md:21`) plus 512 MB head room
     (`Preparation.cs:23`); the player learns this only from a DiskFull error.
   - **F16. Progress restarts per step** (`DiscSetup.cs:86`, `TitleUpdateSetup.cs:87` each call
     `reporter.Start`), and the checking steps report no fraction, so the bar sits at 0
     (`SetupFlow.cs:108`, `MainWindow.axaml.cs:221-225`).
5. **If it fails.** The phase becomes `Failed`, the message is shown in the shared red line, and
   focus goes to SET UP because the inputs are still set (`SetupFlow.cs:74-89`,
   `MainWindow.axaml.cs:236`). Examples of what the player reads:
   - "This disc image is not the supported Top Spin 4 disc: it is another edition of Top Spin 4
     (media ID 0A1B2C3D). The port runs only the Xbox 360 disc with media ID 60819732."
     (`DiscSetup.cs:59, 76-79`)
   - "…its default.xex is 0.0.0.2, and the supported disc's Modules are 0.0.0.3."
     (`DiscSetup.cs:60-61`)
   - "That package is not Title Update 1 for the supported Top Spin 4 disc: its … raises the
     Module to …" (`TitleUpdateSetup.cs:63-64, 78-81`)
   - "Preparation is not allowed to write to <folder>. Choose a cache folder you can write to."
     (`Preparation.cs:79-80`)
   - "Not enough disk space for the game data: it needs 6.3 GB free on C:\ and only 2.1 GB is
     free. Free some space or choose another cache folder." (`PreparedFolder.cs:70-72`)
   - "Preparation failed: " plus a raw .NET exception message (`SetupFlow.cs:85-89`).
   - **F4. Errors are far from their cause.** One line serves both inputs; the panel at fault is
     not marked.
   - **F5. Jargon and codes.** Media ID, title ID, Module, version numbers and "content
     package" are developer terms (they match `GLOSSARY.md`, which is written for the project,
     not for players).
   - **F6. Dead end.** "Choose a cache folder" has no control in the window; only
     `--cache <folder>` on the command line (`Program.cs:8, 18`).
   - Focus on SET UP after a failure invites pressing it again with the same file.
6. **Setup finishes.** The view switches to Play with focus on PLAY (`.cs:191-200, 234`). The
   launcher then runs `topspin4.exe --print-settings-schema` in the background (up to 20 s,
   `SettingsExport.cs:15-28`) and adds the renderer row under PLAY when it returns
   (`.cs:147-162`).
   - **F8. A missing game exe is found late.** If `topspin4.exe` is not beside the launcher, the
     settings rows silently never appear (`.cs:149-150`), and PLAY then says "topspin4.exe was
     not found next to the Launcher. Put the Launcher in the game's folder."
     (`.cs:111-114`): after a full setup.
   - **F9. Three words for one task.** "Setup" (intro), "SET UP" (button), "preparation"
     (stop and failure messages), and "Stop" versus the hint bar's "B: stop".
7. **The renderer row.** A button reading "Renderer: classic"; each press cycles to the next
   choice and saves it at once (`.cs:171-179`, `BeforeStartSettings.cs:34`). The help under it
   is the registry text: "Renderer: classic (Xenos GPU emulation, the reference) or native
   (Vulkan; not built yet, so Classic draws until it is). Takes effect at the next start"
   (`src/settings/registry.h:125-129`).
   - **F10.** A button that changes a value when pressed, labelled with a raw lowercase value,
     reads as an action, not a setting. The help text is jargon (Xenos, Vulkan, "the
     reference") and stale ("not built yet").
8. **Safe start** sits permanently under PLAY with its explanation (`MainWindow.axaml:63-67`).
   - **F11.** A recovery action shown on every visit, with equal weight to the settings, is
     noise for the 99 % of starts that work **[inferred]**. The spec only requires the button to
     exist (`.scratch/game-settings/spec.md:95`).
9. **Press PLAY.** The game starts with `--game_data_root` and `--update_data_root` only
   (`LaunchArguments.cs:31-40`); the launcher hides and waits (`MainWindow.axaml.cs:116-128`).
   - If the game exits with `0x47505546` (GPU failure), the launcher comes back with "The
     graphics card or its driver stopped working while the game was running. Switch the
     renderer below, then press Play again." (`GameExitReport.cs:10-15`,
     `MainWindow.axaml.cs:130-136`).
   - **Any other exit, crash included, closes the launcher** (`MainWindow.axaml.cs:137`). The
     game shows its own crash dialog with the report path (`src/crash_report.cpp:258-270`),
     and its comment says "The process ends with the exception code, which the launcher
     reports" (`src/crash_report.cpp:344`), which the launcher does not.
   - **F12.** No status while the game is starting or running, and no report after a crash.
     WWE classifies exits and offers "Open crash report" (§3.5).
10. **Later starts.** With the cache complete, the launcher opens on the Play view
    (`SetupFlow.cs:21`); starting `topspin4.exe` directly finds the prepared data itself
    (`src/topspin4_app.h:85-94`). What `topspin4.exe` does when started before setup is not
    designed **[unverified]**.

### 1.3 Pad, keyboard and mouse

- The pad is polled every 16 ms through XInput, first connected pad only, and only while the
  window is active (`MainWindow.axaml.cs:23, 244-252`, `Platform/XInputPad.cs:15-33`). D-pad or
  left stick move, A presses, B "goes back", with console-style repeat
  (`PadNavigator.cs:22-66`). Arrow keys and Escape map to the same actions
  (`MainWindow.axaml.cs:254-268`).
- **F13. Navigation is a list, not spatial.** Up and Left both mean "previous", Down and Right
  "next" (`MainWindow.axaml.cs:274-279, 290-299`). On the Setup view, Down from "Choose disc
  image…" goes to "Next one found" beside it, not to the panel below.
- **F14. B does one thing in one state.** B and Escape only cancel a running setup
  (`MainWindow.axaml.cs:284-286`); elsewhere they do nothing, though the hint bar always says
  "B: stop". There is no pad-reachable way to quit the launcher.
- The focus visual is a 3 px white border (`Theme.axaml:57-61`). Text contrast is good: muted
  text `#9AA8D0` on the base is 7.9:1, error text 8.2:1, body text 15.9:1 (computed with the
  WCAG formula from `Theme.axaml:13-22`).
- **F15. Text is centred everywhere and the small style is 16 px** (`Theme.axaml:46-50`); all
  body text is `TextAlignment="Center"` (`MainWindow.axaml:18, 56, 65`).
- No `AutomationProperties` are set, so status and error changes are not announced to a screen
  reader (`MainWindow.axaml`, whole file) **[inferred from a read; not tested with Narrator]**.

### 1.4 What WWE's launcher has that ours dropped

ADR-0004 had us copy WWE's launcher and the spec cut it to setup, Play, before-start settings and
Safe start (`.scratch/game-settings/spec.md:97`). Some of what was cut is UX, not scope:

- a "FIRST-TIME SETUP" kicker and a resume state: the button reads "RESUME SETUP", with a
  "Start over" button and "A previous setup did not finish. Choose your disc image and the
  update again, then resume it or start over." (`WWE:launcher/src/Launcher.App/MainWindow.axaml:98, 153-157`,
  `WWE:launcher/src/Launcher.App/MainWindow.axaml.cs:238-252`);
- drop zones, "Drop your disc image here, or" (`WWE:…/MainWindow.axaml:102-110, 115-125`);
- Title Update by file or by folder (`WWE:…/MainWindow.axaml:121-122`);
- "GAME FILES GO TO" the folder, with "Change…" (`WWE:…/MainWindow.axaml:145-148`);
- a home screen with a status line ("Ready to play", game running, how the last session ended)
  and actions: Settings, Open logs, Open crash report, Repair, Shortcuts, About, Exit
  (`WWE:…/MainWindow.axaml:180-227`);
- exit classification: Closed, Crashed (with plain words for each Windows exception code),
  RendererFallback and GpuFailure, with the crash report path read from the session log
  (`WWE:launcher/src/Launcher.Core/GameExitReport.cs:6-78`).

WWE's launcher is about 2,150 lines of window code (`WWE:…/MainWindow.axaml.cs`), so not all of
it is worth copying; the list above is the part that bears on the friction points.

## 2. Best-practice findings by topic

### 2.1 First-run setup as a short wizard

- **Use a wizard for an infrequent, multi-step task, but keep it short.** NN/g: "Use wizards for
  novice users or infrequent processes (e.g., configuration or setup)", and show "a list or a
  diagram of the steps involved and highlighting the current step"
  ([NN/g, Wizards](https://www.nngroup.com/articles/wizards/)). Microsoft: "Make your wizard as
  concise and streamlined as possible", "use smart defaults"
  ([Win32 UX, Wizards](https://learn.microsoft.com/en-us/windows/win32/uxguide/win-wizards),
  Guidelines › Wizard length).
- **No welcome page; make the first page functional,** with an optional "Before you begin" page
  only when "the wizard has prerequisites that are necessary to complete the wizard
  successfully" (same page, Wizard pages). Disc plus TU1 is exactly such a prerequisite
  **[inferred]**.
- **Page integrity:** "the contents of a page should belong together", one sub-task per page
  (same page, Design concepts › Page integrity).
- **Commit buttons say what they do:** "use a commit button that is a specific response to the
  main instruction (for example, Print, Connect, or Start) … Always start commit button labels
  with a verb" (same page, Commit buttons).
- **Avoid disabling the positive commit button:** "Otherwise users have to deduce why the commit
  buttons are disabled. It's better to leave commit buttons enabled, and give a helpful error
  message" unless the reason "is obvious and unambiguous" (same page, Commit buttons). Our SET UP
  is disabled until both inputs are chosen; with two visible empty rows the reason is fairly
  obvious **[inferred]**.
- **Explain inline, not in Help:** "Design wizard pages to provide enough information so that
  referring to documentation in program Help is unnecessary"; if needed, a short question-style
  help link in the lower left (same page, Help).
- **Language:** "using language your target audience uses to describe the task, not the jargon
  of the technology" (same page, Finding the right level of communication).
- **After success, move on:** "Upon successful completion, the progress page should advance to
  the next step automatically" (same page, Wizard pages). Ours does.
- **Apple, as a cross-check:** onboarding should be "fast, fun, and optional"; "Postpone
  nonessential setup flows or customization steps. Provide reasonable default settings so most
  people can immediately start"; "display these instructions near that area"
  ([HIG, Onboarding](https://developer.apple.com/design/human-interface-guidelines/onboarding)).
  "Restore the previous state when your app restarts so people can continue where they left
  off" ([HIG, Launching](https://developer.apple.com/design/human-interface-guidelines/launching)).
  Our resume logic already does this for setup.

### 2.2 Progress

From [Win32 UX, Progress bars](https://learn.microsoft.com/en-us/windows/win32/uxguide/progress-bars):

- "Don't restart progress … have all the steps in the operation share a portion of the progress
  and have the progress bar go to completion once" (Determinate progress bars).
- "Use indeterminate progress bars only for operations whose overall progress cannot be
  determined" and "Convert to a determinate progress bar once the overall progress can be
  determined" (Indeterminate progress bars). A bar parked at 0 while checking reads as stuck
  **[inferred]**.
- "Label the button Cancel if canceling returns the environment to its previous state …
  otherwise, label the button Stop" (Modal progress bars). Our "Stop" is correct.
- "Don't give the percentage completed … because that information is conveyed by the progress
  bar itself"; time left as "m minutes remaining" (Labels, Time remaining).
- "Don't provide unnecessary details … users of a setup program don't care about the specific
  file being copied" (Guidelines › General). We don't show file names; good.

### 2.3 Errors

- **Problem, cause, solution.** "Effective error messages inform users that a problem occurred,
  explain why it happened, and provide a solution so users can fix the problem"; good messages
  are "relevant, actionable, brief, clear, specific, courteous, and rare"
  ([Win32 UX, Error messages](https://learn.microsoft.com/en-us/windows/win32/uxguide/mess-error)).
- **Place them at the source.** "Use in-place errors for delayed error detection, usually errors
  found by clicking a commit button … placing them directly next to the problem whenever
  possible" (same page, Guidelines › Presentation). NN/g: "Display the error message close to
  the error's source" ([NN/g, Error-message guidelines](https://www.nngroup.com/articles/error-message-guidelines/)).
- **Codes and jargon out of the main text.** NN/g: "Hide or minimize the use of obscure error
  codes or abbreviations; show them for technical diagnostic purposes only." Microsoft: put error
  codes behind "Show/Hide details", phrased "Error code: <code>" (mess-error, Error codes).
  Nielsen's heuristic 9: "Error messages should be expressed in plain language (no error codes),
  precisely indicate the problem" ([10 heuristics](https://www.nngroup.com/articles/ten-usability-heuristics/)).
- **Prevent, then reduce correction effort.** "Safeguard against likely mistakes", "Preserve the
  user's input", "Reduce error-correction effort" (NN/g, same article). Microsoft: "It's usually
  better to prevent an error than to report one" (mess-error, Design concepts).
- **Tone.** "Be warm and relaxed … avoiding unfamiliar terms and technical jargon"; "Be ready to
  lend a hand … providing a realistic solution they can accomplish"
  ([Windows apps, Writing style](https://learn.microsoft.com/en-us/windows/apps/design/style/writing-style),
  Error messages).

### 2.4 Wording and consistency

- Nielsen 2, "speak the users' language"; Nielsen 4, "Users should not have to wonder whether
  different words, situations, or actions mean the same thing"; Nielsen 6, recognition rather
  than recall; Nielsen 8, "Interfaces should not contain information that is irrelevant or
  rarely needed" ([10 heuristics](https://www.nngroup.com/articles/ten-usability-heuristics/)).
- Windows writing style: "Lead with what's important", active voice, button text "a couple
  short words" that "represent actions", define abbreviations the first time
  ([Writing style](https://learn.microsoft.com/en-us/windows/apps/design/style/writing-style)).
- Nielsen 3: users "need a clearly marked 'emergency exit'". Our only exit is the window close
  button.

### 2.5 Controller, keyboard and text (games)

- **Same input everywhere.** Game Accessibility Guidelines (basic): "if … you have designed
  gameplay controls for a choice of either keyboard or joypad, ensure that all menus also work
  for both keyboard and joypad"
  ([GAG](https://gameaccessibilityguidelines.com/ensure-that-all-areas-of-the-user-interface-can-be-accessed-using-the-same-input-method-as-the-gameplay/)).
  Ours does.
- **Quick start.** GAG (basic): "Allow the game to be started without the need to navigate
  through multiple levels of menus"
  ([GAG](https://gameaccessibilityguidelines.com/allow-the-game-to-be-started-without-the-need-to-navigate-through-multiple-levels-of-menus/)).
  Ours: PLAY has focus on open.
- **XAG 112, UI navigation** ([XAG 112](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/112)):
  - "The UI is fully navigable by keyboard and controller digital input alone";
  - focus order "aligned with the flow of the visual design": "If a single down-press had moved
    the focus … to … any other menu element that isn't visually intuitive, this design wouldn't
    be following the meaning or operation of the UI" (F13);
  - consistent mechanics: "'A' to select, 'B' for back", with prompts in the same place on every
    screen; "press 'B' to go back to the previous menu screen" (F14);
  - linear menus loop from last to first;
  - "Grounded … players are even provided a 'Quit' menu option" reachable by keyboard alone.
- **XAG 101, text display** ([XAG 101](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/101)):
  minimum default text on PC "18 px at 1080p" (console 26 px); body text "left/right-aligned
  based on the player language preference (not centered or fully justified)"; sentence case for
  lines of text (one- or two-word labels are exempt). XAG measures body height from descender
  to ascender, not the font-size property, so whether our 16 px small style falls short needs a
  screenshot measurement **[unverified]**.
- **Steam Deck.** Valve wants any launcher fully navigable with a controller and recommends
  against requiring one at all; already summarised in game-settings.md §2.8 (read through a
  summarising fetch there, so recheck quotes).

### 2.6 What Avalonia 11.3 offers

The launcher uses Avalonia 11.3.22 (`launcher/Directory.Packages.props`).

- **Spatial navigation:** `XYFocus.NavigationModes` with Up/Down/Left/Right strategies such as
  `Projection`, and explicit `XYFocus.Left/Right` targets for wrap-around
  ([AV docs, Focus](https://github.com/avaloniaui/avalonia-docs/blob/main/docs/input-interaction/focus.md)).
  The docs add that "Avalonia currently lacks cross-platform Gamepad APIs", so the XInput
  polling stays, but it could raise arrow-key navigation and let `XYFocus` pick the target. The
  `XYFocus` sources are in the 11.3.0 tag (`AV:src/Avalonia.Base/Input/Navigation/XYFocus.*.cs`).
- **Default and cancel buttons:** `IsDefault` (Enter) and `IsCancel` (Escape)
  ([AV docs, Button](https://github.com/avaloniaui/avalonia-docs/blob/main/controls/input/buttons/button.md)).
  WWE sets `PlayButton.IsDefault` (`WWE:…/MainWindow.axaml.cs:226-227`); ours sets neither.
- **Announcements:** `AutomationProperties.LiveSetting="Polite"` or `"Assertive"` on a status or
  error `TextBlock`
  ([AV docs, Accessibility](https://github.com/avaloniaui/avalonia-docs/blob/main/docs/app-development/accessibility.md)).
- **Drag and drop:** `DragDrop.AllowDrop` with Drop handlers that read files
  ([AV docs, Drag and drop](https://github.com/avaloniaui/avalonia-docs/blob/main/docs/input-interaction/drag-and-drop.md)).
  WWE uses it for the disc and TU (§1.4).

## 3. How comparable recomp launchers do it

### 3.1 Unleashed Recompiled (in-exe ImGui installer wizard)

- **Runs from the game exe when the game isn't installed**, or with `--install`
  (`UR:UnleashedRecomp/main.cpp:327-342`, `UR:README.md:370`).
- **Pages:** SelectLanguage, Introduction, SelectGameAndUpdate, SelectDLC, CheckSpace,
  Installing, InstallSucceeded, InstallFailed (`UR:UnleashedRecomp/ui/installer_wizard.cpp:128-138`).
  The intro: "You'll need an Xbox 360 copy of Sonic Unleashed in order to proceed with the
  installation." (`UR:UnleashedRecomp/locale/locale.cpp:304-305`).
- **One page for game and update, sources auto-detected:** "Add the sources for the game and its
  title update." (`locale.cpp:316-317`). Every picked path is parsed at once as game or update;
  anything else is listed under "The following selected files are invalid:"
  (`installer_wizard.cpp:1175-1233`; `locale.cpp:490-491`). Each slot is drawn with its set or
  unset state (`installer_wizard.cpp:1034, 1303-1304`).
- **Add Files and Add Folder** (`installer_wizard.cpp:1275-1290`); the README explains both,
  including that dumps "often come in the form of very large files without associated
  extensions … the installer will automatically detect what type of content is inside"
  (`UR:README.md:77-79`).
- **A picker tutorial before the first picker opens:** "Select a digital dump with content from
  the game. These files can be obtained from your Xbox 360 hard drive by following the
  instructions on the GitHub page." (`installer_wizard.cpp:1160-1173`; `locale.cpp:516-517`).
  The README links dumping guides before install (`UR:README.md:66-71`).
- **Game and update checked together on Next:** "The specified game and title update are
  incompatible. Please ensure the files are for the same version and region and try again."
  (`installer_wizard.cpp:1438-1442`; `locale.cpp:542-543`).
- **Space page:** "Required space: %2.2f GiB" and the available space
  (`installer_wizard.cpp:728-736`; `locale.cpp:410-411`).
- **Failure page with Retry**, which returns to the first page (`installer_wizard.cpp:1423-1425,
  1498-1500`).
- **Known weakness:** "the file picker may not be available in Game Mode" on Deck; the README
  says to install in Desktop Mode (`UR:README.md:203, 276-280`).

### 3.2 MarathonRecomp

The same wizard, with SelectGame instead of SelectGameAndUpdate
(`MR:MarathonRecomp/ui/installer_wizard.cpp:128-138`).

### 3.3 Zelda64Recomp (in-exe RmlUi launcher)

- **One primary item that changes with state:** "Select ROM" when no valid ROM is known, "Start
  game" once one is; each has `autofocus` (`Z64:assets/launcher.rml:38-45`). Then "Setup
  controls", "Settings", "Mods", "Exit" (`:46-61`).
- **Checked on pick, one message per cause:** "This is not a valid ROM file.", "This ROM is not
  the correct game.", "This ROM is the correct game, but the wrong version. This project requires
  the NTSC-U N64 version of the game." (`Z64:src/ui/ui_launcher.cpp:17-44`).

### 3.4 Ship of Harkinian (first-run asset extraction)

- **Searches, then confirms what it found:** "Rom detected: <path> … It appears to be: <version>.
  Use this rom?" with Yes, No and "Find ROM" (`SOH:soh/soh/Extractor/Extract.cpp:148-170`).
  With nothing found: "No roms found. Look for one?" (`Extract.cpp:516`). A bad match says "Rom
  CRC did not match the list of known compatible roms. Trying the next one…" and links a
  validation site (`Extract.cpp:540-545`).

### 3.5 WWE All Stars (our source, Avalonia)

See §1.4: welcome and setup screen with drop zones and a visible destination, resume and start
over, a home screen with a status line and secondary actions, and exit classification with crash
report links.

### 3.6 Not surveyed

Xenia Edge/Canary game picking and the RPCS3 and Dolphin first-run flows were not read for this
note; only Dolphin's and RPCS3's main-window toolbars were (§0.3).
Emulators that run many games solve a library problem, not a one-game setup, so they are a weaker
model here **[inferred]**.

### 3.7 What the ports agree on

- Check inputs **when chosen**, and say which input is wrong (UR, Z64, SoH).
- **Say where the files come from** before asking (UR tutorial and README).
- **Show what was found and let the player confirm** (SoH), rather than filling silently.
- **One primary action at a time** (Z64's swap; UR's one Next per page).
- **Accept the forms players actually have** (UR files or folder; WWE file, folder or archive).
- **Show space before installing** (UR).
- **An Exit item reachable by pad** (Z64, WWE).

## 4. Gap table

Effort: S under a day, M one to three days, L more **[inferred guesses]**.

| # | Our behaviour (§0, §1) | Best practice (§2, §3) | Proposed change | Effort |
|---|---|---|---|---|
| F0a | Settings rows skipped silently when `topspin4.exe` isn't beside the launcher | Visibility of system status (heuristic 1) | Say "Settings can't be shown: topspin4.exe wasn't found next to the launcher" where the settings would be | S |
| F0b | No Settings label or entry; one unlabelled row styled like Safe start | A named Settings entry next to Play (Z64, WWE, Dolphin, RPCS3) | A "Settings" button beside PLAY opening a panel with a heading, Back, and the before-start rows (copy WWE's panel) | S–M |
| F0c | Nothing points to the in-game settings page | WWE's "You can also change them in game…" line | One line in the panel and on the Play screen: where the other settings are (F4, Back + Start, PC SETTINGS in OPTIONS) | S |
| F0d | Row appears up to 20 s later, below PLAY | Stable layout; show loading state | Reserve the place, show "Reading the game's settings…", or read the list once at open and cache it | S |
| — | Play screen: big title, "Everything is ready.", PLAY, Safe start | Main actions visible at a glance (§0.3, §0.4) | WWE-style home: PLAY plus status line, labelled secondary actions (Settings, Help, Open logs, Exit) | M |
| — | Dev build: launcher published apart from the game exe (`build.ps1:48-53`) | Test what players get | Publish or copy the launcher beside `topspin4.exe`, or have the dev launcher pass `--game-exe` | S |
| F2 | Files checked only after SET UP | Prevent errors; check on pick (NN/g, MS; UR, Z64) | Run the header and identity checks when a file is chosen or found; show the result on the row (✓ "Top Spin 4, supported disc" / ✗ reason) | M |
| F1 | First `*.iso` in Downloads silently filled in | Confirm what was found (SoH); recognition over recall | Only auto-fill files that pass the check; label them "Found in Downloads"; keep "Choose another…" | S with F2 |
| F3 | TU1 explained only in errors | Explain prerequisites up front (MS "Before you begin"; UR tutorial) | One line under the TU1 heading: what it is, the file name to look for, where it lives on a console; a "How do I get these files?" link to a guide | S |
| F4 | One shared red line for all errors | In-place, next to the source (MS, NN/g) | Show each error inside its panel, with a marker that isn't colour alone; move focus to that panel's Choose button | S |
| F5 | Hex IDs, versions, "Module", raw exception text | Plain words; codes under details (NN/g, MS, heuristic 9) | Lead with the plain cause and fix ("This is a different edition of Top Spin 4. The port needs the …"); put IDs and versions behind "Details" or in the log | S |
| F6 | "Choose a cache folder" with no control | No dead ends; actionable solutions (MS) | Either add "Game files go to … Change…" (WWE) or reword both messages to actions the window offers | S / M |
| F7 | Space needed unknown until DiskFull | Space page (UR) | Show "Needs about 6.2 GB on C:, 40 GB free" beside SET UP; warn before starting | S |
| F8 | Missing `topspin4.exe` reported after setup | Prevent; check early | Check at window open; show the problem before any setup | S |
| F9 | Setup / SET UP / preparation / Stop | Consistency (heuristic 4); verb labels (MS) | One term ("set up"); button "Set up the game"; resume state "Resume setup" plus "Start over" (WWE) | S |
| — | Two inputs on one centred page, no step marker | Show steps and current step (NN/g); page integrity (MS) | Number the panels "1 Disc" and "2 Title Update 1" with done marks; or two short pages. Keep one primary button | S–M |
| F10 | "Renderer: classic" cycling button; jargon help | Labelled choice; user's language | Label plus value control (e.g. two radio-style options "Native (recommended)" / "Classic (compatibility)"); rewrite the registry help for players and drop "not built yet" | S (+ registry text) |
| F11 | Safe start always under PLAY | Minimalist design (heuristic 8) | Put it in a small "Trouble starting?" area, and surface it prominently after a failed or crashed start | S |
| F12 | Only GPUF reported; other exits close the launcher silently | Visibility of status (heuristic 1); WWE's exit report | Copy WWE's `GameExitReport` classification and "Open crash report"; show "Game running" while waiting | M |
| F13 | Up/Left = previous, Down/Right = next | Focus follows visual layout (XAG 112) | Enable `XYFocus` with projection, feed it from the pad; loop linear lists | S–M |
| F14 | B only stops setup; no Exit | Consistent B = back; reachable Quit (XAG 112) | B backs out of a sub-step, otherwise focuses Exit; add an "Exit" button; hint bar shows only actions that apply now and hides pad glyphs when the mouse or keyboard is in use | S |
| F15 | Centred body text, 16 px small style | Left-aligned, ≥ 18 px at 1080p (XAG 101) | Left-align paragraphs; raise small text; measure on screen | S |
| F16 | Bar restarts per step; sits at 0 while checking | One bar to completion; indeterminate while unknown (MS) | One bar weighted over check, extract, unpack; indeterminate during checks; drop the "%" text | S |
| — | No drag and drop | WWE drop zones; Avalonia `DragDrop` | Accept a dropped `.iso` or TU file anywhere on the setup view, auto-detecting which it is (UR) | S |
| — | Only `.iso` and a TU file | Accept the forms players have (UR, WWE) | Decide whether to accept an extracted disc folder and a TU folder (owner, §5) | M |
| — | No `IsDefault`, no live regions | Avalonia `IsDefault`, `LiveSetting` | PLAY and SET UP as default buttons; `LiveSetting` on status and error lines | S |
| — | No About, Open logs, Repair | WWE home actions | About (unofficial, no game data); Open logs; "Check game files" re-verify | M |

## 5. Open questions for the owner

1. **Which launcher exe did you open, and did a "Renderer: …" button appear under PLAY?** If it
   was `out/launcher/publish-app/topspin4-launcher.exe`, §0.2 explains the empty screen.
2. **Which settings did you expect in the launcher?** The spec limits it to before-start
   settings (renderer, GPU) and keeps the rest in game. If you expected resolution, MSAA or
   controls there too, that is a scope change to the spec, not only a UX fix. A middle way is
   to show the in-game settings read-only with "change in game", or to let the launcher write
   them too, since they live in the same file **[inferred]**.
3. **Beyond settings, what felt weird?** The layout, the words, not knowing what TU1 is, the
   pad? A recording of one first run (yours or a friend's) would rank §4 better than
   guidelines.
4. **Should `topspin4.exe` open the launcher when the game isn't set up?** Unleashed runs its
   installer from the exe (§3.1); today the exe's behaviour without data is undesigned.
5. **Which inputs to accept:** `.iso` only, or also an extracted disc folder, a Games on Demand
   package, and a TU1 folder? This depends on what dumps Top Spin 4 owners have.
6. **Where should a "How do I get these files?" link go?** The GitHub README, a docs page, or
   only inline text?
7. **Auto-search:** keep looking in Downloads and Desktop, but only fill files that pass the
   check, or ask before using a found file (SoH)?
8. **Custom location for the 6 GB of game files:** add a "Change…" (WWE), or drop the advice from
   the messages?
9. **One page or two steps?** Keep both inputs on one screen with numbered rows, or a short
   two-step flow?
10. **Safe start placement:** main screen, a "Trouble starting?" area, or only after a failure?
11. **After the game exits normally:** close the launcher (today) or return to it?
12. **How much of WWE's home screen to bring back** (status line, Open logs, crash report,
    Repair, About, Exit), given the spec's "cut down" launcher?

## 6. Redesign as a PC game launcher (2026-10-31)

The owner asked for the look of a store launcher's game page (Battle.net, Rockstar, EA app,
Steam). Art comes only from the player's disc (`userdata\art`, see disc-art.md §6); before
setup the Launcher draws its own gradient and wordmark. What changed:

- **Chrome:** a side rail (the disc's title icon, then Home, Settings, Achievements, Help) and
  a footer (version and commit, the renderer the Settings file chooses, the GPU, the game
  files folder, the credit line). Before setup the rail holds only Help.
- **Home:** the disc's background full-bleed under a left-to-right scrim, the logo top left,
  PLAY with the tools beside it (icon over label), the status line, "Trouble starting?" with
  Safe start, then a card row: Players, Venues (a photo that changes every 6 s),
  Achievements (icons, total, View all) and What's new. A window under 1000 px wide drops
  What's new; one under 620 px tall draws the logo and cards smaller.
- **Setup, Settings, Achievements:** one scrolling column on the scrim over the quietened
  setup picture. The game files location and its Change… moved from Home to Settings.
- **Palette:** a navy-black base with the red of the logo's "4"; the contrast tests hold it.
- **Input:** the rail is reached with Left from PLAY; B and Esc leave Settings and
  Achievements for Home. Down from the rail's last item stays in the rail.

## Sources

- **This repo (`c5b1c00`):**
  - `launcher/src/Launcher.App/MainWindow.axaml`, `MainWindow.axaml.cs`, `Theme.axaml`,
    `Program.cs`, `Platform/XInputPad.cs`, `Launcher.App.csproj`;
  - `launcher/src/Launcher.Core/SetupFlow.cs`, `SetupTypes.cs`, `DiscSetup.cs`,
    `TitleUpdateSetup.cs`, `Preparation.cs`, `PreparedFolder.cs`, `Reporter.cs`,
    `InputFinder.cs`, `LaunchArguments.cs`, `SettingsExport.cs`, `SettingsSchema.cs`,
    `BeforeStartSettings.cs`, `PadNavigator.cs`, `GameExitReport.cs`, `UserDirectories.cs`;
  - `launcher/Directory.Packages.props`;
  - `src/settings/registry.h` (125-129), `src/settings/settings_cvars.cpp` (11, 25-34),
    `src/settings/open_combo.h` (1), `src/topspin4_app.h` (85-94), `src/crash_report.cpp`
    (258-270, 344); `scripts/build.ps1` (48-53);
  - on disk (not in the repo): `%LOCALAPPDATA%\TopSpin4Recomp\gamedata` and `updatedata` with
    their `.topspin4-prepared` markers; `out/launcher/publish-app/topspin4-launcher.exe` and
    `out/build/win-amd64-relwithdebinfo/topspin4.exe`;
  - `.scratch/game-settings/spec.md`, issues 13, 14 and 15; `.scratch/pc-port-1-0/issues/12-disc-preparation.md`,
    `30-release-candidate-deck.md`; `docs/goals.md`; ADR-0004; `GLOSSARY.md`;
    `docs/research/game-settings.md`.
- **WWE All Stars (local, `387e216`):** `launcher/src/Launcher.App/MainWindow.axaml`
  (98-282), `MainWindow.axaml.cs` (206-252), `launcher/src/Launcher.Core/GameExitReport.cs`
  (6-78), `docs/research/community-feedback.md` (58, 62, 66), `docs/research/launcher.md` §3.
- **Upstream repos** (base URL plus `path#Lx-Ly`):
  - `UR:` https://github.com/hedge-dev/UnleashedRecomp/blob/cf829a9eca8fb680fba4b0409ddeb6ca92f22e3c/
  - `MR:` https://github.com/sonicnext-dev/MarathonRecomp/blob/dd91996040e37e1245729055f83d72c69c3b78f0/
  - `Z64:` https://github.com/Zelda64Recomp/Zelda64Recomp/blob/b65c482ed672258eb684ab94a4e8c8aa662615ee/
  - `SOH:` https://github.com/HarbourMasters/Shipwright/blob/2827fc2a0f56a9cdc18a87adf8c1bb7e91315f41/
  - `DOL:` https://github.com/dolphin-emu/dolphin/blob/3eb30c7620b4475b2e0404228ba0d2b238a132f1/
  - `RP:` https://github.com/RPCS3/rpcs3/blob/6c44fff7549e941389a4ce072a95c5ff9db7237c/
  - `AV:` https://github.com/AvaloniaUI/Avalonia/tree/11.3.0/ (tag; `XYFocus` sources under
    `src/Avalonia.Base/Input/Navigation/`)
- **Microsoft:**
  - [Win32 UX guidelines: Wizards](https://learn.microsoft.com/en-us/windows/win32/uxguide/win-wizards)
    (written for Windows 7; Microsoft notes "much of the guidance still applies in principle");
  - [Win32 UX guidelines: Error messages](https://learn.microsoft.com/en-us/windows/win32/uxguide/mess-error);
  - [Win32 UX guidelines: Progress bars](https://learn.microsoft.com/en-us/windows/win32/uxguide/progress-bars);
  - [Windows apps: Writing style](https://learn.microsoft.com/en-us/windows/apps/design/style/writing-style)
    (Fluent voice and tone);
  - [Xbox Accessibility Guideline 112, UI navigation](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/112);
  - [Xbox Accessibility Guideline 101, Text display](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/101).
- **Nielsen Norman Group:** [10 Usability Heuristics](https://www.nngroup.com/articles/ten-usability-heuristics/)
  (Nielsen, reviewed 2024); [Error-Message Guidelines](https://www.nngroup.com/articles/error-message-guidelines/);
  [Wizards: Definition and Design Recommendations](https://www.nngroup.com/articles/wizards/)
  (Budiu, 2017).
- **Apple:** [HIG Onboarding](https://developer.apple.com/design/human-interface-guidelines/onboarding),
  [HIG Launching](https://developer.apple.com/design/human-interface-guidelines/launching)
  (read through Apple's JSON data endpoint for those pages).
- **Game Accessibility Guidelines:** [Same input method for all UI](https://gameaccessibilityguidelines.com/ensure-that-all-areas-of-the-user-interface-can-be-accessed-using-the-same-input-method-as-the-gameplay/),
  [Start without navigating multiple menus](https://gameaccessibilityguidelines.com/allow-the-game-to-be-started-without-the-need-to-navigate-through-multiple-levels-of-menus/).
- **Avalonia docs:** [Focus and XYFocus](https://github.com/avaloniaui/avalonia-docs/blob/main/docs/input-interaction/focus.md),
  [Button IsDefault/IsCancel](https://github.com/avaloniaui/avalonia-docs/blob/main/controls/input/buttons/button.md),
  [Accessibility LiveSetting](https://github.com/avaloniaui/avalonia-docs/blob/main/docs/app-development/accessibility.md),
  [Drag and drop](https://github.com/avaloniaui/avalonia-docs/blob/main/docs/input-interaction/drag-and-drop.md).
- **Not checked:** the launcher's actual rendering and pad behaviour (not run); which launcher
  copy the owner opened; XAG 101 text height on screen; what `topspin4.exe` does before setup;
  Xenia, RPCS3 and Dolphin first-run flows (only their main-window toolbars were read).
