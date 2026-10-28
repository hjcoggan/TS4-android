# Launcher look: Top Spin 4's own art

Status: ready-for-agent

Settled with the owner on 2026-10-28: the art comes only from the files the player provides at setup; none is committed or bundled (ADR-0006 unchanged).

Follows `.scratch/launcher-ux/spec.md` (the Launcher as the home screen). That spec settles what the Launcher does; this one settles how it looks.

## Problem Statement

The Launcher looks generic: a dark navy window, two open-licence fonts, ball yellow and court blue, and no picture of the game (`launcher/src/Launcher.App/Theme.axaml`: "No game art"). Nothing on screen says Top Spin 4 except the title text, and the window, taskbar and Direct shortcut icons are not the game's. Players expect a launcher that looks as if it belongs to the game, the way the Xbox 360 dashboard showed it: its background, box art, logo and icon.

The repository cannot hold that art. The game's logo, box art and pictures are game data, and ADR-0006 keeps every byte of game data out of the repository and out of anything we distribute.

## Solution

The Launcher shows Top Spin 4's own art, **read from the player's own disc** at runtime, the same way the game itself is.

- **Where the art comes from.** The disc carries the art the Xbox dashboard used: the `nxeart` package (dashboard background, box art, banner, title icon), the title image inside `default.xex`, and the menu art in the XUI packages under `Media/XUI` (`Startup.xzp`, `Common.xzp`). A **Disc art** step decodes the chosen images to PNG files in the per-user folder.
- **When.** The art arrives with the files the player provides. As soon as the disc row on the setup page accepts a disc, its art is read from the `.iso`, so the rest of setup already looks like Top Spin 4. Setup reads it again from the prepared disc folder if the art folder is missing or incomplete. After setup, the Launcher reads the art from the per-user folder on every start.
- **What it changes.**
  - Home: the dashboard background behind the page (darkened under text for contrast), the Top Spin 4 logo in place of the title text, and the box art beside PLAY.
  - Setup and Settings: the same background, more subdued, and the logo small in the header.
  - Window and taskbar icon: the game's title icon. The Direct shortcut uses the same icon, written as an `.ico` beside the art.
  - Colours: the accent colours are retuned to match the art; the open-licence fonts stay.
- **Before a disc is chosen** (first run, or art missing): the Launcher keeps its own original look, with no game art and with the same layout. It never shows a broken image or an empty frame.
- **Credit line.** A small line in the footer says this is a fan recompilation, not affiliated with 2K Sports or 2K Czech, and that the art comes from the player's disc. The Launcher never presents itself as an official 2K product.

## User Stories

1. As a player, I want the Launcher to show Top Spin 4's background, logo and box art, so that it looks like part of the game.
2. As a player, I want the window, taskbar and shortcut to use the game's icon, so that I recognise it on my Desktop and taskbar.
3. As a first-time player, I want the art to appear as soon as my disc is accepted, so that setup already feels like Top Spin 4.
4. As a player whose art is missing, I want the Launcher to look clean without it, so that I never see a broken picture.
5. As a player, I want text over the art to stay easy to read, so that the look doesn't cost readability.
6. As the owner, I want no game art in the repository or the published Launcher, so that the project stays within ADR-0006.

## Implementation Decisions

- **Disc art module in Launcher.Core.** Given a disc `.iso` (or the prepared disc folder), it returns the decoded images: background, box art, logo, title icon; each optional. It writes them as PNG, plus a multi-size `.ico` from the title icon, to an `art` folder in the per-user folder, with a small index naming what was found and the disc it came from. Decoding the `nxeart` STFS package, the XEX title-image resource and the XUI packages sits behind this module. Which images are used, and how to decode them, is settled by ticket 01's research note.
- **Theme reads the art, never bundles it.** The Avalonia theme gets image and accent resources that default to the original look and are replaced when the art folder has the images. The published Launcher holds no game art; the exe's own file icon stays an original icon. Only the window icon, taskbar icon and shortcut icon use the game's.
- **Readability.** Text never sits directly on the picture: a dark scrim or panel goes under it, and text keeps a 4.5:1 contrast against it.
- **Setup hook.** When the setup page model accepts a disc, it asks the Disc art module to read the art; a failure there is logged and leaves the original look, never failing the disc row.

## Testing Decisions

- **Launcher.Tests, Disc art module:** given small fixture packages built in the tests (an STFS package and an XEX resource with a generated test image, not game art), it writes the PNGs, the `.ico` and the index; a package with no image gives no file and no error; a damaged package gives a logged reason and no file.
- **Launcher.Tests, theme selection:** with a full art folder the theme resources point at the art; with an empty or partial one each missing image falls back to the original resource.
- **Owner run:** on the owner's disc, the art appears after the disc row accepts, the home screen, setup and Settings look as described, and the shortcut and taskbar show the game icon.

## Out of Scope

- Animated backgrounds, the attract video (`Attract_1280x720.wmv`), music or sound.
- Art from Title Update 1 or downloadable content.
- Player portraits or court photos from the game's `.arc` archives.
- A new layout. This spec changes the look of the pages that `.scratch/launcher-ux/` defines.
- A bundled or downloadable fallback copy of the game's art.

## Further Notes

- Text and graphics within the art are the game's own; the Launcher shows them unchanged and does not add 2K's trademarks anywhere else.
- No new ADR is needed: reading art from the player's disc at runtime follows ADR-0006 in the same way the game's data does.
