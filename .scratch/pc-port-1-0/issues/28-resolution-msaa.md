# 28: Resolution scale and MSAA

**What to build:** Native-only Enhancements for internal resolution scaling and MSAA, defaulting to original behaviour.

**Blocked by:** 27 (Native Exhibition parity)

**Status:** ready-for-agent

- [ ] Internal resolution can be raised and the game stays stable (no device loss)
- [ ] MSAA can be enabled
- [ ] Both are shown as unavailable on Classic
- [ ] Defaults match the original look
- [ ] Scenes at higher resolution show no new artifacts

## Comments
- 2026-10-25 (Claude), status check: unblocked, since ticket 27 is resolved. For the defaults box: Native now creates real 2x MSAA targets where the game asks for them (`f8a55ea`), with the game's alpha-to-mask (`86197b2`), so the default look is the original's. That is the original behaviour, not this ticket's Enhancement: no setting raises resolution or MSAA yet, so no box is ticked.
