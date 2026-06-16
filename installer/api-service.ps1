<#
  فاز ۱۲ P-G8 — مدیریتِ سرویسِ ویندوزیِ «SamaHesabApi».
  سرورِ API را به‌جای کنسولِ دستی، به‌عنوانِ یک Windows Service (اجرای خودکار) نصب می‌کند.
  باید با دسترسیِ Administrator اجرا شود.

  استفاده:
    .\api-service.ps1 install     # نصب + شروع (اجرای خودکار در بوت)
    .\api-service.ps1 uninstall   # توقف + حذف
    .\api-service.ps1 start
    .\api-service.ps1 stop
    .\api-service.ps1 status
#>
param([Parameter(Mandatory=$true)][ValidateSet('install','uninstall','start','stop','status')] [string]$Action)

$ServiceName = 'SamaHesabApi'
$DisplayName = 'سما حساب — سرور API'
$ExePath     = Join-Path $PSScriptRoot 'SamaHesab.API.exe'

function Require-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $p  = New-Object Security.Principal.WindowsPrincipal($id)
    if (-not $p.IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)) {
        throw 'این عملیات نیازمندِ دسترسیِ Administrator است. PowerShell را «Run as administrator» اجرا کنید.'
    }
}

switch ($Action) {
    'install' {
        Require-Admin
        if (-not (Test-Path $ExePath)) { throw "فایلِ سرویس یافت نشد: $ExePath" }
        if (Get-Service $ServiceName -ErrorAction SilentlyContinue) {
            Write-Host "سرویس از قبل وجود دارد؛ به‌روزرسانیِ مسیر…"
            sc.exe config $ServiceName binPath= "`"$ExePath`"" start= auto | Out-Null
        } else {
            New-Service -Name $ServiceName -DisplayName $DisplayName -BinaryPathName "`"$ExePath`"" -StartupType Automatic -Description 'سرورِ مرکزیِ API سما حساب (REST/JWT).' | Out-Null
        }
        Start-Service $ServiceName
        Write-Host "✔ سرویس «$ServiceName» نصب و اجرا شد."
    }
    'uninstall' {
        Require-Admin
        if (Get-Service $ServiceName -ErrorAction SilentlyContinue) {
            Stop-Service $ServiceName -Force -ErrorAction SilentlyContinue
            sc.exe delete $ServiceName | Out-Null
            Write-Host "✔ سرویس «$ServiceName» حذف شد."
        } else { Write-Host "سرویس وجود ندارد." }
    }
    'start'  { Require-Admin; Start-Service $ServiceName; Write-Host '✔ شروع شد.' }
    'stop'   { Require-Admin; Stop-Service $ServiceName -Force; Write-Host '✔ متوقف شد.' }
    'status' { Get-Service $ServiceName -ErrorAction SilentlyContinue | Format-Table Name,Status,StartType -AutoSize }
}
