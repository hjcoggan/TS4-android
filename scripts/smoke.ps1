# Bounded startup probe (copied from WWE All Stars): run the game for N seconds, keep logs and
# screenshots, and report crashes, unregistered call targets, missing jump-table entries, stalls
# and the Title Update patches the Modules loaded with. Screenshots are graded black or not, and
# -StacksAt takes every thread's stack (guest functions included) at the given seconds.
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
    [switch]$Headless,
    # Seconds into the run at which to attach the debugger (scripts/lldb.cmd) and save every
    # thread's stack: stacks-<s>.txt in full, and stacks-<s>-guest.txt with one line per
    # thread that runs guest code (its name and guest functions, innermost first).
    [ValidateRange(1, 600)][int[]]$StacksAt = @()
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
# A capture whose brightest pixel, outside the top-left corner where overlays such as RTSS
# draw, stays below this (0-255) counts as black.
$blackMax = 16
# Saves a window capture and returns its luminance (mean, max) on a 64x36 thumbnail of
# everything but the top-left 16% x 14%; $null when no capture was taken.
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
    $grade = $null
    if ($ok) {
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        $crop = [System.Drawing.Rectangle]::new([int]($w * 0.16), [int]($h * 0.14), $w - [int]($w * 0.16), $h - [int]($h * 0.14))
        $thumb = [System.Drawing.Bitmap]::new(64, 36)
        $tg = [System.Drawing.Graphics]::FromImage($thumb)
        $tg.DrawImage($bitmap, [System.Drawing.Rectangle]::new(0, 0, 64, 36), $crop, [System.Drawing.GraphicsUnit]::Pixel)
        $tg.Dispose()
        $sum = 0.0; $max = 0
        for ($y = 0; $y -lt 36; $y++) {
            for ($x = 0; $x -lt 64; $x++) {
                $c = $thumb.GetPixel($x, $y)
                $l = [int](0.299 * $c.R + 0.587 * $c.G + 0.114 * $c.B)
                $sum += $l; if ($l -gt $max) { $max = $l }
            }
        }
        $thumb.Dispose()
        $grade = [ordered]@{ file = (Split-Path $path -Leaf); mean = [math]::Round($sum / (64 * 36)); max = $max; black = $max -lt $blackMax }
    }
    $graphics.Dispose(); $bitmap.Dispose()
    $grade
}

# Attaches lldb to $p, saves every thread's stack to stacks-<s>.txt and a guest-only summary.
function Save-Stacks([System.Diagnostics.Process]$p, [int]$second) {
    $full = "$run/stacks-$second.txt"
    & "$PSScriptRoot/lldb.cmd" -b -p $p.Id -k 'thread backtrace all -c 60' -k detach *> $full
    $lines = [System.Collections.Generic.List[string]]::new()
    $name = $null; $frames = [System.Collections.Generic.List[string]]::new()
    $flush = { if ($frames.Count) { $lines.Add("$name : $($frames -join ' < ')") } }
    foreach ($line in Get-Content -LiteralPath $full) {
        if ($line -match '^\s*\*?\s*thread #\d+') {
            & $flush
            $name = if ($line -match "name = '([^']*)'") { $Matches[1] } else { ($line -replace ',.*', '').Trim(' *') }
            $frames = [System.Collections.Generic.List[string]]::new()
        } elseif ($line -match 'frame #\d+:.*?(sub_[0-9A-F]+)') {
            $frames.Add($Matches[1])
        }
    }
    & $flush
    $lines | Set-Content "$run/stacks-$second-guest.txt"
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
    $grades = [System.Collections.Generic.List[object]]::new()
    $stackTimes = [System.Collections.Generic.Queue[int]]::new([int[]]@($StacksAt | Where-Object { $_ -lt $Seconds } | Sort-Object -Unique))
    $nextShot = if ($ScreenshotEvery -gt 0) { [double]$ScreenshotEvery } else { [double]::MaxValue }
    while (!$process.HasExited -and $timer.Elapsed.TotalSeconds -lt $Seconds) {
        $nextStack = if ($stackTimes.Count) { [double]$stackTimes.Peek() } else { [double]::MaxValue }
        $next = [math]::Min([math]::Min($nextShot, $nextStack), [double]$Seconds)
        $wait = [math]::Max(0, $next - $timer.Elapsed.TotalSeconds)
        if ($process.WaitForExit([int]($wait * 1000))) { break }
        $now = $timer.Elapsed.TotalSeconds
        if ($now -ge $nextShot) {
            $shot++
            $grade = Save-Screenshot $process ("$run/shot-{0:D3}.png" -f $shot)
            if ($grade) { $grades.Add($grade) }
            $nextShot += $ScreenshotEvery
        }
        if ($stackTimes.Count -and $now -ge $stackTimes.Peek()) { Save-Stacks $process $stackTimes.Dequeue() }
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
        fatal = $fatal; title_update_patches = $titleUpdatePatches; screenshots = $shot
        # Frames presenting but every capture black points at rendering, not a stall
        # (docs/agents/codegen-debugging.md).
        black_screenshots = @($grades | Where-Object { $_.black }).Count
        last_screenshot_black = $(if ($grades.Count) { $grades[-1].black } else { $null })
        screenshot_luminance = @($grades | ForEach-Object { "$($_.file) $($_.mean)/$($_.max)" })
        stacks = @($StacksAt | Where-Object { Test-Path "$run/stacks-$_-guest.txt" } | ForEach-Object { "stacks-$_-guest.txt" })
        logs = $run
    }
    $result | ConvertTo-Json | Set-Content "$run/result.json"
    $result | ConvertTo-Json
} finally {
    if ($started -and !$process.HasExited) { $process.Kill($true); $process.WaitForExit() }
    $process.Dispose()
}
