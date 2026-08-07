<#
.SYNOPSIS
    Runs the API with GC diagnostics on and packs the captured logs into a zip.

.DESCRIPTION
    Start this on the Windows machine that runs Dota 2, play until the client
    misbehaves, then press Ctrl+C. The script zips Logs/ (the rolling server log
    plus unhandled-gc.jsonl) so the whole capture can be sent in one file.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools/capture-gc-logs.ps1 -Urls "http://0.0.0.0:5199"
#>
[CmdletBinding()]
param(
    [string]$Urls = "http://0.0.0.0:5199",
    [string]$OutputDirectory = "captures"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $repoRoot "src/D2ST.Api"
$logDirectory = Join-Path $apiProject "Logs"
$captureDirectory = Join-Path $repoRoot $OutputDirectory

New-Item -ItemType Directory -Force -Path $captureDirectory | Out-Null

Write-Host "Starting D2STServer on $Urls. Play, then press Ctrl+C to stop and pack the logs." -ForegroundColor Cyan

try {
    dotnet run --project $apiProject --no-launch-profile --urls $Urls
}
finally {
    if (Test-Path $logDirectory) {
        $archive = Join-Path $captureDirectory ("d2st-capture-{0}.zip" -f (Get-Date -Format "yyyyMMdd-HHmmss"))
        Compress-Archive -Path (Join-Path $logDirectory "*") -DestinationPath $archive -Force
        Write-Host "Capture written to $archive" -ForegroundColor Green
    }
    else {
        Write-Warning "No logs were produced at $logDirectory."
    }
}
