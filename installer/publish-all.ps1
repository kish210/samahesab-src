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

Write-Host "[7/8] publish API server (SamaHesab.API.exe) ..." -ForegroundColor Cyan
dotnet publish "$root\src\SamaHesab.API\SamaHesab.API.csproj" @common -o $api
if ($LASTEXITCODE) { throw "API publish failed" }

# SP-3b — پنلِ فروشِ گردشگری (Blazor WASM PWA): با base path = /seller منتشر و در
# wwwroot/seller سرور قرار می‌گیرد تا API آن را روی http://<server>:5080/seller/ سرو کند
# (نصب‌پذیر روی موبایل). UseStaticFilesِ موجودِ API فایل‌ها را سرو می‌کند.
Write-Host "[8/8] publish Seller Web panel (PWA) -> /seller ..." -ForegroundColor Cyan
$sellerPub = Join-Path $dist "sellerweb"
dotnet publish "$root\src\SamaHesab.SellerWeb\SamaHesab.SellerWeb.csproj" -c Release -o $sellerPub --nologo -v m
if ($LASTEXITCODE) { throw "Seller Web publish failed" }
# base href و پایهٔ service worker باید /seller/ باشند تا وقتی API زیرِ /seller/ سرو می‌کند،
# _framework/دارایی‌ها و تطبیقِ ناوبری/کش درست کار کنند. (Replaceِ literal — مقاوم به کاراکترهای خاص)
$idxFile = Join-Path $sellerPub "wwwroot\index.html"
[IO.File]::WriteAllText($idxFile, ([IO.File]::ReadAllText($idxFile)).Replace('<base href="/" />', '<base href="/seller/" />'))
$swFile = Join-Path $sellerPub "wwwroot\service-worker.js"
if (Test-Path $swFile) {
    [IO.File]::WriteAllText($swFile, ([IO.File]::ReadAllText($swFile)).Replace('const base = "/";', 'const base = "/seller/";'))
}
$sellerDst = Join-Path $api "wwwroot\seller"
if (Test-Path $sellerDst) { Remove-Item $sellerDst -Recurse -Force }
New-Item -ItemType Directory -Force -Path $sellerDst | Out-Null
Copy-Item (Join-Path $sellerPub "wwwroot\*") $sellerDst -Recurse -Force

Write-Host "`nDONE." -ForegroundColor Green
"seller web: " + (Test-Path "$sellerDst\index.html") + " (-> http://<server>:5080/seller/)"
"app exe   : " + (Test-Path "$app\SamaHesab.exe")
"pos exe   : " + (Test-Path "$app\pos.exe")
"res exe   : " + (Test-Path "$app\restoran.exe")
"waiter exe: " + (Test-Path "$app\waiter.exe")
"kitchen   : " + (Test-Path "$app\kitchen.exe")
"warehouse : " + (Test-Path "$app\warehouse.exe")
"api exe   : " + (Test-Path "$api\SamaHesab.API.exe")
"app size: {0:N0} MB" -f ((Get-ChildItem $app -Recurse | Measure-Object Length -Sum).Sum/1MB)
"api size: {0:N0} MB" -f ((Get-ChildItem $api -Recurse | Measure-Object Length -Sum).Sum/1MB)
