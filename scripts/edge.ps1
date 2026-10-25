# Runs the Oracle (Xenia Edge on the Reference build) unattended, for comparisons by frame.
#
#   pwsh scripts/edge.ps1 [-InputScript scripts/input/edge-boot-to-practice.txt] [-Seconds 45]
#       [-ShotsAt 20,30,40 | -ShotEvery 2] [-Renderer d3d12|vulkan] [-RenderDocAt 35] [-PresentMon]
#       [-NoPad]
#
# Edge ignores posted and injected keys, so the input script plays on a virtual Xbox 360 pad
# (scripts/edge_pad.py; needs the ViGEmBus driver: winget install ViGEm.ViGEmBus, and
# pip install vgamepad). -NoPad plugs in no virtual pad and plays no script, so the owner can play
# on their own controller. The run ends after -Seconds or when Edge is closed. Window screenshots
# go to analysis/edge-<time>/shot-<s>s.png, at the -ShotsAt seconds or every -ShotEvery seconds;
# Edge must stay windowed for them. -Renderer switches Edge's gpu setting for this run and puts it
# back. -RenderDocAt starts Edge under RenderDoc and captures the frame at that second (Vulkan
# only; RenderDoc does not capture Edge's D3D12). -PresentMon records Edge's presents to
# presentmon.csv (.tools/presentmon; its TimeInSeconds starts with the input script, within a
# second). The run stops unless Edge's title bar shows the Reference build, v0.0.1.3: TU1
# installed in Edge through its Tools > Install Content.
param(
    [string]$InputScript = "$PSScriptRoot/input/edge-boot-to-practice.txt",
    [ValidateRange(5, 1800)][int]$Seconds = 45,
    # Seconds; a comma list also works through pwsh -File.
    [string[]]$ShotsAt = @(),
    [int]$ShotEvery = 0,
    [switch]$NoPad,
    [ValidateSet('d3d12', 'vulkan')][string]$Renderer = 'd3d12',
    [int]$RenderDocAt = -1,
    [switch]$PresentMon,
    [string]$Out
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$edgeDir = "$projectRoot/.tools/xenia-edge"
$config = "$edgeDir/xenia-edge.config.toml"
$iso = Get-ChildItem "$projectRoot/.iso" -Recurse -Filter '*.iso' | Select-Object -First 1
if (!$iso) { throw "No disc image under .iso/" }
if ($RenderDocAt -ge 0 -and $Renderer -ne 'vulkan') { throw '-RenderDocAt needs -Renderer vulkan' }
if (!$Out) { $Out = Join-Path $projectRoot ("analysis/edge-" + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$Out = [IO.Path]::GetFullPath($Out, (Get-Location).Path)
New-Item -ItemType Directory -Force $Out | Out-Null
if (Get-Process xenia_edge -ErrorAction SilentlyContinue) { throw 'Xenia Edge is already running; close it first.' }

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class EdgeShot {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
}
'@
function Save-Shot([System.Diagnostics.Process]$p, [string]$path) {
    $p.Refresh()
    $h = $p.MainWindowHandle
    $r = [EdgeShot+RECT]::new()
    if ($h -eq [IntPtr]::Zero -or ![EdgeShot]::GetWindowRect($h, [ref]$r)) { return }
    # A minimized window draws nothing; count the miss instead of saving a sliver of title bar.
    if ([EdgeShot]::IsIconic($h)) { $script:minimizedShots++; return }
    $w = $r.R - $r.L; $hh = $r.B - $r.T
    if ($w -le 0 -or $hh -le 0) { return }
    $bitmap = [Drawing.Bitmap]::new($w, $hh)
    $g = [Drawing.Graphics]::FromImage($bitmap)
    $dc = $g.GetHdc()
    # PW_RENDERFULLCONTENT: the window renders itself, so other windows never get in.
    [void][EdgeShot]::PrintWindow($h, $dc, 2)
    $g.ReleaseHdc($dc)
    $bitmap.Save($path, [Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bitmap.Dispose()
}

$originalConfig = Get-Content -LiteralPath $config -Raw
$edge = $null; $pad = $null; $presentMon = $null; $minimizedShots = 0
try {
    if ($Renderer -ne 'd3d12') {
        ($originalConfig -replace '(?m)^gpu = "\w+"', "gpu = `"$Renderer`"") | Set-Content -LiteralPath $config -NoNewline
    }
    $edgeArgs = @("`"$($iso.FullName)`"")
    if ($RenderDocAt -ge 0) {
        $renderdoccmd = 'C:\Program Files\RenderDoc\renderdoccmd.exe'
        if (!(Test-Path $renderdoccmd)) { throw 'RenderDoc is not installed (winget install BaldurKarlsson.RenderDoc)' }
        $launcher = Start-Process $renderdoccmd -PassThru -ArgumentList (@('capture', '--working-dir', "`"$edgeDir`"",
                '--capture-file', "`"$Out\edge`"", "`"$edgeDir\xenia_edge.exe`"") + $edgeArgs)
        for ($i = 0; $i -lt 40 -and !$edge; $i++) { Start-Sleep -Milliseconds 250; $edge = Get-Process xenia_edge -ErrorAction SilentlyContinue | Select-Object -First 1 }
        if (!$edge) { throw 'Edge did not start under RenderDoc' }
    } else {
        $edge = Start-Process "$edgeDir\xenia_edge.exe" -ArgumentList $edgeArgs -WorkingDirectory $edgeDir -PassThru
    }
    if ($PresentMon) {
        $presentMon = Start-Process "$projectRoot/.tools/presentmon/PresentMon-2.6.0-x64.exe" -PassThru -WindowStyle Hidden -ArgumentList @(
            '--process_name', 'xenia_edge.exe', '--output_file', "`"$Out\presentmon.csv`"", '--no_console_stats',
            '--stop_existing_session', '--terminate_on_proc_exit')
    }
    $timer = [Diagnostics.Stopwatch]::StartNew()
    if (!$NoPad) {
        $pad = Start-Process python -ArgumentList @('-I', "`"$PSScriptRoot\edge_pad.py`"", "`"$InputScript`"",
                '--hold-after', "$Seconds") -PassThru -NoNewWindow -RedirectStandardOutput "$Out\pad.txt"
    }
    # The title bar names the game's build once the game is loaded.
    $title = $null
    while ($timer.Elapsed.TotalSeconds -lt 30 -and !$edge.HasExited) {
        Start-Sleep -Milliseconds 500
        $edge.Refresh()
        if ($edge.MainWindowTitle -match '\[54540859 (v[\d.]+)\]') { $title = $Matches[1]; break }
    }
    if ($title -ne 'v0.0.1.3') { throw "Edge is not running the Reference build (title shows '$title'); install TU1 in Edge." }
    if ($ShotEvery -gt 0) { $ShotsAt = @(for ($s = $ShotEvery; $s -le $Seconds; $s += $ShotEvery) { $s }) }
    $shots = [Collections.Generic.Queue[int]]::new([int[]]@($ShotsAt | ForEach-Object { $_ -split '[,\s]+' } | Where-Object { $_ } | ForEach-Object { [int]$_ } | Sort-Object))
    $captured = $RenderDocAt -lt 0
    while ($timer.Elapsed.TotalSeconds -lt $Seconds -and !$edge.HasExited) {
        Start-Sleep -Milliseconds 100
        $now = $timer.Elapsed.TotalSeconds
        if ($shots.Count -and $now -ge $shots.Peek()) { Save-Shot $edge ("$Out\shot-{0:D3}s.png" -f $shots.Dequeue()) }
        if (!$captured -and $now -ge $RenderDocAt) {
            & "$PSScriptRoot/rdoc.ps1" trigger -Out $Out | Write-Output
            $captured = $true
        }
    }
    [ordered]@{ build = $title; renderer = $Renderer; out = $Out
        shots = @(Get-ChildItem $Out -Filter 'shot-*.png' | ForEach-Object Name)
        missedMinimized = $minimizedShots
        captures = @(Get-ChildItem $Out -Filter '*.rdc' | ForEach-Object Name)
        presentmon = Test-Path "$Out\presentmon.csv" } | ConvertTo-Json
} finally {
    if ($pad -and !$pad.HasExited) { $pad.Kill() }
    if ($edge -and !$edge.HasExited) { $edge.Kill() }
    if ($presentMon -and !$presentMon.WaitForExit(5000)) { $presentMon.Kill() }
    Start-Sleep -Milliseconds 500
    Set-Content -LiteralPath $config -Value $originalConfig -NoNewline
}
