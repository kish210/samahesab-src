# از نسخهٔ ۲.۹ به بعد تنها مسیرِ انتشارِ رسمی — فقط سرور+وب (بدونِ دسکتاپِ WPF/POS/رستوران/...).
# publish-all.ps1 (که هنوز دسکتاپ را هم publish می‌کند) برایِ آرشیو/کاربردِ داخلی نگه داشته شده،
# ولی نصابِ منتشرشده به کاربران از این اسکریپت + installer\web-setup.iss ساخته می‌شود.
#   dist\api -> SamaHesab.API.exe (self-contained) + wwwroot\web (React) + wwwroot\seller/guest (Blazor)
# پیش‌نیاز: .NET SDK + Node.js/npm
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot          # repo root (installer\..)
$dist = Join-Path $root "dist"
$api  = Join-Path $dist "api"

if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Force -Path $api | Out-Null

$common = @("-c","Release","-r","win-x64","--self-contained","true",
            "-p:PublishSingleFile=false","-p:DebugType=none","--nologo","-v","m")

Write-Host "[1/3] publish API server (SamaHesab.API.exe) ..." -ForegroundColor Cyan
dotnet publish "$root\src\SamaHesab.API\SamaHesab.API.csproj" @common -o $api
if ($LASTEXITCODE) { throw "API publish failed" }

Write-Host "[1b/3] publish API Tray (SamaHesabApiTray.exe) ..." -ForegroundColor Cyan
dotnet publish "$root\src\SamaHesab.ApiTray\SamaHesab.ApiTray.csproj" @common -o $api
if ($LASTEXITCODE) { throw "API Tray publish failed" }

# پنلِ فروشِ گردشگری (Blazor WASM PWA) روی /seller + پنلِ مهمانِ اقامتی روی /guest (پورتِ ۵۰۹۰، base href=/)
Write-Host "[2/3] publish Seller/Guest Web panel (PWA) -> /seller + /guest ..." -ForegroundColor Cyan
$sellerPub = Join-Path $dist "sellerweb"
dotnet publish "$root\src\SamaHesab.SellerWeb\SamaHesab.SellerWeb.csproj" -c Release -o $sellerPub --nologo -v m
if ($LASTEXITCODE) { throw "Seller Web publish failed" }
$guestDst = Join-Path $api "wwwroot\guest"
if (Test-Path $guestDst) { Remove-Item $guestDst -Recurse -Force }
New-Item -ItemType Directory -Force -Path $guestDst | Out-Null
Copy-Item (Join-Path $sellerPub "wwwroot\*") $guestDst -Recurse -Force
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

# کلاینتِ اصلیِ وبِ React (Core ERP + ماژول‌ها): buildِ Vite (base=/web/) → wwwroot/web سرور
Write-Host "[3/3] build Web client (React) -> /web ..." -ForegroundColor Cyan
$webSrc = Join-Path $root "web"
if (-not (Get-Command npm -ErrorAction SilentlyContinue)) { throw "npm پیدا نشد — برایِ ساختِ کلاینتِ وب Node.js لازم است." }
Push-Location $webSrc
try {
    if (Test-Path (Join-Path $webSrc "package-lock.json")) { npm ci --no-fund --no-audit } else { npm install --no-fund --no-audit }
    if ($LASTEXITCODE) { throw "npm install failed" }
    npm run build
    if ($LASTEXITCODE) { throw "Web client build failed" }
} finally { Pop-Location }
$webDst = Join-Path $api "wwwroot\web"
if (Test-Path $webDst) { Remove-Item $webDst -Recurse -Force }
New-Item -ItemType Directory -Force -Path $webDst | Out-Null
Copy-Item (Join-Path $webSrc "dist\*") $webDst -Recurse -Force

Write-Host "`nDONE." -ForegroundColor Green
"web client: " + (Test-Path "$webDst\index.html") + " (-> http://<server>:5080/web/)"
"seller web: " + (Test-Path "$sellerDst\index.html") + " (-> http://<server>:5080/seller/)"
"api exe   : " + (Test-Path "$api\SamaHesab.API.exe")
"api tray  : " + (Test-Path "$api\SamaHesabApiTray.exe")
"api size: {0:N0} MB" -f ((Get-ChildItem $api -Recurse | Measure-Object Length -Sum).Sum/1MB)
