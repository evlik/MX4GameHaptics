# MX4 Game Haptics - Installation Script
# Run: powershell -ExecutionPolicy Bypass -File install.ps1

param(
    [switch]$Uninstall
)

$ErrorActionPreference = "Stop"

$installDir = "$env:LOCALAPPDATA\MX4GameHaptics"
$startMenu = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "MX4 Game Haptics Installer" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan
Write-Host ""

if ($Uninstall) {
    Write-Host "Uninstalling..." -ForegroundColor Yellow

    # Remove Start Menu shortcuts
    $shortcuts = @(
        "$startMenu\MX4 Haptic Service.lnk",
        "$startMenu\MX4 Haptic Configurator.lnk"
    )
    foreach ($shortcut in $shortcuts) {
        if (Test-Path $shortcut) {
            Remove-Item $shortcut -Force
            Write-Host "Removed: $shortcut" -ForegroundColor Gray
        }
    }

    # Remove install directory
    if (Test-Path $installDir) {
        Remove-Item $installDir -Recurse -Force
        Write-Host "Removed: $installDir" -ForegroundColor Gray
    }

    Write-Host ""
    Write-Host "Uninstalled successfully!" -ForegroundColor Green
    exit 0
}

# Build projects
Write-Host "Building projects..." -ForegroundColor Yellow

$projectRoot = Split-Path -Parent $scriptDir

Push-Location $projectRoot
try {
    # Build MX4HapticService
    Write-Host "  Building MX4HapticService..." -ForegroundColor Gray
    dotnet publish tools/MX4HapticService -c Release -o "$installDir" --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "Failed to build MX4HapticService" }

    # Build HapticConfigurator
    Write-Host "  Building HapticConfigurator..." -ForegroundColor Gray
    dotnet publish tools/HapticConfigurator -c Release -o "$installDir" --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "Failed to build HapticConfigurator" }
}
finally {
    Pop-Location
}

Write-Host "  Done!" -ForegroundColor Green
Write-Host ""

# Create Start Menu shortcuts
Write-Host "Creating Start Menu shortcuts..." -ForegroundColor Yellow

$shell = New-Object -ComObject WScript.Shell

# MX4 Haptic Service shortcut
$shortcut = $shell.CreateShortcut("$startMenu\MX4 Haptic Service.lnk")
$shortcut.TargetPath = "$installDir\MX4HapticService.exe"
$shortcut.WorkingDirectory = $installDir
$shortcut.Description = "MX4 Game Haptics - System Tray Service"
$iconPath = "$installDir\app.ico"
if (Test-Path $iconPath) {
    $shortcut.IconLocation = $iconPath
}
$shortcut.Save()
Write-Host "  Created: MX4 Haptic Service" -ForegroundColor Gray

# HapticConfigurator shortcut
$shortcut = $shell.CreateShortcut("$startMenu\MX4 Haptic Configurator.lnk")
$shortcut.TargetPath = "$installDir\HapticConfigurator.exe"
$shortcut.WorkingDirectory = $installDir
$shortcut.Description = "MX4 Game Haptics - Configuration Tool"
if (Test-Path $iconPath) {
    $shortcut.IconLocation = $iconPath
}
$shortcut.Save()
Write-Host "  Created: MX4 Haptic Configurator" -ForegroundColor Gray

Write-Host ""
Write-Host "Installation complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Installed to: $installDir" -ForegroundColor Cyan
Write-Host ""
Write-Host "You can now:" -ForegroundColor White
Write-Host "  1. Search 'MX4 Haptic' in Start Menu" -ForegroundColor Gray
Write-Host "  2. Run MX4 Haptic Service (runs in system tray)" -ForegroundColor Gray
Write-Host "  3. Use MX4 Haptic Configurator to adjust settings" -ForegroundColor Gray
Write-Host ""
Write-Host "To uninstall: .\install.ps1 -Uninstall" -ForegroundColor DarkGray
