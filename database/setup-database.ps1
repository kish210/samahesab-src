# =============================================================================
#  Sama Hesab - SQL Server Express database setup (Windows)
#  Usage:   powershell -ExecutionPolicy Bypass -File setup-database.ps1
#  Optional: -Server ".\SQLEXPRESS"
#  IMPORTANT: uses -f 65001 so Persian (UTF-8) text is stored correctly.
# =============================================================================
param([string]$Server = ".\SQLEXPRESS")

$ErrorActionPreference = "Stop"
$db = $PSScriptRoot

Write-Host "Setting up SamaHesab database on $Server ..." -ForegroundColor Cyan

# 1) create database + schemas (Windows default data dir)
& sqlcmd -S $Server -E -b -f 65001 -i "$db\_win_01_create.sql"
if ($LASTEXITCODE -ne 0) { Write-Host "Database creation failed." -ForegroundColor Red; exit 1 }

# 2) schema + data scripts (UTF-8)
$scripts = @(
    "02_CreateTables", "03_CreateIndexes", "04_CreateViews",
    "05_StoredProcedures", "06_SeedData", "07_DefaultChartOfAccounts"
)
foreach ($s in $scripts) {
    Write-Host "  running $s ..." -ForegroundColor Gray
    & sqlcmd -S $Server -E -d SamaHesab -f 65001 -i "$db\$s.sql"
}

Write-Host "`nDone. Summary:" -ForegroundColor Green
& sqlcmd -S $Server -E -d SamaHesab -f 65001 -h -1 -W -s "=" -Q @"
SELECT 'Tables', COUNT(*) FROM sys.tables
UNION ALL SELECT 'Accounts', COUNT(*) FROM Acc.Accounts
UNION ALL SELECT 'Units', COUNT(*) FROM Cfg.Units
UNION ALL SELECT 'VoucherTypes', COUNT(*) FROM Acc.VoucherTypes
UNION ALL SELECT 'Companies', COUNT(*) FROM Cfg.Companies
UNION ALL SELECT 'Users', COUNT(*) FROM Sec.Users;
"@

Write-Host "`nConnection string for the app:" -ForegroundColor Cyan
Write-Host "Server=$Server;Database=SamaHesab;Trusted_Connection=True;TrustServerCertificate=True;" -ForegroundColor Yellow
