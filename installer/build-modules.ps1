# بسته‌بندیِ ماژول‌های استخراج‌شده برای بازارِ ماژول (github kish210/SamaHesab).
#   هر ماژول → dist/modules/<Name>.mspkg (zip شاملِ DLLِ ماژول + module.json + version.json)
#   + dist/modules/modules-catalog.json (فهرستِ بازار که برنامه از github می‌خواند).
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot
$out  = Join-Path $root "dist\modules"
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force -Path $out | Out-Null

# نسخهٔ هسته (برای سازگاری) از Directory.Build.props
$coreVersion = ([xml](Get-Content (Join-Path $root "Directory.Build.props"))).Project.PropertyGroup.Version

# ماژول‌های قابلِ بسته‌بندی: نام نمایشی، پروژه، آیکونِ متریال، شرح
$modules = @(
  @{ Key="Hotel";       Name="هتل / اقامتگاه (PMS)"; Proj="SamaHesab.Modules.Hotel";       Schema="Htl"; Desc="مدیریتِ اقامتگاه: اتاق/نرخ/رزرو/فولیو/شب‌حسابرسی." }
  @{ Key="Contracting"; Name="پیمانکاری";            Proj="SamaHesab.Modules.Contracting"; Schema="Con"; Desc="صورت‌وضعیتِ پیمان، آبشارِ کسورات، ضمانت‌نامه و گزارش‌ها." }
  @{ Key="Restaurant";  Name="رستوران";              Proj="SamaHesab.Modules.Restaurant";  Schema="Rst"; Desc="سالن/میز/سفارش/آشپزخانه — صندوقِ رستوران و گارسون." }
  @{ Key="POS";         Name="صندوق فروش (POS)";      Proj="SamaHesab.Modules.POS";         Schema="Pos"; Desc="شیفتِ صندوق (Z/X) + فاکتورِ معلق (Hold/Recall) برای فروشِ سریع." }
  @{ Key="HR";          Name="منابع انسانی";          Proj="SamaHesab.Modules.HR";          Schema="Hrm"; Desc="حقوق و دستمزد، حضور و غیاب، کارمندان و فیش." }
  @{ Key="CRM";         Name="باشگاه مشتریان (CRM)";  Proj="SamaHesab.Modules.CRM";         Schema="Crm"; Desc="امتیاز و وفاداریِ مشتریان (باشگاه)." }
  @{ Key="Tourism";     Name="گردشگری";              Proj="SamaHesab.Modules.Tourism";     Schema="Tur"; Desc="فروشِ خدماتِ گردشگری، ودیعهٔ تأمین‌کننده، پورسانت، سند و گزارش." }
  @{ Key="Attendance";  Name="حضور و غیاب";          Proj="SamaHesab.Modules.Attendance";  Schema="Hrm"; Desc="ورود/خروج، شیفت، تقویمِ تعطیلات، کارکردِ ماهانه (مستقل از حقوق)." }
)

$catalog = @()
foreach ($m in $modules) {
  Write-Host "packaging $($m.Key) ..." -ForegroundColor Cyan
  $proj = Join-Path $root "src\$($m.Proj)\$($m.Proj).csproj"
  $stage = Join-Path $out "_stage_$($m.Key)"
  dotnet build $proj -c Release -o $stage --nologo | Out-Null
  if ($LASTEXITCODE) { throw "build $($m.Key) failed" }

  # module.json + version.json
  $moduleJson = @{ key=$m.Key; displayName=$m.Name; version="$coreVersion"; schema=$m.Schema; assembly="$($m.Proj).dll"; minCore="$coreVersion" } | ConvertTo-Json
  Set-Content -Path (Join-Path $stage "module.json")  -Value $moduleJson -Encoding utf8
  Set-Content -Path (Join-Path $stage "version.json") -Value (@{ version="$coreVersion" } | ConvertTo-Json) -Encoding utf8

  # فقط DLLِ خودِ ماژول + مانیفست‌ها در بسته (وابستگی‌های هسته در خودِ برنامه هستند)
  $pkgDir = Join-Path $out "_pkg_$($m.Key)"
  New-Item -ItemType Directory -Force -Path $pkgDir | Out-Null
  Copy-Item (Join-Path $stage "$($m.Proj).dll") $pkgDir
  Copy-Item (Join-Path $stage "module.json")    $pkgDir
  Copy-Item (Join-Path $stage "version.json")   $pkgDir

  $mspkg = Join-Path $out "$($m.Key).mspkg"
  $tmpZip = Join-Path $out "$($m.Key).zip"
  Compress-Archive -Path "$pkgDir\*" -DestinationPath $tmpZip -Force
  if (Test-Path $mspkg) { Remove-Item $mspkg -Force }
  Move-Item $tmpZip $mspkg
  $size = [math]::Round((Get-Item $mspkg).Length/1KB)

  $catalog += [ordered]@{
    key=$m.Key; displayName=$m.Name; version="$coreVersion"; schema=$m.Schema;
    description=$m.Desc; package="$($m.Key).mspkg"; sizeKB=$size; minCore="$coreVersion"
  }
  Remove-Item $stage,$pkgDir -Recurse -Force
}

$catalogObj = [ordered]@{ catalogVersion=1; coreVersion="$coreVersion"; updatedAt=(Get-Date -Format "yyyy-MM-dd"); modules=$catalog }
Set-Content -Path (Join-Path $out "modules-catalog.json") -Value ($catalogObj | ConvertTo-Json -Depth 5) -Encoding utf8

Write-Host "`nDONE → $out" -ForegroundColor Green
Get-ChildItem $out -Filter *.mspkg | ForEach-Object { "  {0}  ({1:N0} KB)" -f $_.Name, ($_.Length/1KB) }
"  modules-catalog.json"
