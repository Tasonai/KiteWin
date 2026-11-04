# Builds this checkout (Debug) without installing, then runs the self-test or snapshots against that build.
# For agents working in their own git worktree: nothing is installed, so nobody's installed app is closed or replaced.
# Usage: tools/dev.ps1 build
#        tools/dev.ps1 selftest
#        tools/dev.ps1 snap -Pages all,settings
param(
    [Parameter(Mandatory = $true)][ValidateSet('build', 'selftest', 'snap')][string]$Do,
    [string[]]$Pages = @()
)
$name = 'KiteWin'
$root = Split-Path $PSScriptRoot -Parent
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { $env:PATH = "$env:ProgramFiles\dotnet;$env:PATH" }
$output = dotnet build "$root\src\$name\$name.csproj" -c Debug --nologo 2>&1
$code = $LASTEXITCODE
$output | Where-Object { $_ -match ': (error|warning) ' } | ForEach-Object { ($_ -replace '\s+\[[^\]]+\.csproj.*\]$', '').Trim() } | Sort-Object -Unique
if ($code -ne 0) { Write-Output 'build failed'; exit 1 }
$arch = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }
$exe = Join-Path $env:LOCALAPPDATA "Dev\$name\$(Split-Path $root -Leaf)\bin\$name\debug_win-$arch\$name.exe"
Write-Output "built $exe"
if ($Do -eq 'build') { exit 0 }

$env:APP_EXE = $exe
try {
    if ($Do -eq 'selftest') { & "$PSScriptRoot\selftest.ps1" }
    else { & "$PSScriptRoot\snap.ps1" -Pages $Pages }
    exit $LASTEXITCODE
}
finally {
    Remove-Item Env:APP_EXE -ErrorAction SilentlyContinue
}
