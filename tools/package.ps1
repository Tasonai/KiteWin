# Packages KiteWin for distribution: creates standalone ZIP and Inno Setup installer.
param([switch]$Aot)
$ErrorActionPreference = 'Continue'
$name = 'KiteWin'
$version = '1.0.0'
$root = Split-Path $PSScriptRoot -Parent
$dist = Join-Path $root 'dist'
Remove-Item $dist -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $dist -Force | Out-Null

Write-Host "1. Publishing $name (self-contained/AOT)..." -ForegroundColor Cyan
$publishArgs = @('-Zip')
if ($Aot) { $publishArgs += '-Aot' }
& "$PSScriptRoot\publish.ps1" @publishArgs
if ($LASTEXITCODE -ne 0) { Write-Error "Publish failed"; exit 1 }

$kind = if ($Aot) { 'aot' } else { 'self-contained' }
$publishDir = Join-Path $env:LOCALAPPDATA "Dev\$name\publish\$kind"
$zipSource = Join-Path $env:LOCALAPPDATA "Dev\$name\publish\$name-$version-win-x64-$kind.zip"

# Copy zip to dist
$zipDest = Join-Path $dist "$name-$version-win-x64.zip"
if (Test-Path $zipSource) {
    Copy-Item $zipSource $zipDest -Force
    Write-Host "Copied release zip to $zipDest" -ForegroundColor Green
}

# 2. Check for Inno Setup compiler (ISCC.exe)
$isccPaths = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "ISCC.exe"
)
$iscc = $null
foreach ($p in $isccPaths) {
    if (Get-Command $p -ErrorAction SilentlyContinue) { $iscc = $p; break }
    if (Test-Path $p) { $iscc = $p; break }
}

if ($iscc) {
    Write-Host "2. Compiling Inno Setup installer..." -ForegroundColor Cyan
    & $iscc "$PSScriptRoot\installer.iss"
    Write-Host "Compiled Inno Setup installer in $dist" -ForegroundColor Green
} else {
    Write-Host "Inno Setup (ISCC.exe) not found. Packaging with PowerShell native installer..." -ForegroundColor Yellow
    Copy-Item "$PSScriptRoot\Install-$name.ps1" "$dist\Install-$name.ps1" -Force
    Copy-Item "$PSScriptRoot\Uninstall-$name.ps1" "$dist\Uninstall-$name.ps1" -Force
}

# 3. Generate Checksums
Write-Host "3. Generating SHA256 checksums..." -ForegroundColor Cyan
$checksumFile = Join-Path $dist "checksums.txt"
$files = Get-ChildItem -Path $dist -File | Where-Object { $_.Name -ne "checksums.txt" }
$hashes = foreach ($f in $files) {
    $h = Get-FileHash -Path $f.FullName -Algorithm SHA256
    "$($h.Hash.ToLower())  $($f.Name)"
}
$hashes | Out-File -FilePath $checksumFile -Encoding utf8
Write-Host "Packaging complete! Output directory: $dist" -ForegroundColor Green
Get-ChildItem $dist | Format-Table Name, Length
