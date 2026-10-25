# Oracle Baseline

Recorded 2026-10-25 (ticket `.scratch/pc-port-1-0/issues/01-oracle-baseline.md`, milestone M0). This is how the Reference build behaves on the Oracle. The port is measured against it, and a divergence is classed as a port bug, an Oracle bug or an Original bug ([GLOSSARY.md](../../GLOSSARY.md)). The runs, screenshots and captures named here are kept locally under `analysis/` and are not committed (ADR-0006).

## Setup

- **Oracle:** Xenia Edge `edge@4d0720457` (Oct 24 2026), portable, in `.tools/xenia-edge`.
- **Game:** disc title `54540859` with TU1 installed through Edge's Tools > Install Content. The title bar reads `v0.0.1.3`, and the log reads `XEX patch applied successfully: base version: 0.0.0.3, new version: 0.0.1.3`.
- **Edge settings:** the shipped config. D3D12 with RTV/DSV render targets, XAudio2, `draw_resolution_scale = 1`, `framerate_limit = 0`, `guest_display_refresh_cap = true`, `async_shader_compilation = true`. The resolution capture used Vulkan, since RenderDoc doesn't capture Edge's D3D12.
- **Tooling:** `scripts/edge.ps1` runs Edge unattended. It plays input scripts on a ViGEm virtual pad, or with `-NoPad` the owner plays on their own controller. It takes window screenshots (`-ShotsAt`, `-ShotEvery`), a RenderDoc capture (`-RenderDocAt`) and the guest frame rate (`-FrameLog`, through `scripts/edge_fps.py`).

## Sessions

| Run | What | Kept in |
|---|---|---|
| Owner, session 1 | Exhibition singles, Federer v Nadal (CPU, Normal), Rod Laver Arena, 1 set, games per set TIE-BREAK. A full match (Nadal 7-0), its End of Match menu, and a rematch (Nadal 7-1). A screenshot every second for 685 of 691 s. | `analysis/edge-baseline-1/` |
| Owner, session 2 | The same match setup, with `-FrameLog`: 680 s, a screenshot every second, `fps.csv`. | `analysis/edge-20261025-101451/` |
| Scripted | Boot to the practice ground, plus a Vulkan RenderDoc capture at 40 s. | `analysis/edge-tooltest/` |
| Scripted | Boot to the main menu. | `analysis/edge-menu/` |
| Scripted | Two 45 s `-FrameLog` checks of the measuring tool. | `analysis/edge-fps-tooltest*/` |

## Match completion

A full Exhibition match against the CPU completes. In session 1, the tie-break ran from the first serve at 75 s to Nadal winning 7-0 at about 179 s. The best-point replay followed (181-186 s), then the celebration and the End of Match menu (Statistics / Rematch / Exit) at 189 s. Rematch started a second match at 197 s.

## Resolution

Swing renders at **1280x720 (16:9)**. The viewport is 1280x720, drawn in two predicated tiles: scissor 1280x384 at row 0, then 1280x336 at row 384. The target is a 1280x1024 EDRAM-shaped 2x MSAA surface. The front buffer is 1280x720. Source: the RenderDoc capture of the practice ground.

## Frame rate

The rate is measured as guest VdSwap calls per wall-clock second, not as host presents. Over the 680 s of session 2 there were 37131 swaps, against an Edge frame counter rise of exactly 37131.

| | Seconds | Share |
|---|---|---|
| 60 fps (58.5 and up) | 530 | 78% |
| 30 fps (25-35) | 100 | 15% |
| In between | 49 | 7% |

**The Reference build drops to 30 fps between points, but only in its cutaways.** Menus and rallies run at 60. Between points the rate stays at 60 while the gameplay camera is up (for example "DOUBLE FAULT" at 148 s, 59.5 fps). It drops to a flat 30 for:

- the match intro and name card (44-58 s);
- player close-ups between points (136, 150-152, 188, 248-250, 285-288, 405-413, 523-524 s);
- the replay wipe and replays (336-350, 614-618, 666-679 s);
- the changeover on the bench (425-448 s).

Single seconds at 30 on the gameplay camera (262 s) sit at the cut back from a close-up. The "in between" seconds straddle a switch or a load. The 30 fps stretches hold within ±1 fps for up to 24 s while rallies hold 60, so this is the game presenting every other vblank in its cinematics, not Edge falling behind.

**[inferred]** The simulation is tied to frame rate (docs/goals.md), so the port's locked 60 (ticket 16) has to keep these 30 fps cutaways rather than run them at 60.

A host-side hitch is also on record. In one scripted run, Edge stalled to 29 and then 2 fps at 42-43 s on the idle practice ground, and briefly dipped to 48-54 fps at 29-32 s. The same seconds were clean in the run before it. This is unexplained, and it's Edge's, not the game's.

## Load times

Measured at 1 s resolution from Edge's launch in session 1, with the owner pressing START:

| From | To | Time |
|---|---|---|
| Launch | Title screen (Press START) | 10 s |
| START on the title (11 s) | Practice ground (22 s; "Discovering downloadable content" at 20 s) | 11 s |
| Practice ground | Main menu | 2 s (24 s) |
| Continue on game settings (58 s) | Match intro | 8 s (66 s) |
| Rematch on End of Match (190 s) | Play | 7 s (197 s) |

Under scripted START presses that skip the intro videos, boot to title is 8-9 s and title to practice ground is about 15 s. The port reaches the title by frame ~350 (about 6 s) with its own intro skip (ticket 08).

## Glitches

**No glitch was seen during play.** The owner watched both sessions and reports that the match looked good on Edge. A sweep of the 1,365 screenshots found nothing during play. It measured the brightness of every frame, checked each one that stood out from its neighbours, and looked over every 4th second of both sessions. No geometry spikes, missing or garbage textures, z-fighting or camera clipping turned up. With one screenshot a second, a flicker of a frame or two can still go unseen. Ticket 20 compares cross-fades frame by frame.

| Session | Second | What | Class |
|---|---|---|---|
| 2 | 10 | The whole frame is white while the boot loading screen switches from its first backdrop (9 s) to the second (11 s). Session 1 shows normal loading screens at 10-13 s. | Unclassified: seen once. Compare at the same point in the port; if the port shows it too, it's an Original bug. |

Things that look like glitches but are the game's own behaviour, so they shouldn't be reported against the port:

- The intro name card fades in from an empty grey box.
- Dark, empty or faintly labelled boxes fade in and out, for call labels and the "serving for match" panel.
- The "Champions" tunnel shot is greyscale (the same shot in both matches).
- Black and dimmed frames come from deliberate fades and the pause menu.
- The 30 fps cutaways (see Frame rate).

## Reference captures

Kept locally, not committed:

- Title screen: `analysis/edge-baseline-1/ref-010.png`
- Main menu: `analysis/edge-menu/shot-043s.png`
- Mid-match: `analysis/edge-baseline-1/ref-300.png`
- Practice ground, as a RenderDoc capture: `analysis/edge-tooltest/`
