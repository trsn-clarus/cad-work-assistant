<#
.SYNOPSIS
  CAD Work Assistant - one-command production release build (Milestone 8 sections 120-122).

.DESCRIPTION
  Clean -> Restore -> Build -> Test -> Publish Desktop -> Build Plugin -> Stage Bundle ->
  Audit Runtime -> Build Installer -> Smoke Test -> Hash.
  A test failure stops the script immediately (section 123) - the Installer is only produced from
  a build that passed the full test suite.

.PARAMETER SkipPlugin
  Use on a machine without AutoCAD installed to release Desktop-only (see docs/AUTOCAD_INTEGRATION.md).

.PARAMETER SkipInstaller
  Stop after the publish output, without requiring Inno Setup to be installed.
#>
param(
    [string]$Configuration = "Release",
    [switch]$SkipTests,
    [switch]$SkipPlugin,
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

function Write-Step($text) {
    Write-Host ""
    Write-Host "=== $text ===" -ForegroundColor Cyan
}

function Assert-LastExitCode($step) {
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAILED: $step (exit code $LASTEXITCODE)" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

# --- Version Source of Truth (Directory.Build.props) ---
Write-Step "Resolving version from Directory.Build.props"
[xml]$buildProps = Get-Content "$RepoRoot\Directory.Build.props"
$version = ($buildProps.Project.PropertyGroup | Where-Object { $_.CwaVersion } | Select-Object -First 1).CwaVersion
if (-not $version) {
    Write-Host "FAILED: could not read CwaVersion from Directory.Build.props" -ForegroundColor Red
    exit 1
}
Write-Host "Version: $version"

$ArtifactsDir = Join-Path $RepoRoot "artifacts"
$PublishDir = Join-Path $RepoRoot "publish\desktop"
$BundleDir = Join-Path $RepoRoot "installer\CADWorkAssistant.bundle"
$BundleWindowsDir = Join-Path $BundleDir "Contents\Windows"
$InstallerOutputDir = Join-Path $ArtifactsDir "installer"

# --- Clean ---
Write-Step "Clean"
Remove-Item -Recurse -Force $ArtifactsDir -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force (Join-Path $RepoRoot "publish") -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force $BundleWindowsDir -ErrorAction SilentlyContinue
dotnet clean "$RepoRoot\CADWorkAssistant.CI.slnf" -v q | Out-Null

# --- Restore ---
Write-Step "Restore"
dotnet restore "$RepoRoot\CADWorkAssistant.CI.slnf"
Assert-LastExitCode "restore"

# --- Build (CI slnf - projects that build without AutoCAD installed) ---
Write-Step "Build (CADWorkAssistant.CI.slnf, $Configuration)"
dotnet build "$RepoRoot\CADWorkAssistant.CI.slnf" -c $Configuration --no-restore
Assert-LastExitCode "build"

# --- Test ---
if (-not $SkipTests) {
    Write-Step "Test (all tests must pass before an Installer is built)"
    dotnet test "$RepoRoot\CADWorkAssistant.CI.slnf" -c $Configuration --no-build
    Assert-LastExitCode "test"
}
else {
    Write-Host "Tests skipped (-SkipTests)" -ForegroundColor Yellow
}

# --- Publish Desktop (self-contained win-x64) ---
Write-Step "Publish Desktop (self-contained win-x64)"
dotnet publish "$RepoRoot\src\CADWorkAssistant.Desktop\CADWorkAssistant.Desktop.csproj" `
    -c $Configuration -r win-x64 --self-contained true -o $PublishDir `
    /p:PublishSingleFile=false /p:PublishTrimmed=false
Assert-LastExitCode "publish desktop"

# --- Build Plugin + Stage Bundle ---
if (-not $SkipPlugin) {
    Write-Step "Build AutoCAD Plugin (net48)"
    dotnet build "$RepoRoot\src\CADWorkAssistant.AutoCAD\CADWorkAssistant.AutoCAD.csproj" -c $Configuration
    Assert-LastExitCode "build plugin"

    Write-Step "Stage AutoCAD Bundle"
    $pluginBinDir = Join-Path $RepoRoot "src\CADWorkAssistant.AutoCAD\bin\$Configuration\net48"
    New-Item -ItemType Directory -Force -Path $BundleWindowsDir | Out-Null
    # Entry DLL + its own project-reference dependencies (Core/Infrastructure). Autodesk's host DLLs
    # (acdbmgd.dll etc.) are never here in the first place - the csproj references them Private=false
    # (verified: section 113).
    Copy-Item "$pluginBinDir\CADWorkAssistant.AutoCAD.dll" $BundleWindowsDir -Force
    Copy-Item "$pluginBinDir\CADWorkAssistant.Core.dll" $BundleWindowsDir -Force -ErrorAction SilentlyContinue
    Copy-Item "$pluginBinDir\CADWorkAssistant.Infrastructure.dll" $BundleWindowsDir -Force -ErrorAction SilentlyContinue
    Copy-Item "$pluginBinDir\Serilog*.dll" $BundleWindowsDir -Force -ErrorAction SilentlyContinue

    # Keep PackageContents.xml's AppVersion in sync with Directory.Build.props (sections 124-125) -
    # the version is not hand-maintained separately in this file.
    $manifestPath = Join-Path $BundleDir "PackageContents.xml"
    $manifest = Get-Content $manifestPath -Raw -Encoding UTF8
    $manifest = [regex]::Replace($manifest, 'AppVersion="[^"]*"', "AppVersion=`"$version`"")
    Set-Content -Path $manifestPath -Value $manifest -NoNewline -Encoding UTF8

    $dllCount = (Get-ChildItem $BundleWindowsDir -Filter "*.dll").Count
    Write-Host "Bundle staged: $dllCount dll(s) in $BundleWindowsDir"
}
else {
    Write-Host "Plugin build skipped (-SkipPlugin) - AutoCAD not available on this machine" -ForegroundColor Yellow
}

# --- Runtime/Network/LLM Audit ---
Write-Step "Runtime Dependency Audit"
& "$PSScriptRoot\audit-runtime.ps1" -PublishDir $PublishDir -BundleDir $BundleDir
Assert-LastExitCode "runtime audit"

# --- Build Installer ---
if (-not $SkipInstaller) {
    Write-Step "Build Installer (Inno Setup)"
    $isccCandidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    $iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $iscc) {
        Write-Host "FAILED: ISCC.exe (Inno Setup) not found. Install with: winget install --id JRSoftware.InnoSetup -e" -ForegroundColor Red
        exit 1
    }
    Write-Host "Using $iscc"

    New-Item -ItemType Directory -Force -Path $InstallerOutputDir | Out-Null
    # ISCC re-parses the text after each /D itself and splits on unescaped spaces, so a value
    # containing spaces (this repo's own path does, via the Korean "Desktop" folder name) needs its
    # OWN embedded quotes, not just the outer PowerShell argv quoting - discovered the hard way when
    # /DSourceDir=<path with a space> silently truncated the value and broke downstream [Files] parsing.
    & $iscc "/DAppVersion=$version" "/DSourceDir=`"$PublishDir`"" "/DBundleDir=`"$BundleDir`"" "/DOutputDir=`"$InstallerOutputDir`"" `
        "$RepoRoot\installer\CADWorkAssistant.iss"
    Assert-LastExitCode "ISCC"

    # --- Installer Smoke Test ---
    Write-Step "Installer Smoke Test"
    $installerExe = Get-ChildItem $InstallerOutputDir -Filter "CADWorkAssistant-Setup-*.exe" | Select-Object -First 1
    if (-not $installerExe) {
        Write-Host "FAILED: installer exe not found in $InstallerOutputDir" -ForegroundColor Red
        exit 1
    }
    if ($installerExe.Length -eq 0) {
        Write-Host "FAILED: installer exe is empty" -ForegroundColor Red
        exit 1
    }
    Write-Host "Installer: $($installerExe.FullName) ($([Math]::Round($installerExe.Length / 1MB, 1)) MB)"

    # --- Hash ---
    Write-Step "SHA256"
    $hash = Get-FileHash $installerExe.FullName -Algorithm SHA256
    "$($hash.Hash)  $($installerExe.Name)" | Set-Content "$($installerExe.FullName).sha256"
    Write-Host "$($hash.Hash)"
}
else {
    Write-Host "Installer build skipped (-SkipInstaller)" -ForegroundColor Yellow
}

Write-Step "Release build complete"
