; =============================================================================
;  SAMA HESAB ERP — SERVER installer  ->  setup.exe
;  نصب کامل روی سیستم سرور: برنامه حسابداری + سرور Web API + پایگاه داده
;  Self-contained (.NET همراه برنامه است؛ نیازی به نصب جداگانه‌ی .NET نیست)
;  پیش‌نیاز فقط: Microsoft SQL Server (Express یا بالاتر)
; =============================================================================

#define AppName       "سما حساب — سرور"
#define AppVersion    "2.5.7"
#define AppPublisher  "سماع رایانه کیش"
#define AppExe        "SamaHesab.exe"
#define ApiExe        "SamaHesab.API.exe"

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
Name: "{group}\راه‌اندازی سرور (API)";    Filename: "{app}\server\{#ApiExe}"; WorkingDir: "{app}\server"
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

; اجرای سرور پس از نصب
Filename: "{app}\server\{#ApiExe}"; Description: "راه‌اندازی سرور (API)"; \
  WorkingDir: "{app}\server"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "netsh.exe"; Parameters: "advfirewall firewall delete rule name=""SamaHesab API 5080"""; Flags: runhidden; RunOnceId: "DelFwRule5080"

[Registry]
; اجرای خودکار سرور هنگام ورود کاربر
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; \
  ValueName: "SamaHesabServer"; ValueData: """{app}\server\{#ApiExe}"""; \
  Flags: uninsdeletevalue; Tasks: autostartapi

[Code]
var
  SqlPage: TInputQueryWizardPage;
  CreateDbPage: TInputOptionWizardPage;

procedure InitializeWizard;
begin
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
