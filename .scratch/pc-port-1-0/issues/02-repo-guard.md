# 02: Repo guard against game data

**What to build:** Committing game data, extracted or decrypted executables, or generated C++ becomes impossible by accident (ADR-0006).

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [x] A pre-commit check rejects files matching game-derived patterns (.xex, .xexp, .arc, .bik, .iso, STFS packages, decrypted PE images, generated sources)
- [x] The check also rejects large binary files in general
- [x] The check is installed by bootstrap
- [x] A test commit of a dummy forbidden file is rejected, and normal source commits pass

## Comments

- 2026-10-24 (Claude): Done in `bd9e7ad`. `.githooks/pre-commit` runs `scripts/repo_guard.py`; bootstrap sets `core.hooksPath`. Tests: `python -m pytest tests/test_repo_guard.py`. `.bin` is rejected along with the listed types, because the disc's `Boot.bin`, `Layoutall.bin` and `CoreTech_Xenon.bin` are game data.
