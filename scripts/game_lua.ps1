# Decompile the game's Lua scripts from its archives, for research.
#   pwsh scripts/game_lua.ps1 [-Archives Core,Melbourne] [-Out analysis/lua-src]
# Steps: scripts/arcdump pulls every Lua 5.0 bytecode entry out of gamedata/<archive>.arc (LZX
# or stored), scripts/lua_wide_strings.py rewrites the game's wide-string constants (type 9) as
# plain strings, and unluac (.tools/unluac/unluac.jar) decompiles with the game's opcode
# numbering (scripts/ts4-lua-opmap.txt: four opcodes added after DIV). The output is game data:
# it stays under analysis/, which is not committed (ADR-0006).
param(
    [string[]]$Archives = @('Core', 'Melbourne', 'CareerMain', 'TopSpinAcademy'),
    [string]$Out = 'analysis/lua-src'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$jar = "$root/.tools/unluac/unluac.jar"
if (-not (Test-Path $jar)) {
    throw "unluac is missing: download unluac_2025_12_23.jar from sourceforge.net/projects/unluac to $jar"
}
$raw = "$root/analysis/lua-raw"
$std = "$root/analysis/lua-std"
foreach ($folder in $raw, $std) { if (Test-Path $folder) { Remove-Item -Recurse -Force $folder } }
$paths = @($Archives | ForEach-Object { "$root/gamedata/$_.arc" })
& "$root/.tools/dotnet-sdk/dotnet.exe" run --project "$root/scripts/arcdump" -c Release -- $raw @paths
if ($LASTEXITCODE -ne 0) { throw "arcdump failed" }
& python -I "$root/scripts/lua_wide_strings.py" --dir $raw $std
if ($LASTEXITCODE -ne 0) { throw "lua_wide_strings.py failed" }
$ok = 0; $failed = 0
Get-ChildItem $std -Recurse -Filter *.luac | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath($std, $_.FullName) -replace '\.luac$', '.lua'
    $target = Join-Path $root $Out $relative
    New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
    & java -jar $jar --opmap "$root/scripts/ts4-lua-opmap.txt" --output $target $_.FullName 2>$null
    if ($LASTEXITCODE -eq 0) { $ok++ } else { $failed++ }
}
Write-Output "decompiled $ok scripts into $Out ($failed failed)"
