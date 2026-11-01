---
status: accepted
---

# The README shows screenshots of the port, kept in docs/images

The GitHub README shows what the port looks like: a banner, a few screenshots, an Original next to Enhanced comparison and the Launcher. These images live in `docs/images/`, so they are versioned with the README and work on forks and branches.

ADR-0006 still holds: no game data is committed. A screenshot is a frame the port drew, scaled and compressed, not a file from the disc, and nothing can be rebuilt from it. Only README images go in `docs/images/`; textures, disc art and any file unpacked from the game stay out. Each image is a JPEG or PNG under the pre-commit guard's 512 KiB limit (`scripts/repo_guard.py`), so the folder stays small.
