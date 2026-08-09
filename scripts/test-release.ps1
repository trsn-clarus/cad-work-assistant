<#
.SYNOPSIS
  CAD Work Assistant - installer smoke test (Milestone 8 sections 170-172).

.DESCRIPTION
  Against a real silent install of the built Setup exe, checks:
  installer exists -> silent install -> files exist -> Desktop launches -> plugin bundle exists ->
  process alive -> uninstall -> binary removed -> user DB retained.
  Simulation Mode (FakeAutoCad) only - this script never launches the real AutoCAD GUI (see
  CLAUDE.md: launching the real AutoCAD 2024 GUI on this dev machine destabilizes the graphics
  driver, so real-GUI verification always needs an explicit human decision first).

.PARAMETER InstallerPath
  If omitted, uses the most recent artifacts\installer\CADWorkAssistant-Setup-*.exe.
#>
param(
    [string]$InstallerPath,
    [string]$InstallDir = "$env:LOCALAPPDATA\Programs\CAD Work Assistant Test"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$failures = @()

function Step($text) { Write-Host ""; Write-Host "=== $text ===" -ForegroundColor Cyan }
function Ok($text) { Write-Host "  OK: $text" -ForegroundColor Green }
function Fail($text) { Write-Host "  FAIL: $text" -ForegroundColor Red; $script:failures += $text }

Step "1. Installer exists"
if (-not $InstallerPath) {
    $found = Get-ChildItem "$RepoRoot\artifacts\installer" -Filter "CADWorkAssistant-Setup-*.exe" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($found) { $InstallerPath = $found.FullName }
}
if ($InstallerPath -and (Test-Path $InstallerPath)) {
    Ok "$InstallerPath"
}
else {
    Fail "no installer exe found"
    Write-Host ""
    Write-Host "Cannot continue without an installer. Run scripts\build-release.ps1 first." -ForegroundColor Red
    exit 1
}

Step "2. Silent install"
if (Test-Path $InstallDir) { Remove-Item -Recurse -Force $InstallDir }
$proc = Start-Process -FilePath $InstallerPath -ArgumentList "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/DIR=`"$InstallDir`"" -PassThru -Wait
if ($proc.ExitCode -eq 0) { Ok "installer exit code 0" } else { Fail "installer exit code $($proc.ExitCode)" }

Step "3. Files exist"
$exePath = Join-Path $InstallDir "CADWorkAssistant.Desktop.exe"
if (Test-Path $exePath) { Ok "$exePath" } else { Fail "$exePath not found" }

Step "4. Plugin bundle exists"
$bundlePath = "$env:APPDATA\Autodesk\ApplicationPlugins\CADWorkAssistant.bundle\PackageContents.xml"
$bundleDll = "$env:APPDATA\Autodesk\ApplicationPlugins\CADWorkAssistant.bundle\Contents\Windows\CADWorkAssistant.AutoCAD.dll"
if (Test-Path $bundlePath) { Ok "$bundlePath" } else { Fail "$bundlePath not found" }
if (Test-Path $bundleDll) { Ok "$bundleDll" } else { Fail "$bundleDll not found" }

Step "5. Desktop launches (Simulation Mode)"
$env:CWA_USE_FAKE_AUTOCAD = "1"
$testDbPath = "$env:LOCALAPPDATA\CADWorkAssistant\data\cadworkassistant.simulation.db"
if (Test-Path $exePath) {
    $desktopProc = Start-Process -FilePath $exePath -PassThru
    Start-Sleep -Seconds 3
    $alive = Get-Process -Id $desktopProc.Id -ErrorAction SilentlyContinue
    if ($alive) { Ok "process alive (PID $($desktopProc.Id))" } else { Fail "process exited immediately" }

    Step "6. User data file created"
    if (Test-Path $testDbPath) { Ok "$testDbPath" } else { Fail "$testDbPath not found after launch" }

    if ($alive) {
        Stop-Process -Id $desktopProc.Id -Force -ErrorAction SilentlyContinue
    }
}
else {
    Fail "cannot launch - exe missing"
}
Remove-Item Env:\CWA_USE_FAKE_AUTOCAD -ErrorAction SilentlyContinue

Step "7. Uninstall"
$uninstaller = Get-ChildItem $InstallDir -Filter "unins*.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($uninstaller) {
    $uninstProc = Start-Process -FilePath $uninstaller.FullName -ArgumentList "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART" -PassThru -Wait
    if ($uninstProc.ExitCode -eq 0) { Ok "uninstaller exit code 0" } else { Fail "uninstaller exit code $($uninstProc.ExitCode)" }
}
else {
    Fail "uninstaller (unins*.exe) not found in $InstallDir"
}

Step "8. Binary removed"
Start-Sleep -Seconds 1
if (-not (Test-Path $exePath)) { Ok "$exePath removed" } else { Fail "$exePath still present after uninstall" }

Step "9. User DB retained (uninstall must not delete project data)"
if (Test-Path $testDbPath) { Ok "$testDbPath retained" } else { Fail "$testDbPath was deleted by uninstall - THIS IS A DATA LOSS BUG" }

Write-Host ""
if ($failures.Count -eq 0) {
    Write-Host "ALL CHECKS PASSED" -ForegroundColor Green
    exit 0
}
else {
    Write-Host "$($failures.Count) CHECK(S) FAILED:" -ForegroundColor Red
    foreach ($f in $failures) { Write-Host "  - $f" -ForegroundColor Red }
    exit 1
}
