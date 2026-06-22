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

Write-Host "[3/7] publish Restaurant launcher (restoran.exe) ..." -ForegroundColor Cyan
dotnet publish "$root\src\SamaHesab.Restaurant\SamaHesab.Restaurant.csproj" @common -o $app
if ($LASTEXITCODE) { throw "Restaurant publish failed" }

Write-Host "[4/7] publish Waiter launcher (waiter.exe) ..." -ForegroundColor Cyan
dotnet publish "$root\src\SamaHesab.Waiter\SamaHesab.Waiter.csproj" @common -o $app
if ($LASTEXITCODE) { throw "Waiter publish failed" }

Write-Host "[5/7] publish Kitchen launcher (kitchen.exe) ..." -ForegroundColor Cyan
dotnet publish "$root\src\SamaHesab.Kitchen\SamaHesab.Kitchen.csproj" @common -o $app
if ($LASTEXITCODE) { throw "Kitchen publish failed" }

Write-Host "[6/7] publish Warehouse launcher (warehouse.exe) ..." -ForegroundColor Cyan
dotnet publish "$root\src\SamaHesab.Warehouse\SamaHesab.Warehouse.csproj" @common -o $app
if ($LASTEXITCODE) { throw "Warehouse publish failed" }

Write-Host "[6b/7] publish Migration tool (mohajerat.exe) ..." -ForegroundColor Cyan
dotnet publish "$root\tools\SamaHesab.Migration\SamaHesab.Migration.csproj" @common -o $app
if ($LASTEXITCODE) { throw "Migration tool publish failed" }

Write-Host "[6c/7] publish Attendance launcher (hozur.exe) ..." -ForegroundColor Cyan
dotnet publish "$root\src\SamaHesab.Attendance\SamaHesab.Attendance.csproj" @common -o $app
if ($LASTEXITCODE) { throw "Attendance launcher publish failed" }

Write-Host "[7/7] publish API server (SamaHesab.API.exe) ..." -ForegroundColor Cyan
dotnet publish "$root\src\SamaHesab.API\SamaHesab.API.csproj" @common -o $api
if ($LASTEXITCODE) { throw "API publish failed" }

Write-Host "`nDONE." -ForegroundColor Green
"app exe   : " + (Test-Path "$app\SamaHesab.exe")
"pos exe   : " + (Test-Path "$app\pos.exe")
"res exe   : " + (Test-Path "$app\restoran.exe")
"waiter exe: " + (Test-Path "$app\waiter.exe")
"kitchen   : " + (Test-Path "$app\kitchen.exe")
"warehouse : " + (Test-Path "$app\warehouse.exe")
"api exe   : " + (Test-Path "$api\SamaHesab.API.exe")
"app size: {0:N0} MB" -f ((Get-ChildItem $app -Recurse | Measure-Object Length -Sum).Sum/1MB)
"api size: {0:N0} MB" -f ((Get-ChildItem $api -Recurse | Measure-Object Length -Sum).Sum/1MB)
