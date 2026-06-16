; =============================================================================
; SAMA HESAB ERP - Inno Setup Installer Script
; Version: 1.0.0
; =============================================================================

#define MyAppName "سما حساب"
#define MyAppNameEn "SamaHesab"
#define MyAppVersion "2.5.0"
#define MyAppPublisher "سما نرم‌افزار"
#define MyAppURL "https://www.samaerp.ir"
#define MyAppExeName "SamaHesab.exe"
#define MyAppGUID "{{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}"

[Setup]
AppId={#MyAppGUID}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppNameEn}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\dist
OutputBaseFilename=SamaHesab_Setup_{#MyAppVersion}
SetupIconFile=..\src\SamaHesab.WPF\Assets\Icons\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
WizardResizable=no
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
PrivilegesRequired=admin
LanguageDetectionMethod=none
ShowLanguageDialog=no
ShowTasksTreeLines=yes
MinVersion=10.0.17763
ArchitecturesInstallIn64BitMode=x64os
ArchitecturesAllowed=x64os

[Languages]
Name: "persian"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "ایجاد آیکن روی دسکتاپ"; GroupDescription: "آیکن‌های اضافی:"; Flags: unchecked
Name: "quicklaunchicon"; Description: "ایجاد آیکن در نوار وظیفه"; GroupDescription: "آیکن‌های اضافی:"

[Dirs]
Name: "{app}"
Name: "{app}\Logs"
Name: "{commonappdata}\SamaHesab"
Name: "{commonappdata}\SamaHesab\Backup"

[Files]
; Main application
Source: "..\src\SamaHesab.WPF\bin\Release\net9.0-windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; Config
Source: "..\src\SamaHesab.WPF\appsettings.json"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist

; Fonts
Source: "..\src\SamaHesab.WPF\Assets\Fonts\*"; DestDir: "{app}\Assets\Fonts"; Flags: ignoreversion recursesubdirs

; SQL Scripts
Source: "..\database\*.sql"; DestDir: "{app}\Database"; Flags: ignoreversion

; Prerequisites check
Source: "prerequisites\*"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\راهنما"; Filename: "{app}\Help.pdf"; Check: FileExists('{app}\Help.pdf')
Name: "{group}\پشتیبانی آنلاین"; Filename: "{#MyAppURL}"
Name: "{group}\حذف {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
; Install fonts
Filename: "{sys}\cmd.exe"; Parameters: "/C copy ""{app}\Assets\Fonts\Vazirmatn-Regular.ttf"" ""{fonts}"""; Flags: runhidden
Filename: "{sys}\cmd.exe"; Parameters: "/C copy ""{app}\Assets\Fonts\Vazirmatn-Bold.ttf"" ""{fonts}"""; Flags: runhidden
Filename: "{sys}\cmd.exe"; Parameters: "/C copy ""{app}\Assets\Fonts\Vazirmatn-SemiBold.ttf"" ""{fonts}"""; Flags: runhidden

; Run app after install
Filename: "{app}\{#MyAppExeName}"; Description: "اجرای {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\Logs"

[Registry]
Root: HKLM; Subkey: "SOFTWARE\{#MyAppNameEn}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\{#MyAppNameEn}"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"
Root: HKLM; Subkey: "SOFTWARE\{#MyAppNameEn}"; ValueType: string; ValueName: "Version"; ValueData: "{#MyAppVersion}"

[Messages]
WelcomeLabel1=به برنامه نصب %1 خوش آمدید
WelcomeLabel2=این برنامه %1 نسخه %2 را روی رایانه شما نصب می‌کند.%n%nپیش از ادامه توصیه می‌شود تمام برنامه‌های در حال اجرا را ببندید.
FinishedLabel=نصب %1 با موفقیت انجام شد.
ClickFinish=برای خروج از برنامه نصب روی دکمه پایان کلیک کنید.

[Code]
// Check .NET 9 installation
function IsDotNet9Installed(): Boolean;
var
  KeyName: string;
  InstallPath: string;
begin
  KeyName := 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sdk';
  Result := RegQueryStringValue(HKLM, KeyName, '9.0.0', InstallPath);
  if not Result then
    Result := RegKeyExists(HKLM, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{BEB1B5BC-B91D-4285-A2C8-9D1E64E4F8B2}');
end;

// Check SQL Server
function IsSqlServerInstalled(): Boolean;
begin
  Result := RegKeyExists(HKLM, 'SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL') or
            RegKeyExists(HKLM, 'SOFTWARE\Microsoft\Microsoft SQL Server Local DB\Installed Versions');
end;

procedure InitializeWizard;
begin
  // Custom initialization
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ErrorMsg: String;
begin
  Result := '';
  ErrorMsg := '';

  if not IsDotNet9Installed() then
    ErrorMsg := ErrorMsg + '.NET 9 Runtime نصب نشده است. لطفاً ابتدا آن را نصب کنید.'#13#10;

  if not IsSqlServerInstalled() then
    ErrorMsg := ErrorMsg + 'SQL Server یافت نشد. لطفاً SQL Server 2019 یا بالاتر را نصب کنید.'#13#10;

  if ErrorMsg <> '' then
    Result := 'پیش‌نیازهای زیر نصب نشده‌اند:'#13#10 + ErrorMsg;
end;
