# RenderDoc for frame comparisons (docs/agents/codegen-debugging.md, "An object renders black").
#
#   pwsh scripts/rdoc.ps1 capture -Frame 1050 [-InputScript scripts/input/boot-to-main-menu.txt]
#       Runs the port headless under RenderDoc and captures exactly guest frame N
#       (--ts4_renderdoc_frame), plus a PNG of that frame. Prints the capture's path.
#   pwsh scripts/rdoc.ps1 query <capture.rdc> 'draws --indices 1999' 'draw 2164' 'texture 5910 --all-mips'
#       Answers every question against one replay (scripts/rdoc/query.py lists them) and prints
#       the answers; PNGs and shader text go beside them in -Out.
#   pwsh scripts/rdoc.ps1 trigger
#       Makes whatever runs under RenderDoc capture its next frame (scripts/edge.ps1 uses it).
#
# qrenderdoc runs the Python scripts. It must be started from PowerShell; started from bash the
# script silently never runs. RenderDoc is installed with: winget install BaldurKarlsson.RenderDoc
param(
    [Parameter(Mandatory, Position = 0)][ValidateSet('capture', 'query', 'trigger')][string]$Verb,
    [Parameter(Position = 1)][string]$Capture,
    [Parameter(Position = 2, ValueFromRemainingArguments)][string[]]$Commands = @(),
    [int]$Frame = -1,
    [string]$InputScript = "$PSScriptRoot/input/boot-to-main-menu.txt",
    [string]$Out,
    [string[]]$ExtraArguments = @(),
    [ValidateRange(10, 3600)][int]$TimeoutSeconds = 900
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$renderdoc = 'C:\Program Files\RenderDoc'
if (!(Test-Path "$renderdoc\qrenderdoc.exe")) { throw "RenderDoc is not installed (winget install BaldurKarlsson.RenderDoc)" }
# Absolute, because capture runs the game from the build folder.
if ($Out) { $Out = [IO.Path]::GetFullPath($Out, (Get-Location).Path) }
if ($InputScript) { $InputScript = [IO.Path]::GetFullPath($InputScript, (Get-Location).Path) }

# Runs one of scripts/rdoc/*.py inside qrenderdoc with $Request as its TS4_RDOC JSON.
function Invoke-RenderDocScript([string]$Script, [hashtable]$Request) {
    $env:TS4_RDOC = $Request | ConvertTo-Json -Compress -Depth 5
    try {
        $p = Start-Process "$renderdoc\qrenderdoc.exe" -ArgumentList '--python', "`"$PSScriptRoot\rdoc\$Script`"" -PassThru -WindowStyle Hidden
        if (!$p.WaitForExit($TimeoutSeconds * 1000)) { $p.Kill(); throw "qrenderdoc did not finish $Script in $TimeoutSeconds s" }
    } finally {
        Remove-Item Env:TS4_RDOC -ErrorAction SilentlyContinue
    }
}

switch ($Verb) {
    'capture' {
        if ($Frame -lt 1) { throw 'capture needs -Frame N (N >= 1)' }
        $executable = "$projectRoot/out/build/win-amd64-relwithdebinfo/topspin4.exe"
        if (!(Test-Path $executable)) { throw "Build first: $executable" }
        if (!$Out) { $Out = Join-Path $projectRoot ("analysis/rdoc-" + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
        New-Item -ItemType Directory -Force $Out | Out-Null
        Copy-Item -LiteralPath $InputScript "$Out/input.txt"
        # pwsh -File hands an array over as one string ('--a=1','--b=2'): split it back into flags.
        $extra = @($ExtraArguments | ForEach-Object { $_ -split "[',\s]+(?=--)" } |
            ForEach-Object { $_.Trim("' ,") } | Where-Object { $_ })
        foreach ($argument in $extra) {
            if ($argument -notmatch '^--[a-z0-9_]+=') { throw "Invalid runtime argument: $argument" }
        }
        $arguments = @('--gpu_plugin=xenos', '--vulkan_device=-1', '--headless', '--window_hidden=true',
            "--game_data_root=$projectRoot/gamedata", "--update_data_root=$projectRoot/updatedata",
            "--user_data_root=$projectRoot/userdata", "--cache_root=$projectRoot/cache",
            "--log_file=$Out/runtime.log", '--log_level=info', '--log_flush_interval=1',
            "--ts4_input_script=$Out/input.txt", "--ts4_renderdoc_frame=$Frame",
            "--ts4_dump_frames=$Frame", "--ts4_dump_dir=$Out", "--ts4_exit_after_frame=$($Frame + 2)") + $extra
        # Runs never overlap (scripts/smoke.ps1, scripts/scene_runner.py hold the same lock).
        try {
            $runLock = [System.IO.File]::Open("$projectRoot/analysis/game-run.lock", 'OpenOrCreate', 'ReadWrite', 'None')
        } catch [System.IO.IOException] {
            throw "A game run is going (analysis/game-run.lock is held); runs never overlap."
        }
        try {
            Push-Location (Split-Path $executable)
            & "$renderdoc\renderdoccmd.exe" capture --wait-for-exit --capture-file "$Out/frame" $executable @arguments *> "$Out/renderdoccmd.txt"
        } finally {
            Pop-Location
            $runLock.Dispose()
        }
        $rdc = Get-ChildItem $Out -Filter '*.rdc' | Select-Object -First 1
        if (!$rdc) { throw "No capture written; see $Out/runtime.log ('RenderDoc capture of frame')" }
        $rdc.FullName
    }
    'query' {
        if (!$Capture -or !(Test-Path -LiteralPath $Capture)) { throw "query needs a capture file: $Capture" }
        if (!$Commands) { throw "query needs at least one command (scripts/rdoc/query.py lists them)" }
        if (!$Out) { $Out = Join-Path ([IO.Path]::GetDirectoryName($Capture)) 'query' }
        $list = @($Commands | ForEach-Object { , @($_ -split '\s+' | Where-Object { $_ }) })
        Invoke-RenderDocScript 'query.py' @{ capture = (Resolve-Path -LiteralPath $Capture).Path; out = $Out; commands = $list }
        Get-Content "$Out/query.txt"
    }
    'trigger' {
        if (!$Out) { $Out = Join-Path $projectRoot 'analysis/rdoc-trigger' }
        Invoke-RenderDocScript 'trigger.py' @{ out = $Out }
        Get-Content "$Out/trigger.txt"
    }
}
