<#
.SYNOPSIS
  Production package exclusion + offline-runtime audit (Milestone 8 sections 128-131, 155).

.DESCRIPTION
  Verifies that publish/desktop and installer/CADWorkAssistant.bundle do not contain dev-only
  artifacts (FakeAutoCad, test assemblies, source files, Node/Python, Claude/MCP-related files,
  Autodesk host DLLs). Any hit makes this script exit 1, which stops build-release.ps1.
#>
param(
    [Parameter(Mandatory = $true)][string]$PublishDir,
    [Parameter(Mandatory = $true)][string]$BundleDir
)

$ErrorActionPreference = "Stop"
$failures = @()

function Test-NoFilesMatch($root, $pattern, $label) {
    if (-not (Test-Path $root)) { return }
    $found = Get-ChildItem -Path $root -Recurse -Filter $pattern -ErrorAction SilentlyContinue
    if ($found.Count -gt 0) {
        $script:failures += "$label - found $($found.Count): $($found[0].FullName)"
    }
}

Write-Host "Publish dir: $PublishDir"
Write-Host "Bundle dir:  $BundleDir"

# --- Things that must not appear in the Desktop publish output ---
Test-NoFilesMatch $PublishDir "CADWorkAssistant.FakeAutoCad.exe" "FakeAutoCad executable"
Test-NoFilesMatch $PublishDir "*.Tests.dll" "test assembly"
Test-NoFilesMatch $PublishDir "node.exe" "Node.js runtime"
Test-NoFilesMatch $PublishDir "python.exe" "Python runtime"
Test-NoFilesMatch $PublishDir "*.cs" "source file (.cs)"
Test-NoFilesMatch $PublishDir "*.xaml" "source file (.xaml - publish output should only have compiled resources)"

# --- User Manual (Milestone 13 Part B section 133) - only the built PDF ships, never the dev-only
#     ManualBuilder tool or its Markdown/screenshot sources. ---
Test-NoFilesMatch $PublishDir "CADWorkAssistant.ManualBuilder.exe" "ManualBuilder executable"
Test-NoFilesMatch $PublishDir "*.md" "Markdown source file"
$manualPdfPath = Join-Path $PublishDir "Documentation\CAD_Work_Assistant_User_Guide_ko-KR.pdf"
if (-not (Test-Path $manualPdfPath)) {
    $failures += "User manual PDF missing at $manualPdfPath"
}

if (Test-Path (Join-Path $PublishDir ".claude")) { $failures += ".claude folder present in publish output" }
if (Test-Path (Join-Path $PublishDir ".21st")) { $failures += ".21st folder present in publish output" }
if (Test-Path (Join-Path $PublishDir "node_modules")) { $failures += "node_modules present in publish output" }

# --- The AutoCAD bundle must never carry Autodesk's own host DLLs (Milestone 8 section 113) ---
Test-NoFilesMatch $BundleDir "acdbmgd.dll" "Autodesk host DLL (acdbmgd.dll)"
Test-NoFilesMatch $BundleDir "acmgd.dll" "Autodesk host DLL (acmgd.dll)"
Test-NoFilesMatch $BundleDir "accoremgd.dll" "Autodesk host DLL (accoremgd.dll)"

# --- Filename-level check for AI/LLM tooling traces (source-level grep is the stronger signal;
#     this just double-checks nothing with a telltale name got copied into the package) ---
$suspiciousNamePatterns = @("*claude*", "*anthropic*", "*openai*", "*ollama*")
foreach ($pattern in $suspiciousNamePatterns) {
    Test-NoFilesMatch $PublishDir $pattern "suspicious filename ($pattern)"
}

if ($failures.Count -gt 0) {
    Write-Host ""
    Write-Host "RUNTIME AUDIT FAILED:" -ForegroundColor Red
    foreach ($f in $failures) { Write-Host "  - $f" -ForegroundColor Red }
    exit 1
}

Write-Host "Runtime audit passed - no dev-only artifacts found in the production package." -ForegroundColor Green
