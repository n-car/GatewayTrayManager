; Gateway Tray Manager Installer Script
; Requires Inno Setup 6.x - Download from https://jrsoftware.org/isinfo.php

#define MyAppName "Gateway Tray Manager"
#define MyAppVersion "1.1.0"
#define MyAppPublisher "Nicola Carpanese"
#define MyAppURL "https://github.com/n-car/GatewayTrayManager"
#define MyAppExeName "GatewayTrayManager.exe"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
AppId={{B2C3D4E5-F6A7-8901-BCDE-F12345678901}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
; Require admin privileges for installation (needed for service management)
PrivilegesRequired=admin
; PrivilegesRequiredOverridesAllowed=dialog  ; Disabled - admin only
OutputDir=output
OutputBaseFilename=GatewayTrayManager_Setup_{#MyAppVersion}
; Setup icon - uncomment the line below after generating the icon
SetupIconFile=..\src\GatewayTrayManager\Resources\app.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Minimum Windows version
MinVersion=10.0
UninstallDisplayIcon={app}\{#MyAppExeName}
; Close running applications automatically
CloseApplications=force
CloseApplicationsFilter=*.exe
RestartApplications=yes
; Upgrade detection - use same install directory as previous version
UsePreviousAppDir=yes
; Detect previous installations and allow upgrade
UsePreviousGroup=yes
UsePreviousTasks=yes
; Update mode - allow upgrading existing installation
UpdateUninstallLogAppName=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"

[CustomMessages]
; English messages
english.StartupOptions=Startup Options:
english.StartAutomatically=Start automatically with Windows
english.UpgradeMessage=%1 version %2 is already installed.%n%nDo you want to upgrade to version %3?%n%nYour settings will be preserved.
english.AppRunningInstall=%1 is currently running.%n%nThe installer needs to close it to continue.%n%nClick OK to close the application automatically,%nor Cancel to exit the installer.
english.AppRunningUninstall=%1 is currently running.%n%nThe uninstaller needs to close it to continue.%n%nClick OK to close the application automatically,%nor Cancel to exit the uninstaller.
english.CouldNotClose=Could not close %1.%n%nPlease close it manually and run the installer again.
english.CouldNotCloseUninstall=Could not close %1.%n%nPlease close it manually and run the uninstaller again.
english.DeleteConfigTitle=Delete Configuration?
english.DeleteConfigMessage=Do you want to delete the configuration file (appsettings.json)?%n%nThis file contains your settings (Gateway URL, credentials, etc.).%n%nClick Yes to delete it, or No to keep it for future installations.

; Italian messages
italian.StartupOptions=Opzioni di avvio:
italian.StartAutomatically=Avvia automaticamente con Windows
italian.UpgradeMessage=%1 versione %2 è già installato.%n%nVuoi aggiornare alla versione %3?%n%nLe tue impostazioni saranno preservate.
italian.AppRunningInstall=%1 è attualmente in esecuzione.%n%nL'installer deve chiuderlo per continuare.%n%nClicca OK per chiudere l'applicazione automaticamente,%no Annulla per uscire dall'installer.
italian.AppRunningUninstall=%1 è attualmente in esecuzione.%n%nIl programma di disinstallazione deve chiuderlo per continuare.%n%nClicca OK per chiudere l'applicazione automaticamente,%no Annulla per uscire.
italian.CouldNotClose=Impossibile chiudere %1.%n%nChiudilo manualmente e riavvia l'installer.
italian.CouldNotCloseUninstall=Impossibile chiudere %1.%n%nChiudilo manualmente e riavvia il programma di disinstallazione.
italian.DeleteConfigTitle=Eliminare la configurazione?
italian.DeleteConfigMessage=Vuoi eliminare il file di configurazione (appsettings.json)?%n%nQuesto file contiene le tue impostazioni (URL Gateway, credenziali, ecc.).%n%nClicca Sì per eliminarlo, o No per conservarlo per future installazioni.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "{cm:StartAutomatically}"; GroupDescription: "{cm:StartupOptions}"; Flags: unchecked

[Files]
; Main application files (self-contained publish output)
Source: "..\src\GatewayTrayManager\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Configuration file (only if doesn't exist - preserve user settings on upgrade)
Source: "..\src\GatewayTrayManager\appsettings.json"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Add to Windows startup if selected
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startupicon

[Run]
; Option to run application after installation
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent runascurrentuser

[UninstallRun]
; Close the application before uninstalling
Filename: "taskkill"; Parameters: "/F /IM {#MyAppExeName}"; Flags: runhidden; RunOnceId: "KillApp"

[UninstallDelete]
; Clean up log files but preserve config
Type: filesandordirs; Name: "{app}\logs"

[Code]
// Global variable to store previous version
var
  PreviousVersion: string;
  IsUpgrade: Boolean;

// Get the installed version from registry
function GetInstalledVersion(): string;
var
  Version: string;
begin
  Result := '';
  if RegQueryStringValue(HKLM, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#SetupSetting("AppId")}_is1', 
                         'DisplayVersion', Version) then
    Result := Version
  else if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#SetupSetting("AppId")}_is1', 
                              'DisplayVersion', Version) then
    Result := Version;
end;

// Check if previous version is installed
function IsPreviousVersionInstalled(): Boolean;
begin
  Result := (GetInstalledVersion() <> '');
end;

// Check if a process is running by name
function IsAppRunning(const FileName: string): Boolean;
var
  FSWbemLocator: Variant;
  FWMIService: Variant;
  FWbemObjectSet: Variant;
begin
  Result := False;
  try
    FSWbemLocator := CreateOleObject('WBEMScripting.SWBEMLocator');
    FWMIService := FSWbemLocator.ConnectServer('localhost', 'root\CIMV2', '', '');
    FWbemObjectSet := FWMIService.ExecQuery('SELECT * FROM Win32_Process WHERE Name="' + FileName + '"');
    Result := (FWbemObjectSet.Count > 0);
  except
    // WMI not available, try tasklist method
    Result := False;
  end;
end;

// Kill a running process
function KillApp(const FileName: string): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('taskkill', '/F /IM "' + FileName + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  if Result then
    Sleep(1000);  // Wait for process to close
end;

// Called at the start of installation
function InitializeSetup(): Boolean;
var
  Response: Integer;
  Attempts: Integer;
begin
  Result := True;
  Attempts := 0;

  // Check for previous version
  PreviousVersion := GetInstalledVersion();
  IsUpgrade := (PreviousVersion <> '');

  if IsUpgrade then
  begin
    // Show upgrade message
    Response := MsgBox(FmtMessage(CustomMessage('UpgradeMessage'), ['{#MyAppName}', PreviousVersion, '{#MyAppVersion}']),
                       mbConfirmation, MB_YESNO);

    if Response = IDNO then
    begin
      Result := False;
      Exit;
    end;
  end;

  // Check if the application is running
  while IsAppRunning('{#MyAppExeName}') and (Attempts < 3) do
  begin
    if Attempts = 0 then
    begin
      Response := MsgBox(FmtMessage(CustomMessage('AppRunningInstall'), ['{#MyAppName}']),
                         mbConfirmation, MB_OKCANCEL);

      if Response = IDCANCEL then
      begin
        Result := False;
        Exit;
      end;
    end;

    // Try to kill the application
    KillApp('{#MyAppExeName}');
    Attempts := Attempts + 1;

    // Wait and check again
    Sleep(500);
  end;

  // Final check
  if IsAppRunning('{#MyAppExeName}') then
  begin
    MsgBox(FmtMessage(CustomMessage('CouldNotClose'), ['{#MyAppName}']),
           mbError, MB_OK);
    Result := False;
  end;
end;

// Called at the start of uninstallation
function InitializeUninstall(): Boolean;
var
  Response: Integer;
begin
  Result := True;

  // Check if the application is running
  if IsAppRunning('{#MyAppExeName}') then
  begin
    Response := MsgBox(FmtMessage(CustomMessage('AppRunningUninstall'), ['{#MyAppName}']),
                       mbConfirmation, MB_OKCANCEL);

    if Response = IDCANCEL then
    begin
      Result := False;
      Exit;
    end;

    // Try to kill the application
    if not KillApp('{#MyAppExeName}') then
    begin
      if IsAppRunning('{#MyAppExeName}') then
      begin
        MsgBox(FmtMessage(CustomMessage('CouldNotCloseUninstall'), ['{#MyAppName}']),
               mbError, MB_OK);
        Result := False;
        Exit;
      end;
    end;
  end;
end;

// Clean up registry on uninstall
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ConfigPath: string;
begin
  if CurUninstallStep = usUninstall then
  begin
    // Ask user if they want to delete the configuration file
    ConfigPath := ExpandConstant('{app}\appsettings.json');
    if FileExists(ConfigPath) then
    begin
      if MsgBox(CustomMessage('DeleteConfigMessage'), mbConfirmation, MB_YESNO) = IDYES then
      begin
        DeleteFile(ConfigPath);
      end;
    end;
  end;

  if CurUninstallStep = usPostUninstall then
  begin
    // Remove startup registry entry
    RegDeleteValue(HKEY_LOCAL_MACHINE, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#MyAppName}');
  end;
end;
