# Applies each numbered patch series in patches/<tool>/ to .tools/<tool-source>, in file-name
# order. Later patches may change lines earlier ones added, so a stamp in the tool tree records
# the exact series applied instead of checking patches one by one. A patch that fails leaves the
# tree as it was before this run and stops with the patch's name. -Reset first discards the
# tree's changes (the applied series and any edits), so a changed series applies from scratch.
param([switch]$Reset)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Invoke-Hidden.ps1"
$projectRoot = Split-Path $PSScriptRoot -Parent
$git = (Get-Command git).Source
# git status and diff refresh the index under an optional lock, which interrupted runs left
# behind in the tool trees and which then blocked every git command there.
$env:GIT_OPTIONAL_LOCKS = '0'

# Runs git in a tool tree, waiting out an index.lock that another git process (an editor's git
# integration refreshing the same tree, say) holds for a moment. A lock still there after the
# wait while no git process runs at all was left by one that died (seen while new-patch.ps1
# rewrote the tree), so it is removed.
function Invoke-TreeGit([string]$Tree, [string[]]$Arguments) {
    $lockPath = "$Tree/.git/index.lock"
    for ($attempt = 1; ; $attempt++) {
        for ($wait = 0; $wait -lt 50 -and (Test-Path -LiteralPath $lockPath); $wait++) {
            Start-Sleep -Milliseconds 200
        }
        if ((Test-Path -LiteralPath $lockPath) -and !(Get-Process git -ErrorAction SilentlyContinue)) {
            Remove-Item -LiteralPath $lockPath
            Write-Host "Removed an ownerless $($Tree | Split-Path -Leaf)/.git/index.lock"
        }
        try {
            return Invoke-Hidden $git (@('-C', $Tree) + $Arguments)
        } catch {
            if ($attempt -ge 5 -or !(Test-Path -LiteralPath "$Tree/.git/index.lock")) { throw }
        }
    }
}

$series = @(
    @{ Patches = 'patches/rexglue'; Tree = '.tools/rexglue-source' }
)
foreach ($entry in $series) {
    $tree = "$projectRoot/$($entry.Tree)"
    $patches = @(Get-ChildItem "$projectRoot/$($entry.Patches)" -Filter '*.patch' | Sort-Object Name)
    if (!$patches) { continue }
    foreach ($patch in $patches) {
        if ($patch.Name -notmatch '^\d{4}-[a-z0-9-]+\.patch$') {
            throw "Patch names are NNNN-name.patch: $($entry.Patches)/$($patch.Name)"
        }
    }
    $expected = ($patches | ForEach-Object { "$($_.Name) $((Get-FileHash $_.FullName -Algorithm SHA256).Hash)" }) -join "`n"
    $stamp = "$tree/.topspin4-patches"
    # Every git command here takes well under a minute, so an older lock is left over.
    $lock = Get-Item -LiteralPath "$tree/.git/index.lock" -ErrorAction SilentlyContinue
    if ($lock -and $lock.LastWriteTime -lt (Get-Date).AddMinutes(-1)) {
        Remove-Item -LiteralPath $lock.FullName
        Write-Output "Removed a stale $($entry.Tree)/.git/index.lock from $($lock.LastWriteTime)"
    }
    if ($Reset) {
        $null = Invoke-TreeGit $tree @('checkout', '--', '.')
        $null = Invoke-TreeGit $tree @('clean', '-fdq')
        Remove-Item -LiteralPath $stamp -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $stamp) {
        if ((Get-Content -LiteralPath $stamp -Raw).Trim() -eq $expected.Trim()) {
            Write-Output "Patched $($entry.Tree) ($($patches.Count) patches, unchanged)"
            continue
        }
        throw "$($entry.Tree) carries a different patch set. Rerun with -Reset to apply this one from scratch."
    }
    $dirty = (Invoke-TreeGit $tree @('status', '--porcelain', '--ignore-submodules=dirty') | Out-String).Trim()
    if ($dirty) { throw "$($entry.Tree) has local changes and no patch stamp. Rerun with -Reset to discard them." }
    foreach ($patch in $patches) {
        $result = & $git -C $tree apply --whitespace=nowarn $patch.FullName 2>&1
        if ($LASTEXITCODE -ne 0) {
            $null = Invoke-TreeGit $tree @('checkout', '--', '.')
            $null = Invoke-TreeGit $tree @('clean', '-fdq')
            throw "Patch $($entry.Patches)/$($patch.Name) does not apply to $($entry.Tree); the tree was reset.`n$($result -join "`n")"
        }
    }
    Set-Content -LiteralPath $stamp -Value $expected -NoNewline
    Write-Output "Patched $($entry.Tree) ($($patches.Count) patches)"
}
