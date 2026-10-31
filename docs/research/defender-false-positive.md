# Windows Defender and XenosRecomp

Date: 2026-10-31. Windows Defender deleted `XenosRecomp.exe` as **Trojan:Win32/Bearfoos.B!ml**
(a cloud machine-learning verdict) on dev machines and in the first release candidate. The game
then logged `XenosRecomp did not start (error 225)` (ERROR_VIRUS_INFECTED) for every live shader
conversion. Setup's conversion had already run, so the pack was built; only shaders the pack
lacked waited for the Launcher.

## What triggered it

Tested in a folder Defender does not exclude, with a fresh copy of the binary each time (a
convicted hash is blocked everywhere afterwards, so a test needs a new one; changing one byte of
its version text gives one):

| Who starts XenosRecomp | Result |
|---|---|
| `topspin4.exe` (no version information) | blocked at start, file deleted |
| PowerShell, the game's exact arguments, 5 runs | fine |
| A small test exe with the game's exact `CreateProcess` call (job, attribute list, inherited log handle, no window), 3 runs | fine |
| `topspin4.exe` with version information | fine; both missing shaders converted live |

The verdict depends on the parent: the model rated the unsigned game exe, which carried no
version information, as suspicious, and convicted the program it started. The way the process
was started was not the cause (the test exe used the same call), though the game no longer
starts it suspended (see below).

## Fix

- `src/topspin4.rc`: version information for `topspin4.exe` (CMake builds it into the exe).
- `scripts/xenosrecomp/XenosRecomp.rc`: version information for `XenosRecomp.exe`, linked in by
  `scripts/build-xenosrecomp.ps1`, not patched in, so the converter id does not change.
- `src/native/topup_converter.cpp`: XenosRecomp starts inside the game's job object through
  `PROC_THREAD_ATTRIBUTE_JOB_LIST` instead of start-suspended, assign, resume (the pattern
  process injection uses), and inherits only its log handle.

## Still worth doing

A verdict is per binary and per model version, so a later build can be flagged again. Signing
the release (`topspin4.exe`, the Launcher, `XenosRecomp.exe`) is the lasting fix, and a flagged
build can be submitted as a false positive at microsoft.com/wdsi/filesubmission.
