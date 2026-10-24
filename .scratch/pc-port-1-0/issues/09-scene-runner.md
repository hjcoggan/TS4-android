# 09: Scene runner and boot-to-main-menu scene

**What to build:** Scenes (scripted input from boot plus guest frames to capture) play headless, dump frames, exit on their own, and write a session report.

**Blocked by:** 08 (Title screen on Classic, no server wait)

**Status:** ready-for-agent

- [ ] The WWE scene runner, input scripting and frame-dump / exit-after-frame support are copied and adapted
- [ ] A boot-to-main-menu scene reaches the main menu and captures it
- [ ] Running the scene twice gives the same captures
- [ ] Runs never overlap
- [ ] The main-menu capture matches the Oracle reference
