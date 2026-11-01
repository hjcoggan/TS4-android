# A GitHub README for the port: precedents, constraints, material and a plan

Research date: 2026-11-01, for the public GitHub README (the repo has no remote yet; `docs/goals.md`
says it goes public after M2). It surveys how comparable recomps present themselves, what GitHub's
Markdown allows, what ADR-0006 permits, what this repo can already show, and then plans the README
section by section. Nothing here changes the README; implementation comes later.

**[inferred]** marks my own reasoning, **[unverified]** a claim I could not check against a primary
source. Every other claim comes from a file cited by path or a URL under the claim.

## Summary and recommendation

1. **Split the player help out of the README.** The Launcher's Help opens `README.md` beside it in
   Notepad (`launcher/src/Launcher.Core/HelpDocument.cs`, `MainWindow.axaml.cs:264-281`). A GitHub
   README with a banner, image tables and `<picture>` reads as raw HTML there, and its relative
   image paths point at nothing in the release folder. Ship the current `## Help` text as its own
   plain file (`HELP.md`) and let the GitHub README link to it.
2. **Follow the WWE All Stars README's shape**, the owner's own, which is already the most polished
   of the set: banner, pitch, bold "no game files" line, link row, video card, What works table,
   2x2 screenshot grid, features with a side-by-side comparison table, Play it, FAQ, Build from
   source (with a `<details>` block), How it works, Credits, Legal. Trim it to this port's features.
3. **Lead with one comparison, not many.** The strongest material on disk is the `scene_runner_shots`
   before-and-after set: the same guest frame, Original next to Native with Enhancements, with
   close-up crops. Show one match frame full width and two crops at 200 %, in a two-column table.
4. **Images: decide between repo and user-attachments first** (§3). ADR-0006's text permits
   screenshots of the running port; the repo's own practice so far forbids committing game art.
   User-attachments sidestep both the ADR question and `repo_guard.py`'s 512 KiB binary limit.
5. **Missing and needed:** a hero shot taken at the shipped presets, Launcher screenshots (no
   harness here), and a short match video (no encoder in the repo). §4 lists the script for each.

## 1. How comparable recomps present their READMEs

Read as raw `README.md` on 2026-11-01. The WWE All Stars research note
(`D:\Projects\WWE_ALLSTARS-RECOMP\docs\research\github-and-reddit-launch.md`, §B, 2026-10-10)
surveyed nine READMEs; this note re-reads the ones that matter and adds Kaiko's SVR2011 port.

### WWE All Stars Recomp (the owner's, `shaneomac1337/WWE-AllStars-Recomp`)

Local checkout `D:\Projects\WWE_ALLSTARS-RECOMP`, remote
`https://github.com/shaneomac1337/WWE-AllStars-Recomp.git`; 51 stars
([API](https://api.github.com/repos/shaneomac1337/WWE-AllStars-Recomp)). The live README matches
the local one ([raw](https://raw.githubusercontent.com/shaneomac1337/WWE-AllStars-Recomp/main/README.md)).
433 lines.

- **Order:** banner; two-paragraph pitch; bold "**This repository and its releases contain no game
  files.**"; a link row ("**[Download the latest release]** · What works · Install · FAQ · Build
  from source"); an HTML comment holding the place for a teaser video; a YouTube video card; What
  works; What the PC version adds (one `###` per feature); Play it; Starting without the launcher;
  Crash reports; If something is off; FAQ; Build from source; How it works; Credits; Legal.
- **Hero:** `docs/images/banner.jpg` at `width="100%"` in `<p align="center">`, with a full
  sentence of alt text.
- **Video:** no inline player. A thumbnail (`docs/images/video-card.jpg`, 80 % width) links to
  YouTube, with a `<sub>` caption naming the match and the hardware. The teaser is still an HTML
  comment: "drag out/launch/WWE-AllStars-Recomp-teaser-720p.mp4 into GitHub's editor and put the
  github.com/user-attachments URL it gives on its own line."
- **Screenshots:** a 2x2 `<table>` of `<td width="50%">` JPEGs right after What works; a second 2x2
  of launcher screens under "A launcher that sets everything up".
- **Before/after:** "Next to the Xbox 360", a two-column `<table>` with `<th>` headers "Xbox 360
  (720p)" and "PC, highest settings". Each pair is a 200 % zoomed crop above the full frame. A
  `<sub>` paragraph states exactly how each side was made (renderer, resolution, settings, "the
  same frames of one exhibition match ... nobody touching a controller").
- **Features:** prose per feature, tables where there are choices (settings, keyboard). No emoji.
- **Badges:** none.
- **Install:** "You need" list, five numbered steps, where the update file lives, Updating.
- **FAQ:** bold question, plain answer: emulator?, piracy?, Xenia?, why the TU, Steam Deck,
  ReShade, "Was AI used? Yes, a lot."
- **Build:** one PowerShell block, then "Build variants and PGO" inside `<details>`.
- **Credits** list, then **Legal**: "not affiliated with or endorsed by WWE, THQ, Yuke's or
  Microsoft. This repository contains no game code or data ... Do not share the `gamedata` or
  `updatedata` folders, a disc image, the update or DLC packs." Licences per folder.
- **Images:** 21 files in `docs/images/`, 115-460 KB each, JPEG for photos and PNG for the zoomed
  crops (local `ls -la`). Its `.git` is 20 MB.

### SVR2011-Recomp (the owner's, `shaneomac1337/SVR2011-Recomp`)

Local checkout `D:\Projects\SVR2011-RECOMP`, 163 lines. No images at all: H1, pitch, bold "**This
repository contains no game files.**", What works table, Play it, If something is off, Build from
source, How it works, Legal. This is the plain house model the WWE README grew from (WWE research
note §B).

### Kaiko's WWE SmackDown vs. Raw 2011 port (`KaikoClanworth1/wwe-svr2011-pc`)

[Raw README](https://raw.githubusercontent.com/KaikoClanworth1/wwe-svr2011-pc/main/README.md), about
54 KB of Markdown. Credited in the WWE README as where parts of its tooling started.

- **Order:** SVG banner; H1 "WWE SmackDown vs. Raw 2011 — PC Port"; pitch; a bulleted "It also
  adds:" list of 13 bold-led features; `> [!IMPORTANT]` "This repository has no game in it" and,
  straight after it, `> [!NOTE]` "This port was made with Claude Code"; Screenshots; Contents
  (manual TOC); What you need; Install and play; Controls; Settings; Features; Building from
  source; Troubleshooting; Contributors; Legal and credits.
- **Screenshots:** a 5-row, 2-column `<table>`, each cell an image plus a `<sub><b>caption</b></sub>`,
  stored in `docs/screenshots/` in the repo. Feature sections repeat single shots centred at
  60-80 % width.
- **Diagrams:** SVG banners (`docs/images/install-flow.svg`, `entrance-media.svg`) and a Mermaid
  flowchart of disc to game folder.
- **Legal:** "The screenshots in `docs/screenshots/` were taken of the port running. They're used
  only to show the port, and they belong to the game's owners." This is the only README in the set
  that addresses screenshots in the repo directly.
- **What not to copy:** emoji in table cells ("💿 **The game**", "🧑‍💻", "🤖"), an em-dash in the
  title, two alerts stacked back to back (GitHub's docs say not to, §2), and a 54 KB page that
  carries a full Android and modding manual.

### Unleashed Recompiled (`hedge-dev/UnleashedRecomp`)

[Raw README](https://raw.githubusercontent.com/hedge-dev/UnleashedRecomp/main/README.md).

- **Order:** centred 512 px logo (hosted in a separate `UnleashedRecompResources` repo); `---`;
  one-paragraph pitch; bold "This project does not include any game assets. You must provide the
  files from your own legally acquired copy"; "Check out the latest release here"; lineage
  (XenonRecomp, XenosRecomp, N64Recomp); Table of Contents; Minimum System Requirements; How to
  Install; Features; Update Roadmap; Known Issues; FAQ; Building (moved to `docs/BUILDING.md`);
  Credits.
- **No screenshots, GIFs or video.** Features are prose `###` sections, with `> [!NOTE]` and
  `> [!TIP]` alerts for caveats.
- **Install:** numbered steps; dumping guides in separate files (`docs/DUMPING-en.md`,
  `docs/DUMPING-USB-en.md`).
- **FAQ:** `###` per question; an anti-scam `> [!CAUTION]`: "We will never distribute builds on
  other websites, via Discord servers or via third-party update tools."
- **Known Issues** includes "Original Game Bugs ... intentionally preserved and will not be fixed",
  which matches this port's Fix original glitches stance (`docs/goals.md`, Oracle).

### Marathon Recompiled (`sonicnext-dev/MarathonRecomp`)

[Raw README](https://raw.githubusercontent.com/sonicnext-dev/MarathonRecomp/main/README.md). The
Unleashed template cut down: logo from a resources repo, `> [!CAUTION]` "still under active
development and is NOT meant for public use", pitch, the same asset line, TOC, Known Issues, FAQ,
Building (`docs/BUILDING.md`), Credits. No screenshots.

### Zelda 64: Recompiled (`Zelda64Recomp/Zelda64Recomp`)

[Raw README](https://raw.githubusercontent.com/Zelda64Recomp/Zelda64Recomp/dev/README.md).

- **Order:** plain H1, no logo; pitch naming N64: Recompiled and RT64; `###` "Check out the latest
  release here"; Discord widget (the only badge-like image); `###` "**This repository and its
  releases do not contain game assets. The original game is required to build or run this
  project.**"; a "No-AI stance" section; TOC; System Requirements; Features (`####` per feature);
  Planned Features; FAQ; Known Issues; Building (`BUILDING.md`); Libraries Used.
- **One image:** a Steam Deck settings photo inside the FAQ (`docs/deck_gyro_1.jpg`).
- FAQ opens with "What is static recompilation?" and "How do I choose a different ROM? **You
  don't.**"

### ReXGlue SDK (`rexglue/rexglue-sdk`, the toolkit under this port)

[Raw README](https://raw.githubusercontent.com/rexglue/rexglue-sdk/main/README.md): banner from a
`rexglue-media` repo plus shields.io badges (Discord, stars) inside a centred `<h1>`, CI badges in a
Builds table, and a "Disclaimer": "not affiliated with nor endorsed by Microsoft or Xbox ... not
intended to promote piracy nor unauthorized use of copyrighted material." The only README in the
set with shields badges; it is a tool, not a game port.

### What the set says

- Every one states near the top, in bold or an alert, that no game files are included **[all
  URLs above]**.
- The Sonic and Zelda ports show no gameplay at all. The two WWE-scene ports (the owner's and
  Kaiko's) show screenshots in the repo, and only the owner's WWE README shows a before/after
  comparison. **[inferred]** A comparison is the one thing that sets a port with Enhancements apart.
- Badges are rare for game ports; a release-version badge or a stars badge adds little next to a
  "Download the latest release" link **[inferred]**.
- Building goes to its own file in the larger projects (Unleashed, Marathon, Zelda) or into a
  `<details>` block (WWE).

## 2. GitHub Markdown constraints

- **HTML is sanitized.** "The HTML is sanitized, aggressively removing things that could harm you
  and your kin—such as `script` tags, inline-styles, and `class` or `id` attributes"
  ([github/markup README](https://github.com/github/markup), step 1; this is GitHub's markup
  library, not docs.github.com, which has no allowlist page). So: no CSS, no JS, no sliders. Layout
  is `align` and `width` attributes on `<p>`, `<img>`, `<td>`, plus `<table>`, `<sub>`, `<br>`.
  **[inferred]** A before/after slider like `analysis/before-after/page/index.html` is impossible;
  side-by-side table cells or a GIF are the options.
- **`<picture>` is supported**: "The `<picture>` HTML element is supported"
  ([basic writing and formatting](https://docs.github.com/en/get-started/writing-on-github/getting-started-with-writing-and-formatting-on-github/basic-writing-and-formatting-syntax#the-picture-element)).
  Theme-specific images use `<source media="(prefers-color-scheme: dark)" srcset="...">` with an
  `<img>` fallback ([quickstart for writing on GitHub](https://docs.github.com/en/get-started/writing-on-github/getting-started-with-writing-and-formatting-on-github/quickstart-for-writing-on-github)).
  The older `#gh-dark-mode-only` URL fragment is not in the current docs **[unverified]**. Only a
  transparent logo or banner needs this; a photographic banner or screenshot does not
  **[inferred]**.
- **Collapsed sections:** `<details>` with `<summary>`; "Any Markdown within the `<details>` block
  will be collapsed until the reader clicks"; `<details open>` starts it open
  ([collapsed sections](https://docs.github.com/en/get-started/writing-on-github/working-with-advanced-formatting/organizing-information-with-collapsed-sections)).
- **Alerts:** `> [!NOTE]`, `[!TIP]`, `[!IMPORTANT]`, `[!WARNING]`, `[!CAUTION]`. "Use alerts only
  when they are crucial for user success and limit them to one or two per article ... avoid placing
  alerts consecutively. Alerts cannot be nested within other elements"
  ([basic writing and formatting, Alerts](https://docs.github.com/en/get-started/writing-on-github/getting-started-with-writing-and-formatting-on-github/basic-writing-and-formatting-syntax#alerts)).
- **Images in the repo:** "use relative links instead of absolute links"; GitHub rewrites relative
  links and image paths for the branch being viewed
  ([about READMEs](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-readmes)).
- **README size:** "any content beyond 500 KiB will be truncated"; GitHub builds an Outline menu
  from the headings, so a manual TOC is optional (same page).
- **Uploaded attachments (user-attachments):** dragged into the web editor, "uploaded immediately
  to GitHub and the text field is updated to show the anonymized URL"; "For public repositories,
  uploaded files can be accessed without authentication." Limits: "10MB for images and gifs",
  "10MB for videos uploaded to a repository owned by a user or organization on a free GitHub plan",
  100 MB on paid plans. Types: PNG, GIF, JPEG, SVG, video `.mp4`, `.mov`, `.webm`; "we recommend
  using H.264 for greatest compatibility"
  ([attaching files](https://docs.github.com/en/get-started/writing-on-github/working-with-advanced-formatting/attaching-files)).
  WebP is not on the list.
- **Video in a README:** not documented on docs.github.com **[unverified]**. A relative-path `.mp4` committed to the
  repo does not embed: "GitHub's markdown rendering does not support embedding videos directly with
  the video tag or using the image syntax for video files"
  ([community discussion 133813](https://github.com/orgs/community/discussions/133813), secondary).
  A `github.com/user-attachments/assets/...` URL does play, on its own line or in
  `<video src="...">` ([discussion 173635](https://github.com/orgs/community/discussions/173635),
  a 2026-08-19 report, secondary; WWE's placeholder comment assumes the same).
  Plan for a thumbnail that links out as the fallback.
- **Repo size:** "We recommend repositories remain small, ideally less than 1 GB"
  ([about large files](https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-large-files-on-github)).
  A dozen 300 KB images is negligible **[inferred]**.
- **Social preview:** "a PNG, JPG, or GIF file under 1 MB ... at least 640 by 320 pixels (1280 by
  640 pixels for best display)", uploaded in the repo settings, not committed
  ([social media preview](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/customizing-your-repositorys-social-media-preview)).
- **Badges:** shields.io images are plain `<img>`/`![]()` links and render like any image
  **[inferred]**; none of the game ports above needs them.
- **This repo's own gate:** `scripts/repo_guard.py`, run from the pre-commit hook, refuses any
  staged binary above `MAX_BINARY_BYTES = 512 * 1024` ("Binary files above this are refused
  whatever they are"). Every committed image must be under 512 KiB, or the guard changes.

## 3. ADR-0006 and game screenshots

ADR-0006, in full:

> The repository holds only original code, patches, analysis notes and tooling. Game data,
> extracted or decrypted executables, and ReXGlue's generated C++ are never committed, because the
> generated code is derived from the game's executable. Players supply their own disc image and
> TU1, and the build generates everything else from those files. This is the same posture as the
> WWE ports and topspin4-decomp, and it is why `.iso/`, `gamedata/`, `updatedata/`,
> `analysis/xex/` and the generated sources are permanently git-ignored.

The repo's practice goes further than the text:

- `.gitignore`: "rexglue init writes the disc's title image here; never commit or embed game art."
- `scripts/scene_runner_shots.py` docstring: "Everything is drawn from game data: local, never
  committed (ADR-0006)."
- `analysis/before-after/page-spec.json` intro: "Local only: drawn from game data, never committed."
- `docs/research/disc-art.md`: "No image or game byte is in this note or the repository (ADR-0006)."

**Verdict.** The ADR's text names game data, executables and generated code; a screenshot of the
port running is none of those, and "the same posture as the WWE ports" points at a sibling that
commits 21 game screenshots. But this repo has consistently treated rendered game frames as "drawn
from game data" and kept them out, and the `.gitignore` line says "never ... embed game art", which
covers embedding in the README too, wherever the file lives. **[inferred]** So:

- **Committing screenshots to `docs/images/`** needs an owner decision recorded as an ADR-0006
  amendment or a new ADR-0012 ("screenshots and video of the port running may be committed for
  the README; no disc image, texture or art file extracted from the disc"), and each file under
  512 KiB for `repo_guard.py`.
- **User-attachments** put nothing in the repo, so ADR-0006 and the guard are not touched, but the
  `.gitignore` wording "embed game art" still argues for the owner's explicit OK. They also need no
  amendment to make images 1-2 MB, which helps a 2560x1440 comparison.
- Either way, Launcher screenshots without disc art are plain original work; the Launcher's home
  screen shows disc key art (`docs/research/disc-art.md`), which is game art.
- **Rights, not repo policy:** the frames show real players (Federer, Nadal, Davydenko, Simon) and
  Australian Open branding (`analysis/before-after/page/pair-02`, `pair-03`, `pair-17`).
  **[inferred]** Screenshots for commentary are common, and none of the takedowns in the WWE
  research (§C) targeted screenshots, but who holds Top Spin 4's rights today (2K) is
  **[unverified]**, and the owner should decide with that in mind. Kaiko's legal line ("taken of the
  port running ... used only to show the port, and they belong to the game's owners") is a model
  sentence for whichever route is chosen.

## 4. Showable material in this repo

Everything below is in `analysis/`, which `.gitignore` ignores (`/analysis/*`). No image, GIF or
video is tracked anywhere in the repo; the only video files on disk are FFmpeg test fixtures under
`.tools/rexglue-source/thirdparty/FFmpeg/tests/ref/` (not ours).

### What exists

| Path | What it shows | Size | README use |
|---|---|---|---|
| `analysis/before-after/page/pair-NN-original.png` (26 pairs) | The guest frame on Classic as the presenter shows it at 2560x1440 (the 1280x720 frame scaled up): title, main menu, Exhibition player select (Federer v Nadal), match frames 3600/4200/4800, Player Creator, Career screens, Academy, King of the Court, practice ground | 0.8-2.4 MB PNG each | The "Original" column |
| `analysis/before-after/page/pair-NN-variant0.png` | Native, same run and same guest frame, Resolution scale 4x and MSAA 8x per `page-spec.json`, which made these files; the script's current `MATCHED` is scale 3, MSAA 8, so a re-run as is gives scale 3 | 0.8-4.3 MB | Exact Classic-vs-Native-enhanced pair, pixel-aligned with the original |
| `analysis/before-after/page/pair-NN-variant1.png` | Native, every Enhancement at maximum (also 16x AF, High CAS), a separate run, so a live match moment can differ a little | 1.5-6.6 MB | Too sharp and not a shipped preset; skip or caption as maximum |
| `analysis/before-after/page/pair-NN-cropK-*.png` | Close-up crops of the same three images (crowd boards, line judges, Melbourne court text, faces, menu text) | 20-420 KB | The 200 % zoom row; `pair-03-crop0` (line judges, Australian Open boards) is the clearest |
| `analysis/before-after/page/index.html` | The slider page (`scripts/compare_page.py`) | 109 KB | Local review only (no JS on GitHub) |
| `analysis/before-after/oracle/practice-ground-001300.png` | Xenia Edge (Oracle) frame of the practice ground, 1280x720 | 893 KB | An "Xbox 360 (emulated)" reference, if wanted |
| `analysis/edge-menu/compare-title.png`, `compare-menu.png`, `sheet.png` | Edge vs port, title and menu, side by side, 1280x360 / 1280x600 | 336-403 KB | Parity evidence, not hero material |
| `analysis/edge-baseline-1/hud*.png`, `cards.png` | Edge baseline HUD crops | up to 945 KB | No |
| `analysis/edge-20261025-101451/shot-00Ns.png`, `fps.csv` | Edge timed shots, 1296x821 (window with border) | ~177 KB | No (odd size, window chrome) |
| `analysis/smoke-*/frame_times.csv`, `present_pacing.csv` | Per-frame timing of smoke runs | small | A frame-time chart is possible but niche; skip |

The best-looking existing frames (seen on a contact sheet of `variant1` at 480x270): player select
(pair 02), the match from the baseline camera (pair 03), the Player Creator (06), the Academy lesson
card over the grounds (12), King of the Court four-player select (17), and a close-up of Federer
walking out (21).

Caveat: the set was made on 2026-10-28, before the Default and Enhanced presets existed (ADR-0011).
Its variants are research settings, not what a player picks. The Enhanced preset is
`ts4_resolution_scale=auto`, `ts4_msaa=4`, `ts4_anisotropic_filtering=16`, `ts4_sharpening=off`;
Original is Classic, scale 1, AF original, Fix original glitches off (`src/settings/presets.h`).

### What is missing, and what makes each

| Missing | How to produce it |
|---|---|
| **Hero shot**, 16:9, the match at the Enhanced preset | `python scripts/scene_runner.py tests/scenes/exhibition-match.toml --native 1` with the Enhanced values passed as flags, capturing frame 3600 or 4200 with `--ts4_dump_presented` at 2560x1440; downscale to 1920 wide JPEG q85 (about 300-450 KB) |
| **Original vs Enhanced pair**, labelled with the shipped preset names | Re-run `python scripts/scene_runner_shots.py --scenes exhibition-match` with `MATCHED` set to the Enhanced values (scale auto at the window size, MSAA 4) and a max run swapped for Enhanced; or run the scene twice, once per preset, and accept that a live match moment can drift. The dual run is the only exact same-frame pair |
| **Classic vs Native pair** | Exists as `original` vs `variant0`. At equal settings Native matches Classic by design (M5, Native parity), so this pair is a parity statement, not a selling point; the Original vs Enhanced pair says more |
| **Screenshot grid** (4 shots) | Captures already listed in the scene files: `exhibition-match.toml` (player select 2400, match), `career-start.toml` (Player Creator 2880), `academy-lesson.toml` (2200), `king-of-the-court.toml` (2200). One re-run at Enhanced via `scene_runner_shots.py` covers all four |
| **Achievement notice** | `tests/scenes/achievement-toast.toml` already captures frames 498 and 560 with a "toast" crop |
| **Settings page in the game** | No scene opens it today; add a scene that presses F4 (or the PC SETTINGS row from `main-menu-rows.toml`) and captures; it is ImGui drawn over the game, so `--ts4_dump_presented` should include it **[unverified]** |
| **Launcher screenshots** (home, setup, Settings, Achievements) | No harness here. WWE All Stars has `launcher/tools/Launcher.Screenshots` (Avalonia headless, `MainWindow` at 100 % and 200 %; WWE research note §D), same stack, so it can be copied under ADR-0004. Without disc key art the home screen falls back to its plain look |
| **Short match video** (20-30 s, a rally and a replay at 60 fps cutscenes) | Nothing in the repo records video. Two routes: the owner records with Radeon ReLive or OBS as for the SVR2011 and WWE videos; or `--ts4_dump_frames` every frame of a scene run, then encode with ffmpeg. ffmpeg is not installed as a binary here, so that route needs the owner's agreement to install it. H.264 MP4 under 10 MB for a user-attachment |
| **Social preview** 1280x640 | Crop of the hero shot, uploaded in repo settings |

## 5. The plan

### Two files, not one

- **`README.md`**: the GitHub page. HTML for layout, images, links into `docs/`.
- **`HELP.md`**: today's `## Help` section moved out unchanged, plain Markdown with no HTML and no
  images, so it reads in Notepad. Its first line says what it is ("Top Spin 4 Recomp: help for
  players. The project page is https://github.com/<owner>/Top-Spin-4-Recomp.").
- Code that changes with the split: `HelpDocument.FileName` (`launcher/src/Launcher.Core/HelpDocument.cs`),
  the shipped file list at `scripts/package.ps1:26` (`'README.md'` becomes `'HELP.md'`, or both),
  `launcher/tests/Launcher.Tests/HomeModelTests.cs:102-106`, the SetupPage comment "opens the
  README's help section" (`SetupPage.axaml.cs:41`), and the CHANGELOG line about Help opening the
  README. The README's Help links (`[HELP.md](HELP.md)`) replace the section.
- Fallback if the owner wants one file: keep `## Help` as the first section after a plain-text
  intro and put every image below it. **[inferred]** Worse: Notepad still shows the HTML below, and
  a GitHub visitor meets the help text before any picture.

### Where images live

- Recommended: `docs/images/` in the repo, JPEG q85 at 1920 wide (photos) and PNG for 200 % crops,
  each under 512 KiB, after the owner records the ADR decision (§3). Relative paths, so forks and
  branches keep working, and the images are versioned with the README.
- Alternative: user-attachments for every game image (no ADR change, up to 10 MB each), and only
  Launcher screenshots in `docs/images/`.
- The video is a user-attachment in either case (a committed `.mp4` does not play).

### Section by section

Target length 200-280 lines, a third shorter than WWE's 433: this port has no online, mods, DLC or
keyboard table yet.

1. **Banner.** `<p align="center"><img src="docs/images/banner.jpg" alt="Top Spin 4 Recomp: an
   unofficial PC port of the Xbox 360 game" width="100%"></p>`. A wide crop of the hero match frame
   with the project name set in the Launcher's typeface. Photographic, so no `<picture>` variant.
   No H1 below it (the banner carries the name), as in WWE.

2. **Pitch**, two short paragraphs, in the current README's voice:

   > An unofficial PC port of the Xbox 360 game Top Spin 4 (2011), built by recompiling the game's
   > own code. It is not an emulator: the game runs as a Windows program, and the Launcher copies
   > the game files from your disc once.
   >
   > **This repository and its releases contain no game files.** You need your own Top Spin 4 disc
   > image and Title Update 1, both from your own console.

3. **Link row**: `**[Download the latest release](../../releases/latest)** · [What works](#what-works)
   · [Play it](#play-it) · [Help](HELP.md) · [FAQ](#faq) · [Build from source](#build-from-source)`.

4. **Video.** The 20-30 s match clip as a user-attachment URL on its own line, with a `<sub>`
   caption ("An Exhibition match on the Enhanced preset, Windows 11, RX 7900 XT"). Until it exists,
   an HTML comment placeholder as in WWE. Fallback: a thumbnail linking to YouTube.

5. **What works.** A two-column table, as WWE: Tested and playable (Exhibition, Career, Academy,
   King of the Court, with saves; local multiplayer if tested), Tested hardware, Not tested yet
   (NVIDIA, Intel, Windows 10, Steam Deck until the owner's pass), Not supported (online: Player
   Match and World Tour show "unavailable", from `docs/goals.md`). One sentence: "The game runs at
   its original 60 frames per second." Then the 2x2 grid: match, player select, Player Creator,
   Academy, each `<td width="50%">` with descriptive alt text.

6. **What the PC version adds**, one `###` each, two to four sentences:
   - *Two renderers* (Native recommended, Classic for compatibility; ADR-0005).
   - *Graphics presets* with a table: Original (the Xbox 360 look, Classic), Default (what a fresh
     install starts on), Enhanced (adds 4x MSAA). Then **"Next to the Xbox 360"**: a two-column
     `<table>`, `<th>` "Original (the Xbox 360's 720p)" and "Enhanced", a 200 % crop row above the
     full frame, and a `<sub>` paragraph on how it was made ("The same guest frame of one scripted
     Exhibition match, drawn by both renderers in one run ...").
   - *60 fps cutscenes* (match intro, replays and changeovers at 60, original speed; off by
     default). The video carries this better than a still.
   - *Achievements* (the notice in the game, the Launcher page), with the toast crop.
   - *Settings in the game and in the Launcher* (PC SETTINGS row, F4, Back+Start; remapping).
   - *Your gamertag* (one line).
   - *A Launcher that sets everything up*: 2x2 Launcher grid (home, setup, Settings, Achievements),
     plus Safe start and Create shortcut in a sentence each.

7. **Play it.** "You need" list (Windows 10/11 64-bit, a Vulkan GPU, about 6 GB, the disc with
   media ID `60819732`, Title Update 1), five numbered steps (download, extract to a writable
   folder not Program Files, open the Launcher and pass SmartScreen, pick the `.iso` and TU1, Play),
   and "Dumping your disc and finding Title Update 1: see [HELP.md](HELP.md)". One `> [!NOTE]` at
   most, for the SmartScreen/Defender warning (`docs/research/defender-false-positive.md`).

8. **If something is off.** A short table: will not start (Safe start), crash (Open crash report),
   wrong graphics (Classic), where the logs are. Each row points to HELP.md for detail.

9. **FAQ.** Bold question, plain answer, in WWE's form: Is this an emulator? Is this piracy? How is
   it different from Xenia? Why Title Update 1? Steam Deck? Was AI used? (WWE's honest answer,
   reworded; the recomp community is sensitive to this, WWE research note §B).

10. **Build from source.** Requirements list and one PowerShell block (`bootstrap.ps1`, Codegen,
    Build, Launcher, `package.ps1`), the accepted XEX and TU1 hashes, and a `<details>` for
    developer extras (scene runner, smoke, Check).

11. **How it works.** Five numbered lines as in WWE: disc read, ReXGlue codegen of the three TU1
    Modules, runtime from Xenia, Native renderer and XenosRecomp, `patches/`. Link `docs/adr/` and
    `docs/research/`.

12. **Credits.** ReXGlue, Xenia, Xenia Edge (the Oracle), XenosRecomp, the WWE All Stars port
    (ADR-0004), topspin4-decomp if code or findings were used, fonts.

13. **Legal.** "This project is not affiliated with or endorsed by 2K, Take-Two Interactive, 2K
    Czech or Microsoft." (rights holders to confirm, §3). No game code or data; do not share
    `gamedata`, `updatedata` or disc images. The screenshot sentence for whichever image route is
    chosen. Licence.

### Style

The current README is the model. Two lines that set the tone:

> It contains no game data: you supply your own disc and Title Update 1, and the Launcher prepares
> them once.

> The Launcher never changes your disc image or update file.

Short declarative sentences, the reader as "you", facts with numbers ("about 6 GB", "up to 15
letters"), no adjectives doing the selling. Avoid: "blazing fast", "seamless", "unleash", emoji
bullets or table icons (Kaiko), em-dash chains, three-item padding, "Experience ...". Captions say
what the picture is and how it was made, not how it feels.

## 6. Open questions for the owner

1. **Split Help out?** Ship `HELP.md` for the Launcher and make `README.md` the GitHub page (§5),
   or keep one file?
2. **Images in the repo or as user-attachments?** If in the repo, record it as an ADR-0006
   amendment or ADR-0012, and keep each file under `repo_guard.py`'s 512 KiB.
3. **Who holds Top Spin 4's rights today**, for the Legal line, and does that change whether real
   players and Australian Open branding appear in the README?
4. **Comparison labels:** Original vs Enhanced (the shipped presets, needs a re-run), or reuse the
   existing Original vs "Resolution scale 4x, MSAA 8x" set as is?
5. **Video:** will you record the clip (ReLive or OBS), or install ffmpeg to encode a scene run's
   dumped frames? Which moment: a rally, or a 60 fps replay?
6. **Launcher shots:** copy WWE's `Launcher.Screenshots` harness here (ADR-0004), and show the home
   screen with or without disc key art?
7. **Banner:** a cropped game frame with the name set in the Launcher's type, or a text-only banner
   with no game art?
8. **"Was AI used?"** in the FAQ, as in WWE?

## Sources

- Comparable READMEs (raw, 2026-11-01):
  [WWE All Stars](https://raw.githubusercontent.com/shaneomac1337/WWE-AllStars-Recomp/main/README.md),
  [Kaiko SVR2011](https://raw.githubusercontent.com/KaikoClanworth1/wwe-svr2011-pc/main/README.md),
  [Unleashed](https://raw.githubusercontent.com/hedge-dev/UnleashedRecomp/main/README.md),
  [Marathon](https://raw.githubusercontent.com/sonicnext-dev/MarathonRecomp/main/README.md),
  [Zelda 64](https://raw.githubusercontent.com/Zelda64Recomp/Zelda64Recomp/dev/README.md),
  [ReXGlue SDK](https://raw.githubusercontent.com/rexglue/rexglue-sdk/main/README.md); SVR2011-Recomp
  read from the local checkout `D:\Projects\SVR2011-RECOMP\README.md`.
- GitHub: [basic writing and formatting](https://docs.github.com/en/get-started/writing-on-github/getting-started-with-writing-and-formatting-on-github/basic-writing-and-formatting-syntax),
  [quickstart for writing](https://docs.github.com/en/get-started/writing-on-github/getting-started-with-writing-and-formatting-on-github/quickstart-for-writing-on-github),
  [collapsed sections](https://docs.github.com/en/get-started/writing-on-github/working-with-advanced-formatting/organizing-information-with-collapsed-sections),
  [attaching files](https://docs.github.com/en/get-started/writing-on-github/working-with-advanced-formatting/attaching-files),
  [about READMEs](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-readmes),
  [about large files](https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-large-files-on-github),
  [social media preview](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/customizing-your-repositorys-social-media-preview),
  [github/markup](https://github.com/github/markup) (sanitization).
- Secondary: [community discussion 133813](https://github.com/orgs/community/discussions/133813),
  [discussion 173635](https://github.com/orgs/community/discussions/173635) (video in READMEs).
- Earlier survey: `D:\Projects\WWE_ALLSTARS-RECOMP\docs\research\github-and-reddit-launch.md` §B-D.
- PCGamingWiki: not checked. Top Spin 4 had no PC release, and nothing here is a PC fix question.
