; MX4 Game Haptics Installer
; Inno Setup Script

#define MyAppName "MX4 Game Haptics"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "MX4GameHaptics"
#define MyAppURL "https://github.com/user/MX4GameHaptics"
#define MyAppExeName "MX4HapticService.exe"
#define MyAppConfiguratorName "HapticConfigurator.exe"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=..\LICENSE
OutputDir=output
OutputBaseFilename=MX4GameHaptics-Setup-{#MyAppVersion}
SetupIconFile=..\tools\MX4HapticService\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Start with Windows"; GroupDescription: "Startup:"

[Files]
; Main service files
Source: "publish\service\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Configurator files
Source: "publish\configurator\*"; DestDir: "{app}\Configurator"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\MX4 Haptic Service"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"
Name: "{group}\MX4 Haptic Configurator"; Filename: "{app}\Configurator\{#MyAppConfiguratorName}"; IconFilename: "{app}\Configurator\app.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\MX4 Haptic Service"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"; Tasks: desktopicon
Name: "{autodesktop}\MX4 Haptic Configurator"; Filename: "{app}\Configurator\{#MyAppConfiguratorName}"; IconFilename: "{app}\Configurator\app.ico"; Tasks: desktopicon

[Registry]
; Add to startup if selected
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "MX4HapticService"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startupicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,MX4 Haptic Service}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Stop the service before uninstall
Filename: "taskkill"; Parameters: "/F /IM MX4HapticService.exe"; Flags: runhidden; RunOnceId: "StopService"

[Code]
// Check for ViGEmBus driver
function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;

  // Check if ViGEmBus is installed by looking for the driver
  if not RegKeyExists(HKEY_LOCAL_MACHINE, 'SYSTEM\CurrentControlSet\Services\ViGEmBus') then
  begin
    if MsgBox('ViGEmBus driver is not installed. This driver is required for the virtual controller.' + #13#10 + #13#10 +
              'Would you like to download it now?', mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', 'https://github.com/nefarius/ViGEmBus/releases', '', '', SW_SHOW, ewNoWait, ResultCode);
    end;

    if MsgBox('Continue installation without ViGEmBus?' + #13#10 +
              '(You can install it later)', mbConfirmation, MB_YESNO) = IDNO then
    begin
      Result := False;
    end;
  end;
end;
