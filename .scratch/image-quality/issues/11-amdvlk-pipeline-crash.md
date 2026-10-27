# 11: Driver crash building a pipeline after the 8x alpha-to-mask change

**What to build:** Find out whether the access violation in `amdvlk64` while building a pipeline (`analysis/scenes/iq08/exhibition-match-msaa8-b-crash/native-1/runtime.log`, `pipelines.cpp:396`) can recur. It happened on the first form of XenosRecomp patch 0009, in a run at the Original MSAA setting. Every shader in the pack carries the 8x branch, so a crash there would hit every Native player. After 0009 was rewritten without a local array, one clean rerun did not crash, which does not rule out a driver flake. Spec: `../spec.md`.

**Blocked by:** None

**Status:** needs-triage

- [ ] The shader whose pipeline crashed is identified (pipeline cache key or the last shader hash logged before the crash)
- [ ] That shader's pipeline is built at 1x, 2x, 4x and 8x without a crash, several times, on the AMD driver
- [ ] If it still crashes, the 8x case is moved out of the shared path (a specialization constant) or reported upstream

## Comments

- 2026-10-27 (Claude): opened from the QA of 70a727b (ticket 08 fixes).
