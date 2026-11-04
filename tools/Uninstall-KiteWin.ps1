<#
.SYNOPSIS
    Clean uninstaller for KiteWin on Windows 11.
#>
$ErrorActionPreference = 'SilentlyContinue'

$AppName = "KiteWin"
$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\$AppName"

Write-Host "Uninstalling $AppName..." -ForegroundColor Cyan

# Stop running processes
Get-Process -Name $AppName -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

# Remove shortcuts
$StartShortcut = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\$AppName.lnk"
$DeskShortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) "$AppName.lnk"

Remove-Item $StartShortcut -Force
Remove-Item $DeskShortcut -Force

# Remove install directory
Remove-Item $InstallDir -Recurse -Force

# Remove registry key
Remove-Item "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppName" -Recurse -Force

Write-Host "$AppName has been cleanly uninstalled." -ForegroundColor Green
