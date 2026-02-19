# MX4 Game Haptics - Build Installer
# This script builds the applications and creates the installer

param(
    [switch]$SkipBuild,
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir
$publishDir = Join-Path $scriptDir "publish"

Write-Host "MX4 Game Haptics - Build Installer" -ForegroundColor Cyan
Write-Host "===================================" -ForegroundColor Cyan
Write-Host ""

# Clean publish directory
if (Test-Path $publishDir) {
    Write-Host "Cleaning publish directory..." -ForegroundColor Yellow
    Remove-Item -Path $publishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path "$publishDir\service" -Force | Out-Null
New-Item -ItemType Directory -Path "$publishDir\configurator" -Force | Out-Null

if (-not $SkipBuild) {
    Write-Host ""
    Write-Host "Building MX4HapticService..." -ForegroundColor Green
    dotnet publish "$rootDir\tools\MX4HapticService" `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -o "$publishDir\service"

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        exit 1
    }

    Write-Host ""
    Write-Host "Building HapticConfigurator..." -ForegroundColor Green
    dotnet publish "$rootDir\tools\HapticConfigurator" `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -o "$publishDir\configurator"

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        exit 1
    }

    Write-Host ""
    Write-Host "Build completed!" -ForegroundColor Green
}

# Check for Inno Setup
$innoPath = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $SkipInstaller) {
    if ($innoPath) {
        Write-Host ""
        Write-Host "Building installer with Inno Setup..." -ForegroundColor Green

        # Create output directory
        $outputDir = Join-Path $scriptDir "output"
        if (-not (Test-Path $outputDir)) {
            New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
        }

        & $innoPath "$scriptDir\setup.iss"

        if ($LASTEXITCODE -eq 0) {
            Write-Host ""
            Write-Host "Installer created successfully!" -ForegroundColor Green
            Write-Host "Output: $outputDir" -ForegroundColor Cyan
            Get-ChildItem $outputDir -Filter "*.exe" | ForEach-Object {
                Write-Host "  - $($_.Name)" -ForegroundColor White
            }
        } else {
            Write-Host "Installer build failed!" -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host ""
        Write-Host "Inno Setup not found!" -ForegroundColor Yellow
        Write-Host "Download from: https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Self-contained builds are ready in: $publishDir" -ForegroundColor Cyan
        Write-Host "You can run the Inno Setup script manually after installing Inno Setup." -ForegroundColor White
    }
}

Write-Host ""
Write-Host "Done!" -ForegroundColor Green
