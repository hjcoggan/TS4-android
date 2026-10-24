# 12: Disc and TU1 preparation

**What to build:** The launcher takes the player's disc image and TU1 package, checks they are the Reference build, prepares the game data and caches it per user.

**Blocked by:** 03 (Skeleton and pinned toolchain bootstrap)

**Status:** ready-for-agent

- [ ] A disc image and TU1 package are accepted as input
- [ ] Media ID, module versions and TU1 package identity are checked, and a wrong disc, missing TU1 or wrong TU each give a clear, distinct error
- [ ] Region-free or security-sector-stripped images with the right game partition are accepted
- [ ] Extraction and TU1 patching produce the prepared data, cached per user
- [ ] Later launches reuse the cache without redoing the work
- [ ] Unit tests cover the validation outcomes, using synthetic headers rather than game data
