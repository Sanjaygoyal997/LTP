<#
    Builds a self-contained deployment: the display and the .NET runtime are included, so the
    target machine needs neither Node nor .NET.

    The display is checked in already built, under /display, and the project links it as
    wwwroot. Node is therefore not needed to publish — only to change the display, which is
    what -RebuildDisplay is for.

    Usage:  .\publish.ps1 [-Output C:\CuringStatus] [-RebuildDisplay]
#>
param(
    [string]$Output = "$PSScriptRoot\publish",
    [switch]$RebuildDisplay
)

$ErrorActionPreference = 'Stop'

if ($RebuildDisplay) {
    Write-Host "Rebuilding the display..." -ForegroundColor Cyan
    Push-Location "$PSScriptRoot\frontend"
    npm ci
    npm run build
    Pop-Location

    $display = "$PSScriptRoot\display"
    if (Test-Path $display) { Remove-Item $display -Recurse -Force }
    New-Item -ItemType Directory -Path $display | Out-Null
    Copy-Item "$PSScriptRoot\frontend\dist\*" $display -Recurse
    Write-Host "Commit /display so the next publish serves this build." -ForegroundColor Yellow
}

Write-Host "Publishing self-contained (win-x86)..." -ForegroundColor Cyan
# x86 to match the OPC DA automation wrapper, which is normally registered 32-bit only.
dotnet publish "$PSScriptRoot\backend\src\CuringMonitor.Api\CuringMonitor.Api.csproj" `
    -c Release -r win-x86 --self-contained true `
    -p:PublishSingleFile=false `
    -o $Output

$index = Join-Path $Output 'wwwroot\index.html'
if (-not (Test-Path $index)) {
    throw "Published without a display: '$index' is missing. Check that /display is present."
}

Write-Host ""
Write-Host "Done. Copy '$Output' to the target machine and run CuringMonitor.Api.exe." -ForegroundColor Green
Write-Host "The display is then at http://localhost:5080" -ForegroundColor Green
