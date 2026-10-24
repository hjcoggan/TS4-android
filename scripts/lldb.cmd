@echo off
rem Runs the pinned lldb (.tools\llvm-21.1.8) with the Python it was built against: the embeddable
rem Python 3.10.11 in .tools\python-3.10.11-embed (scripts\bootstrap.ps1). Without python310.dll on
rem PATH lldb does not start at all. A .cmd, not a .ps1, so arguments such as --x=D:/y reach lldb
rem untouched. Arguments go to lldb as given, e.g.
rem   scripts\lldb.cmd -b -o run -k bt -- out\build\win-amd64-relwithdebinfo\topspin4.exe ^<flags^>
setlocal
set "ROOT=%~dp0.."
if not exist "%ROOT%\.tools\python-3.10.11-embed\python310.dll" (
  echo Missing .tools\python-3.10.11-embed ^(run scripts\bootstrap.ps1^) 1>&2
  exit /b 1
)
set "PATH=%ROOT%\.tools\python-3.10.11-embed;%PATH%"
set "PYTHONHOME=%ROOT%\.tools\python-3.10.11-embed"
rem The default PDB reader needs Microsoft's DIA, which is not installed; without the native one
rem the generated code shows only as offsets from a DLL's exports.
set "LLDB_USE_NATIVE_PDB_READER=1"
"%ROOT%\.tools\llvm-21.1.8\bin\lldb.exe" %*
