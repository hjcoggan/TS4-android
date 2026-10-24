# 10: Settings system and renderer switch

**What to build:** Settings are saved per user with original-behaviour defaults, and the renderer can be switched between Classic and Native.

**Blocked by:** 08 (Title screen on Classic, no server wait)

**Status:** ready-for-agent

- [ ] The WWE settings system is copied and adapted
- [ ] Settings live in the per-user directory
- [ ] Every setting defaults to original behaviour
- [ ] A renderer setting selects Classic or Native (Native may still be unavailable at this point)
- [ ] Unit tests cover defaults, persistence, and recovery from a corrupt settings file
