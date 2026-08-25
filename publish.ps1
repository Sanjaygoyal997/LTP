<#
    Builds a self-contained deployment: the display is compiled into the service's wwwroot
    and the .NET runtime is included, so the target machine needs neither Node nor .NET.

    Usage:  .\publish.ps1 [-Output C:\CuringStatus]
#>
param(
    [string]$Output = "$PSScriptRoot\publish"
)

$ErrorActionPreference = 'Stop'

Write-Host "1/3  Building the display..." -ForegroundColor Cyan
Push-Location "$PSScriptRoot\frontend"
npm ci
npm run build
Pop-Location

Write-Host "2/3  Copying it into the service..." -ForegroundColor Cyan
$webRoot = "$PSScriptRoot\backend\src\CuringMonitor.Api\wwwroot"
if (Test-Path $webRoot) { Remove-Item $webRoot -Recurse -Force }
New-Item -ItemType Directory -Path $webRoot | Out-Null
Copy-Item "$PSScriptRoot\frontend\dist\*" $webRoot -Recurse

Write-Host "3/3  Publishing self-contained (win-x86)..." -ForegroundColor Cyan
# x86 to match the OPC DA automation wrapper, which is normally registered 32-bit only.
dotnet publish "$PSScriptRoot\backend\src\CuringMonitor.Api\CuringMonitor.Api.csproj" `
    -c Release -r win-x86 --self-contained true `
    -p:PublishSingleFile=false `
    -o $Output

Write-Host ""
Write-Host "Done. Copy '$Output' to the target machine and run CuringMonitor.Api.exe." -ForegroundColor Green
Write-Host "The display is then at http://localhost:5080" -ForegroundColor Green
