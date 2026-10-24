param([string]$SevenZipPath)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Invoke-Hidden.ps1"
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location $projectRoot
New-Item -ItemType Directory -Force .tools | Out-Null
$git = (Get-Command git).Source

# The pre-commit guard against game data and generated code (ADR-0006, .githooks/pre-commit).
$null = Invoke-Hidden $git @('config', 'core.hooksPath', '.githooks')
Write-Output 'Installed the pre-commit guard'

# Toolchains shared with the sister recomps. A junction to an existing copy saves
# ~2 GB; without one, the pinned artifact below is downloaded instead.
$shared = @(
    @{ Name = 'rexglue-0.10.0'; From = 'D:/Projects/SVR2011-RECOMP/.tools/rexglue-0.10.0' },
    @{ Name = 'llvm-21.1.8'; From = 'D:/Projects/SVR2011-RECOMP/.tools/llvm-21.1.8' },
    @{ Name = 'tracy-0.13.1'; From = 'D:/Projects/SVR2011-RECOMP/.tools/tracy-0.13.1' },
    @{ Name = 'fidelityfx-sdk'; From = 'D:/Projects/SVR2011-RECOMP/.tools/fidelityfx-sdk' },
    @{ Name = 'presentmon'; From = 'D:/Projects/WWE_ALLSTARS-RECOMP/.tools/presentmon' },
    @{ Name = 'dotnet-sdk'; From = 'D:/Projects/WWE_ALLSTARS-RECOMP/.tools/dotnet-sdk' }
)
foreach ($link in $shared) {
    $path = Join-Path '.tools' $link.Name
    if (!(Test-Path -LiteralPath $path) -and (Test-Path -LiteralPath $link.From)) {
        New-Item -ItemType Junction -Path $path -Target $link.From | Out-Null
        Write-Output "Linked $($link.Name) -> $($link.From)"
    }
}

$artifacts = @(
    @{
        File = 'rexglue-sdk-0.10.0-win-amd64.zip'
        Url = 'https://github.com/rexglue/rexglue-sdk/releases/download/v0.10.0/rexglue-sdk-0.10.0-win-amd64.zip'
        Sha256 = 'ad4485da853fbe932e9f5dbb643f9b5fe85dc73775ebbad4df6879fe6df0c11b'
        Destination = '.tools/rexglue-0.10.0'
        Executable = '.tools/rexglue-0.10.0/win-amd64/bin/rexglue.exe'
    },
    @{
        File = 'LLVM-21.1.8-win64.exe'
        Url = 'https://github.com/llvm/llvm-project/releases/download/llvmorg-21.1.8/LLVM-21.1.8-win64.exe'
        Sha256 = '7a5386c26497db1691f320121e5b113364dd0274b98e55f15f4dbc00c0450113'
        Destination = '.tools/llvm-21.1.8'
        Executable = '.tools/llvm-21.1.8/bin/clang++.exe'
    },
    @{
        # The launcher's SDK, pinned by launcher/global.json. Microsoft publishes SHA-512 only
        # (https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json).
        File = 'dotnet-sdk-10.0.401-win-x64.zip'
        Url = 'https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.401/dotnet-sdk-10.0.401-win-x64.zip'
        Sha512 = '24b670ad3d923bfcf47df6c3b034152398b42f6dbc388e10d783aee1cfb5e5817d399fc0ae2a12cfa822a55e61d34830ccb15c50ef6efee437ab874bb7c79430'
        Destination = '.tools/dotnet-sdk'
        Executable = '.tools/dotnet-sdk/dotnet.exe'
    },
    @{
        # Reference emulator and oracle (docs/research/feasibility.md). Edge cuts several
        # releases a day; bump the tag and hash together. portable.txt keeps its config here.
        File = 'xenia-edge-4d07204.zip'
        Url = 'https://github.com/has207/xenia-edge/releases/download/4d07204/xenia_edge_windows.zip'
        Sha256 = '0df8d0742b12a64ed60de1d26d7f15edfd847ade4979af98be38a87ff5d9591e'
        Destination = '.tools/xenia-edge'
        Executable = '.tools/xenia-edge/xenia_edge.exe'
    },
    @{
        File = 'extract-xiso-202609111233.zip'
        Url = 'https://github.com/XboxDev/extract-xiso/releases/download/build-202609111233/extract-xiso-Win64_Release.zip'
        Sha256 = 'fc891dfa72f08e3838a2f8b7ec34a8e4d61678da1cc8fee879677f00bf114ce3'
        Destination = '.tools/extract-xiso'
        Executable = '.tools/extract-xiso/artifacts/extract-xiso.exe'
    },
    @{
        # The 'latest' tag is rolling: a new upload changes the hash and fails the check.
        File = 'xextool-20261002.zip'
        Url = 'https://github.com/Team-Resurgent/XexTool/releases/download/latest/XexTool-windows-x64.zip'
        Sha256 = 'e0c8cb74e98e3b87f147786688d9ab13dd755d1fcf5badc621bc66a4e89501e5'
        Destination = '.tools/xextool'
        Executable = '.tools/xextool/XexTool-windows-x64/XexTool.exe'
    },
    @{
        # The Python the pinned LLVM's lldb is built against (it imports python310.dll), for
        # scripts/lldb.cmd. 3.10.11 is the last 3.10 release with Windows binaries; the hash is
        # of the copy whose DLLs carry the Python Software Foundation's Authenticode signature.
        File = 'python-3.10.11-embed-amd64.zip'
        Url = 'https://www.python.org/ftp/python/3.10.11/python-3.10.11-embed-amd64.zip'
        Sha256 = '608619F8619075629C9C69F361352A0DA6ED7E62F83A0E19C63E0EA32EB7629D'
        Destination = '.tools/python-3.10.11-embed'
        Executable = '.tools/python-3.10.11-embed/python310.dll'
    }
)
foreach ($artifact in $artifacts) {
    if (Test-Path -LiteralPath $artifact.Executable) {
        Write-Output "Present $($artifact.Executable)"
        continue
    }
    $archive = Join-Path '.tools' $artifact.File
    if (!(Test-Path -LiteralPath $archive)) {
        Invoke-WebRequest $artifact.Url -OutFile $archive
    }
    $algorithm = if ($artifact.Sha512) { 'SHA512' } else { 'SHA256' }
    $expected = if ($artifact.Sha512) { $artifact.Sha512 } else { $artifact.Sha256 }
    if ((Get-FileHash $archive -Algorithm $algorithm).Hash -ne $expected) {
        throw "Checksum mismatch: $archive. No files were extracted."
    }
    if ($archive.EndsWith('.zip')) {
        Expand-Archive $archive $artifact.Destination
    } else {
        if (!$SevenZipPath) {
            $SevenZipPath = @((Get-Command 7z -ErrorAction SilentlyContinue).Source,
                "$env:ProgramFiles/7-Zip/7z.exe", 'D:/Tools/7-Zip/7z.exe') |
                Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
        }
        if (!$SevenZipPath -or !(Test-Path -LiteralPath $SevenZipPath)) {
            throw '7-Zip not found. Install it from https://www.7-zip.org or pass -SevenZipPath with the path to 7z.exe.'
        }
        Invoke-Hidden $SevenZipPath @('x', $archive, "-o$($artifact.Destination)", '-y')
    }
    Write-Output "Verified $($artifact.File)"
}
New-Item -ItemType File -Force .tools/xenia-edge/portable.txt | Out-Null

# Sources, unpatched. rexglue-source and xenosrecomp sit at the commits
# WWE_ALLSTARS-RECOMP builds on, so its patches/ port across cleanly.
$sources = @(
    @{ Name = 'rexglue-source'; Url = 'https://github.com/rexglue/rexglue-sdk.git'; Commit = 'f5337cd' },
    @{ Name = 'xenosrecomp'; Url = 'https://github.com/zolaware/reblue-XenosRecomp.git'; Commit = '339af41' },
    # Function names and module map only; see feasibility.md section 4.
    @{ Name = 'topspin4-decomp'; Url = 'https://github.com/g-guthrie/topspin4-decomp.git'; Commit = '553f783' }
)
foreach ($source in $sources) {
    $path = ".tools/$($source.Name)"
    if (!(Test-Path -LiteralPath "$path/.git")) {
        $null = Invoke-Hidden $git @('clone', $source.Url, $path)
        $null = Invoke-Hidden $git @('-C', $path, 'checkout', $source.Commit)
        $null = Invoke-Hidden $git @('-C', $path, 'submodule', 'update', '--init', '--recursive')
    }
    Write-Output "Cloned $($source.Name) @ $($source.Commit)"
}
# Git for Windows may check out symlinks as text. Materialize only these known libmspack links,
# keeping real symlinks and refusing to replace edited files.
$mspack = '.tools/rexglue-source/thirdparty/libmspack'
foreach ($name in @('cab.h', 'cabd.c', 'lzx.h', 'lzxd.c', 'macros.h',
    'mspack.h', 'mszip.h', 'mszipd.c', 'qtm.h', 'qtmd.c', 'readbits.h', 'readhuff.h', 'system.c', 'system.h')) {
    $destination = "$mspack/cabextract/mspack/$name"
    $target = "$mspack/libmspack/mspack/$name"
    if ((Get-Item -LiteralPath $destination).LinkType) { continue }
    if ((Get-Content -LiteralPath $destination -Raw).Trim() -eq "../../libmspack/mspack/$name") {
        Copy-Item -LiteralPath $target -Destination $destination
    } elseif ((Get-FileHash $destination).Hash -ne (Get-FileHash $target).Hash) {
        throw "Refusing to overwrite modified libmspack file: $destination"
    }
}

# Our changes to the upstream sources, in order (patches/README.md).
& "$PSScriptRoot/apply-patches.ps1"
Write-Output 'Local toolchain is ready. No system installation or PATH changes were made.'
