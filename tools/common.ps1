# Shared by the tools: where the installed app lives, and a way to run it that never hangs.
# Dot-source it: . "$PSScriptRoot\common.ps1"
$AppName = 'KiteWin'
# APP_EXE runs another build: a publish folder, or a worktree's Debug build.
$AppExe = if ($env:APP_EXE) { $env:APP_EXE } else { Join-Path $env:LOCALAPPDATA "Programs\$AppName\$AppName.exe" }

# Runs the installed app with extra environment variables, prints its output, and returns its exit code.
# The app writes its output to the file in APP_LOG (AgentLog.cs): a WinUI app has no dependable stdout.
# The variables are set for the child only: the shell keeps its own values.
# A run that takes longer than -TimeoutSec is killed, so an agent never waits forever.
function Invoke-App {
    param([hashtable]$Environment = @{}, [string[]]$Arguments = @(), [int]$TimeoutSec = 180)
    if (-not (Test-Path $AppExe)) { Write-Host "not installed: $AppExe (run ./build.ps1)"; return 1 }
    $log = Join-Path $env:TEMP "$AppName-run-$PID.log"
    Remove-Item $log -ErrorAction SilentlyContinue
    $vars = @{ APP_LOG = $log } + $Environment
    $old = @{}
    foreach ($key in $vars.Keys) {
        $old[$key] = [Environment]::GetEnvironmentVariable($key)
        [Environment]::SetEnvironmentVariable($key, [string]$vars[$key])
    }
    try {
        $start = @{ FilePath = $AppExe; PassThru = $true }
        if ($Arguments.Count -gt 0) { $start.ArgumentList = $Arguments }
        $process = Start-Process @start
        $null = $process.Handle   # without this, ExitCode is empty after the process ends
        $finished = $process.WaitForExit($TimeoutSec * 1000)
        if (-not $finished) { Stop-Process -Id $process.Id -Force }
        # The app writes UTF-8; Windows PowerShell 5.1 would read it as the ANSI code page and garble every non-ASCII character.
        Get-Content $log -Encoding UTF8 -ErrorAction SilentlyContinue | ForEach-Object { Write-Host $_ }
        if (-not $finished) { Write-Host "TIMEOUT after $TimeoutSec s"; return 1 }
        if ($process.ExitCode -ne 0 -and $process.ExitCode -ne 1) {
            Write-Host ("CRASH exit code 0x{0:X8}: see Event Viewer > Windows Logs > Application" -f $process.ExitCode)
        }
        return $process.ExitCode
    }
    finally {
        foreach ($key in $old.Keys) { [Environment]::SetEnvironmentVariable($key, $old[$key]) }
        Remove-Item $log -ErrorAction SilentlyContinue
    }
}

# Deletes a SQLite store with its -wal and -shm files and its test settings.
function Remove-Store([string]$Path) {
    Remove-Item "$Path", "$Path-wal", "$Path-shm", "$Path.settings.json" -Force -ErrorAction SilentlyContinue
}
