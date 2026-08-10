<#
.SYNOPSIS
  CAD Work Assistant distribution release-candidate build.

.DESCRIPTION
  Repository preflight -> clean -> restore -> release build -> full tests -> manual PDF ->
  desktop publish -> AutoCAD plugin bundle -> runtime audit -> installer -> installer smoke ->
  SHA256 -> distribution folder -> release manifest -> ZIP.

  Formal distribution mode fails when tracked source is dirty. Use -AllowDirty only for local
  validation before committing release changes.
#>
param(
    [string]$Configuration = "Release",
    [switch]$SkipTests,
    [switch]$SkipPlugin,
    [switch]$SkipInstaller,
    [switch]$SkipInstallerSmoke,
    [switch]$AllowDirty
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

function Get-BuildProperty([string]$name) {
    [xml]$buildProps = Get-Content "$RepoRoot\Directory.Build.props"
    $node = $buildProps.Project.PropertyGroup | Where-Object { $_.$name } | Select-Object -First 1
    if (-not $node) { return $null }
    return $node.$name
}

function Get-CommitHash {
    git rev-parse --short=12 HEAD
    Assert-LastExitCode "git rev-parse"
}

Write-Step "Repository preflight"
$gitStatus = git status --porcelain
Assert-LastExitCode "git status"
$trackedDirty = @($gitStatus | Where-Object { $_ -notmatch '^\?\? ' })
$untracked = @($gitStatus | Where-Object { $_ -match '^\?\? ' })
$releaseUntracked = @()
if ($trackedDirty.Count -gt 0 -and -not $AllowDirty) {
    Write-Host "FAILED: tracked source is dirty. Commit or stash changes before a formal release." -ForegroundColor Red
    $trackedDirty | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}
if ($trackedDirty.Count -gt 0) {
    Write-Host "WARNING: tracked source is dirty because -AllowDirty was used." -ForegroundColor Yellow
    $trackedDirty | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
}
foreach ($item in $untracked) {
    if ($item -eq "?? .claude/scheduled_tasks.lock") {
        Write-Host "Ignoring development lock file: .claude/scheduled_tasks.lock" -ForegroundColor Yellow
    }
    else {
        $releaseUntracked += $item
        Write-Host "WARNING: untracked file is not release content: $item" -ForegroundColor Yellow
    }
}

$commit = Get-CommitHash
$version = Get-BuildProperty "CwaVersion"
$releaseChannel = Get-BuildProperty "ReleaseChannel"
if (-not $version) {
    Write-Host "FAILED: CwaVersion missing from Directory.Build.props" -ForegroundColor Red
    exit 1
}
if (-not $releaseChannel) {
    Write-Host "FAILED: ReleaseChannel missing from Directory.Build.props" -ForegroundColor Red
    exit 1
}
Write-Host "Version: $version"
Write-Host "Channel: $releaseChannel"
Write-Host "Commit: $commit"

$ArtifactsDir = Join-Path $RepoRoot "artifacts"
$PublishRoot = Join-Path $RepoRoot "publish"
$PublishDir = Join-Path $PublishRoot "desktop"
$BundleDir = Join-Path $RepoRoot "installer\CADWorkAssistant.bundle"
$BundleWindowsDir = Join-Path $BundleDir "Contents\Windows"
$InstallerOutputDir = Join-Path $ArtifactsDir "installer"
$ManualOutputDir = Join-Path $ArtifactsDir "manual"
$ReleaseFolderName = "CADWorkAssistant-$version-$releaseChannel"
$ReleaseDir = Join-Path $ArtifactsDir "release\$ReleaseFolderName"
$ZipPath = Join-Path $ArtifactsDir "release\CADWorkAssistant-$version-$releaseChannel-x64.zip"
$ManualPdfName = "CAD_Work_Assistant_User_Guide_ko-KR.pdf"
$SetupFileName = "CADWorkAssistant-Setup-$version-$releaseChannel-x64.exe"
$ReleaseNotesName = "RELEASE_NOTES_$version-$releaseChannel.md"

Write-Step "Clean"
Remove-Item -Recurse -Force $ArtifactsDir -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force $PublishRoot -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force $BundleWindowsDir -ErrorAction SilentlyContinue
dotnet clean "$RepoRoot\CADWorkAssistant.CI.slnf" -v q | Out-Null
Assert-LastExitCode "clean"

Write-Step "Restore"
dotnet restore "$RepoRoot\CADWorkAssistant.CI.slnf"
Assert-LastExitCode "restore"

Write-Step "Build (CADWorkAssistant.CI.slnf, $Configuration)"
dotnet build "$RepoRoot\CADWorkAssistant.CI.slnf" -c $Configuration --no-restore
Assert-LastExitCode "build"

if (-not $SkipTests) {
    Write-Step "Full tests"
    dotnet test "$RepoRoot\CADWorkAssistant.CI.slnf" -c $Configuration --no-build
    Assert-LastExitCode "test"
}
else {
    Write-Host "Tests skipped (-SkipTests)" -ForegroundColor Yellow
}

Write-Step "Publish Desktop (self-contained win-x64)"
dotnet publish "$RepoRoot\src\CADWorkAssistant.Desktop\CADWorkAssistant.Desktop.csproj" `
    -c $Configuration -r win-x64 --self-contained true -o $PublishDir `
    /p:PublishSingleFile=false /p:PublishTrimmed=false /p:DebugType=None /p:DebugSymbols=false
Assert-LastExitCode "publish desktop"

Write-Step "Build User Manual PDF"
$manualBuilderExe = Join-Path $RepoRoot "tools\CADWorkAssistant.ManualBuilder\bin\$Configuration\net8.0\CADWorkAssistant.ManualBuilder.exe"
if (-not (Test-Path $manualBuilderExe)) {
    Write-Host "FAILED: ManualBuilder not found at $manualBuilderExe" -ForegroundColor Red
    exit 1
}
New-Item -ItemType Directory -Force -Path $ManualOutputDir | Out-Null
$manualSourceMd = Join-Path $RepoRoot "docs\user-guide\ko-KR\USER_GUIDE.md"
$manualPdfPath = Join-Path $ManualOutputDir $ManualPdfName
& $manualBuilderExe $manualSourceMd $manualPdfPath
Assert-LastExitCode "user manual build"
$manualDestDir = Join-Path $PublishDir "Documentation"
New-Item -ItemType Directory -Force -Path $manualDestDir | Out-Null
Copy-Item $manualPdfPath (Join-Path $manualDestDir $ManualPdfName) -Force

if (-not $SkipPlugin) {
    Write-Step "Build AutoCAD Plugin (net48)"
    dotnet build "$RepoRoot\src\CADWorkAssistant.AutoCAD\CADWorkAssistant.AutoCAD.csproj" -c $Configuration
    Assert-LastExitCode "build plugin"

    Write-Step "Stage AutoCAD Bundle"
    $pluginBinDir = Join-Path $RepoRoot "src\CADWorkAssistant.AutoCAD\bin\$Configuration\net48"
    New-Item -ItemType Directory -Force -Path $BundleWindowsDir | Out-Null
    Copy-Item "$pluginBinDir\CADWorkAssistant.AutoCAD.dll" $BundleWindowsDir -Force
    Copy-Item "$pluginBinDir\CADWorkAssistant.Core.dll" $BundleWindowsDir -Force -ErrorAction SilentlyContinue
    Copy-Item "$pluginBinDir\CADWorkAssistant.Infrastructure.dll" $BundleWindowsDir -Force -ErrorAction SilentlyContinue
    Copy-Item "$pluginBinDir\Serilog*.dll" $BundleWindowsDir -Force -ErrorAction SilentlyContinue

    $manifestPath = Join-Path $BundleDir "PackageContents.xml"
    $manifest = Get-Content $manifestPath -Raw -Encoding UTF8
    $manifest = [regex]::Replace($manifest, 'AppVersion="[^"]*"', "AppVersion=`"$version`"")
    Set-Content -Path $manifestPath -Value $manifest -NoNewline -Encoding UTF8
}
else {
    Write-Host "Plugin build skipped (-SkipPlugin)" -ForegroundColor Yellow
}

Write-Step "Runtime Dependency Audit"
& "$PSScriptRoot\audit-runtime.ps1" -PublishDir $PublishDir -BundleDir $BundleDir
Assert-LastExitCode "runtime audit"

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

    New-Item -ItemType Directory -Force -Path $InstallerOutputDir | Out-Null
    & $iscc "/DAppVersion=$version" "/DReleaseChannel=$releaseChannel" "/DSourceDir=`"$PublishDir`"" "/DBundleDir=`"$BundleDir`"" "/DOutputDir=`"$InstallerOutputDir`"" `
        "$RepoRoot\installer\CADWorkAssistant.iss"
    Assert-LastExitCode "ISCC"

    $installerExePath = Join-Path $InstallerOutputDir $SetupFileName
    if (-not (Test-Path $installerExePath)) {
        Write-Host "FAILED: expected installer missing: $installerExePath" -ForegroundColor Red
        exit 1
    }

    if (-not $SkipInstallerSmoke) {
        Write-Step "Installer smoke test"
        & "$PSScriptRoot\test-release.ps1" -InstallerPath $installerExePath
        Assert-LastExitCode "installer smoke"
    }
    else {
        Write-Host "Installer smoke skipped (-SkipInstallerSmoke)" -ForegroundColor Yellow
    }

    Write-Step "SHA256"
    $installerHash = Get-FileHash $installerExePath -Algorithm SHA256
    "$($installerHash.Hash)  $SetupFileName" | Set-Content "$installerExePath.sha256" -Encoding UTF8

    Write-Step "Distribution folder"
    New-Item -ItemType Directory -Force -Path $ReleaseDir | Out-Null
    Copy-Item $installerExePath (Join-Path $ReleaseDir $SetupFileName) -Force
    Copy-Item "$installerExePath.sha256" (Join-Path $ReleaseDir "$SetupFileName.sha256") -Force
    Copy-Item $manualPdfPath (Join-Path $ReleaseDir $ManualPdfName) -Force

    $releaseNotesPath = Join-Path $RepoRoot "docs\releases\$ReleaseNotesName"
    if (-not (Test-Path $releaseNotesPath)) {
        Write-Host "FAILED: release notes missing at $releaseNotesPath" -ForegroundColor Red
        exit 1
    }
    Copy-Item $releaseNotesPath (Join-Path $ReleaseDir $ReleaseNotesName) -Force

    $readmeFirstSource = Join-Path $RepoRoot "docs\releases\README_FIRST.txt"
    if (-not (Test-Path $readmeFirstSource)) {
        Write-Host "FAILED: README_FIRST missing at $readmeFirstSource" -ForegroundColor Red
        exit 1
    }
    Copy-Item $readmeFirstSource (Join-Path $ReleaseDir "README_FIRST.txt") -Force

    $thirdParty = Join-Path $RepoRoot "THIRD_PARTY_NOTICES.txt"
    if (Test-Path $thirdParty) {
        Copy-Item $thirdParty (Join-Path $ReleaseDir "THIRD_PARTY_NOTICES.txt") -Force
    }

    $manifestObject = [ordered]@{
        product = "CAD Work Assistant"
        version = $version
        channel = $releaseChannel
        commit = $commit
        source = @{
            commit = $commit
            allowDirty = [bool]$AllowDirty
            trackedDirty = @($trackedDirty)
            untracked = @($releaseUntracked)
        }
        builtAt = (Get-Date).ToUniversalTime().ToString("o")
        configuration = $Configuration
        installer = @{
            file = $SetupFileName
            sha256 = $installerHash.Hash
            sizeBytes = (Get-Item $installerExePath).Length
        }
        manual = $ManualPdfName
        releaseNotes = $ReleaseNotesName
        unsigned = $true
        autoCadValidation = @{
            realAutoCadIntegration = "pending"
            realDrawingPlot = "pending"
            realTextTools = "pending"
        }
    }
    $manifestObject | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $ReleaseDir "release-manifest.json") -Encoding UTF8

    Write-Step "Verify distribution"
    & "$PSScriptRoot\verify-distribution.ps1" -DistributionDir $ReleaseDir -Version $version -ReleaseChannel $releaseChannel
    Assert-LastExitCode "verify distribution"

    Write-Step "ZIP"
    Remove-Item $ZipPath -Force -ErrorAction SilentlyContinue
    Compress-Archive -Path $ReleaseDir -DestinationPath $ZipPath -Force
    $zipHash = Get-FileHash $ZipPath -Algorithm SHA256
    "$($zipHash.Hash)  $(Split-Path $ZipPath -Leaf)" | Set-Content "$ZipPath.sha256" -Encoding UTF8
    Write-Host "Distribution: $ReleaseDir"
    Write-Host "ZIP: $ZipPath"
}
else {
    Write-Host "Installer build skipped (-SkipInstaller)" -ForegroundColor Yellow
}

Write-Step "Release build complete"
