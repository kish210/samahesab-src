; =============================================================================
;  SAMA HESAB ERP — CLIENT installer  ->  client.exe
;  نصب روی سیستم‌های کلاینت (صندوق فروشگاه / رستوران)
;  این برنامه از طریق Web API به سرور مرکزی وصل می‌شود (نه پایگاه داده).
;  Self-contained — نیازی به نصب .NET یا SQL Server روی کلاینت نیست.
; =============================================================================

#define AppName       "سما حساب — صندوق"
#define AppVersion    "2.5.20"
#define AppPublisher  "سماع رایانه کیش"
#define AppExe        "SamaHesab.exe"

[Setup]
AppId={{8F2A1C40-7B3E-4D5A-9C11-SAMAHESABCLI01}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\SamaHesab POS
DefaultGroupName=سما حساب
DisableProgramGroupPage=yes
SetupIconFile=..\src\SamaHesab.WPF\app.ico
UninstallDisplayIcon={app}\{#AppExe}
OutputDir=Output
OutputBaseFilename=client
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
Name: "desktopicon"; Description: "ایجاد میانبر صندوق روی دسکتاپ"

[Files]
; فقط برنامه‌ی کلاینت (شامل SamaHesab.exe + pos.exe + restoran.exe + رانتایم)
Source: "..\dist\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; راهنمای کاربر (PDF)
Source: "..\docs\SamaHesab-UserGuide.pdf"; DestDir: "{app}"; DestName: "راهنمای کاربر.pdf"; Flags: ignoreversion

[Icons]
Name: "{group}\راهنمای کاربر";              Filename: "{app}\راهنمای کاربر.pdf"; WorkingDir: "{app}"
Name: "{group}\صندوق فروشگاه";              Filename: "{app}\{#AppExe}"; Parameters: "--pos";        WorkingDir: "{app}"
Name: "{group}\صندوق رستوران";              Filename: "{app}\{#AppExe}"; Parameters: "--restaurant"; WorkingDir: "{app}"
Name: "{group}\صندوق گارسون";              Filename: "{app}\{#AppExe}"; Parameters: "--waiter"; WorkingDir: "{app}"
Name: "{group}\نمایشگر آشپزخانه";          Filename: "{app}\{#AppExe}"; Parameters: "--kitchen"; WorkingDir: "{app}"
Name: "{group}\انبارداری";                 Filename: "{app}\{#AppExe}"; Parameters: "--warehouse"; WorkingDir: "{app}"
Name: "{group}\حذف صندوق سما حساب";        Filename: "{uninstallexe}"
Name: "{commondesktop}\صندوق فروشگاه";       Filename: "{app}\{#AppExe}"; Parameters: "--pos";        WorkingDir: "{app}"; Tasks: desktopicon
Name: "{commondesktop}\صندوق رستوران";       Filename: "{app}\{#AppExe}"; Parameters: "--restaurant"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
; اجرای صندوق فروشگاه پس از نصب
Filename: "{app}\{#AppExe}"; Parameters: "--pos"; Description: "اجرای صندوق فروشگاه"; \
  WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[Code]
var
  ServerPage: TInputQueryWizardPage;

procedure InitializeWizard;
begin
  ServerPage := CreateInputQueryPage(wpSelectDir,
    'اتصال به سرور',
    'آدرس سرور مرکزی سما حساب',
    'آدرس سرور را به‌صورت http://آی‌پی‌سرور:5080 وارد کنید. ' +
    'بعداً هم می‌توانید از دکمه‌ی «تنظیمات اتصال به سرور» در صفحه‌ی ورود آن را تغییر دهید.');
  ServerPage.Add('آدرس سرور (Base URL):', False);
  ServerPage.Add('شناسهٔ شعبهٔ این کلاینت (عدد):', False);
  ServerPage.Values[0] := 'http://192.168.1.10:5080';
  ServerPage.Values[1] := '1';
end;

// پس از نصب: نوشتن آدرس سرور در تنظیمات کاربر تا کلاینت بداند به کجا وصل شود
procedure CurStepChanged(CurStep: TSetupStep);
var
  Dir, Url, Branch, Json: String;
begin
  if CurStep = ssPostInstall then
  begin
    Url := ServerPage.Values[0];
    StringChangeEx(Url, '\', '\\', True);
    Branch := Trim(ServerPage.Values[1]);
    if Branch = '' then Branch := '1';
    Dir := ExpandConstant('{userappdata}\SamaHesab');
    ForceDirectories(Dir);
    // فقط اگر تنظیمات از قبل وجود ندارد بنویس تا پیکربندی موجود پاک نشود
    // (در غیر این صورت کاربر می‌تواند از دکمه‌ی «تنظیمات اتصال به سرور» تغییر دهد)
    if not FileExists(Dir + '\settings.user.json') then
    begin
      Json :=
        '{' + #13#10 +
        '  "Api": {' + #13#10 +
        '    "BaseUrl": "' + Url + '",' + #13#10 +
        '    "BranchId": ' + Branch + #13#10 +
        '  }' + #13#10 +
        '}';
      SaveStringToFile(Dir + '\settings.user.json', Json, False);
    end;
  end;
end;
