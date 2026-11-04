<#
.SYNOPSIS
    One-click per-user installer for KiteWin on Windows 11.
    Installs into %LOCALAPPDATA%\Programs\KiteWin, creates Start Menu & Desktop shortcuts.
    Zero UAC / Administrator prompt needed.
#>
[CmdletBinding()]
param(
    [string]$SourcePath,
    [switch]$LaunchAfterInstall = $true
)

$ErrorActionPreference = 'Stop'

$AppName = "KiteWin"
$Version = "1.0.0"
$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\$AppName"
$ExePath = Join-Path $InstallDir "$AppName.exe"
$IconPath = Join-Path $InstallDir "Assets\AppIcon.ico"
$Root = Split-Path $PSScriptRoot -Parent

Write-Host "Installing $AppName v$Version for current user..." -ForegroundColor Cyan

# 1. Determine Source Directory
if (-not $SourcePath) {
    # Check publish directories
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "Dev\$AppName\publish\self-contained"),
        (Join-Path $env:LOCALAPPDATA "Dev\$AppName\publish\aot"),
        (Join-Path $Root "src\$AppName\bin\Release\net10.0-windows10.0.26100.0\win-x64\publish"),
        (Join-Path $Root "dist\publish")
    )
    foreach ($cand in $candidates) {
        if (Test-Path "$cand\$AppName.exe") {
            $SourcePath = $cand
            break
        }
    }
}

if (-not $SourcePath -or -not (Test-Path "$SourcePath\$AppName.exe")) {
    Write-Host "Source build not found. Running tools/publish.ps1 first..." -ForegroundColor Yellow
    & "$PSScriptRoot\publish.ps1"
    $SourcePath = Join-Path $env:LOCALAPPDATA "Dev\$AppName\publish\self-contained"
}

if (-not (Test-Path "$SourcePath\$AppName.exe")) {
    Write-Error "Cannot find $AppName.exe in source directory: $SourcePath"
    exit 1
}

# 2. Stop running instances if any
Get-Process -Name $AppName -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

# 3. Copy files to %LOCALAPPDATA%\Programs\KiteWin
Write-Host "Copying application files to $InstallDir..." -ForegroundColor Gray
if (Test-Path $InstallDir) {
    Remove-Item $InstallDir -Recurse -Force
}
New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
Copy-Item -Path "$SourcePath\*" -Destination $InstallDir -Recurse -Force

# Ensure icon exists
$assetsDir = Join-Path $InstallDir "Assets"
New-Item -ItemType Directory -Path $assetsDir -Force | Out-Null
if (Test-Path "$Root\src\$AppName\Assets\AppIcon.ico") {
    Copy-Item "$Root\src\$AppName\Assets\AppIcon.ico" $IconPath -Force
}

# 4. Create Start Menu Shortcut
$StartMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$StartShortcutPath = Join-Path $StartMenuDir "$AppName.lnk"

$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut($StartShortcutPath)
$Shortcut.TargetPath = $ExePath
$Shortcut.WorkingDirectory = $InstallDir
if (Test-Path $IconPath) { $Shortcut.IconLocation = "$IconPath,0" }
$Shortcut.Description = "Kite AI Studio & Prompt Translator for Windows 11"
$Shortcut.Save()
Write-Host "Created Start Menu shortcut: $StartShortcutPath" -ForegroundColor Green

# 5. Create Desktop Shortcut
$DesktopDir = [Environment]::GetFolderPath('Desktop')
$DesktopShortcutPath = Join-Path $DesktopDir "$AppName.lnk"
$DeskShortcut = $WshShell.CreateShortcut($DesktopShortcutPath)
$DeskShortcut.TargetPath = $ExePath
$DeskShortcut.WorkingDirectory = $InstallDir
if (Test-Path $IconPath) { $DeskShortcut.IconLocation = "$IconPath,0" }
$DeskShortcut.Description = "Kite AI Studio & Prompt Translator for Windows 11"
$DeskShortcut.Save()
Write-Host "Created Desktop shortcut: $DesktopShortcutPath" -ForegroundColor Green

# 6. Register Uninstaller in Windows Registry (Settings > Apps)
$UninstallRegKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppName"
if (-not (Test-Path $UninstallRegKey)) {
    New-Item -Path $UninstallRegKey -Force | Out-Null
}
Set-ItemProperty -Path $UninstallRegKey -Name "DisplayName" -Value "$AppName (AI Studio)"
Set-ItemProperty -Path $UninstallRegKey -Name "DisplayVersion" -Value $Version
Set-ItemProperty -Path $UninstallRegKey -Name "Publisher" -Value "Antigravity Team"
Set-ItemProperty -Path $UninstallRegKey -Name "InstallLocation" -Value $InstallDir
Set-ItemProperty -Path $UninstallRegKey -Name "DisplayIcon" -Value "$IconPath,0"
Set-ItemProperty -Path $UninstallRegKey -Name "UninstallString" -Value "powershell.exe -ExecutionPolicy Bypass -File `"$PSScriptRoot\Uninstall-KiteWin.ps1`""
Set-ItemProperty -Path $UninstallRegKey -Name "NoModify" -Value 1
Set-ItemProperty -Path $UninstallRegKey -Name "NoRepair" -Value 1

Write-Host "Installation completed successfully!" -ForegroundColor Green

# 7. Launch if requested
if ($LaunchAfterInstall) {
    Write-Host "Launching $AppName..." -ForegroundColor Cyan
    Start-Process -FilePath $ExePath -WorkingDirectory $InstallDir
}
