# Bounded startup probe (copied from WWE All Stars): run the game for N seconds, keep logs and
# screenshots, and report crashes, unregistered call targets, missing jump-table entries, stalls
# and the Title Update patches the Modules loaded with.
param(
    [ValidateRange(1, 600)][int]$Seconds = 30,
    [ValidateSet('info', 'debug', 'trace')][string]$LogLevel = 'info',
    # Window screenshots every N seconds (0 = none); needs a visible window.
    [ValidateRange(0, 600)][int]$ScreenshotEvery = 0,
    # A run still alive at the deadline whose log was quiet this long is reported as stalled.
    [ValidateRange(1, 600)][int]$StallSeconds = 10,
    [ValidateSet('RelWithDebInfo', 'Release', 'Debug')][string]$Configuration = 'RelWithDebInfo',
    [string[]]$ExtraArguments = @(),
    # A scripted pad (src/script_input.h) replaces every real controller, so a run is never
    # steered by a pad someone happens to touch. The default skips the intro videos and stops
    # on the title screen; -InputScript '' plays with the real pads. Copied into the run folder.
    [string]$InputScript = "$PSScriptRoot/input/boot-to-title.txt",
    [switch]$Headless
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$executable = "$projectRoot/out/build/win-amd64-$($Configuration.ToLowerInvariant())/topspin4.exe"
if (!(Test-Path $executable)) { throw "Build first: $executable" }
$run = Join-Path $projectRoot ("analysis/smoke-" + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force $run | Out-Null

$info = [System.Diagnostics.ProcessStartInfo]::new()
$info.FileName = $executable
$info.WorkingDirectory = Split-Path $executable -Parent
$info.UseShellExecute = $false
$info.RedirectStandardOutput = $true
$info.RedirectStandardError = $true
$arguments = @('--gpu_plugin=xenos', '--vulkan_device=-1',
    "--game_data_root=$projectRoot/gamedata", "--update_data_root=$projectRoot/updatedata",
    "--user_data_root=$projectRoot/userdata", "--cache_root=$projectRoot/cache",
    "--log_file=$run/runtime.log", "--log_level=$LogLevel", '--log_flush_interval=1')
if ($Headless) { $arguments += '--headless' }
if ($InputScript) {
    if (!(Test-Path -LiteralPath $InputScript)) { throw "No input script at $InputScript" }
    Copy-Item -LiteralPath $InputScript "$run/input.txt"
    $arguments += "--ts4_input_script=$run/input.txt"
}
foreach ($argument in $ExtraArguments) {
    if ($argument -notmatch '^--[a-z0-9_]+=') { throw "Invalid runtime argument: $argument" }
    $arguments += $argument
}
foreach ($argument in $arguments) { $info.ArgumentList.Add($argument) }

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class SmokeWin {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
}
'@
function Save-Screenshot([System.Diagnostics.Process]$p, [string]$path) {
    $p.Refresh()
    $handle = $p.MainWindowHandle
    if ($handle -eq [IntPtr]::Zero) { return }
    $rect = [SmokeWin+RECT]::new()
    if (![SmokeWin]::GetWindowRect($handle, [ref]$rect)) { return }
    $w = $rect.R - $rect.L; $h = $rect.B - $rect.T
    if ($w -le 0 -or $h -le 0) { return }
    $bitmap = [System.Drawing.Bitmap]::new($w, $h)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    # Ask the window to render itself (PW_RENDERFULLCONTENT) instead of copying
    # the screen, so other windows on the desktop never end up in a capture.
    $hdc = $graphics.GetHdc()
    $ok = [SmokeWin]::PrintWindow($handle, $hdc, 2)
    $graphics.ReleaseHdc($hdc)
    if ($ok) { $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png) }
    $graphics.Dispose(); $bitmap.Dispose()
}

# Every distinct capture of $pattern's first group in the log, in order of first appearance.
function Get-LogMatches([string]$log, [string]$pattern) {
    if (!(Test-Path -LiteralPath $log)) { return ,@() }
    ,@(Select-String -LiteralPath $log -Pattern $pattern | ForEach-Object { $_.Matches[0].Groups[1].Value } |
        Select-Object -Unique)
}

$process = [System.Diagnostics.Process]::new()
$process.StartInfo = $info
$started = $false
$timer = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $started = $process.Start()
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    $shot = 0
    while (!$process.HasExited -and $timer.Elapsed.TotalSeconds -lt $Seconds) {
        $step = if ($ScreenshotEvery -gt 0) { $ScreenshotEvery } else { $Seconds }
        $remaining = [math]::Max(0, $Seconds - $timer.Elapsed.TotalSeconds)
        $null = $process.WaitForExit([int]([math]::Min($step, $remaining) * 1000))
        if ($ScreenshotEvery -gt 0 -and !$process.HasExited) {
            $shot++
            Save-Screenshot $process ("$run/shot-{0:D3}.png" -f $shot)
        }
    }
    $timedOut = !$process.HasExited
    $quietSeconds = $null
    if ($timedOut) {
        if (Test-Path "$run/runtime.log") {
            $quietSeconds = [math]::Round(((Get-Date) - (Get-Item "$run/runtime.log").LastWriteTime).TotalSeconds, 1)
        }
        $process.Kill($true); $process.WaitForExit()
    }
    $stdout.GetAwaiter().GetResult() | Set-Content "$run/stdout.txt"
    $stderr.GetAwaiter().GetResult() | Set-Content "$run/stderr.txt"
    $log = "$run/runtime.log"
    $unregistered = Get-LogMatches $log 'unregistered function at guest address (0x[0-9A-Fa-f]+)'
    $jumpTables = Get-LogMatches $log '\[FATAL\].*((?:Jump target|Unresolved (?:call|branch)) .*)$'
    $fatal = Get-LogMatches $log '\[FATAL\]\s*(.*)$'
    $titleUpdatePatches = Get-LogMatches $log 'Loading XEX patch from (\S+)'
    $outcome = if ($timedOut) {
        if ($null -ne $quietSeconds -and $quietSeconds -ge $StallSeconds) { 'stalled' } else { 'alive_at_deadline' }
    } elseif ($process.ExitCode -eq 0 -and !$fatal) { 'exited' } else { 'crashed' }
    $result = [ordered]@{
        seconds = $Seconds; elapsed_seconds = [math]::Round($timer.Elapsed.TotalSeconds, 2)
        exit_code = $(if ($timedOut) { $null } else { $process.ExitCode }); outcome = $outcome
        log_quiet_seconds = $quietSeconds
        unregistered_call_targets = $unregistered; missing_jump_table_entries = $jumpTables
        fatal = $fatal; title_update_patches = $titleUpdatePatches; screenshots = $shot; logs = $run
    }
    $result | ConvertTo-Json | Set-Content "$run/result.json"
    $result | ConvertTo-Json
} finally {
    if ($started -and !$process.HasExited) { $process.Kill($true); $process.WaitForExit() }
    $process.Dispose()
}
