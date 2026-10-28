# Portable install

Status: ready-for-agent

Decision: `docs/adr/0009-portable-install.md` (owner, 2026-10-28).

## Problem Statement

The game and the Launcher spread a player's install over three places: the build folder, `%LOCALAPPDATA%\TopSpin4Recomp` (prepared disc and TU1, shader cache) and `%APPDATA%\TopSpin4Recomp` (settings, saves, achievements, disc art, the game files location file). The owner wants a portable install: when the player starts the Launcher, it unpacks the game files into the folder with the build files, and everything else the game writes stays there too.

## Solution

All default folders are resolved from the folder that holds `topspin4.exe` (for the Launcher: the folder it runs from, which is beside the game exe in the dev build and in a release):

| Folder | Holds |
|---|---|
| `gamedata\`, `updatedata\` | the prepared disc and Title Update 1 |
| `cache\` | the shader and pipeline cache |
| `userdata\` | the Settings file, saves, achievements, `art\`, `game-files-location.txt` |
| `logs\` | unchanged |

- No default ever points under `%APPDATA%`, `%LOCALAPPDATA%` or the XDG data folder, on Windows or under Proton.
- The command line still wins (`--user_data_root`, `--cache_root`, `--game_data_root`, `--update_data_root` for the game; `--user-data`, `--cache` for the Launcher). The developer scripts are unchanged.
- The game files location (moving the prepared files elsewhere) still works; its file is in `userdata\`.
- **Not writable.** When the build folder can't be written to, the Launcher says so on the setup page ("This folder can't be written to: <folder>. Move the game folder somewhere you can write to, such as your Documents or a games drive, and start the Launcher again.") and "Set up the game" stays disabled. It never falls back to the user's folders. The game started directly from such a folder opens the Launcher as it does when not set up, which then shows that message.
- **No migration.** An existing `%APPDATA%` / `%LOCALAPPDATA%` install is ignored.
- `topspin4-setup` (Launcher.Cli) resolves the same way from its own folder, `--cache` still wins.

## Testing Decisions

- Host tests (`tests/host/test_user_paths.cpp`): the default user, prepared-data and cache roots are `<exe folder>\userdata`, `<exe folder>`, `<exe folder>\cache`, including under Wine; a location file in `userdata` still moves the prepared and cache roots; the command line still wins.
- Launcher.Tests: the same roots from the Launcher's folder; a folder that can't be written to gives the refusal on the setup page with setup disabled; the art folder is `userdata\art`.
- Owner run: start the published Launcher beside `topspin4.exe` with no `gamedata` etc., set up, play; every new file is inside the build folder and nothing new appears under `%APPDATA%\TopSpin4Recomp` or `%LOCALAPPDATA%\TopSpin4Recomp`.

## Out of Scope

- Migrating an earlier per-user install.
- An installer, or a "per-user" option.
