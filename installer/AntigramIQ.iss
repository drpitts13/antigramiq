#define MyAppName "AntigramIQ"
#define MyAppVersion "2.0"
#define MyAppPublisher "AntigramIQ"
#define MyAppExeName "AntigramIQ.exe"
#define PublishDir "..\AntigramIQ\bin\Release\net8.0-windows\win-x64\publish"

[Setup]
AppId={{A8E3C4D1-7B2F-4E9A-91C6-3D5F8A0B2E17}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\AntigramIQ
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=AntigramIQ-Setup-2.0
#ifexist "..\AntigramIQ\app.ico"
SetupIconFile=..\AntigramIQ\app.ico
#endif
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; Shortcuts and executables left by releases named "Antibody Panel Management System" / AntibodyPanels.
Type: files; Name: "{autoprograms}\Antibody Panel Management System.lnk"
Type: files; Name: "{autodesktop}\Antibody Panel Management System.lnk"
Type: files; Name: "{app}\AntibodyPanels.exe"
Type: files; Name: "{app}\AntibodyPanels.dll"
Type: files; Name: "{app}\AntibodyPanels.pdb"
Type: files; Name: "{app}\AntibodyPanels.deps.json"
Type: files; Name: "{app}\AntibodyPanels.runtimeconfig.json"

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
