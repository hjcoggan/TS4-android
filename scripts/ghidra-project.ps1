# Builds the local Ghidra project of the three TU1 Modules (ticket 04): imports the TU1-patched
# XEX images through the Xenon360 loader, analyzes them, repairs the register save helpers and
# applies symbols/tu1-symbols.csv. The project is game-derived and stays in the ignored ghidra/.
param(
    [string]$GhidraRoot = 'D:/tools/ghidra-mcp/ghidra_12.1.4_PUBLIC',
    [ValidateRange(60, 36000)][int]$AnalysisTimeoutSeconds = 3600
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$headless = "$GhidraRoot/support/analyzeHeadless.bat"
if (!(Test-Path -LiteralPath $headless)) { throw "Ghidra not found: $headless (pass -GhidraRoot)" }
New-Item -ItemType Directory -Force "$projectRoot/ghidra", "$projectRoot/analysis" | Out-Null
# The XexTool -p output of each Module with TU1 applied (docs/research/module-inspection.md), and
# its __savegprlr_14 (generated/<module>/topspin4_init.cpp).
$modules = @(
    @{ File = 'default.xex'; SaveHelper = '0x82010c60' },
    @{ File = 'Loader_DLL.xex'; SaveHelper = '0x833e3130' },
    @{ File = 'Swing_DLL.xex'; SaveHelper = '0x8856ac40' }
)
foreach ($module in $modules) {
    $image = "$projectRoot/analysis/xex/tu1/$($module.File)"
    if (!(Test-Path -LiteralPath $image)) { throw "Missing $image (XexTool -p with the TU1 .xexp)" }
    $arguments = @("$projectRoot/ghidra", 'TopSpin4TU1', '-import', $image, '-overwrite',
        '-analysisTimeoutPerFile', "$AnalysisTimeoutSeconds", '-scriptPath', "$projectRoot/tools/ghidra",
        '-postScript', 'FixXbox360SaveHelpers.java', $module.SaveHelper)
    if ($module.File -eq 'Swing_DLL.xex') {
        $arguments += @('-postScript', 'ApplySymbolMap.java', "$projectRoot/symbols/tu1-symbols.csv")
    }
    & $headless @arguments *> "$projectRoot/analysis/ghidra-$($module.File).log"
    # A failing post-script leaves the exit code at 0, so its report line is checked too.
    if ($LASTEXITCODE -ne 0 -or (Select-String -Quiet -Path "$projectRoot/analysis/ghidra-$($module.File).log" -Pattern 'SCRIPT ERROR')) {
        throw "Ghidra failed on $($module.File); see analysis/ghidra-$($module.File).log"
    }
    Write-Output "Imported $($module.File)"
}
