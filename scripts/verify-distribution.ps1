<#
.SYNOPSIS
  Verify a CAD Work Assistant distribution folder or generated release artifact.
#>
param(
    [Parameter(Mandatory = $true)][string]$DistributionDir,
    [string]$Version = "0.9.0",
    [string]$ReleaseChannel = "RC"
)

$ErrorActionPreference = "Stop"
$failures = @()

function Fail($message) {
    Write-Host "  FAIL: $message" -ForegroundColor Red
    $script:failures += $message
}

function Ok($message) {
    Write-Host "  OK: $message" -ForegroundColor Green
}

function Test-RequiredFile($path) {
    if (Test-Path $path) {
        Ok $path
    }
    else {
        Fail "missing file: $path"
    }
}

Write-Host "Distribution dir: $DistributionDir"
if (-not (Test-Path $DistributionDir)) {
    Fail "distribution directory does not exist"
}

$setupName = "CADWorkAssistant-Setup-$Version-$ReleaseChannel-x64.exe"
$zipName = "CADWorkAssistant-$Version-$ReleaseChannel-x64.zip"
$releaseNotesName = "RELEASE_NOTES_$Version-$ReleaseChannel.md"
$manualName = "CAD_Work_Assistant_User_Guide_ko-KR.pdf"

$setupPath = Join-Path $DistributionDir $setupName
$setupShaPath = "$setupPath.sha256"
$manualPath = Join-Path $DistributionDir $manualName
$releaseNotesPath = Join-Path $DistributionDir $releaseNotesName
$readmePath = Join-Path $DistributionDir "README_FIRST.txt"
$manifestPath = Join-Path $DistributionDir "release-manifest.json"

Write-Host ""
Write-Host "=== Required files ===" -ForegroundColor Cyan
Test-RequiredFile $setupPath
Test-RequiredFile $setupShaPath
Test-RequiredFile $manualPath
Test-RequiredFile $releaseNotesPath
Test-RequiredFile $readmePath
Test-RequiredFile $manifestPath

Write-Host ""
Write-Host "=== SHA256 ===" -ForegroundColor Cyan
if ((Test-Path $setupPath) -and (Test-Path $setupShaPath)) {
    $actual = (Get-FileHash $setupPath -Algorithm SHA256).Hash
    $recorded = (Get-Content $setupShaPath -Raw).Trim().Split(" ", [System.StringSplitOptions]::RemoveEmptyEntries)[0]
    if ($actual -eq $recorded) {
        Ok "setup SHA256 matches"
    }
    else {
        Fail "setup SHA256 mismatch"
    }
}

Write-Host ""
Write-Host "=== Version content ===" -ForegroundColor Cyan
if (Test-Path $releaseNotesPath) {
    $releaseNotes = Get-Content $releaseNotesPath -Raw
    if ($releaseNotes.Contains("CAD Work Assistant $Version Release Candidate")) {
        Ok "release notes version"
    }
    else {
        Fail "release notes version mismatch"
    }
}
if (Test-Path $readmePath) {
    $readme = Get-Content $readmePath -Raw
    if ($readme.Contains("CAD Work Assistant $Version Release Candidate")) {
        Ok "README version"
    }
    else {
        Fail "README version mismatch"
    }
}
if (Test-Path $manifestPath) {
    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.version -eq $Version -and $manifest.channel -eq $ReleaseChannel) {
        Ok "manifest version/channel"
    }
    else {
        Fail "manifest version/channel mismatch"
    }
}

Write-Host ""
Write-Host "=== Forbidden files ===" -ForegroundColor Cyan
$forbiddenPatterns = @(
    "*.cs",
    "*.xaml",
    "*.pdb",
    "*.Tests.dll",
    "*FakeAutoCad*",
    "node.exe",
    "python.exe",
    "node_modules",
    ".claude",
    ".21st",
    "acdbmgd.dll",
    "acmgd.dll",
    "accoremgd.dll"
)
foreach ($pattern in $forbiddenPatterns) {
    $hits = Get-ChildItem -Path $DistributionDir -Recurse -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like $pattern -or $_.FullName -like "*\$pattern" }
    if ($hits.Count -gt 0) {
        Fail "forbidden artifact '$pattern' found: $($hits[0].FullName)"
    }
}
if ($failures.Count -eq 0) {
    Ok "no forbidden artifacts in distribution folder"
}

Write-Host ""
if ($failures.Count -eq 0) {
    Write-Host "DISTRIBUTION VERIFICATION PASSED" -ForegroundColor Green
    exit 0
}

Write-Host "$($failures.Count) CHECK(S) FAILED:" -ForegroundColor Red
foreach ($failure in $failures) {
    Write-Host "  - $failure" -ForegroundColor Red
}
exit 1
