# Third-party code and licences

This repository is a **GPL-3.0** work (see `LICENSE`). It is an Android port built on top of
several other projects; their licences are reproduced or referenced below.

Nothing here contains game data. The game's assets, executables and Title Update are **not**
distributed — you supply your own disc dump (see `docs/dumping.md`).

## ReXGlue SDK — BSD-3-Clause

The runtime, Vulkan UI layer and recompiled-code support come from
[ReXGlue](https://github.com/rexglue/rexglue-sdk), pinned at `f5337cd`, which is itself derived
from [Xenia](https://xenia.jp):

```
Copyright (c) 2026, Tom Clay <tomc@tctechstuff.com>
Portions of this software are derived from the Xenia project:
Copyright (c) 2022, Ben Vanik and Xenia project contributors
All rights reserved.
```

Redistribution and use in source and binary forms, with or without modification, are permitted
provided that the copyright notice, this list of conditions and the disclaimer are retained, and
that neither the copyright holder's nor the contributors' names are used to endorse derived
products. Full text: the SDK's `LICENSE`, fetched by `scripts/prepare-sdk.sh`.

The SDK is **not vendored** here. `scripts/prepare-sdk.sh` clones it and applies:

- the reference port's patch series (46 patches, from
  [shaneomac1337/Top-Spin-4-Recomp](https://github.com/shaneomac1337/Top-Spin-4-Recomp)), and
- this port's Android series (`patches/rexglue-android/`), and
- this port's SDL series (`patches/sdl3/`).

Our SDK patches are derived from the BSD-3-Clause SDK and are BSD-3-Clause as patch content; they
are distributed here inside a GPL-3.0 work.

## Android app layer — GPL-3.0

`android/` is derived from the Android app layer of
[codepdbh/nfsmw-android](https://github.com/codepdbh/nfsmw-android), which is **GPL-3.0**. That is
why this repository as a whole is GPL-3.0 and not a permissive licence: the app shell and the
native code it loads form one distributed program.

## libadrenotools and liblinkernsbypass — BSD-2-Clause

Vendored into the SDK by patch `0027` (custom Vulkan driver loading), from
[bylaws/libadrenotools](https://github.com/bylaws/libadrenotools) and
[bylaws/liblinkernsbypass](https://github.com/bylaws/liblinkernsbypass). Both licence files travel
with the vendored tree in the SDK working copy.

The vendored tree includes `thirdparty/libadrenotools/src/hook/kgsl.h`, a **Linux UAPI header**
marked `SPDX-License-Identifier: GPL-2.0 WITH Linux-syscall-note`. That note exists precisely so
userspace programs can include the header without becoming GPL; it does not affect this project's
licence.

## SDL3 — zlib

Pulled by the SDK as a submodule (`https://github.com/libsdl-org/SDL`, branch `release-3.4.x`).
`patches/sdl3/` contains one change to it; the patch is ours and is GPL-3.0 like the rest of this
repository, while SDL itself stays zlib-licensed.

## Prior art read but not copied

Several other Android recomps were read while writing the SDK patches. The detailed record —
including which file each change came from and under which licence — is in
`patches/rexglue-android/THIRD_PARTY.md`. In summary: only BSD-3-Clause and MIT sources were
adapted; repositories with no licence file, and the GPL-3.0 app layers other than nfsmw's, were
used for ideas only.

## The original PC port — no licence file

The reference project this repository is forked from,
[shaneomac1337/Top-Spin-4-Recomp](https://github.com/shaneomac1337/Top-Spin-4-Recomp), has no
`LICENSE` file. Its code is **not** redistributed here: the build fetches its patch series at
setup time. Credit for the original static-recompilation approach, the recompiler toolchain
wiring and the 46-patch series belongs to that project and its author.

## Tools

- [XenonRecomp](https://github.com/hedge-dev/XenonRecomp) — PowerPC → C++ recompiler
- [XenosRecomp](https://github.com/hedge-dev/XenosRecomp) — Xenos shader translator
- [xxHash](https://github.com/Cyan4973/xxHash), [zstd](https://github.com/facebook/zstd),
  [smol-v](https://github.com/aras-p/smol-v) — vendored in `src/thirdparty/`, under their own
  licences in those directories
