; Script generated for File Cleanup Manager Service
; Inno Setup Script

#define MyAppName "File Cleanup Manager (Multi-Tab)"
#define MyAppVersion "1.0"
#define MyAppPublisher "Serik Mufakhidinov"
#define MyAppURL "https://github.com/leitoxa/clean_v2"
#define MyAppExeName "CleanupManager.exe"
#define MyAppServiceName "FileCleanupManager"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
AppId={{A3B2C1D4-E5F6-4A5B-8C9D-1E2F3A4B5C6D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=
OutputDir=output
OutputBaseFilename=FileCleanupManager_Setup_v{#MyAppVersion}
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64
SetupIconFile=
UninstallDisplayIcon={app}\{#MyAppExeName}
; Version Information
VersionInfoVersion={#MyAppVersion}.0.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Service Installer
VersionInfoTextVersion=Version {#MyAppVersion}
VersionInfoCopyright=Copyright (C) 2025 {#MyAppPublisher}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
; Additional Setup Info
AppVerName={#MyAppName} {#MyAppVersion}
AppCopyright=Copyright (C) 2025 {#MyAppPublisher}
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Files]
Source: "CleanupManager.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "FolderConfig.cs"; DestDir: "{app}"; Flags: ignoreversion
Source: "CleanupManager.cs"; DestDir: "{app}"; Flags: ignoreversion
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Dirs]
Name: "{app}\logs"; Permissions: users-full

[INI]
Filename: "{app}\version.txt"; Section: "Version"; Key: "Version"; String: "{#MyAppVersion}"
Filename: "{app}\version.txt"; Section: "Version"; Key: "BuildDate"; String: "{#GetDateTimeString('yyyy-mm-dd HH:nn:ss', '', '')}"
Filename: "{app}\version.txt"; Section: "Version"; Key: "Publisher"; String: "{#MyAppPublisher}"
Filename: "{app}\version.txt"; Section: "Version"; Key: "ProductName"; String: "{#MyAppName}"
Filename: "{app}\version.txt"; Section: "Version"; Key: "ServiceName"; String: "{#MyAppServiceName}"


[Code]
var
  ServiceInstalled: Boolean;

function IsServiceInstalled(): Boolean;
var
  ResultCode: Integer;
begin
  Result := False;
  if Exec('sc', 'query {#MyAppServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Result := (ResultCode = 0);
  end;
end;

procedure StopAndRemoveService();
var
  ResultCode: Integer;
begin
  if IsServiceInstalled() then
  begin
    // Stop service
    Exec('net', 'stop {#MyAppServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(1000);
    
    // Remove service
    Exec('sc', 'delete {#MyAppServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(1000);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    // Install service using InstallUtil
    if Exec(ExpandConstant('{dotnet40}\InstallUtil.exe'), 
            ExpandConstant('"{app}\{#MyAppExeName}"'), 
            '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      if ResultCode = 0 then
      begin
        // Start service
        Exec('net', 'start {#MyAppServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
        
        if ResultCode = 0 then
          MsgBox('{#MyAppName} v{#MyAppVersion}' + #13#10 + 
                 'Service installed and started successfully!' + #13#10#13#10 +
                 'Installation Directory: ' + ExpandConstant('{app}'), 
                 mbInformation, MB_OK)
        else
          MsgBox('Service installed but failed to start. Please start it manually.', mbError, MB_OK);
      end
      else
        MsgBox('Failed to install service. Error code: ' + IntToStr(ResultCode), mbError, MB_OK);
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    StopAndRemoveService();
  end;
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  ServiceInstalled := IsServiceInstalled();
  
  if ServiceInstalled then
  begin
    if MsgBox('The service is already installed. Do you want to stop and update it?', 
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      StopAndRemoveService();
      Result := True;
    end
    else
      Result := False;
  end;
end;

[UninstallDelete]
Type: filesandordirs; Name: "{app}\logs"

[Icons]
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Configure Service"; Flags: postinstall shellexec skipifsilent nowait
