# Builds KiteWin and installs it to %LOCALAPPDATA%\Programs\KiteWin, with a Start menu entry.
# Build products live in %LOCALAPPDATA%\Dev\KiteWin (Directory.Build.props), outside the repo
# (and outside OneDrive, which would try to sync them).
#
# Usage: ./build.ps1          release build, install
#        ./build.ps1 -Dev     debug build, install (faster while iterating)
#        ./build.ps1 -Open    install, then close the running copy and open the new one
param([switch]$Dev, [switch]$Open)
$ErrorActionPreference = 'Stop'
$name = 'KiteWin'
$here = $PSScriptRoot
$config = if ($Dev) { 'Debug' } else { 'Release' }
$install = Join-Path $env:LOCALAPPDATA "Programs\$name"

# A terminal opened before the .NET SDK was installed has no dotnet on its PATH yet.
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $sdk = Join-Path $env:ProgramFiles 'dotnet'
    if (Test-Path (Join-Path $sdk 'dotnet.exe')) { $env:PATH = "$sdk;$env:PATH" }
    else { Write-Output 'No .NET SDK. Install it with: winget install Microsoft.DotNet.SDK.10'; exit 1 }
}

# The icon: Icon\AppIcon.svg (and AppIcon-<size>.svg) -> Assets\AppIcon.ico + AppIcon.png, rebuilt when an SVG changed.
$svgs = @(Get-ChildItem (Join-Path $here 'Icon') -Filter 'AppIcon*.svg' -ErrorAction SilentlyContinue)
$ico = Join-Path $here "src\$name\Assets\AppIcon.ico"
if ($svgs.Count -gt 0 -and (-not (Test-Path $ico) -or @($svgs | Where-Object { $_.LastWriteTime -gt (Get-Item $ico).LastWriteTime }).Count -gt 0)) {
    & "$here\tools\make-icon.ps1"
    if ($LASTEXITCODE -ne 0) { Write-Output 'icon failed'; exit 1 }
}

# A running copy keeps its files locked. Close it first.
Get-Process -Name $name -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 300

$stamp = Get-Date -Format 'yyyyMMddHHmm'
# dotnet build, not dotnet publish: publish leaves out the app's .pri and compiled XAML
# for an unpackaged WinUI app, and the app then dies in InitializeComponent.
$output = dotnet build "$here\src\$name\$name.csproj" -c $config --nologo "-p:SourceRevisionId=$stamp" 2>&1
$code = $LASTEXITCODE
# Show errors and warnings once each, then the result. Fix every warning.
$output | Where-Object { $_ -match ': (error|warning) ' } | ForEach-Object { ($_ -replace '\s+\[[^\]]+\.csproj.*\]$', '').Trim() } | Sort-Object -Unique
if ($code -ne 0) { Write-Output 'build failed'; exit 1 }

# Mirror the build folder into the install folder: new files in, stale files out.
$arch = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }
$bin = Join-Path $env:LOCALAPPDATA "Dev\$name\$(Split-Path $here -Leaf)\bin\$name\$($config.ToLower())_win-$arch"
robocopy $bin $install /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { Write-Output "install failed: robocopy $bin $install"; exit 1 }
if (-not (Test-Path (Join-Path $install "$name.pri"))) { Write-Output "install failed: $name.pri is missing"; exit 1 }

# A Start menu entry, so the app is one search away and can be pinned to the taskbar.
$programs = [Environment]::GetFolderPath('Programs')
$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut((Join-Path $programs "$name.lnk"))
$link.TargetPath = Join-Path $install "$name.exe"
$link.WorkingDirectory = $install
$link.Save()
Write-Output "installed $install\$name.exe ($config, build $stamp)"

if ($Open) { Start-Process (Join-Path $install "$name.exe") }
# robocopy returns 1 when it copied files. That is success, so say so.
exit 0
