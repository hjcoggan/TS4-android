# No game data in the repository; players supply their own copy

The repository holds only original code, patches, analysis notes and tooling. Game data, extracted or decrypted executables, and ReXGlue's generated C++ are never committed, because the generated code is derived from the game's executable. Players supply their own disc image and TU1, and the build generates everything else from those files. This is the same posture as the WWE ports and topspin4-decomp, and it is why `.iso/`, `gamedata/`, `updatedata/`, `analysis/xex/` and the generated sources are permanently git-ignored.
