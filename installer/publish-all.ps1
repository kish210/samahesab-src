# Publishes everything self-contained (win-x64) for the installers.
#   dist\app  -> SamaHesab.exe (accounting) + pos.exe + restoran.exe + .NET runtime
#   dist\api  -> SamaHesab.API.exe (central Web API server) + .NET runtime
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot          # repo root (installer\..)
$dist = Join-Path $root "dist"
$app  = Join-Path $dist "app"
$api  = Join-Path $dist "api"

if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Force -Path $app,$api | Out-Null

$common = @("-c","Release","-r","win-x64","--self-contained","true",
            "-p:PublishSingleFile=false","-p:DebugType=none","--nologo","-v","m")

Write-Host "[1/4] publish WPF (SamaHesab.exe) ..." -ForegroundColor Cyan
dotnet publish "$root\src\SamaHesab.WPF\SamaHesab.WPF.csproj" @common -o $app
if ($LASTEXITCODE) { throw "WPF publish failed" }

Write-Host "[2/4] publish POS launcher (pos.exe) ..." -ForegroundColor Cyan
dotnet publish "$root\src\SamaHesab.POS\SamaHesab.POS.csproj" @common -o $app
if ($LASTEXITCODE) { throw "POS publish failed" }

Write-Host "[3/4] publish Restaurant launcher (restoran.exe) ..." -ForegroundColor Cyan
dotnet publish "$root\src\SamaHesab.Restaurant\SamaHesab.Restaurant.csproj" @common -o $app
if ($LASTEXITCODE) { throw "Restaurant publish failed" }

Write-Host "[4/4] publish API server (SamaHesab.API.exe) ..." -ForegroundColor Cyan
dotnet publish "$root\src\SamaHesab.API\SamaHesab.API.csproj" @common -o $api
if ($LASTEXITCODE) { throw "API publish failed" }

Write-Host "`nDONE." -ForegroundColor Green
"app exe : " + (Test-Path "$app\SamaHesab.exe")
"pos exe : " + (Test-Path "$app\pos.exe")
"res exe : " + (Test-Path "$app\restoran.exe")
"api exe : " + (Test-Path "$api\SamaHesab.API.exe")
"app size: {0:N0} MB" -f ((Get-ChildItem $app -Recurse | Measure-Object Length -Sum).Sum/1MB)
"api size: {0:N0} MB" -f ((Get-ChildItem $api -Recurse | Measure-Object Length -Sum).Sum/1MB)
