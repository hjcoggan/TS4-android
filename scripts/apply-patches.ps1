# Applies each numbered patch series in patches/<tool>/ to .tools/<tool-source>, in file-name
# order. Later patches may change lines earlier ones added, so a stamp in the tool tree records
# the exact series applied instead of checking patches one by one. A patch that fails leaves the
# tree as it was before this run and stops with the patch's name.
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Invoke-Hidden.ps1"
$projectRoot = Split-Path $PSScriptRoot -Parent
$git = (Get-Command git).Source

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
    if (Test-Path -LiteralPath $stamp) {
        if ((Get-Content -LiteralPath $stamp -Raw).Trim() -eq $expected.Trim()) {
            Write-Output "Patched $($entry.Tree) ($($patches.Count) patches, unchanged)"
            continue
        }
        throw "$($entry.Tree) carries a different patch set. Reset it (git -C $($entry.Tree) checkout -- . ; git -C $($entry.Tree) clean -fd), then rerun."
    }
    $dirty = (Invoke-Hidden $git @('--no-optional-locks', '-C', $tree, 'status', '--porcelain', '--ignore-submodules=dirty') | Out-String).Trim()
    if ($dirty) { throw "$($entry.Tree) has local changes and no patch stamp. Reset it before patching." }
    foreach ($patch in $patches) {
        $result = & $git -C $tree apply --whitespace=nowarn $patch.FullName 2>&1
        if ($LASTEXITCODE -ne 0) {
            $null = Invoke-Hidden $git @('-C', $tree, 'checkout', '--', '.')
            $null = Invoke-Hidden $git @('-C', $tree, 'clean', '-fdq')
            throw "Patch $($entry.Patches)/$($patch.Name) does not apply to $($entry.Tree); the tree was reset.`n$($result -join "`n")"
        }
    }
    Set-Content -LiteralPath $stamp -Value $expected -NoNewline
    Write-Output "Patched $($entry.Tree) ($($patches.Count) patches)"
}
