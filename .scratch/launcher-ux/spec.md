# Launcher: home screen, clear setup, all settings

Status: ready-for-agent

Research: `docs/research/launcher-ux.md`. Settled in a grilling session on 2026-10-28. Supersedes the Launcher parts of `.scratch/game-settings/spec.md` (the "cut down" Launcher, before-start settings only, remapping out of scope) and the conflicting parts of its tickets 13–15.

## Problem Statement

The owner opened the Launcher and found it unintuitive: there were no settings to change and no sign of what else it could do. The research note found why:

- The Launcher shows settings only when `topspin4.exe` sits beside it. When it doesn't, the settings rows are skipped without any message, and the screen is left with a title, "Everything is ready.", PLAY and Safe start. The dev build puts the Launcher and the game exe in different folders, so this is what the owner saw.
- Even when it works, the Launcher shows one setting, the renderer, as an unlabelled "Renderer: classic" button under PLAY. Every other setting is in game only, and the Launcher never says so or how to get there.
- Setup checks nothing until SET UP is pressed, explains Title Update 1 only in error messages, silently fills in the first `.iso` in Downloads, shows every error in one shared red line with hex IDs, and tells players to "choose a cache folder" when the window has no way to do it.
- After a normal game exit the Launcher closes, so there is nowhere to come back to.

The glossary defined the Launcher as a setup tool the game starts without. Players expect what Zelda64Recomp, WWE All Stars, Dolphin and RPCS3 give them: a home screen with Play and Settings next to each other.

## Solution

The **Launcher** becomes the program players open every time to start the game.

- **First run:** one setup page with two numbered rows, "1 Disc" and "2 Title Update 1". Each row takes one file (a disc `.iso`, the TU1 file), is checked the moment the file is picked, and shows ✓ with what was found or the reason it can't be used, right on that row. Each row has one sentence of help, and a "How do I get these files?" link leads to a README section on dumping your own disc and TU1. A "Game files go to … [Change…]" line shows where the ~6 GB will go and the free space there. Nothing is searched for or filled in automatically.
- **Home screen:** PLAY with a status line, then labelled actions: Settings, Create shortcut, Help, Open logs (and Open crash report after a crash). Safe start lives in a small "Trouble starting?" area and is offered prominently after a failed start or a crash.
- **Settings:** its own page with Back and one tab per section (Graphics, Controls, …), the same sections as the in-game Settings page. Every setting is there and editable, button and key remapping included. The Graphics Preset row is at the top of its section and shows Custom after a tweak. Each change is saved to the Settings file at once. "Reset to defaults" restores every setting.
- **Playing:** the Launcher hides while the game runs and comes back when it exits, with a crash report and Safe start on offer if it failed.
- **Direct shortcut:** "Create shortcut" puts a shortcut on the Desktop and in the Start menu that starts the game without showing the Launcher, using the saved Settings file.
- **Not set up:** when the game is started directly but setup isn't done or the prepared files are missing, the game opens the Launcher on the setup page with a line saying why, and exits.
- **Moving game files:** changing the location after setup moves the prepared files there with a progress bar.

The window is designed for mouse and keyboard first. A controller still works throughout, with focus moving to what is visually next to it. The in-game Settings page is unchanged; both read and write the same Settings file.

## User Stories

1. As a first-time player, I want the Launcher to open on a setup page that says exactly what it needs, so that I know what to do first.
2. As a first-time player, I want the two things I need numbered "1 Disc" and "2 Title Update 1", so that I see how far along I am.
3. As a first-time player, I want one sentence under each row saying what the file is and what it's usually called, so that I can find it on my drive.
4. As a first-time player, I want a "How do I get these files?" link, so that I can learn how to dump my own disc and Title Update 1.
5. As a player, I want the disc I pick checked as soon as I pick it, so that I don't find out it's wrong only after pressing Set up.
6. As a player, I want a picked disc that is Top Spin 4's supported edition shown with a ✓ and its name, so that I know it was accepted.
7. As a player, I want a disc of a different edition or game rejected with a plain reason on its own row, so that I know which file is wrong and why.
8. As a player, I want the Title Update file checked as soon as I pick it, with ✓ or a plain reason on its row, so that I can fix it before setting up.
9. As a player, I want error text in plain words, with media IDs, versions and exception text kept behind "Details" or in the log, so that I'm not faced with hex codes.
10. As a player, I want the Launcher never to fill in a file I didn't choose, so that it can't set up from a file I didn't mean.
11. As a player, I want to see where the game files will go and how much space is free there, so that I can avoid filling a small drive.
12. As a player, I want to change where the game files go before setup, so that I can put the 6 GB on another drive.
13. As a player, I want to be told before setup starts when there isn't enough space, so that setup doesn't fail halfway.
14. As a player, I want one "Set up the game" button that is only enabled when both rows have a ✓, so that I know when I can go on.
15. As a player, I want one progress bar for the whole setup with the time left, so that I know how long to wait.
16. As a player, I want to be able to stop setup and resume it later, so that I don't have to start over.
17. As a player, I want setup to finish on the home screen, so that I can play straight away.
18. As a player, I want the home screen to show PLAY first, with a status line saying the game is ready, so that the main action is obvious.
19. As a player, I want labelled Settings, Create shortcut, Help and Open logs actions on the home screen, so that I can see everything the Launcher can do at a glance.
20. As a player, I want the home screen to tell me when the game exe is missing as soon as the window opens, so that I don't find out only when I press PLAY.
21. As a player, I want a Settings page that lists every setting the game has, so that I can set the game up before I play.
22. As a player, I want the Settings page split into tabs that match the in-game page's sections, so that I find a setting in the same place in both.
23. As a player, I want a Graphics Preset at the top with Original and Enhanced, so that I can pick a look in one step.
24. As a player, I want the Preset to show Custom when I change one of its settings, so that I know I've moved off it.
25. As a player, I want each setting to show its label, current value and a short description, so that I know what it does.
26. As a player, I want each change saved at once, so that I don't need a Save button and can't lose a change.
27. As a player, I want a "Reset to defaults" button, so that I can undo a bad set of changes.
28. As a player, I want to remap pad buttons and keys in the Launcher, so that my controls are right before my first match.
29. As a player, I want a binding that clashes with another to be pointed out, so that I don't end up with two actions on one button.
30. As a player, I want settings that only work on the Native renderer shown as unavailable on Classic with the reason, so that I know why they do nothing.
31. As a player, I want the Launcher's Settings page and the in-game page always to show the same values, so that I never see two answers.
32. As a player, I want the renderer choice shown with a label and plain options ("Native (recommended)", "Classic (compatibility)"), so that I understand what I'm choosing.
33. As a player, I want the Settings page to say I can also change settings in game and how (PC SETTINGS in OPTIONS, F4, the pad combo), so that I know I don't have to quit to change them.
34. As a player, I want the Settings page to say why when it can't read the game's settings, so that I'm never shown an empty page.
35. As a player, I want the Launcher to hide while I play and come back when the game closes, so that I can change a setting and play again.
36. As a player whose game crashed, I want the Launcher to say so and offer the crash report, so that I can report it.
37. As a player whose game failed to start on the GPU, I want the Launcher to tell me to switch renderer, so that I have a way forward.
38. As a player whose game failed or crashed, I want Safe start offered prominently, so that I can get back into the game.
39. As a player, I want Safe start available under "Trouble starting?" at any time, so that I can use it without a failure first.
40. As a player, I want a "Create shortcut" action that puts Top Spin 4 on my Desktop and in the Start menu, so that I can start the game in one click.
41. As a player, I want that shortcut to start the game without showing the Launcher, so that nothing stands between me and the game.
42. As a player, I want the shortcut to use my saved settings, so that it plays the same as PLAY in the Launcher.
43. As a player, I want creating the shortcut again to replace the old one, so that I don't get duplicates.
44. As a player, I want the game, when started from the shortcut without setup done, to open the Launcher on the setup page with the reason, so that I'm never left with a silent failure.
45. As a player who moved or deleted the game files, I want starting the game to bring me to the Launcher saying the files are missing, so that I can set up again.
46. As a player, I want to change the game files location after setup and have the files moved there with a progress bar, so that I can free space on a drive.
47. As a player, I want the game to find the files in their new place afterwards, so that moving them doesn't break the shortcut or PLAY.
48. As a player, I want Help to open the README's help section, so that I can read about files, controls and the in-game page.
49. As a player, I want Open logs to open the logs folder, so that I can attach logs to a bug report.
50. As a mouse-and-keyboard player, I want a normal desktop window with PLAY as the default button and Esc as Back, so that it behaves like other Windows apps.
51. As a controller player, I want to move focus with the D-pad to what is visually next to it, A to select and B to go back, so that the Launcher works from the couch.
52. As a player, I want controller button hints shown only when I'm using a controller, so that they don't clutter the window otherwise.
53. As a player, I want body text left-aligned and large enough to read, so that the window is easy to read.
54. As a screen-reader user, I want status and error lines announced when they change, so that I know what happened.
55. As a developer, I want the dev build to put the Launcher beside the game exe, so that I see what players see.

## Implementation Decisions

- **Glossary.** `GLOSSARY.md` now defines the Launcher as the program players open to start the game, with setup as its first-run part, and adds **Direct shortcut**. The Settings page is no longer "the main way players change settings"; the Launcher shows the same settings before the game starts.
- **View models in Launcher.Core, drawn by the App.** Each screen's behaviour lives in a pure model in Launcher.Core; the Avalonia windows only draw it and turn clicks, keys and pad input into model actions (the pattern SetupFlow already follows). Models:
  - **Setup page model** (extends SetupFlow): two input rows, each with its chosen path and a check result (accepted, with the edition or update it found, or rejected, with a plain reason and the technical details apart); the game files location with free and needed space; whether "Set up the game" is enabled; one overall progress value weighted across check, extract and unpack, indeterminate while unknown; stop and resume. The disc row accepts only a disc `.iso`; the TU1 row only the TU1 file. Picking a file runs the existing header and identity checks at once. The automatic search for files is removed.
  - **Home model:** status (ready, game exe missing, game files missing, game running, last session failed on the GPU, last session crashed with its report), and which actions are offered: Safe start is prominent only after a failure or crash.
  - **Settings screen model:** built from the game's settings export, never from a list of its own. It gives the tabs (the export's sections, in order), each tab's rows with label, value, choices, help, and availability on the chosen renderer, and the Preset row. Changing a row writes that one setting to the Settings file at once with the existing line-preserving rules and file version handling; choosing a Preset writes all its settings; a change to a covered setting turns the Preset row to Custom, with Native-only settings left out of the match on Classic, as in game. "Reset to defaults" writes every setting's default. When the export can't be read, the model says why instead of giving no rows.
  - **Bindings:** remapping rows take the next key or pad button pressed, write it in the format the game's Controls section uses, and report a clash with another binding in the same device's set.
  - **Game files location:** a move operation copies the prepared folders to the new place with progress, verifies them against the manifest, records the new location, then removes the old copy. Interrupted moves leave the old copy as the one in use.
  - **Direct shortcut:** creates or replaces "Top Spin 4" shortcuts on the Desktop and in the Start menu that run the game exe with no arguments, the working folder being the exe's folder. Shortcut writing sits behind a small interface so tests check target and arguments without touching the real Desktop.
- **Settings export format 2.** The game's export gains the Graphics Presets (each Preset's value, label and the value of each covered setting) so the Launcher shows Original, Enhanced and Custom exactly as the game does. Bindings are exported with whatever the Launcher needs to show and write them (device, action label). The Launcher reads format 2 and refuses other formats with the existing message.
- **Game files location shared with the game.** The Launcher records a changed location in a small location file in the per-user folder; the game reads it when looking for prepared data, before falling back to the default prepared-data folder. The command line still wins. The shader cache stays beside the prepared data.
- **Not set up.** When the game is started without a command-line game folder and the prepared disc or TU1 isn't found, it starts the Launcher (found beside the exe) with an argument naming the reason, and exits. The Launcher opens on the setup page and shows that reason. When the Launcher isn't beside the exe, the game shows a message box saying setup is needed, as a fallback. Developer scripts pass the folders on the command line, so headless runs are unaffected.
- **Game session from the Launcher.** PLAY and Safe start (with `--safe`) start the game, hide the Launcher, wait for the exit, then show the home screen again with the exit classified as normal, GPU failure or crash, copying WWE All Stars' exit classification and its crash report link (ADR-0004).
- **Window behaviour.** Mouse and keyboard first: PLAY and "Set up the game" are default buttons, Esc is Back. Pad input drives Avalonia's directional focus (XYFocus) so focus follows the layout; A selects, B goes back. Pad hints show only after pad input. Paragraphs left-aligned; small text raised. Status and error lines are live regions.
- **Help.** A README section on dumping your own disc and TU1 and on the in-game Settings page keys. Help and "How do I get these files?" open it. No link or text may point to sources of game data (ADR-0006).
- **Dev build.** The build script places the published Launcher beside `topspin4.exe` in the build output.

## Testing Decisions

A good test checks what the player sees and what lands on disk (rows and their check results, enabled actions, status, Settings file contents, shortcut target and arguments, the location the game uses), given inputs, files and the export. It does not check how the models compute it.

- **Seam 1: Launcher.Core models (existing Launcher.Tests).** Cases:
  - picking a supported disc gives an accepted row naming it; a wrong edition, a non-disc file and an unreadable file each give a rejected row with a plain reason and the details apart; the same for the TU1 row;
  - "Set up the game" is enabled only when both rows are accepted and there is enough free space; not enough space is reported before setup starts;
  - no file is filled in without a choice;
  - progress is one value across all steps and never goes back;
  - home status for ready, game exe missing, game files missing, GPU failure exit and crash exit; Safe start is prominent only after a failure or crash;
  - the settings screen built from a format 2 export: tabs in the export's order, every setting present, renderer availability, Preset row Original on defaults, choosing Enhanced writes its settings, a tweak gives Custom, Native-only settings ignored for Custom on Classic;
  - a change writes only its line in an existing Settings file; Reset to defaults writes all defaults; an unreadable export gives a reason, not an empty list;
  - a binding change writes the game's format; a clash is reported;
  - moving the game files: files arrive verified, the location file names the new place, the old copy is removed; an interrupted move keeps the old place in use;
  - the shortcut writer gets the game exe as target, no arguments and its folder as working folder, for Desktop and Start menu; a second call replaces rather than adds.
  Prior art: SetupFlowTests with SetupFakes, BeforeStartSettingsTests, PreparationTests, UserDirectoriesTests.
- **Seam 2: game host tests (existing settings tests).** Export format 2 contains every setting, the Presets with their covered values and the binding fields, and reading it back gives the same registry and Presets; the prepared-data lookup prefers the command line, then the location file, then the default folder; the not-set-up decision returns "open the Launcher with reason X" when the disc or TU1 is missing and nothing when both are found or folders were given on the command line. Prior art: the settings, presets and schema export host tests.
- **Seam 3: one owner run of the real window** at the end, on a fresh per-user folder: setup from an `.iso` and TU1, the home screen, Settings with a change that the in-game page then shows, Create shortcut and start from it, a moved game files folder, and starting from the shortcut with the files moved away. No automated UI tests.

## Out of Scope

- Extracted disc folders, Games on Demand packages and TU1 inside a folder as setup inputs.
- Automatic search for the disc or TU1.
- A "Check game files" / repair action on the home screen.
- An About screen and an Exit button beyond the window's own close button.
- Adding the game to Steam as a non-Steam game.
- Changes to the in-game Settings page, other than what export format 2 needs.
- Controller-first (big picture) layout; the window is mouse and keyboard first.
- New settings or Presets.
- Native RGui settings pages (ADR-0007).

## Further Notes

- The owner was offered an ADR recording that the Launcher is the home screen with every setting, reversing the game-settings spec's setup-only Launcher; not yet written.
- `docs/research/launcher-ux.md` holds the full gap table (§4) with sources; use its wording advice for errors and labels.
- Under Proton an Avalonia launcher, Start menu shortcuts and the not-set-up hand-off are unverified.
- Tickets 13–15 in `.scratch/game-settings/issues/` stay as history; mark them superseded where this spec changes them when the tickets for this spec are written.
