# Build the XenosRecomp shader converter (re:Blue fork, MIT) from .tools/xenosrecomp. Copied
# from WWE All Stars (ADR-0004).
# It turns the game's Xenos shader containers into HLSL, then SPIR-V (and DXIL).
param(
    [string]$VisualStudioRoot = 'C:/Program Files (x86)/Microsoft Visual Studio/18/BuildTools',
    [string]$WindowsSdkRoot = 'C:/Program Files (x86)/Windows Kits/10',
    # Another directory keeps a second build, e.g. a baseline for
    # scripts/shader_regen_diff.py.
    [string]$BuildDirectory = 'out/build/xenosrecomp',
    [ValidateRange(1, 64)][int]$Jobs = 4,
    # RelWithDebInfo gives a debugger symbols, for a conversion that crashes.
    [ValidateSet('Release', 'RelWithDebInfo')][string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Invoke-Hidden.ps1"
$projectRoot = Split-Path $PSScriptRoot -Parent
$source = "$projectRoot/.tools/xenosrecomp"
if (!(Test-Path "$source/XenosRecomp/main.cpp")) {
    throw 'Fetch it first: pwsh scripts/bootstrap.ps1'
}
# SVR2011-Recomp's mode (32 sampler slots, COLOR0-7 interpolators, Xenos boolean file) plus
# WWE All Stars' fixes, from the numbered series in patches/xenosrecomp.
& "$PSScriptRoot/apply-patches.ps1" | Out-Null
$llvm = "$projectRoot/.tools/llvm-21.1.8/bin"
$cmakeRoot = "$VisualStudioRoot/Common7/IDE/CommonExtensions/Microsoft/CMake"
$cmake = "$cmakeRoot/CMake/bin/cmake.exe"
$msvc = Get-ChildItem "$VisualStudioRoot/VC/Tools/MSVC" -Directory |
    Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$windowsVersion = Get-ChildItem "$WindowsSdkRoot/Include" -Directory |
    Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$sdkInclude = "$WindowsSdkRoot/Include/$($windowsVersion.Name)"
$sdkLib = "$WindowsSdkRoot/Lib/$($windowsVersion.Name)"
$env:PATH = "$llvm;$cmakeRoot/Ninja;$($msvc.FullName)/bin/Hostx64/x64;$WindowsSdkRoot/bin/$($windowsVersion.Name)/x64;$env:PATH"
$env:INCLUDE = "$($msvc.FullName)/include;$sdkInclude/ucrt;$sdkInclude/shared;$sdkInclude/um;$sdkInclude/winrt"
$env:LIB = "$($msvc.FullName)/lib/x64;$sdkLib/ucrt/x64;$sdkLib/um/x64"
$build = if ([IO.Path]::IsPathRooted($BuildDirectory)) { $BuildDirectory } else { "$projectRoot/$BuildDirectory" }
# Version information (scripts/xenosrecomp/XenosRecomp.rc): without it Windows
# Defender's machine-learning model took the unsigned exe for a trojan and deleted it. Linked in
# here, not patched in, so the converter id stays the patch series' hash.
New-Item -ItemType Directory -Force $build | Out-Null
$resource = "$build/XenosRecomp-version.res"
& rc.exe /nologo /fo $resource "$PSScriptRoot/xenosrecomp/XenosRecomp.rc"
if ($LASTEXITCODE -ne 0) { throw "rc.exe failed on scripts/xenosrecomp/XenosRecomp.rc (exit $LASTEXITCODE)" }
Invoke-Hidden $cmake @('-S', $source, '-B', $build, '-G', 'Ninja', "-DCMAKE_BUILD_TYPE=$Configuration",
    '-DCMAKE_C_COMPILER=clang', '-DCMAKE_CXX_COMPILER=clang++', '-DCMAKE_CXX_FLAGS=-DSVR2011_RECOMP', '-DXENOS_RECOMP_DXIL=OFF',
    "-DCMAKE_EXE_LINKER_FLAGS=$($resource -replace '\\', '/')") -LogPath "$projectRoot/analysis/configure-xenosrecomp.log"
Invoke-Hidden $cmake @('--build', $build, '--parallel', "$Jobs") -LogPath "$projectRoot/analysis/build-xenosrecomp.log"
Write-Output "XenosRecomp built: $build/XenosRecomp/XenosRecomp.exe"
