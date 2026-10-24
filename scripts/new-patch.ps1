# Turns your edits in .tools/rexglue-source into the next numbered patch in patches/rexglue/.
#
#   1. Edit files in .tools/rexglue-source (it carries the current series; scripts/apply-patches.ps1).
#   2. pwsh scripts/new-patch.ps1 <name>     e.g. guest-swap-count -> 0030-guest-swap-count.patch
#
# The patch is the difference between the tree as the series leaves it and the tree as it is now,
# new files included, so it applies on top of the series. The script then resets the tree and
# reapplies the whole series with the new patch, which proves it applies (and rebuilds will pick
# it up). Add a line for it to patches/README.md.
param([Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]+$')][string]$Name)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$git = (Get-Command git).Source
$env:GIT_OPTIONAL_LOCKS = '0'
$tree = "$projectRoot/.tools/rexglue-source"
$folder = "$projectRoot/patches/rexglue"
$patches = @(Get-ChildItem $folder -Filter '*.patch' | Sort-Object Name)
$stamp = "$tree/.topspin4-patches"
$expected = ($patches | ForEach-Object { "$($_.Name) $((Get-FileHash $_.FullName -Algorithm SHA256).Hash)" }) -join "`n"
if (!(Test-Path -LiteralPath $stamp) -or (Get-Content -LiteralPath $stamp -Raw).Trim() -ne $expected.Trim()) {
    throw 'The tree does not carry the current series, so its edits have no baseline. Run scripts/apply-patches.ps1 -Reset, then make the edits again.'
}

function Invoke-Git([string[]]$Arguments) {
    $output = & $git -C $tree @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "git $($Arguments -join ' ') failed:`n$($output -join "`n")" }
    $output
}

# Two scratch indexes, so the tree's own index is never touched: the series applied to HEAD,
# and the working tree as it is.
$scratch = New-Item -ItemType Directory -Force (Join-Path ([IO.Path]::GetTempPath()) "topspin4-new-patch-$PID")
try {
    $env:GIT_INDEX_FILE = "$scratch/series.index"
    $null = Invoke-Git @('read-tree', 'HEAD')
    foreach ($patch in $patches) { $null = Invoke-Git @('apply', '--cached', '--whitespace=nowarn', $patch.FullName) }
    $seriesTree = (Invoke-Git @('write-tree') | Out-String).Trim()
    $env:GIT_INDEX_FILE = "$scratch/work.index"
    $null = Invoke-Git @('read-tree', 'HEAD')
    $null = Invoke-Git @('add', '-A', '--', '.', ':!.topspin4-patches')
    $workTree = (Invoke-Git @('write-tree') | Out-String).Trim()
} finally {
    Remove-Item Env:GIT_INDEX_FILE -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force $scratch
}
if ($seriesTree -eq $workTree) { throw 'No edits in the tree beyond the current series.' }

$number = if ($patches) { [int]$patches[-1].Name.Substring(0, 4) + 1 } else { 1 }
$path = "$folder/{0:D4}-$Name.patch" -f $number
# --output writes git's bytes as they are (no console re-encoding).
$null = Invoke-Git @('diff', '--binary', "--output=$path", $seriesTree, $workTree)
Write-Output "Wrote patches/rexglue/$(Split-Path $path -Leaf)"
& "$PSScriptRoot/apply-patches.ps1" -Reset
