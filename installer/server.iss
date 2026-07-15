; =============================================================================
;  SAMA HESAB ERP — SERVER installer  ->  setup.exe
;  نصب کامل روی سیستم سرور: برنامه حسابداری + سرور Web API + پایگاه داده
;  Self-contained (.NET همراه برنامه است؛ نیازی به نصب جداگانه‌ی .NET نیست)
;  پیش‌نیاز فقط: Microsoft SQL Server (Express یا بالاتر)
; =============================================================================

#define AppName       "سما حساب — سرور"
#define AppVersion    "2.8.20"
#define AppPublisher  "سماع رایانه کیش"
#define AppExe        "SamaHesab.exe"
#define ApiExe        "SamaHesab.API.exe"
#define ApiTrayExe    "SamaHesabApiTray.exe"

; نصابِ آفلاینِ SQL Server Express (اختیاری): SQLEXPR_x64_ENU.exe را در installer\redist\
; بگذارید تا bundle و آفلاین نصب شود؛ نبودش → دانلود از اینترنت.
#if FileExists(SourcePath + "redist\SQLEXPR_x64_ENU.exe")
  #define SqlExprBundle
#endif

[Setup]
AppId={{8F2A1C40-7B3E-4D5A-9C11-SAMAHESABSRV01}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\SamaHesab
DefaultGroupName=سما حساب
DisableProgramGroupPage=yes
SetupIconFile=..\src\SamaHesab.WPF\app.ico
UninstallDisplayIcon={app}\{#AppExe}
OutputDir=Output
OutputBaseFilename=SamaHesab_Server_Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
UninstallDisplayName={#AppName}

[Languages]
Name: "fa"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "ایجاد میانبر روی دسکتاپ"; Flags: unchecked
Name: "autostartapi"; Description: "اجرای خودکار سرور (API) هنگام ورود به ویندوز"
Name: "firewall";     Description: "باز کردن پورت 5080 در فایروال ویندوز برای کلاینت‌ها"

[Files]
; برنامه‌ی اصلی + لانچرهای فروشگاه/رستوران (خودکفا)
Source: "..\dist\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; سرور Web API (خودکفا)
Source: "..\dist\api\*"; DestDir: "{app}\server"; Flags: ignoreversion recursesubdirs createallsubdirs
; اسکریپت‌های پایگاه داده
Source: "..\database\*"; DestDir: "{app}\database"; Flags: ignoreversion recursesubdirs createallsubdirs

#ifdef SqlExprBundle
; نصابِ آفلاینِ SQL Server Express — هنگامِ نیاز در {tmp} استخراج می‌شود (در {app} کپی نمی‌شود)
Source: "redist\SQLEXPR_x64_ENU.exe"; Flags: dontcopy
#endif

[Dirs]
Name: "{app}\logs";   Permissions: everyone-full
Name: "{app}\backup"; Permissions: everyone-full

[Icons]
Name: "{group}\حسابداری سما حساب";        Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"
Name: "{group}\صندوق فروشگاه";            Filename: "{app}\{#AppExe}"; Parameters: "--pos"; WorkingDir: "{app}"
Name: "{group}\صندوق رستوران";            Filename: "{app}\{#AppExe}"; Parameters: "--restaurant"; WorkingDir: "{app}"
Name: "{group}\صندوق گارسون";            Filename: "{app}\{#AppExe}"; Parameters: "--waiter"; WorkingDir: "{app}"
Name: "{group}\نمایشگر آشپزخانه";        Filename: "{app}\{#AppExe}"; Parameters: "--kitchen"; WorkingDir: "{app}"
Name: "{group}\انبارداری";               Filename: "{app}\{#AppExe}"; Parameters: "--warehouse"; WorkingDir: "{app}"
Name: "{group}\راه‌اندازی سرور (API)";    Filename: "{app}\server\{#ApiTrayExe}"; WorkingDir: "{app}\server"
Name: "{group}\حذف سما حساب";            Filename: "{uninstallexe}"
Name: "{commondesktop}\حسابداری سما حساب"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
; ساخت پایگاه داده با sqlcmd (از طریق اسکریپت موجود) — با نام سروری که کاربر وارد می‌کند
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\database\setup-database.ps1"" -Server ""{code:GetSqlServer}"""; \
  StatusMsg: "در حال ساخت پایگاه داده روی SQL Server..."; \
  Flags: runhidden waituntilterminated; Check: ShouldCreateDb

; باز کردن پورت فایروال برای کلاینت‌ها
Filename: "netsh.exe"; \
  Parameters: "advfirewall firewall add rule name=""SamaHesab API 5080"" dir=in action=allow protocol=TCP localport=5080"; \
  Flags: runhidden; Tasks: firewall
; پورتِ پنلِ مهمانِ برنامه‌ریزیِ اقامتی (جدا از API)
Filename: "netsh.exe"; \
  Parameters: "advfirewall firewall add rule name=""SamaHesab Guest 5090"" dir=in action=allow protocol=TCP localport=5090"; \
  Flags: runhidden; Tasks: firewall

; اجرای سرور پس از نصب — از طریقِ آیکونِ سینی (نه مستقیم)، تا کاربر وضعیتِ سرور را ببیند و بتواند
; تمیز متوقفش کند (پیش‌تر API مستقیم بی‌هیچ UIای بالا می‌آمد و تنها راهِ توقف Task Manager بود).
Filename: "{app}\server\{#ApiTrayExe}"; Description: "راه‌اندازی سرور (API)"; \
  WorkingDir: "{app}\server"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "netsh.exe"; Parameters: "advfirewall firewall delete rule name=""SamaHesab API 5080"""; Flags: runhidden; RunOnceId: "DelFwRule5080"
Filename: "netsh.exe"; Parameters: "advfirewall firewall delete rule name=""SamaHesab Guest 5090"""; Flags: runhidden; RunOnceId: "DelFwRule5090"

[Registry]
; اجرای خودکار سرور هنگام ورود کاربر — از طریقِ آیکونِ سینی (وضعیت/توقفِ تمیز در دسترسِ کاربر)
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; \
  ValueName: "SamaHesabServer"; ValueData: """{app}\server\{#ApiTrayExe}"""; \
  Flags: uninsdeletevalue; Tasks: autostartapi

[Code]
// دانلودِ مستقیم از سرورِ مایکروسافت برایِ بسیاری از کاربران قطع/ناموفق می‌شد و کلِ نصب را کنسل
// می‌کرد. به‌جایش همان بستهٔ کاملِ SQLEXPR_x64_ENU.exe روی ریلیزِ دائمیِ «redist» در ریپازیتوریِ
// خودمان (kish210/SamaHesab) میزبانی شده.
const
  SqlExprUrl = 'https://github.com/kish210/SamaHesab/releases/download/redist/SQLEXPR_x64_ENU.exe';

var
  SqlPage: TInputQueryWizardPage;
  CreateDbPage: TInputOptionWizardPage;
  DownloadPage: TDownloadWizardPage;
  NeedSqlInstall: Boolean;

// تشخیصِ دقیقِ نمونهٔ SQLEXPRESS (نه هر باقی‌ماندهٔ SQL).
function IsSqlExpressInstalled: Boolean;
var
  Val: String;
begin
  Result :=
    RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL', 'SQLEXPRESS', Val)
    or RegKeyExists(HKLM, 'SYSTEM\CurrentControlSet\Services\MSSQL$SQLEXPRESS');
end;

function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  Result := True;
end;

// گیتِ پیش‌نیاز: اگر SQLEXPRESS نبود، با تأییدِ کاربر برای نصبِ خودکار علامت می‌زند.
function InitializeSetup: Boolean;
begin
  Result := True;
  NeedSqlInstall := False;
  if IsSqlExpressInstalled then Exit;

  if MsgBox(
      'بررسیِ پیش‌نیازها:' + #13#10#13#10 +
      '✗ نمونهٔ SQL Server Express (SQLEXPRESS) روی این سرور پیدا نشد.' + #13#10 +
      '   سما حساب بدونِ آن کار نمی‌کند.' + #13#10#13#10 +
      'آیا نصاب خودش SQL Server Express (رایگان) را نصب کند؟' + #13#10 +
#ifdef SqlExprBundle
      '(آفلاین — همراهِ نصاب؛ چند دقیقه طول می‌کشد. «خیر» = لغوِ نصب.)',
#else
      '(نیازمندِ اینترنت؛ چند دقیقه طول می‌کشد. «خیر» = لغوِ نصب.)',
#endif
      mbConfirmation, MB_YESNO) = IDYES then
    NeedSqlInstall := True
  else
    Result := False;
end;

function RunSqlSetup(const ExePath, Params: String): Boolean;
var
  Rc: Integer;
begin
  WizardForm.StatusLabel.Caption := 'در حال نصبِ SQL Server Express… (چند دقیقه طول می‌کشد، صبور باشید)';
  Result := Exec(ExePath, Params, '', SW_SHOW, ewWaitUntilTerminated, Rc) and ((Rc = 0) or (Rc = 3010));
  if not Result then
    MsgBox('نصبِ SQL Server Express ناموفق بود (کدِ خروجی: ' + IntToStr(Rc) + ').' + #13#10 +
      'می‌توانید SQL Server Express را دستی نصب و سپس نصاب را دوباره اجرا کنید.', mbError, MB_OK);
end;

function InstallSqlExpress: Boolean;
begin
  Result := False;
#ifdef SqlExprBundle
  WizardForm.StatusLabel.Caption := 'در حال آماده‌سازیِ نصابِ SQL Server Express…';
  ExtractTemporaryFile('SQLEXPR_x64_ENU.exe');
  Result := RunSqlSetup(ExpandConstant('{tmp}\SQLEXPR_x64_ENU.exe'),
    '/QS /ACTION=Install /FEATURES=SQLEngine /INSTANCENAME=SQLEXPRESS ' +
    '/SQLSVCACCOUNT="NT AUTHORITY\SYSTEM" /SQLSYSADMINACCOUNTS="BUILTIN\Administrators" ' +
    '/SQLSVCSTARTUPTYPE=Automatic /TCPENABLED=1 /IACCEPTSQLSERVERLICENSETERMS');
#else
  DownloadPage.Clear;
  DownloadPage.Add(SqlExprUrl, 'SQLEXPR_x64_ENU.exe', '');
  DownloadPage.Show;
  try
    try
      DownloadPage.Download;
    except
      MsgBox('دانلودِ SQL Server ناموفق بود:' + #13#10 + GetExceptionMessage + #13#10#13#10 +
        'اتصالِ اینترنت را بررسی کنید یا SQL Server Express را دستی نصب کنید.', mbError, MB_OK);
      Exit;
    end;
  finally
    DownloadPage.Hide;
  end;
  Result := RunSqlSetup(ExpandConstant('{tmp}\SQLEXPR_x64_ENU.exe'),
    '/QS /ACTION=Install /FEATURES=SQLEngine /INSTANCENAME=SQLEXPRESS ' +
    '/SQLSVCACCOUNT="NT AUTHORITY\SYSTEM" /SQLSYSADMINACCOUNTS="BUILTIN\Administrators" ' +
    '/SQLSVCSTARTUPTYPE=Automatic /TCPENABLED=1 /IACCEPTSQLSERVERLICENSETERMS');
#endif

  if Result then Result := IsSqlExpressInstalled;
  if not Result then
    MsgBox('نصبِ SQL Server کامل تأیید نشد (شاید به ری‌استارتِ ویندوز نیاز باشد).' + #13#10 +
      'پس از ری‌استارت، نصاب را دوباره اجرا کنید.', mbError, MB_OK);
end;

// درست پیش از شروعِ کپیِ فایل‌ها: اگر SQL لازم است، اول آن را نصب و تأیید کن.
function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = wpReady) and NeedSqlInstall then
  begin
    if InstallSqlExpress then
      NeedSqlInstall := False
    else
      Result := False;
  end;
end;

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage('نصبِ پیش‌نیاز — SQL Server Express',
    'در حال دریافتِ بستهٔ نصبِ SQL Server Express…', @OnDownloadProgress);

  SqlPage := CreateInputQueryPage(wpSelectDir,
    'تنظیمات پایگاه داده',
    'اطلاعات اتصال به SQL Server',
    'نام نمونه‌ی SQL Server خود را وارد کنید (مثال: .\SQLEXPRESS یا localhost).');
  SqlPage.Add('نام سرور SQL:', False);
  SqlPage.Values[0] := '.\SQLEXPRESS';

  CreateDbPage := CreateInputOptionPage(SqlPage.ID,
    'ساخت پایگاه داده',
    'آیا پایگاه داده‌ی SamaHesab ساخته شود؟',
    'اگر این اولین نصب روی این سرور است، گزینه را علامت بزنید تا جداول و داده‌های اولیه ساخته شوند. ' +
    'اگر پایگاه داده از قبل وجود دارد، علامت نزنید.', False, False);
  CreateDbPage.Add('ساخت پایگاه داده‌ی SamaHesab روی سرور بالا (نیازمند sqlcmd)');
  CreateDbPage.Values[0] := True;
end;

function GetSqlServer(Param: String): String;
begin
  Result := SqlPage.Values[0];
end;

function ShouldCreateDb: Boolean;
begin
  Result := CreateDbPage.Values[0];
end;

// پس از نصب: نوشتن رشته‌ی اتصال سرور SQL در تنظیمات API (بدون دست‌زدن به کلید JWT)
procedure CurStepChanged(CurStep: TSetupStep);
var
  Json, Srv: String;
begin
  if CurStep = ssPostInstall then
  begin
    Srv := SqlPage.Values[0];
    StringChangeEx(Srv, '\', '\\', True);   // escape backslash for JSON
    Json :=
      '{' + #13#10 +
      '  "ConnectionStrings": {' + #13#10 +
      '    "DefaultConnection": "Server=' + Srv + ';Database=SamaHesab;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;Encrypt=False;"' + #13#10 +
      '  }' + #13#10 +
      '}';
    SaveStringToFile(ExpandConstant('{app}\server\appsettings.Production.json'), Json, False);
  end;
end;
