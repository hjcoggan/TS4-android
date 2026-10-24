# 06: Loader loads and runs

**What to build:** Loader is built as its own host library, Boot loads it through ReXGlue's module path, and Loader's code runs.

**Blocked by:** 05 (Boot runs)

**Status:** ready-for-agent

- [ ] Loader is a module entry in the manifest, compiled to a separate host library
- [ ] Boot's load of Loader succeeds, and procedure lookup on Loader returns working addresses
- [ ] Loader's image is writable where the game expects it to be
- [ ] Any ReXGlue fixes needed are numbered patches in the repo
- [ ] The smoke run shows Loader running its boot sequence, up to its load of Swing
- [ ] Changing only Swing or Boot does not rebuild Loader
