# Shaders and pipelines prepared at setup

Status: ready-for-agent

Owner decision (2026-10-31): the Launcher's first-run setup prepares every shader and pipeline in advance, so a fresh install never converts a shader during play. Test seams agreed with the owner the same day (see Testing Decisions).

## Problem Statement

On the Native renderer, a shader the shader pack lacks is converted while the game runs ("live top-up"). Conversions run one at a time and take 50-640 ms each, and every draw that needs a shader still being converted is skipped. The owner met this in an arena the install had never shown: 17 new shaders took about 4.5 s to convert, 63-80 draws a frame were skipped, and the arena's surfaces were missing or solid black until each shader arrived. Converted shaders join the pack at the next start, so the same place is fine the second time.

The pack is built locally from shaders our own runs captured, and it can never ship with the port because it is derived from game data (ADR-0006). A fresh install therefore starts with no pack at all: every screen, the title screen included, shows this on its first visit, and every new place stutters while its pipelines compile for the first time.

## Solution

The Launcher's first-run setup gains two steps after the disc and Title Update 1 are prepared:

- **Prepare shaders.** The Launcher reads every shader container the game can create straight from the prepared game files, converts them all with XenosRecomp (shipped beside the Launcher) and writes the complete shader pack into the cache folder. A reverse-engineering pass (2026-10-31) found the set is closed: the disc's `.arc` archives hold 1007 unique containers (643 pixel, 364 vertex) covering every arena, each as its own `<hash>-<perm>.vso` / `.pso` entry passed to Create*Shader unchanged; about 90 more sit in the read-only data of the Loader and Swing Modules; 2 are written word by word by one Swing function. All 493 containers our runs ever dumped were found, 491 of them byte for byte in the archives and Modules.
- **Prepare pipelines.** The Launcher runs the game's existing headless prepare-pipelines mode, which builds the pack's shader modules and the planned pipelines into the pipeline store and the driver cache.

Both steps show in setup's one progress bar with the time left. They run once; afterwards Play starts straight away. A pack that becomes stale (a new XenosRecomp patch series) or a pipeline store the game marks stale is rebuilt by the Launcher before the next Play, without a click. Live top-ups stay as the safety net for anything the pack still lacks, and now convert several shaders at once, so a miss catches up in about a second instead of several.

## User Stories

1. As a player on a fresh install, I want every arena to show its real surfaces the first time I play there, so that a new arena never looks broken.
2. As a player on a fresh install, I want the title screen and menus to draw completely on the first start, so that the game looks finished from the first second.
3. As a player, I want the shader work done during setup, so that I wait once with a progress bar instead of meeting gaps and stutters during play.
4. As a player, I want setup's single progress bar to include the shader and pipeline steps, so that I know how long setup still takes.
5. As a player, I want the progress bar to say which step it is in ("Preparing shaders", "Preparing pipelines"), so that a long step does not look stuck.
6. As a player, I want to cancel setup during the shader or pipeline step and resume later, so that I am not forced to wait at a bad moment.
7. As a player who resumes setup, I want the steps already finished to be skipped, so that resuming does not redo work.
8. As a player, I want a clear message when the shader step fails (for example XenosRecomp is missing or a game file is damaged), so that I know what went wrong and that the game still plays on the Classic renderer.
9. As a player whose GPU or driver changed, I want the Launcher to rebuild pipelines before the next Play without asking, so that the change never costs me stutter.
10. As a player who updates the port to a build with another shader converter, I want the Launcher to rebuild the shader pack before the next Play, so that Native keeps working instead of falling back to Classic.
11. As a player, I want shaders the game still meets during play (ones the pack lacks) to be folded into the pack by the Launcher after the session, so that they are never converted live twice.
12. As a player who meets a shader the pack lacks, I want it converted within about a second, so that a rare miss is hardly visible.
13. As a player on Classic, I want setup's shader steps not to change anything I see, so that the reference renderer stays the reference.
14. As a player with a small disk, I want setup's space check to count the shader pack and pipeline cache, so that setup does not run out of space part way.
15. As a player starting the game from a Direct shortcut, I want the prepared pack to be used the same way, so that the shortcut start is as complete as a Launcher start.
16. As a player who moved the prepared game files elsewhere, I want the shader step to read them from where they now are, so that moving files does not break setup.
17. As a player using the command-line setup tool, I want the same shader and pipeline steps with text progress, so that a scripted or Steam Deck install gets the full pack too.
18. As a player, I want the Launcher's Repair to rebuild the shader pack and pipelines, so that a damaged cache can be fixed with one button.
19. As a developer, I want the shader reader to work on the player's prepared files only and never write game data into the repo, so that ADR-0006 holds.
20. As a developer, I want a fresh-install end-to-end run that proves zero skipped draws for missing shaders and zero live conversions from boot to a match, so that coverage is measured, not assumed.
21. As a developer, I want the archive reader tested on small synthetic archives, so that its parsing and error paths are covered without game data.
22. As a developer, I want the shader preparation tested with a fake converter, so that progress, cancellation and failure messages are covered without running XenosRecomp.
23. As a developer, I want the pipeline step tested with a fake process runner, so that the reading of the game's prepare output is covered without a GPU.
24. As a developer, I want the shader set's sources documented (archives, Module data, the two built shaders), so that the next person can find a shader that goes missing.
25. As a developer, I want the containers from Module data to come from the Modules as the game runs them (Title Update 1 applied), so that the pack's hashes match what the game creates.
26. As the owner, I want the Launcher to reuse WWE All Stars' shader and pipeline preparation where it fits (ADR-0004), so that proven code is not written twice.

## Implementation Decisions

- **Shader reader (new, Launcher.Core).** Reads the prepared game folder's `.arc` archives with the layout recorded in the game-settings research notes: PAM-LZX entries (a 0x2C-byte header, then frames of `FF u16 uncompressed u16 compressed` or `u16 compressed`), LZX with a 2^20 window and 0x8000-byte frames; `CutScenes.arc` entries are stored uncompressed. Every single-part entry is decoded and kept when it starts with the container magic (big-endian `0x102A1100` pixel, `0x102A1101` vertex) and its header size plus microcode size equals the entry's length. Entries are matched on the magic, not on names, because the archives' name tables are incomplete. Multi-part entries (all over 1 MB) are skipped; no shader is multi-part. Containers are named as the game's dump names them (`ps_`/`vs_` plus the FNV-1a-64 of the bytes) and deduplicated. A damaged archive fails with a message that names it.
- **LZX decoding** uses the managed LZX decoder the Launcher already vendors for the Disc art's XEX reading. No new dependency. If it cannot decode the archives' frames, libmspack's `lzxd` is the fallback, and that needs the owner's approval as a new dependency first.
- **Module shaders (new, game side).** A headless mode of the game exe, alongside the existing prepare-pipelines mode: it loads Loader and Swing the way play does (Title Update 1 applied), scans their read-only data for valid containers (the same magic and size rule) and writes each into the shader top-up folder under its dump name, then exits with a result line. The two containers Swing writes word by word (the reverse-engineering pass names them `vs_5780e914afa75799` and `ps_0c62d19530927dac`) are left to live top-up; they are tiny and convert in about 50 ms.
- **Shader preparation (copied from WWE, adapted).** WWE's converter interface, the XenosRecomp converter that runs the copy shipped beside the Launcher, and the pack file builder and checker. The pack's layout stays owned by the game's shader pack code; the Launcher writes the same bytes the developer script does. The converter id is generated at build time from the XenosRecomp patch series, as the game computes it, so the game accepts the pack.
- **Pipeline preparation (copied from WWE, adapted).** Runs the game's `--ts4_prepare_pipelines` mode through a process-runner interface and reads its progress and result lines (the game's prepare output format: header, `progress` lines, one `result` line; exit 0 ok, 3 partial, 2 failed). After a finished prepare the Launcher records what it was built from (the pack, the top-ups, the update folder); a change to any of them, a missing store, or the game's "pipelines stale" marker means a rebuild before the next Play, without a click. The header word is renamed from WWE's to Top Spin 4's.
- **Setup flow.** The disc and TU1 bands are followed by a shaders band and a pipelines band in setup's overall progress. "Prepared" now means: disc, TU1, a usable pack, and a pipeline store that is not stale. A setup stopped during either step resumes there. A shader step failure is reported and leaves Play available on Classic (Native falls back as it does today without a pack); a pipeline step failure leaves Play available, and the pipelines build during play as they do today.
- **Top-up folding.** After a session ends, the Launcher converts what the game left in the top-up folder and adds it to the pack, so a shader is converted live at most once per install (WWE's owner decision of 2026-10-04, kept).
- **Parallel live conversion (game side).** The game's live top-up converter runs several conversions at once, bounded by the thread budget, instead of one at a time.
- **Command-line setup tool.** It runs the same two steps with text progress.
- **Space check.** The space needed adds the pack and the pipeline cache to what the disc and TU1 move.
- **Glossary.** "Shader pack" and "Top-up" get glossary entries: the pack is the converted shaders Native loads at start; a top-up is a shader the game met that the pack lacked.

## Testing Decisions

- **A good test checks behaviour at the seam a player or caller sees**: what the reader returns for an archive, what preparation writes and reports, what the pipeline step decides from the game's output. Not how frames are split internally or which helper ran.
- **End-to-end (the highest seam, agreed with the owner).** On a fresh install folder (no cache): run the command-line setup tool, then a Native scene run from boot to a match. The session report must show zero `no_shader` draw skips, and the log zero live conversions. This is what proves the pack covers what the game uses. The game's Module-shader mode has no unit seam of its own; this run checks it.
- **Shader reader:** unit tests on small synthetic archives built in the test (a compressed entry, an uncompressed entry, a multi-part entry that is skipped, a non-shader entry, a damaged archive), checking the containers, their names and the error message. Prior art: WWE's disc-shader tests.
- **Shader preparation:** unit tests with a fake converter: the pack is written, progress rises to 1, cancellation stops it, and a converter failure gives the player's message. Prior art: WWE's shader preparation and shader pack file tests.
- **Pipeline preparation:** unit tests with a fake process runner fed recorded prepare output: progress, the ok, partial and failed outcomes, and the rebuild decision (stale marker, missing store, changed pack). Prior art: WWE's pipeline output tests.
- **Setup flow:** the existing setup flow tests gain the two bands, resuming at each, and a failure at each. Prior art: the existing setup flow and overall progress tests.
- **Game side:** the parallel live conversion keeps its queue logic pure and covered by the host tests next to the existing live top-up tests.

## Out of Scope

- Converting the shaders at the port's build time and shipping the pack: it is derived from game data (ADR-0006).
- The two shaders Swing builds in code: they stay on live top-up.
- Multi-part archive entries: no shader is in one; decoding them is not needed.
- Classic renderer changes: Classic compiles its own shaders and is untouched.
- Shader or pipeline work for mods or DLC.
- Changing the shader pack's file format.

## Further Notes

- The reverse-engineering pass's scratch tools (an `.arc` reader, the scan, a libmspack build) are throwaway and not committed. Its findings: Swing `0x8874F710` builds archive shader names with `"%x-%llx"` plus `.vso`/`.pso` and passes the read buffer straight to CreateVertexShader `0x88290CE8` / CreatePixelShader `0x88290940`; Module `.rdata` containers are passed by Swing `0x888F3040`, `0x888F3090`, `0x8890E0D8`, `0x887CA188` and Loader `0x83221D48`, `0x833A31B0`; the code-built pair comes from Swing `0x8873B460`. Swing has no HLSL compiler, so the comment in the game's shader top-up code about runtime-compiled shaders came from WWE and does not apply here.
- `TitleUpdate.arc` holds no shaders.
- The game already ships the prepare-pipelines mode, its output format and the top-up folder; the Launcher side is what is missing.
