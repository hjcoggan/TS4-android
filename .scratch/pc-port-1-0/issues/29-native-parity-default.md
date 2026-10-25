# 29: Native parity everywhere, made default

**What to build:** Every 1.0 scene passes on Native, and Native becomes the default renderer. This is milestone M5.

**Blocked by:** 27 (Native Exhibition parity), 18 (Academy), 19 (Venue and roster sweep), 20 (Cutscenes and cross-fades), 22 (Career complete), 23 (King of the Court)

**Status:** ready-for-agent

- [ ] Every 1.0 scene passes FLIP against Classic with no fallbacks
- [ ] Native is the default renderer, and Classic is still selectable
- [ ] A second run with a warm pipeline cache is free of hitches

## Comments
- 2026-10-25 (Claude), status check: of the 1.0 scenes, `boot-main-menu`, `exhibition-match`, `career-start`, `academy-lesson` and `king-of-the-court` pass on Native in dual mode with no fallbacks or draw skips (tickets 26, 27). Still blocked by 18, 19, 20, 22 and 23, whose scenes don't exist or don't complete yet, so no box is ticked. Native is not the default (`ts4_renderer` defaults to classic).
