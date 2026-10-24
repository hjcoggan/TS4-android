# 02: Repo guard against game data

**What to build:** Committing game data, extracted or decrypted executables, or generated C++ becomes impossible by accident (ADR-0006).

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] A pre-commit check rejects files matching game-derived patterns (.xex, .xexp, .arc, .bik, .iso, STFS packages, decrypted PE images, generated sources)
- [ ] The check also rejects large binary files in general
- [ ] The check is installed by bootstrap
- [ ] A test commit of a dummy forbidden file is rejected, and normal source commits pass
