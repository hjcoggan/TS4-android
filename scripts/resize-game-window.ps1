# Resizes the running game's window, still hidden, so a run can check what follows the window
# (image-quality ticket 06: Auto Resolution scale). The window gets each size in turn, -Every
# seconds apart, starting -After seconds after the game's window appears; Windows sends it a
# WM_SIZE, as for a player's resize, without showing it or taking focus.
#
#   pwsh scripts/resize-game-window.ps1 -Sizes 2560x1440,1280x720 [-After 15] [-Every 10]
param(
    [Parameter(Mandatory)][string[]]$Sizes,
    [int]$After = 15,
    [int]$Every = 10,
    [int]$WaitSeconds = 60
)
$ErrorActionPreference = 'Stop'
Add-Type @'
using System; using System.Runtime.InteropServices; using System.Collections.Generic;
public static class GameWindow {
  delegate bool EnumProc(IntPtr h, IntPtr p);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr p);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int hh, uint flags);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
  public static IntPtr Find(uint pid) {
    IntPtr found = IntPtr.Zero;
    EnumWindows((h, p) => {
      uint owner; GetWindowThreadProcessId(h, out owner);
      var name = new System.Text.StringBuilder(64); GetClassName(h, name, 64);
      if (owner == pid && name.ToString() == "SDL_app") { found = h; return false; }
      return true;
    }, IntPtr.Zero);
    return found;
  }
}
'@
$deadline = (Get-Date).AddSeconds($WaitSeconds)
$window = [IntPtr]::Zero
while ($window -eq [IntPtr]::Zero -and (Get-Date) -lt $deadline) {
    foreach ($game in @(Get-Process topspin4 -ErrorAction SilentlyContinue)) {
        $window = [GameWindow]::Find([uint32]$game.Id)
        if ($window -ne [IntPtr]::Zero) { break }
    }
    Start-Sleep -Milliseconds 250
}
if ($window -eq [IntPtr]::Zero) { throw 'No game window appeared' }
Start-Sleep -Seconds $After
# SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE
$flags = 0x2 -bor 0x4 -bor 0x10
$sizeList = @($Sizes | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
for ($i = 0; $i -lt $sizeList.Count; $i++) {
    if ($i) { Start-Sleep -Seconds $Every }
    $w, $h = [int[]]($sizeList[$i] -split 'x')
    [void][GameWindow]::SetWindowPos($window, [IntPtr]::Zero, 0, 0, $w, $h, $flags)
    $r = [GameWindow+RECT]::new()
    [void][GameWindow]::GetClientRect($window, [ref]$r)
    Write-Output ("{0:HH:mm:ss.fff} resized to {1}x{2} (client {3}x{4})" -f (Get-Date), $w, $h, ($r.R - $r.L), ($r.B - $r.T))
}
