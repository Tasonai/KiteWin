# Kite Agent CLI for Windows PowerShell
# Connects to KiteWin on localhost:24243
[CmdletBinding()]
param(
    [Parameter(Position=0)]
    [string]$Command = "status",
    [Parameter(Position=1)]
    [string]$Arg1 = "",
    [string]$Concept = "",
    [string]$Platform = "web",
    [string]$Style = "Editorial & Swiss Modern",
    [switch]$Starred
)

$Endpoint = "http://127.0.0.1:24243"

switch ($Command) {
    "status" {
        $res = Invoke-RestMethod -Uri "$Endpoint/api/status" -Method Get
        Write-Host "KiteWin Station: $($res.app)"
        Write-Host "Status:          $($res.status)"
        Write-Host "Port:            $($res.port)"
    }
    "list" {
        $res = Invoke-RestMethod -Uri "$Endpoint/api/ideas" -Method Get
        $res.ideas | Format-Table -Property isStarred, id, status, platform, title
    }
    "add" {
        if (-not $Arg1) { Write-Error "Please specify an idea title."; exit 1 }
        $payload = @{
            ideas = @(
                @{
                    title = $Arg1
                    description = $Concept
                    platform = $Platform
                    preferredStyle = $Style
                    status = "backlog"
                    isStarred = $Starred.IsPresent
                }
            )
        } | ConvertTo-Json -Depth 5
        $res = Invoke-RestMethod -Uri "$Endpoint/api/ideas" -Method Post -Body $payload -ContentType "application/json"
        Write-Host "Added idea: $Arg1 (Imported: $($res.imported))"
    }
    "canvas" {
        $res = Invoke-RestMethod -Uri "$Endpoint/api/canvas" -Method Get
        $res | ConvertTo-Json -Depth 5
    }
    Default {
        Write-Host "Usage: .\kite.ps1 [status | list | add <title> | canvas]"
    }
}
