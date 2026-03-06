; Gateway Tray Manager Installer Script
; Requires Inno Setup 6.x - Download from https://jrsoftware.org/isinfo.php

#define MyAppName "Gateway Tray Manager"
#define MyAppVersion "1.0.0"
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

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Start automatically with Windows"; GroupDescription: "Startup Options:"; Flags: unchecked

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
  
  // Check if the application is running
  while IsAppRunning('{#MyAppExeName}') and (Attempts < 3) do
  begin
    if Attempts = 0 then
    begin
      Response := MsgBox('{#MyAppName} is currently running.' + #13#10 + #13#10 +
                         'The installer needs to close it to continue.' + #13#10 + #13#10 +
                         'Click OK to close the application automatically,' + #13#10 +
                         'or Cancel to exit the installer.',
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
    MsgBox('Could not close {#MyAppName}.' + #13#10 + #13#10 +
           'Please close it manually and run the installer again.',
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
    Response := MsgBox('{#MyAppName} is currently running.' + #13#10 + #13#10 +
                       'The uninstaller needs to close it to continue.' + #13#10 + #13#10 +
                       'Click OK to close the application automatically,' + #13#10 +
                       'or Cancel to exit the uninstaller.',
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
        MsgBox('Could not close {#MyAppName}.' + #13#10 + #13#10 +
               'Please close it manually and run the uninstaller again.',
               mbError, MB_OK);
        Result := False;
        Exit;
      end;
    end;
  end;
end;

// Clean up registry on uninstall
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Remove startup registry entry
    RegDeleteValue(HKEY_LOCAL_MACHINE, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#MyAppName}');
  end;
end;
