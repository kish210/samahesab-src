; =============================================================================
; SAMA HESAB ERP - Inno Setup Installer Script
; سامانه جامع سما حساب - اسکریپت نصب
; Version 2.0.0
; =============================================================================

#define MyAppName      "سما حساب"
#define MyAppVersion   "2.0.0"
#define MyAppPublisher "سما نرم‌افزار"
#define MyAppURL       "https://www.samanarm.ir"
#define MyAppExeName   "SamaHesab.exe"
#define MyAppId        "{{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} نسخه {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/support
AppUpdatesURL={#MyAppURL}/updates
DefaultDirName={autopf}\SamaHesab
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=Output
OutputBaseFilename=SamaHesab_Setup_v{#MyAppVersion}
SetupIconFile=..\src\SamaHesab.WPF\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
WizardResizable=yes
DisableProgramGroupPage=no
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0.17763
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=سامانه جامع مدیریت کسب و کار
VersionInfoProductName={#MyAppName}

[Languages]
Name: "persian"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon";    Description: "ایجاد میانبر روی دسکتاپ";    GroupDescription: "میانبرها:"; Flags: unchecked
Name: "quicklaunchicon"; Description: "ایجاد میانبر در نوار وظیفه"; GroupDescription: "میانبرها:"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode

[Files]
; برنامهٔ دسکتاپ (خودکفا — شاملِ رانتایم؛ SamaHesab.exe + لانچرها)
Source: "..\dist\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; سرورِ Web API (خودکفا) — برای نصبِ تک‌سیستمی (سرور+کلاینت روی یک دستگاه)
Source: "..\dist\api\*"; DestDir: "{app}\server"; Flags: ignoreversion recursesubdirs createallsubdirs

; User Guide (PDF) — مطابقِ آیکونِ «راهنمای کاربر»
Source: "..\docs\SamaHesab-UserGuide.pdf"; DestDir: "{app}\docs"; DestName: "UserGuide.pdf"; Flags: ignoreversion

; اسکریپت‌های پایگاه‌داده (همهٔ مهاجرت‌ها ۰۱..۲۲ — برای اجرای دستی روی SQL Server)
Source: "..\database\*.sql"; DestDir: "{app}\database"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}";                    Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\راهنمای کاربر";                   Filename: "{app}\docs\UserGuide.pdf"
Name: "{group}\حذف {#MyAppName}";               Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}";            Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
; build خودکفاست؛ نیازی به نصبِ .NET/VC++ نیست. راه‌اندازیِ دستیِ پایگاه‌داده از پوشهٔ database.
; Launch Application
Filename: "{app}\{#MyAppExeName}"; Description: "راه‌اندازی {#MyAppName}"; \
  Flags: nowait postinstall skipifsilent; WorkingDir: "{app}"

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C net stop SamaHesabService 2>nul"; Flags: runhidden

[Registry]
; App registration
Root: HKLM; Subkey: "SOFTWARE\SamaNarmAfzar\SamaHesab"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\SamaNarmAfzar\SamaHesab"; ValueType: string; ValueName: "Version"; ValueData: "{#MyAppVersion}"

; File association for .shdb backup files
Root: HKCR; Subkey: ".shbak"; ValueType: string; ValueName: ""; ValueData: "SamaHesab.Backup"; Flags: uninsdeletekey
Root: HKCR; Subkey: "SamaHesab.Backup"; ValueType: string; ValueName: ""; ValueData: "فایل پشتیبان سما حساب"; Flags: uninsdeletekey
Root: HKCR; Subkey: "SamaHesab.Backup\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"

[Dirs]
Name: "{app}\logs";    Permissions: everyone-full
Name: "{app}\backup";  Permissions: everyone-full
Name: "{app}\reports"; Permissions: everyone-full
Name: "{app}\temp";    Permissions: everyone-full

[Code]
// ─── Check .NET 9 Installation ─────────────────────────────────────────────
function IsDotNetInstalled: Boolean;
var
  ResultCode: Integer;
begin
  Result := ShellExec('', 'dotnet', '--version', '', SW_HIDE, ewWaitUntilTerminated, ResultCode)
    and (ResultCode = 0);
end;

// ─── Check SQL Server ──────────────────────────────────────────────────────
function IsSqlServerInstalled: Boolean;
var
  Subkey: String;
begin
  Subkey := 'SOFTWARE\Microsoft\Microsoft SQL Server';
  Result := RegKeyExists(HKLM, Subkey);
end;

// ─── Check previous installation ──────────────────────────────────────────
function InitializeSetup: Boolean;
begin
  if not IsSqlServerInstalled then
  begin
    MsgBox(
      'هشدار: Microsoft SQL Server روی این سیستم نصب نشده است.' + #13#10 +
      'برای استفاده از سما حساب، ابتدا SQL Server 2019 یا 2022 را نصب کنید.' + #13#10#13#10 +
      'می‌توانید SQL Server Express را به صورت رایگان از سایت مایکروسافت دانلود کنید.',
      mbCriticalError, MB_OK);
    Result := True; // Still allow installation, user might configure later
  end
  else
    Result := True;
end;

// ─── Custom install page ───────────────────────────────────────────────────
var
  ServerPage: TInputQueryWizardPage;
  ServerName: String;
  DatabaseName: String;

procedure InitializeWizard;
begin
  ServerPage := CreateInputQueryPage(wpSelectDir,
    'تنظیمات پایگاه داده',
    'اطلاعات اتصال به SQL Server را وارد کنید',
    'لطفاً مشخصات سرور SQL Server خود را وارد نمایید:');

  ServerPage.Add('نام سرور SQL (مثال: . یا localhost\SQLEXPRESS):', False);
  ServerPage.Add('نام پایگاه داده:', False);

  ServerPage.Values[0] := '.';
  ServerPage.Values[1] := 'SamaHesab';
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  if CurPageID = ServerPage.ID then
  begin
    ServerName   := ServerPage.Values[0];
    DatabaseName := ServerPage.Values[1];

    if ServerName = '' then
    begin
      MsgBox('نام سرور SQL الزامی است.', mbError, MB_OK);
      Result := False;
      Exit;
    end;

    if DatabaseName = '' then
    begin
      MsgBox('نام پایگاه داده الزامی است.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ApiSettingsPath, Srv, Content: String;
begin
  if CurStep = ssPostInstall then
  begin
    // نوشتنِ رشتهٔ اتصال در appsettings سرورِ API (نه برنامهٔ دسکتاپ که از طریق API کار می‌کند)
    ApiSettingsPath := ExpandConstant('{app}\server\appsettings.json');
    if FileExists(ApiSettingsPath) then
    begin
      Srv := ServerName;
      StringChangeEx(Srv, '\', '\\', True);   // فرار دادنِ بک‌اسلش برای JSON
      Content :=
        '{' + #13#10 +
        '  "Urls": "http://0.0.0.0:5080",' + #13#10 +
        '  "ConnectionStrings": {' + #13#10 +
        '    "DefaultConnection": "Server=' + Srv + ';Database=' + DatabaseName +
            ';Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"' + #13#10 +
        '  },' + #13#10 +
        '  "Jwt": {' + #13#10 +
        '    "Issuer": "SamaHesab", "Audience": "SamaHesabClients",' + #13#10 +
        '    "Key": "CHANGE_THIS_TO_A_LONG_RANDOM_SECRET_KEY_AT_LEAST_32_CHARS_LONG_1404",' + #13#10 +
        '    "AccessTokenMinutes": 60, "RefreshTokenDays": 14' + #13#10 +
        '  },' + #13#10 +
        '  "Sms": { "Provider": "null" },' + #13#10 +
        '  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },' + #13#10 +
        '  "AllowedHosts": "*"' + #13#10 +
        '}';
      SaveStringToFile(ApiSettingsPath, Content, False);
    end;
  end;
end;

// ─── Uninstall confirmation ────────────────────────────────────────────────
function InitializeUninstall: Boolean;
begin
  Result := MsgBox(
    'آیا مطمئن هستید که می‌خواهید سما حساب را حذف کنید؟' + #13#10 +
    'اطلاعات پایگاه داده حذف نخواهد شد.',
    mbConfirmation, MB_YESNO) = IDYES;
end;
