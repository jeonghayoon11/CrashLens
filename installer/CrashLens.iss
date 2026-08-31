#define AppName "CrashLens"
#define AppVersion "0.1.11"
#define AppPublisher "Jeong Hayoon"
#define AppExeName "CrashLens.exe"

[Setup]
AppId={{3D16D931-9C59-4293-8497-AE7D8EB089F9}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://jhynx.com
AppSupportURL=mailto:contact@jhynx.com
ShowLanguageDialog=yes
DefaultDirName={autopf}\CrashLens
DefaultGroupName=CrashLens
LicenseFile=..\LICENSE
OutputDir=..\artifacts\installer-0.1.11
OutputBaseFilename=CrashLens-Setup-0.1.11
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#AppExeName}
CloseApplications=yes

[Files]
Source: "..\artifacts\CrashLens-release-0.1.11\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\docs\images\crashlens.ico"; DestDir: "{app}"; DestName: "CrashLens-0.1.7.ico"; Flags: ignoreversion

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "CrashLens"; ValueData: """{app}\{#AppExeName}"" --background"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\CrashLens"; ValueType: string; ValueName: "NotificationLanguage"; ValueData: "{language}"; Flags: uninsdeletevalue

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Icons]
Name: "{group}\CrashLens"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\CrashLens-0.1.7.ico"
Name: "{autodesktop}\CrashLens"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\CrashLens-0.1.7.ico"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch CrashLens"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; The main window closes to the tray, so explicitly stop the monitor before files are removed.
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM ""{#AppExeName}"""; Flags: runhidden waituntilterminated

[UninstallDelete]
; Remove residual runtime files as well as the files tracked by the installer.
Type: filesandordirs; Name: "{app}"
