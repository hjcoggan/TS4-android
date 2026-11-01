# Game settings: launcher, in-game page, config file, or a mix?

Research date: 2026-10-28. The owner asked what the best practice is for showing the port's
growing list of settings to players. The image Enhancements (resolution scale, MSAA,
anisotropic filtering, CAS sharpening, `.scratch/image-quality/`) have just landed, and more
settings are coming. This note lays out the options with their evidence and makes a
recommendation. It decides nothing and designs no tickets. No code was changed and the game
was not run.

Heads read:

- this repo at `1fafff6`;
- the vendored ReXGlue `.tools/rexglue-source` (`f5337cd` plus `patches/rexglue/`), written
  `SDK:` below;
- WWE All Stars at `D:\Projects\WWE_ALLSTARS-RECOMP` (`387e216`), written `WWE:`, whose
  settings, launcher and in-game page ADR-0004 says we copy;
- upstream repos at the commits listed under Sources. Each is cited with a short prefix:
  `UR:` Unleashed Recompiled, `MR:` MarathonRecomp, `Z64:` Zelda64Recomp, `N64MR:`
  N64ModernRuntime (the commit Z64 pins; `N64MR-HEAD:` its newer head), `RT64:`, `SOH:` Ship of Harkinian, `LUS:`
  libultraship (the commit SOH pins), `XC:` Xenia Canary, `XE:` Xenia Edge, `REX:` upstream
  rexglue-sdk, `IMG:` Dear ImGui, `RML:` RmlUi. A citation `UR:user/config.cpp:747-750` is the
  base URL under Sources plus `path#L747-L750`.

**[inferred]** marks my own reasoning. **[unverified]** marks a claim I could not check against
a primary source. Every other claim has a URL or a `file:line` beside it.

## Summary and recommendation

- **Recommendation: a mix, built around one registry, with the in-game page as the player's
  main surface.** In priority order:
  1. **The settings file stays the source of truth, and the command line and environment stay
     on top of it.** That is how it works today (§1.2) and how every port surveyed works
     (§2). It keeps `--headless --window_hidden=true`, per-run `--user_data_root` and scripted
     `set` working unchanged.
  2. **An in-game PC settings page, driven by the pad, is the main way players change
     settings.** It is drawn with the SDK's Dear ImGui over the presenter, so it works the same
     on Classic and Native. Copy WWE All Stars' page, model, presets and schema export
     (ADR-0004; §1.5). Unleashed, Marathon, Zelda64 and Ship of Harkinian all ship an
     in-game menu (§2). Here it also fits the settings themselves: all four image
     Enhancements already apply live (§1.3), so the player sees the result behind the page,
     which beats any static preview.
  3. **The launcher stays small.** It does disc and TU1 setup, Play, and the few settings that
     only make sense before start (renderer, GPU, reset or safe start), all read from the
     game's own schema export so that nothing is kept twice. Valve recommends "strongly
     against requiring the user to navigate a launcher", and requires any launcher to be fully
     navigable with a controller (§2.8). So after setup the game exe must start straight into
     the game.
  4. **The SDK's F6 cvar overlay stays a developer tool.** It has no gamepad navigation
     (§2.5), and it lists every debug cvar.
- **Add three things none of the current code has:**
  - a schema version in the settings file, with migrations. Ship of Harkinian and Xenia both
    version their files; WWE needed a migration within weeks;
  - a safe start that runs on the defaults without deleting the player's file;
  - per-option "applies now / at next start / set by launch options" marks. Copy WWE's model.
- **Why not one surface alone:**
  - A file alone is unusable on a couch or on a Deck in Game Mode.
  - A launcher alone means a restart for every tweak, can't show the change in place, and
    goes against Valve's guidance. WWE's first bug report was its launcher hanging before
    the game started (§1.5).
  - An in-game menu alone can't do setup: the exe can't start without game data (WWE
    ADR-0018).
- **What applies when is already mostly settled.** Resolution scale and MSAA, which most
  ports make restart-only, rebuild live here (tickets 04, 07). Only the renderer, pad
  bindings and device choice need a restart. Mark those as next-start, as WWE does, and
  don't force a restart the way Unleashed does.
- **Main open questions (§4):**
  - Should the page be reachable from Top Spin 4's own OPTIONS menu? That needs reverse
    engineering; whether the menus are Scaleform is unverified.
  - Which pad combo opens the page?
  - How much should the 1.0 launcher do?
  - Portable mode?
  - Graphics presets, and a Deck default, under ADR-0001's "defaults are original"?

## 1. How settings work in the project today

### 1.1 One registry, one file, cvars underneath

- **One list.** `PlayerSettings()` in `src/settings/registry.h:69-119` holds every player
  setting: key, kind (choice, bool, text), default, choices, help text, and whether it works
  on both renderers or Native only (`registry.h:21-44`). Today that is the renderer, the four
  image Enhancements, keyboard-and-mouse mode, the 16 pad bindings and 24 key bindings.
- **Cvars carry the values.** Each setting is a ReXGlue cvar of the same name
  (`src/settings/settings_cvars.cpp`). The cvar's lifecycle says when it applies:
  `ts4_renderer` and the pad bindings are `kRequiresRestart` (`settings_cvars.cpp:11, 35`),
  and all four image settings are `kHotReload` (`:19-28`). The SDK defines three lifecycles:
  `kInitOnly`, `kHotReload` (the default) and `kRequiresRestart`
  (`SDK:include/rex/cvar.h:136-140`).
- **One file.** `topspin4.settings.toml` in the per-user folder (`src/settings/config_file.h:18-21`):
  `%APPDATA%\TopSpin4Recomp` on Windows, the XDG data folder through Wine's `Z:` drive under
  Proton (`src/user_paths.h:1-5, 23-45`). It is flat TOML, one `key = value` per line.
- **Saving one key** rewrites only that line and goes through a temporary file and a rename,
  so a crash never leaves a half-written file (`config_file.h:32-110`). This exists because
  the SDK's `SaveConfig` writes every non-default cvar, debug and command-line values
  included (`config_file.h:1-4`).
- **Loading** happens once, in `OnPostInitLogging` (`src/topspin4_app.h:80-125`). A value the
  registry does not allow is skipped with a warning, an unknown key is ignored, and a file that
  is not TOML at all is renamed to `.corrupt` and the defaults are used
  (`src/settings/settings_file.cpp:20-60`, `settings_file.h:1-5`). At start the game logs an
  error if a cvar's default and the registry's default differ (`topspin4_app.h:107-117`).
- **There is no schema version and no migration.** WWE's `config_file.h` was copied "without
  its migrations" (`config_file.h:3-4`).

### 1.2 Precedence, and why scripts and agents keep working

From lowest to highest:

1. the cvar's compiled-in default (the original behaviour, ADR-0001);
2. the SDK's developer config `topspin4.toml` beside the exe, loaded first
   (`SDK:src/ui/rex_app.cpp:146-159`);
3. the player's settings file, applied over it (`topspin4_app.h:92-101`);
4. a `REX_<NAME>` environment variable or a `--flag` on the command line. The settings file
   yields to both (`topspin4_app.h:95-100`); the SDK records each cvar's source
   (`SDK:include/rex/cvar.h:144-149`).

The scripts rely on rule 4 and on a private user folder per run. `scripts/smoke.ps1:48-53` and
`scripts/scene_runner.py:443-449` pass `--user_data_root`, `--cache_root`, `--headless` and
`--window_hidden=true`, and the scene runner passes `--ts4_renderer=...` and `--enhance`
values the same way (`scene_runner.py:490-509`). A run therefore never reads or writes the
player's file. A scripted `set <cvar> <value>` changes a setting mid-run through
`rex::cvar::SetFlagByName` on the UI thread, the same path a settings page uses
(`src/script_input.h:19`, `topspin4_app.h:141-155`). Tickets 02, 04 and 07 used it to prove
the live changes (`.scratch/image-quality/issues/02-anisotropic-filtering.md:40-45`).

### 1.3 What applies live

The image work answered most of the "live or restart" question for this port already:

| Setting | Applies | Evidence |
|---|---|---|
| Anisotropic filtering | live, both renderers | `practice-ground-live` run, ticket 02 comment (`02-anisotropic-filtering.md:41-45`) |
| Sharpening (CAS) | live, through the presenter on the UI thread | `ApplyLiveSharpening`, `src/native/native_renderer.cpp:1832-1858`; `patches/rexglue/0017-live-sharpening.patch` |
| Resolution scale | live: render targets are made again between frames, Auto follows a window resize | ticket 04 box 2 (`04-resolution-scale.md:24`), ticket 06 (`06-auto-resolution-scale.md:19-23`) |
| MSAA | live, rebuilt mid-match; "the mid-match rebuild is stable, so MSAA stays a live setting" | `07-msaa-4x.md:26-30` |
| VSync (`present_vsync`) | live: patched to rebuild the swapchain | `patches/rexglue/0018-live-vsync.patch` |
| Renderer | next start | `settings_cvars.cpp:8-11` |
| Pad bindings | next start (read when the input system is built) | `settings_cvars.cpp:30-35` |

A live resolution or MSAA change costs a hitch while targets and pipelines are rebuilt
(ticket 07 counts hitch lines, `07-msaa-4x.md:29`), but it works. So the hard case most ports
answer with "restart required" is only the renderer, the bindings and device choice here.

### 1.4 What UI exists

- **No player-facing settings UI exists yet.** Ticket 10 is still open on exactly this: "Only
  the copied WWE settings page and launcher screen remain"
  (`.scratch/pc-port-1-0/issues/10-settings-system.md`, last comment). The image spec says the
  same and asks that the settings "appear in the settings page and launcher automatically
  once those exist" (`.scratch/image-quality/spec.md:24, 55`).
- **The SDK's F6 cvar overlay works.** ReXGlue draws Dear ImGui 1.92.5
  (`SDK:thirdparty/imgui/imgui.h:32`) over the presenter: F3 debug, backtick console, F6
  settings, F7 achievements (`SDK:src/ui/rex_app.cpp:386-428`). The F6 overlay lists every cvar
  with `[live]`/`[restart]` badges (`SDK:src/ui/overlay/settings_overlay.cpp:32-49`). Our patch
  moved it from F4 to F6 so F4 is free for a game settings page
  (`patches/rexglue/0014-in-game-settings.patch`, the `rex_app.cpp` hunk).
- **The input hold is already patched in.** `InputSystem::SetGuestInputHold` makes the game
  read an idle pad while a host menu is open, and `HeldState` gives the menu the real pad
  (`SDK:include/rex/input/input_system.h:44-52`, from patch 0014, "adapted from
  wwe-svr2011-pc (KaikoClanworth1), used with the author's permission"). Nothing in `src/`
  calls it yet.
- **Both renderers draw through the SDK presenter.** Native publishes its finished frame to the
  presenter, which applies "the usual gamma and post-effect path"
  (`patches/rexglue/0007-native-frame.patch:33-34`), and the ImGui overlays are UI drawers on
  that same presenter (`SDK:src/ui/rex_app.cpp:360-371`). One overlay therefore covers Classic
  and Native without per-renderer work [inferred from the two citations; the F6 overlay has
  been used under both, `native_renderer.cpp:1832-1835`].
- **The launcher is a console tool.** `launcher/src/Launcher.Cli/Program.cs:3-8`
  (`topspin4-setup --disc --tu --cache`) prepares the disc and TU1; "the launcher UI
  (ticket 30) replaces it for players". `Launcher.Core` has no settings code. Ticket 30 asks
  that "first run takes the player through disc and TU1 preparation"
  (`.scratch/pc-port-1-0/issues/30-release-candidate-deck.md`).
- **The game already talks to a future launcher** through exit codes and log lines: a GPU
  failure exits with `0x47505546` ("GPUF") and a line that tells the player to switch the
  renderer in the launcher (`src/native/gpu_failure.h:1-14, 42`). A Native start that fails
  falls back to Classic for the session (`src/native/renderer_fallback.h:1-15`).

### 1.5 What WWE All Stars already built (ADR-0004)

WWE went through this exact decision two months earlier, and its code is what ADR-0004 tells
us to copy.

- **Two surfaces over one registry.** ADR-0018: the launcher is a separate Avalonia 11 app
  built with Native AOT, and "the in-game PC SETTINGS page (F4) stays the in-game settings
  surface" (`WWE:docs/adr/0018-launcher-stack.md`). It rejected an in-process ImGui launcher
  because "setup would run inside the game exe, which cannot start without game data"
  (same ADR, Considered options).
- **The in-game page** (`WWE:src/settings/settings_page.{h,cpp}`, about 2,600 lines in
  `src/settings/` with the model, presets and schema export) is hand-styled ImGui in the
  game's fonts. It opens on F4, on both sticks clicked, and from a PC SETTINGS row hooked into
  the game's own Scaleform OPTIONS menu (`WWE:src/settings/options_hooks.cpp:1-11`). It holds
  the game's input while open (`settings_page.cpp:304-319`) and reads the pad itself, because
  the SDK's ImGui drawer is fed only keyboard and mouse events
  (`SDK:src/ui/imgui_drawer.cpp:625`; `WWE:settings_page.cpp:110-111`).
- **Live and next-start rows.** A live change sets the cvar; a restart row is saved, kept as
  "pending" and marked with `*` and a gold note, "Applies the next time the game starts"
  (`WWE:settings_page.cpp:122-137, 242, 277`; `page_model.h:297-306`).
- **Pinned rows.** A setting given on the command line is shown as "Set by launch options: a
  change here lasts until the game closes" (`WWE:page_model.h:82, 297-300`). This is the rule
  that keeps scripted and agent runs honest.
- **Presets.** A Graphics preset row sets several settings at once and shows Custom when they
  no longer match (`WWE:registry.h:17-26`, `page_model.h:49-66, 356-395`). On a first start
  without the launcher, the game applies the preset suggested for the GPU it opened
  (`WWE:settings_page.h:48-53`).
- **The launcher reads the game's schema.** `wweallstars.exe --print-settings-schema` prints
  the registry as JSON (`WWE:src/settings/schema.h:120-177`, `schema_export.cpp:1`), so the C#
  launcher never keeps its own copy. The launcher shows only rows that apply at startup
  (`WWE:launcher/src/Launcher.Core/SettingsSchema.cs:77`) and passes only paths on the command
  line, because "a command-line setting is pinned in game" (ADR-0018, Hand-off).
- **One migration.** When Supersampling became a render resolution, `MigrateSettingsText`
  rewrote the old key once (`WWE:src/settings/config_file.h:150-166`).
- **What players said.** One detailed request asked for a "Game exe without having to use the
  launcher each time" (`WWE:docs/research/community-feedback.md:58`). The first bug report was
  the launcher freezing on Intel UHD 620 when a file was picked, before the game ever started
  (`community-feedback.md:73-80`). WWE's own research also found docs and code disagreeing on
  which settings were live (`WWE:docs/research/live-settings.md`, "Docs that disagree with the
  code").

## 2. Prior art

### 2.1 Unleashed Recompiled (XenonRecomp)

- **Where:** `config.toml` in `%APPDATA%\UnleashedRecomp`, `~/.config/UnleashedRecomp` on
  Linux (`UR:UnleashedRecomp/user/paths.cpp:12-53`, `UR:UnleashedRecomp/user/config.cpp:747-750`).
  An empty `portable.txt` beside the exe puts everything in the exe folder
  (`UR:UnleashedRecomp/user/paths.cpp:7-15`, `UR:README.md:357-359`).
- **Schema:** one X-macro list, `config_def.h`, expanded twice into declarations and
  self-registering definitions (`UR:UnleashedRecomp/user/config_def.h:1-99`,
  `UR:UnleashedRecomp/user/config.h:214-235`). Each option has a section, name, default,
  `InaccessibleValues`, and value/lock/apply callbacks (`config.h:153-212`). Enums are stored
  as strings (`config.cpp:8-19`). Names and descriptions are localised per language
  (`UR:UnleashedRecomp/locale/config_locale.cpp:655-663`).
- **Versioning:** none. A missing key or unknown enum string falls back to the default
  (`config.cpp:456-491`). A parse error is only logged (`config.cpp:792-829`). `Save()`
  rewrites the whole file from the registry (`config.cpp:831-872`).
- **Live or restart:** almost everything is live. Aspect ratio, resolution scale, AA and
  shadow resolution trigger an internal resize; AA and filtering trigger a pipeline recompile
  (`UR:UnleashedRecomp/gpu/video.cpp:7532-7552`). Only language and audio channel layout
  need a restart, and that check is hard-coded (`UR:UnleashedRecomp/ui/options_menu.cpp:1792`).
  On leaving the menu the game says "The game will now restart." and restarts itself
  (`UR:UnleashedRecomp/patches/CTitleStateMenu_patches.cpp:81-89`). Saved when the menu
  closes (`options_menu.cpp:1832-1844`).
- **UI:** Dear ImGui used only as a draw list, with custom fonts, gradients and no ImGui
  widgets (`options_menu.cpp:754-904`). It reads the game's own pad state, not ImGui
  navigation: LB/RB switch category, the D-pad or stick moves, A edits, B cancels and restores
  the old value, and X resets to the default (`options_menu.cpp:454-455, 797-876, 1292-1296`).
  A button guide is drawn (`:1818-1827`).
- **Descriptions and previews:** an info panel shows the description and a thumbnail per
  option or per value. Resolution scale prints the resolution that results
  (`options_menu.cpp:1441-1500`; thumbnails embedded as DDS,
  `UR:UnleashedRecomp/ui/options_menu_thumbnails.cpp:1-40`). Options the hardware can't do
  are greyed out with a reason (`options_menu.cpp:1277-1278`, `video.cpp:1827-1836`).
- **Hardware defaults and safe start:**
  - On an iGPU, under 2 GB of VRAM or on UMA, lower defaults apply, but only to options still
    at their default and not loaded from the file (`video.cpp:1636-1661, 1811-1821`).
  - Device creation is guarded. On a crash the game relaunches itself with
    `--graphics-api-retry`, tries the other API, and saves the one that worked
    (`video.cpp:1675-1805`).
- **File-only settings:** graphics API, GPU, keyboard bindings and window position aren't in
  the menu (`options_menu.cpp:1230-1284`). The README sends players to `config.toml` for them
  (`UR:README.md:338-340, 397`).
- **Installer:** an in-process ImGui wizard that runs when the game isn't installed, or with
  `--install`. It takes keyboard and gamepad input (`UR:UnleashedRecomp/main.cpp:327-342`,
  `UR:UnleashedRecomp/ui/installer_wizard.cpp:200-251`). Its file picker "may not work" in
  Deck Game Mode (`UR:README.md:276-278`).
- **Command line:** only install and graphics-API flags. There are no per-setting overrides
  (`main.cpp:211-233`).

### 2.2 MarathonRecomp and other XenonRecomp or ReXGlue ports

- **MarathonRecomp** copies Unleashed (the same paths, `config.toml` and flags:
  `MR:MarathonRecomp/user/paths.cpp:9-55`, `MR:MarathonRecomp/main.cpp:201-208`). It improves
  one thing: every option declares `requiresRestart`
  (`MR:MarathonRecomp/user/config.h:251-255`, `config_def.h:4, 76-81`), and the restart prompt
  names the changed options (`MR:MarathonRecomp/ui/options_menu.cpp:1100-1113, 1280-1292`).
- **LostOdysseyRecomp** has its own `settings.ini`, a first-run flow and a restart prompt
  ([`settings/config.cpp:18`](https://github.com/freefrank/LostOdysseyRecomp/blob/f9da0ae9bdd481a5ea4fe6431f3a4724d8261147/LostOdysseyRecomp/settings/config.cpp#L18),
  [`settings/menu.cpp:124-126`](https://github.com/freefrank/LostOdysseyRecomp/blob/f9da0ae9bdd481a5ea4fe6431f3a4724d8261147/LostOdysseyRecomp/settings/menu.cpp#L124-L126)).
  Only skimmed.
- **Fable-2-Recomp** is built on a rexglue-sdk fork and ships a separate WPF launcher that
  edits a TOML config
  ([`Fable2.Launcher.csproj:4-5`](https://github.com/himdo/Fable-2-Recomp/blob/a4b0274e67d82514d44b42ac9320c30dc93bdaa3/launcher/Fable2.Launcher/Fable2.Launcher.csproj#L4-L5)).
  Only skimmed.
- **SVR2011-Recomp** has a WPF launcher that passes every setting on the command line, and no
  in-game page, so every change needs a restart (`WWE:docs/research/live-settings.md`,
  "SVR2011-Recomp").

### 2.3 Zelda64Recomp (N64Recomp, RT64)

- **Where:** four JSON files, `general.json`, `graphics.json`, `controls.json` and
  `sound.json` (`Z64:src/game/config.cpp:21-24`), in `%LOCALAPPDATA%\Zelda64Recompiled` or
  `~/.config/<id>`. Portable mode is `portable.txt` in the working directory
  (`Z64:src/game/config.cpp:134-188`, `Z64:README.md:134-135`).
- **Schema:** no central list. There is hand-written JSON code per file with
  `from_or_default` (`Z64:src/game/config.cpp:67-119, 250-260`) and a `GraphicsConfig` struct of
  enums (`N64MR:ultramodern/include/ultramodern/config.hpp:41-81`). Newer N64ModernRuntime adds
  a schema-driven config with typed options, descriptions, dependencies between options and
  `requires_confirmation` (`N64MR-HEAD:librecomp/include/librecomp/config.hpp:24-176`).
- **Deck defaults:** a Deck is detected by DMI vendor "Valve" or `SteamDeck=1`. There it
  defaults to fullscreen (`Z64:src/game/config.cpp:37-62, 254`).
- **Robustness:** saves go to `.temp`, the old file to `.bak`, and reads fall back to `.bak`.
  If both fail the defaults are saved (`Z64:src/game/config.cpp:190-229, 480-515`). There is no
  version.
- **Live or restart:** graphics changes are staged and applied with an Apply button (F, or a
  pad button); closing with unapplied changes asks "Apply or Discard"
  (`Z64:assets/config_menu/graphics.rml:311-320`, `Z64:src/ui/ui_config.cpp:186-208, 509-528`).
  Apply is live, MSAA included: RT64 rebuilds shaders and targets
  (`RT64:src/hle/rt64_application.cpp:457-482`). The graphics API is file-only and is read at
  renderer creation (`Z64:src/main/rt64_render_context.cpp:270-284`).
- **UI:** RmlUi documents with explicit `nav-up/down/left/right` on each control
  (`Z64:assets/config_menu/graphics.rml:18-41`). The pad is custom code that turns the D-pad
  and stick into arrow keys for RmlUi (`Z64:src/ui/ui_state.cpp:486-515, 631-680`). A
  description panel follows focus (`graphics.rml:272-305`). MSAA choices are disabled above
  the device's maximum (`ui_config.cpp:1003-1005`).
- **Launcher:** an in-process RmlUi screen: Select ROM, then Start game, Controls, Settings,
  Mods and Exit (`Z64:assets/launcher.rml:38-61`).

### 2.4 Ship of Harkinian (libultraship)

- **Where:** `shipofharkinian.json` (`SOH:soh/soh/OTRGlobals.cpp:279`), portable by default
  (`"."`; `NON_PORTABLE` is off, `LUS:src/ship/Context.cpp:546-592`). Linux AppImages use
  `$XDG_DATA_HOME` (`SOH:soh/soh/OTRGlobals.cpp:1516-1540`).
- **Schema:** CVars under `"CVars"` as dotted keys (`LUS:src/ship/config/ConsoleVariable.cpp:243-290`).
  Each default is given where the value is read, so there is no central list
  (`SOH:soh/soh/SohGui/SohMenuSettings.cpp:138-148`).
- **Versioning:** real migrations. `ConfigVersionUpdater` subclasses run when their version is
  above the file's `ConfigVersion`, and then the file is saved
  (`LUS:include/ship/config/Config.h:12-51`, `LUS:src/ship/config/Config.cpp:257-271`). SoH has
  updaters 1 to 5, which drop and rename keys (`SOH:soh/soh/config/ConfigUpdaters.cpp:1470-1531`).
- **UI:** an ImGui menu bar (F1) and menu (Esc) (`LUS:src/ship/window/gui/Gui.cpp:20-21, 229-245`).
  Pad navigation is opt-in through "Menu Controller Navigation", whose tooltip warns that "this
  will disable game inputs while the menu is visible" (`SohMenuSettings.cpp:142-148`).
  `ImGuiConfigFlags_NavEnableGamepad` is set only while a menu is visible (`Gui.cpp:97-102, 176-185`).
- **Presets:** JSON blocks per section, built-in and user-saved, merged or overwritten
  (`SOH:soh/soh/Enhancements/Presets/Presets.cpp:57-178`).
- **Restart:** a graphics backend change "requires a restart", per the README
  (`SOH:README.md:62`). No safe mode found.

### 2.5 ReXGlue upstream

- Cvar lifecycles are the same as ours (`REX:include/rex/cvar.h:135-140`). A `kRequiresRestart`
  flag set at runtime goes on `GetPendingRestartFlags()`, and setting a `kInitOnly` flag after
  init is rejected (`REX:src/core/cvar.cpp:305-335`).
- Precedence is default < config < environment < command line < runtime. A lower source never
  overwrites a higher one (`REX:include/rex/cvar.h:142-150`, `REX:src/core/cvar.cpp:654-677`).
- The config is `exe_dir/<App>.toml` (`REX:src/ui/rex_app.cpp:145-158`). `SaveConfig` writes
  only non-default values and no version (`REX:src/core/cvar.cpp:538-546, 693-712`).
- The settings overlay has search, per-entry Reset, Live/Requires restart/Read-only tooltips
  and "Save to config" (`REX:src/ui/overlay/settings_overlay.cpp:204, 300-310, 410-430, 503-509`).
  No `NavEnableGamepad` or `ImGuiKey_Gamepad*` appears in the SDK, so the overlay has no pad
  navigation [inferred from a grep].

### 2.6 Xenia Canary and Xenia Edge (the Oracle)

- **Canary:**
  - `portable` defaults to true on Windows, so its config sits beside the exe; elsewhere it
    is `Documents\Xenia` or XDG (`XC:src/xenia/app/xenia_main.cc:122-130, 486-503`).
  - A global `xenia-canary.config.toml` plus per-game `config/<TITLEID>.config.toml`, loaded
    at launch (`XC:src/xenia/config.cc:34-37, 308-315`, `XC:src/xenia/emulator.cc:1658-1670`).
  - Precedence: command line > game config > global > default (`XC:src/xenia/base/cvar.h:214-225`).
- **Canary migration:**
  - The file is re-saved on every start, with descriptions as comments
    (`XC:src/xenia/config.cc:243-262, 301-304`).
  - A `defaults_date` cvar plus dated `UPDATE_from_*` entries change a value only if the
    player still has the old default (`XC:src/xenia/config.cc:27-31, 106-126`,
    `XC:src/xenia/base/cvar.h:451-536`).
  - A value of the wrong type is reset to the default with a warning box (`config.cc:128-146`).
- **Edge:**
  - The same core, with `portable` defaulting to false (`XE:src/xenia/app/xenia_main.cc:101-103`).
  - A wxWidgets Advanced Settings dialog generated from every cvar, with Reset to Defaults,
    and a per-game config panel (`XE:src/xenia/ui/advanced_settings_dialog_wx.cc:133-154, 423-489`,
    `XE:src/xenia/ui/game_config_panel_wx.cc:543-558`).
  - A GPU backend change relaunches the process (`XE:src/xenia/app/emulator_window.cc:3656-3674`).
  - Our Oracle install runs portable (`.tools/xenia-edge/portable.txt`).
- **Not a model for players.** Per-game files and every-cvar dialogs fit an emulator running
  many games. A single-game port has one game and a curated list [inferred].

### 2.7 WWE All Stars

See §1.5: in-game page plus Avalonia launcher over one registry, schema export, pinned and
next-start marks, presets, one migration.

### 2.8 Steam Deck (Steamworks)

From [Steam Deck compatibility](https://partner.steamgames.com/doc/steamdeck/compat). It was
read through a fetch tool that summarises, so check the exact wording before quoting it to
players:

- Controller: "The default controller configuration must provide users with the ability to
  access all content", and "players must not need to adjust any in-game settings in order to
  enable controller support".
- Launchers: "For games with launchers, those launchers also must meet the requirements …
  including full navigability with a controller." Valve recommends "strongly against
  requiring the user to navigate a launcher to play your game". Needing the touchscreen to
  navigate a launcher rates a game only Playable.
- The default configuration must give a playable frame rate on Deck: 30 fps at 800p. Glyphs
  must match the active input. The smallest text must be 9 px at 1280x800.
- [Recommendations](https://partner.steamgames.com/doc/steamdeck/recommendations): default
  graphics options for specific hardware such as Deck are fine. Don't sync graphics settings
  through Steam Cloud. `ISteamUtils::SetGameLauncherMode` exists for launchers that can't
  avoid it ([ISteamUtils](https://partner.steamgames.com/doc/api/ISteamUtils)).

The port is not a Steam release, but players will add it to Steam as a non-Steam game and
judge it by the same Game Mode experience [inferred].

### 2.9 Summary table

| Port | Where | Format | Live? | Restart told how | UI | Pad | Versioned |
|---|---|---|---|---|---|---|---|
| Unleashed | %APPDATA% or portable.txt | TOML | nearly all | auto restart on menu exit | in-game, ImGui draw list | yes, game's pad | no |
| Marathon | same | TOML | nearly all | prompt names options | same | yes | no |
| Zelda64 | %LOCALAPPDATA% or portable.txt | 4 JSON | yes, after Apply | (none found) | in-game RmlUi, in-exe launcher | yes, custom | no (.bak) |
| Ship of Harkinian | portable by default | JSON CVars | mostly | README | ImGui menu bar | opt-in | yes |
| Xenia Canary/Edge | beside exe or Documents | TOML + per game | varies | relaunch (Edge GPU) | wx dialog (Edge) | no | dated defaults |
| WWE All Stars | %APPDATA% | TOML | most | `*` and note, next start | in-game ImGui + Avalonia launcher | yes, input hold | one migration |
| Top Spin 4 today | %APPDATA% / XDG | TOML | image settings all live | none (no UI) | F6 dev overlay only | no | no |

## 3. Options and trade-offs

### 3.1 Where players change settings

| Option | For | Against |
|---|---|---|
| **A. File and command line only** (today) | Nothing to build. Best for agents and scripts. Unleashed already leaves some settings file-only (§2.1) | Needs a keyboard and a text editor; impossible in Deck Game Mode. Typos are caught only by the load warnings (§1.1). Players get no help text |
| **B. Separate GUI launcher with every setting** (SVR2011, Fable 2 style) | Familiar on PC. Can show settings before the first frame | Every change needs a restart, and the player can't see the result in place. Two UIs to keep in step with the registry. Must be fully pad-navigable for Deck (§2.8). Launcher-only failures happen before the game starts (WWE's first bug, §1.5). Players asked to skip it (§1.5) |
| **C. In-game page only** (Unleashed, Zelda64, SoH) | The setting changes behind the page, so the player previews it live (§1.3). One codebase with the registry. Pad-first. The game exe is all a Deck player launches | Can't do disc setup, because the exe needs game data (WWE ADR-0018). A setting that breaks startup can't be fixed from inside the game |
| **D. In-game page plus a thin launcher** (WWE All Stars) | C's strengths. The launcher covers setup, Play, start-time rows and a safe start. One registry through the schema export | Two front ends, though the launcher's settings part is small and generated |
| **E. In-exe first-run setup instead of a launcher** (Unleashed installer, Zelda64 launcher screen) | One exe, pad-driven | Moves the C# disc preparation (`launcher/src/Launcher.Core`) into C++, or makes the exe spawn it. Unleashed's file picker fails in Deck Game Mode (§2.1). Goes against ADR-0004's launcher copy |

**Recommended: D**, with the launcher's settings part kept to what C can't do [inferred]. The
deciding facts:

- Every image setting is live here (§1.3), so the in-game page shows its effect at once.
- Valve's launcher guidance (§2.8).
- WWE's code is copyable under ADR-0004 (§1.5).
- The SDK and our patches already have the overlay layer, the F4 slot and the input hold
  (§1.4).

### 3.2 UI technology for the in-game page

| Option | Cost | Pad | Notes |
|---|---|---|---|
| **Dear ImGui, hand-styled** (WWE, Unleashed) | Already linked: SDK ImGui 1.92.5 draws over the presenter for both renderers (§1.4). MIT (`IMG:LICENSE.txt:1-3`) | `NavEnableGamepad` needs the backend to feed `ImGuiKey_Gamepad*` (`IMG:imgui.h:1752, 1770`; `IMG:imgui.cpp:176-198`). The SDK feeds only keys and mouse, so WWE and Unleashed read the pad themselves and drive their own row model | ImGui's FAQ says it is not designed for game UIs and its skinning is limited (`WWE:docs/research/launcher.md` §2); Unleashed gets console quality from a draw list with custom art (§2.1) |
| **RmlUi** (Zelda64) | A new dependency: MIT plus FreeType (`RML:readme.md:78-80, 544-548`). Needs a Vulkan render interface on the presenter's device [inferred] | Spatial `nav-*` navigation, "suitable for controllers", since RmlUi 6.0 (`RML:readme.md:76`, `RML:changelog.md:862-869`). Pad events still need app code (§2.3) | Higher ceiling (CSS layout, transitions). Its cost buys looks, not function, for a list of about ten rows |
| **The game's own menu system** (WWE's PC SETTINGS row) | Reverse engineering per menu; WWE needed three guest hooks (`WWE:src/settings/options_hooks.cpp:1-11`) | The game's own | Best looking entry. Top Spin 4's menu technology is unknown; Scaleform is [unverified] (`docs/research/feasibility.md:523`) |

**Recommended:** ImGui, hand-styled, with WWE's page model. Optionally add a row in the game's
OPTIONS menu as the entry point once RE shows it is cheap. Using the game's menus for the page
itself is not worth it.

- **Pad navigation:**
  - Hold the game's input while the page is open (`SetGuestInputHold`, §1.4).
  - Read the pad through `HeldState`: LB/RB switch tabs, up/down moves between rows,
    left/right changes the value, B closes.
  - Swallow the press that opened the page and the one that closed it (WWE spec,
    `WWE:.scratch/in-game-settings/spec.md`, Solution, Input).
  - Show button glyphs for the active device (Valve, §2.8).
- **Text entry** (key bindings are text, `registry.h:111-114`) needs an on-screen keyboard or a
  "press a key" capture (`IMG:docs/FAQ.md:185`; Valve, §2.8) [inferred].

### 3.3 Applying changes and telling the player

- **Live** (every cvar marked `kHotReload`):
  - Filtering, sharpening, resolution scale, MSAA, VSync.
  - Apply on change and save at once, as WWE does. Zelda64's staged Apply button suits a
    renderer whose changes are expensive or risky (§2.3). Here a scale or MSAA change costs
    one hitch (§1.3).
- **Next start** (renderer, pad bindings, GPU device):
  - Save the change, keep it pending, and mark it ("* Applies the next time the game starts",
    WWE, §1.5).
  - Optionally offer "Restart now" on close. Unleashed restarts by itself; Marathon names the
    options that need it (§2.1, §2.2).
  - A relaunch must carry the same command line, so scripted runs stay as they were
    [inferred].
- **Set by launch options:** a cvar whose source is the command line or environment (§1.2) is
  shown as pinned, and a change lasts only for the session (`WWE:page_model.h:297-300`). That
  keeps the file clean in agent runs.
- **Unavailable here:** `Setting::AvailableOn` already marks Native-only rows on Classic
  (`registry.h:31-35`). Values the device can't do (MSAA 8 above its limit, a scale above its
  maximum image size) are shown greyed with a reason (Unleashed, Zelda64), not silently
  clamped. The plan already knows the clamp (`04-resolution-scale.md:26`).
- **Derive the marks from the lifecycle,** not from hand-written help text. WWE's live-settings
  research found the docs and the code disagreeing (§1.5).

### 3.4 Persistence and schema

- **Location:**
  - Keep `%APPDATA%\TopSpin4Recomp` and XDG under Proton (§1.1). That meets goals.md
    ("stored in a per-user directory") and ticket 30's Proton box.
  - Portable mode through `portable.txt` beside the exe is common (Unleashed, Zelda64, Canary,
    Edge, SoH by default, §2) and cheap: one check in `OnConfigurePaths` [inferred]. It is an
    owner call (§4).
  - Don't sync settings through Steam Cloud (Valve, §2.8).
- **Format:**
  - Keep flat TOML with line-preserving, atomic single-key writes (§1.1). It survives hand
    edits and comments, unlike Unleashed's whole-file rewrite (§2.1).
  - Add a `.bak` copy as Zelda64 does (§2.3). The `.corrupt` set-aside already covers a broken
    file.
- **Versioning:**
  - Add a `settings_version` key and an ordered list of migrations, applied once and then
    saved. This is LUS's updater pattern (§2.4) on top of WWE's `MigrateSettingsText` (§1.5).
  - For changing a default, Canary's rule (change it only if the player still has the old
    default, §2.6) fits ADR-0001 well.
  - Settings will be renamed: the WWE port did it within weeks (§1.5).
- **One registry, exported:**
  - Keep `PlayerSettings()` the only list. Add label, tab, group and preset data to it as
    WWE's did.
  - Export it with `--print-settings-schema` so the launcher, docs and tests read the same
    list (`WWE:src/settings/schema.h:120-177`).
  - The existing start-up check that cvar and registry defaults agree (§1.1) stays.
- **For agents and headless runs, keep:**
  - per-run `--user_data_root` (§1.2);
  - command line over file, never written back (`config_file.h:1-4`);
  - scripted `set` through the same `SetFlagByName` path as the page;
  - the page off in scene runs (WWE: "Does nothing where the page is off (scene runs)",
    `WWE:src/settings/settings_page.h:59`);
  - the session report listing the Enhancements in use (`.scratch/image-quality/spec.md:58`).

  A hidden window must never open the page or wait for input [inferred].

### 3.5 PC-port conventions worth copying

- **Descriptions on every row.** The registry already holds help text (`registry.h:72-98`).
  Show it in a panel for the selected row, saying what the setting does, its cost, and when it
  applies (Unleashed, Zelda64, WWE).
- **Previews.** Live application is the preview here. Unleashed's thumbnails (§2.1) matter only
  for next-start rows; a short description is enough there [inferred].
- **Presets.**
  - A Graphics preset row that sets resolution scale, MSAA, filtering and sharpening, and
    shows Custom once they diverge (WWE, §1.5; SoH's section presets, §2.4).
  - The first preset is "Original", to keep ADR-0001.
  - A hardware-suggested preset on first start, as WWE and Unleashed do (§1.5, §2.1), would
    change the out-of-box look away from the original. That conflicts with ADR-0001 unless
    the owner decides otherwise (§4).
  - Deck: the original 720p already meets Valve's playable default (§2.8), assuming ticket
    30's 60 fps holds.
- **Reset:**
  - Per row (Unleashed's X, the SDK overlay's Reset), and "Reset all to original" per tab.
- **Safe start:**
  - A setting that breaks startup must be recoverable without editing files. Prior art:
    - Unleashed relaunches with the other graphics API after a device crash (§2.1);
    - our Native-to-Classic fallback and the GPUF exit code with its launcher hint (§1.4).
  - What is missing:
    - a `--safe` flag, and a launcher button that passes it. It starts on the registry
      defaults (Classic, scale 1) for one session without touching the file [inferred];
    - optionally a startup sentinel: if the last two starts never reached the title screen,
      offer the safe start [inferred].
- **Keep debug cvars out of the player UI.** The F6 overlay stays for developers; the page
  shows only the registry.

## 4. Open questions for the owner

1. **Entry to the page.** Is F4 plus a pad combo enough for 1.0, or should there be a PC
   SETTINGS row in Top Spin 4's own OPTIONS menu? The row needs reverse engineering, and the
   menu technology is unknown.
2. **Which pad combo opens the page?** WWE uses both sticks clicked (`WWE:settings_page.cpp:304-319`).
   Top Spin 4 maps LS and RS as game inputs (`ts4_pad_ls`, `ts4_pad_rs`,
   `settings_cvars.cpp:44-45`). Whether a combo of them collides with play needs checking
   [unverified].
3. **Launcher scope for 1.0.** Copy WWE's Avalonia launcher (ADR-0004, ADR-0018) with setup,
   Play and the next-start rows? Or setup only, with everything else in game? Must the game
   exe start without it after setup (recommended)?
4. **Next-start changes.** Should they only be marked, as in WWE, or offer "Restart now", as in
   Unleashed and Marathon?
5. **Portable mode.** Support `portable.txt` beside the exe, or per-user only?
6. **Presets and first-start defaults.** Add a Graphics preset row? Allow a hardware-suggested
   preset, or a Deck-specific default, on first start? That bends ADR-0001's "defaults are
   original".
7. **How much styling?** Should the page look like the game's own menus (custom fonts and art,
   as WWE and Unleashed do), or be clean and plain?
8. **Bindings in the page.** Remapping is a 1.0 goal (goals.md). Should pad and key bindings
   be edited in game (they need a capture flow and are next-start today) or in the launcher?
9. **Safe start.** Add a `--safe` flag and a launcher button, and should the game itself offer
   it after failed starts?
10. **F6 overlay in release builds.** Keep it for players who want every cvar, or hide it
    behind a developer flag?

## Sources

- **This repo:**
  - `src/settings/registry.h`, `settings_cvars.cpp`, `settings_file.{h,cpp}`,
    `config_file.h`, `renderer_choice.h`;
  - `src/topspin4_app.h` (80-155), `src/user_paths.h` (1-45), `src/script_input.h` (19),
    `src/native/native_renderer.cpp` (1820-1858), `src/native/gpu_failure.h`,
    `src/native/renderer_fallback.h`;
  - `scripts/smoke.ps1` (48-53), `scripts/scene_runner.py` (443-509);
  - `launcher/src/Launcher.Cli/Program.cs`;
  - `patches/rexglue/0007-native-frame.patch`, `0014-in-game-settings.patch`,
    `0017-live-sharpening.patch`, `0018-live-vsync.patch`;
  - `.tools/xenia-edge/portable.txt`;
  - `.scratch/pc-port-1-0/issues/10-settings-system.md`, `30-release-candidate-deck.md`;
    `.scratch/image-quality/spec.md`, issues 02, 04, 06 and 07;
  - docs: goals.md, ADR-0001, ADR-0004, ADR-0005, image-quality.md, feasibility.md.
- **ReXGlue SDK (vendored, `f5337cd`):** `include/rex/cvar.h` (136-149),
  `include/rex/input/input_system.h` (44-52), `src/ui/rex_app.cpp` (106-184, 360-428),
  `src/ui/overlay/settings_overlay.cpp` (32-49), `src/ui/imgui_drawer.cpp` (625),
  `thirdparty/imgui/imgui.h` (32).
- **WWE All Stars (local, `387e216`):**
  - `docs/adr/0018-launcher-stack.md`;
  - `docs/research/launcher.md` (§2, §6), `docs/research/live-settings.md`,
    `docs/research/community-feedback.md` (58, 73-80);
  - `src/settings/registry.h` (17-120), `page_model.h` (49-82, 297-395),
    `settings_page.{h,cpp}`, `schema.h` (120-177), `config_file.h` (150-166),
    `options_hooks.cpp` (1-11), `headless.h`, `port_defaults.h`;
  - `launcher/src/Launcher.Core/SettingsSchema.cs` (77); `.scratch/in-game-settings/spec.md`.
- **Upstream repos** (base URL plus `path#Lx-Ly`):
  - `UR:` https://github.com/hedge-dev/UnleashedRecomp/blob/cf829a9eca8fb680fba4b0409ddeb6ca92f22e3c/
  - `MR:` https://github.com/sonicnext-dev/MarathonRecomp/blob/dd91996040e37e1245729055f83d72c69c3b78f0/
  - `Z64:` https://github.com/Zelda64Recomp/Zelda64Recomp/blob/b65c482ed672258eb684ab94a4e8c8aa662615ee/
  - `N64MR:` (pinned by Z64) https://github.com/N64Recomp/N64ModernRuntime/blob/df7e820d8c55e4fcb4616c210cbb2c01b25cd48c/
  - `N64MR-HEAD:` https://github.com/N64Recomp/N64ModernRuntime/blob/cdf5abbd5026fef5c364c676e4667c45e42b6863/
  - `RT64:` (pinned by Z64) https://github.com/rt64/rt64/blob/23cab603c4f9f4a8b369b38e036f1aa484603878/
  - `SOH:` https://github.com/HarbourMasters/Shipwright/blob/2827fc2a0f56a9cdc18a87adf8c1bb7e91315f41/
  - `LUS:` (pinned by SOH) https://github.com/Kenix3/libultraship/blob/da6daf1a50a5e46d6641408d52227e31e85e46df/
  - `XC:` https://github.com/xenia-canary/xenia-canary/blob/48b318a2ad7c36f8f3901a94e290ba70ae0ffec5/
  - `XE:` https://github.com/has207/xenia-edge/blob/990bfd85948abe6ec1066c71a5ff54601ce25d0c/
    (newer than the Oracle binary `4d07204`)
  - `REX:` https://github.com/rexglue/rexglue-sdk/blob/c94f5ebdcb3c9d1a460ca48e04f9758448f8d518/
  - `IMG:` https://github.com/ocornut/imgui/blob/80d572ce359e2a9ca0c4386ab9cf220d5c15ce0f/
    (1.93.1 WIP; the SDK vendors 1.92.5)
  - `RML:` https://github.com/mikke89/RmlUi/blob/cf6d1f4c5e683a43822cbb669d639f96eca61f08/
  - LostOdysseyRecomp `f9da0ae`, Fable-2-Recomp `a4b0274` (linked inline, §2.2).
- **Valve:** [Steam Deck compatibility](https://partner.steamgames.com/doc/steamdeck/compat),
  [Steam Deck recommendations](https://partner.steamgames.com/doc/steamdeck/recommendations),
  [ISteamUtils](https://partner.steamgames.com/doc/api/ISteamUtils). Read through a
  summarising fetch, so recheck quotes.
- **Not checked:** Proton compatibility of an Avalonia Native AOT launcher [unverified];
  whether Top Spin 4's menus are Scaleform [unverified].

## Addendum (2026-10-28): what Top Spin 4's menus are built with

A follow-up RE pass on the TU1 modules answered §4's Scaleform question. Treat the addresses as leads to confirm, not settled facts.

- **Not Scaleform.** No `GFx`, `Scaleform`, `.swf`, `fscommand` or `ExternalInterface` strings appear in any of the three modules. Loader uses XUI only for the boot and Press Start screens (`Common.xzp`, `PressStart.xzp`, `Startup.xzp`).
- **The Swing front end is PAM's in-house "RGui"/"Rui", driven by Lua 5.0.2.** Strings include `$Lua: Lua 5.0.2 ...`, the `RGuiScene`, `RGuiListNode`, `RGuiGridNode` and `RGuiAction::*` classes, `Rui_ReadLuaFile('%s')`, `PackageHierarchy.lua` and `ItemFill('%s',%d)`. Screens are `S4*.scene` / `.sceneData` / `.anims` / `.actions` files (templates `T4*`) inside `.package`s in PAM-LZX compressed `.arc` archives.
- **Lua API registration** is at `881D26E8` (`PushScene`, `PopScene`, `SetControlData`, `SetTextID`). The `PushScene` handler is at `881D1360` and calls the native scene push at `88202108`. The list node's row fill is at `881DC0A8`; rows come from data bindings (`ItemMapping`, `SourceAttribute`, `@key@` text IDs) with an optional Lua `ItemFill`.
- **OPTIONS is defined in data.** `PracticeGroundPart.arc` holds `S4MainMenuSubOptions`, `S4Options_AudioOptions`, `S4Options_IngameOptions`, `S4Options_IngameHelpers`, `S4PG_OptionsMenu` and `Xbox3DConfig.package`. TU1's `updatedata/TitleUpdate.arc` replaces `S4Options_IngameOptions.scene`/`.actions`, so archives evidently override scene files by name.
- **Script commands** are registered at `887C1768`: `ApplyProfileOptions` `88794C98` (→ `888E6200`), `LoadProfileOptions` `8879B808`, `Is3DAccessible` `8877A338` (returns the flag at `*(88D45A48)+0x70`) and `SwitchDimension` `8877F078`. The career `SetOption*` table is at `88C6A27C`. The string table is `Media\StringTable\%sStringTable.bin` (xref `887EAB70`).
- **WWE's technique carries over in outline.** Patch the scene or `.actions` data as it loads, answer the label through the string-table lookup, and catch the activation (`PushScene` or a command handler). **[inferred, unverified]** The cheapest route reuses the Xbox 3D entry: force `Is3DAccessible` true, relabel it, and open the page when `Xbox3DConfig`/`S4Xbox3DDisclaimer` is pushed. This only works if OPTIONS has a 3D row gated by that flag.
- **Effort [inferred]:** a native row that opens the ImGui page takes 3–5 days with the 3D row, or about 2 weeks if a new row must be added; medium risk. Fully native pages take 4–8 weeks and need a PAM-LZX decompressor and scene/Lua authoring; high risk.
- **Not known:** the row layout inside the scenes (not decompressed), and whether `.actions` files are Lua source or bytecode.

## Addendum 2 (2026-10-28): OPTIONS scene data and the PC SETTINGS route (ticket 10)

All addresses are in **Swing_DLL** (TU1, Ghidra `TopSpin4TU1/Swing_DLL.xex`). Scenes were decompressed from the player's own archives with throwaway tools (an LZX `.arc` unpacker and a PAM Lua 5.0 disassembler); neither the data nor the tools are committed (ADR-0006).


### 1. Archive format (seen)
- `.arc` header (big-endian): `u32 h0` (data base = h0+4), `0x20`, `count`, `count`, `?`, `dirCount`, `dirCount`, `"PAM-LZX\0"` at 0x1C. Then `count` entries of `{u32 nameHash, u32 offset (relative to h0+4), u32 compSize}` sorted by hash. A directory table and the name strings follow.
- Each entry has its own header: `"PAM-LZX\0"`, `u32 1`, `u32 0x100000` (window), `u32 uncompSize`, `u32 uncompSize`, 12 bytes, `u32 hdrSize=0x2C`, `u32 crc?`. After that come XMemCompress-style frames: `0xFF u16 unc u16 comp`, or `u16 comp` for a 32 KB frame. libmspack `lzxd` with window_bits=20 decodes all 56 TitleUpdate entries and 645/651 PracticeGroundPart entries. The 6 failures are multi-part textures with larger headers; they don't matter here.
- Overrides go by name hash. `S4Options_IngameOptions.scene` has hash `44367873` in both PracticeGroundPart.arc and TitleUpdate.arc. TU1 does **not** override `S4MainMenuSubOptions`.

### 2. File formats (seen)
- `.scene`, `.sceneData` and `.package` are **compiled Lua 5.0 bytecode** (`1B 4C 75 61 50`, big-endian, debug info kept, chunk name `@C:\Data\TS4\Asset\UI\RUI\...\S4X.scene`). PAM changed the opcode set: the VM in `8880E280` dispatches on `cmplwi r29,38` and a jump table at `8880E3E8`, which gives 39 ops. Ops 0–15 are standard (ADD..DIV = 12–15). Ops 16–21 are POW, UNM and 4 extra ops, not resolved. NOT is 22, JMP 24, EQ 25, TEST 28, CALL 29, RETURN 31, FORLOOP 32 (its handler at `8880FA1C` references the "`for' initial value" string), SETLIST/SETLISTO 35/36 (shared handler) and CLOSURE 38.
- **`.actions` is neither Lua source nor bytecode.** It is a custom binary node graph: `u32 nodeCount`, `u32 size`, an offset table, then nodes such as `If17555`, `Switch17575`, `Case17590`, `True…` and `EXPORT_*`. Nodes reference Lua closures as text, e.g. `self.__Action_43()`, plus scene names and commands (`ChangeScene`, `PushLayer`, `PlaySound`).

### 3. OPTIONS row layout and binding (seen, S4MainMenuSubOptions.scene, PG entry `d5ff0d94`)
- The list control is `|SubOptions` (ActionPrefix `SubOptions`, `ListProcessInput()`, Up/Down → `SubOptions_SelPrevious/SelNext`).
- Rows are built at runtime by the ItemList closure P47, not taken from sceneData. `Items = { {Id=<hash>, Enabled=true, MappingTitle='<textID>', __index, __count, __ratio}, … }`. The six rows are GamePlayOptions (-238111021), GamePlayHelpers (525693536), AudioOptions (-641984111), SavingDevice (-1630791758), Achievements (-921459450) and Credits (-1901401886).
- `ItemMapping = { {Control='ButtonText.textID', SourceAttribute='MappingTitle'} }`, so each label is a string-table text ID.
- Activation: the A button goes to the `.actions` `Switch17575`. Its Cases test the closures P36–P43 (`currentControl.Items[curSel+1].Id == <hash>`) and branch to that row's action.
- **There is a ready-made extra row.** `EXPORT_AddMoveCalibrationButton` (P28) does `table.insert(Items, {Id=1751522001, Enabled=true, MappingTitle='PS3MoveCalibrate'}); dirty=true`. P43 tests that Id, and P44 calls `ForceMoveRecalibrate(currentControllerNum)`. The graph node `If17555` evaluates P25, which is `IsMoveController(currentControllerNum)==true`, and appears next to the EXPORT node.

### 4. Xbox 3D row (seen)
- **OPTIONS has no 3D row.** The 3D toggle is in the **main menu** (`S4MainMenu.scene`, `|MainMenu`). `EXPORT_PatchList` (P51) inserts it when `Is3DAccessible()` is true: Id 1770957945, label `MainButtonTextTwoD`/`MainButtonTextThreeD`. Selecting it runs `EXPORT_SwitchDimension` → native `SwitchDimension`; `S4Xbox3DDisclaimer` is also involved.
- `Is3DAccessible` also gates `EXPORT_Patch3DCamera` (the CamTv3D camera) in `S4Options_IngameOptions`, both base and TU1. Forcing it true would change gameplay camera options and add a row to the main menu, not to OPTIONS. **Rejected.**

### 5. Recommended route: reuse the PS3 Move calibration row
1. **Show the row.** Hook `IsMoveController` at **`8877B2B8`**. Today it is a stub (`li r3,0; blr`), registered once at `887C2B34`. Return 1 only when the calling scene is `S4MainMenuSubOptions`. Other callers are S4StartScreen, S4ExhibitionAssignPad, S4ExhibitionPlayerSelect and S4PG_OptionsMenu, and those must keep getting 0. **[inferred]** The scene can be found from the handler context: `ctx[1]` is the scene node used by `SetTextID`, and `881CF4C0(&outStr, node)` gives the owning scene name. **Fallback:** call the action natively, the way `88908660` does: format `"$S4MainMenuSubOptions.EXPORT_AddMoveCalibrationButton()"`, then `881C2A58(881CE2B8(scene), &str, 0)`.
2. **Label.** Hook the RGui text resolver **`881EC108`** (`(uiMgr, const String* textID, Text* out, …)`; callers include `8820E790` (SetTextID/textID binding) and `881DD8E0`). Call the original. If `*textID == "PS3MoveCalibrate"`, replace `out` with "PC SETTINGS": build it with `8819A5C8` (wide-string ctor), assign with `8819BAD0`, then free with `88151AE0`. String-table lookups take the provider at `+0xF0`, keyed by `88608618(0, textID)`. The `%sStringTable.bin` load is in `887EAB70` (xref `887EABF0`).
3. **Activation.** Hook `ForceMoveRecalibrate` at **`8877B288`** (stub `li r3,1; blr`, registered once at `887C2D94`) and open the Settings page from it. S4PG_OptionsMenu also references it, but it can only fire if that scene's Move row appears, and item 1 keeps that row hidden.
- All three stubs exist as `sub_8877B2B8`, `sub_8877B288` and `sub_881EC108` in `generated/swing`, so `REX_HOOK_RAW` can override them.

### 6. Addendum addresses rechecked
All confirmed. `881D26E8` registers `PushScene` (string xref `881D2CE8`, handler `881D1360`, which calls `88202108(sceneMgr,&name,…)`). `SetTextID` handler is `881D1C48`. `881DC0A8` is a function in the list-node code (the SourceAttribute xrefs are nearby at `881DBDC4` and `881DD520`); its role as row fill was not rechecked. In the `887C1768` registrations: `ApplyProfileOptions` `88794C98`, `SwitchDimension` `8877F078`, `Is3DAccessible` `8877A338` (`lbz r3,0x70(*(88D45A48))`), `LoadProfileOptions` `8879B808`.

### Inferred, not seen
- That `If17555` (true branch) → `EXPORT_AddMoveCalibrationButton` and `Case17590` → `__Action_44`. The node graph was not fully decoded.
- That `ctx[1]` is the scene in command handlers.
- That P47 fills the list before the Move row is inserted, so the row lands last.

Confidence: high on the data layout and the missing 3D row in OPTIONS; medium on the hook route. The first run should log from each of the three hooks.

## Addendum 3 (2026-11-01): the rows moved to the main menu

The OPTIONS route of Addendum 2 §5 never worked: OPTIONS never calls `IsMoveController`
(`8877B2B8`), so the row never appeared. PC SETTINGS and a new EXIT TO DESKTOP row now live in
the main menu (`S4MainMenu`, list `|MainMenu`). All of this was seen in game runs unless marked.
Code: `src/settings/main_menu_rows.h` (pure parts, host tests) and `main_menu_rows_hooks.cpp`.

### Correction to §4
- `EXPORT_PatchList` (P51) appends **three** hidden rows: the 3D config row (label ID
  `MainButtonText3DConfig`, shown as "STEREOSCOPIC 3D"; choosing it changes scene to
  `S4Xbox3DDisclaimer`), the 3D mode switch (`MainButtonTextTwoD`/`ThreeD`, only when
  `Is3DAccessible()` answers yes, which it does under the recomp), and the PS3 Move tutorial row
  (`MainButtonTextMoveTutorial`, only when `IsAnyMoveConnected()` answers yes; it changes scene
  to `S4MoveTutorialMain`). The `MoveVideoTutorial` row assumed earlier never appears.
- On Xbox nothing calls PatchList, so the menu shows only GAME MODES, Xbox LIVE, PLAYER ZONE,
  UNLOCK OVERVIEW and OPTIONS.

### Route
| Hook | Address | What it does |
| --- | --- | --- |
| `Is3DAccessible` | `8877A338` | Asked just before the menu's UpdateList print when the menu is built: arms. Answers 0 to our PatchList (no 3D mode switch). |
| `Pam.PPrint` | `888048F0` | A no-op in retail. On the armed `"S4MainMenu: EXPORT_UpdateList"` it issues `$S4MainMenu.EXPORT_PatchList()` on the MainMenu node, as `88908560` does for S4LockerRoomSelect (`881E87C0` scene, `881FF158` node, `881CF4C0` node name, `881C2A58(881CE2B8(node), &str, 0)`). |
| `IsAnyMoveConnected` | `88778FA0` | Stub answering 0; answers 1 to our PatchList only (S4PG_Invitation keeps 0). |
| text resolver | `881EC108` | 3DConfig → "PC SETTINGS", MoveTutorial → "EXIT TO DESKTOP". |
| `ChangeScene` | `887C3370` | From the menu, `S4Xbox3DDisclaimer` opens the Settings page and `S4MoveTutorialMain` closes the window; the original is skipped. Other changes pass through. |

Data layouts:
- PPrint's Lua argument: `L = *(*(r5+8))`, index `*(r5+0xC)`; TValue (16 bytes) at
  `*(L+0x10) + (idx-1)*16`, type at +0 (4 = string), TString at +8. The TString header is
  **0x18** bytes: length at +0x14, characters from +0x18.
- ChangeScene's argument: `*(*(r3+0x10)+0x380)` is an object whose +0x10 is a game String
  (one word, the characters; refcounted header 16 bytes before).
- Inside a running action chain `881C2A58` queues the call and returns; it runs later in the
  same frame. So "our PatchList" is told by sequence, not by call stack: issued, then it asks
  Is3DAccessible and then IsAnyMoveConnected (its last query).

### Getting the menu back after PC SETTINGS
The row's action (Case17108 in S4MainMenu.actions) runs `SetActiveControl('')`, the `SceneOff`
animation and `WaitForTexturePacks()` before `ChangeScene`. Swallowing the ChangeScene leaves the
menu current but hidden, without input. `ChangeScene('S4MainMenu')` does nothing then:
`887BD8B8` skips a switch to the current scene. What works is `$S4MainMenu.On()` on the MainMenu
node, which a normal return to the menu runs (via OnActivate): it rebuilds the list (UpdateList,
so the rows come back through the hooks), plays SceneOn and gives `|MainMenu` the input. The
selection comes back on GAME MODES.

### Exit
EXIT TO DESKTOP calls the window's `RequestClose()` on the UI thread. That skips
`OnWindowCloseRequested`, so the session end is written first; the window's closing then exits
with 0, as a click on X does.

`IsMoveController` (`8877B2B8`) and `ForceMoveRecalibrate` (`8877B288`) are no longer hooked.
