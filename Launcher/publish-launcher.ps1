# Windows에서 런처 빌드
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

dotnet publish -c Release

$publishDir = Join-Path $PSScriptRoot "bin\Release\net8.0-windows\win-x64\publish"
Copy-Item -Force (Join-Path $PSScriptRoot "launcher-config.json") $publishDir

Write-Host ""
Write-Host "Done. Send this folder to friends:"
Write-Host $publishDir
Write-Host "  - 2DFightLauncher.exe"
Write-Host "  - launcher-config.json"
