#define AppName "CrashLens"
#define AppVersion "0.1.0"
#define AppPublisher "CrashLens contributors"
#define AppExeName "CrashLens.exe"

[Setup]
AppId={{3D16D931-9C59-4293-8497-AE7D8EB089F9}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\CrashLens
DefaultGroupName=CrashLens
LicenseFile=LICENSE.txt
OutputDir=..\artifacts\installer
OutputBaseFilename=CrashLens-Setup-0.1.0
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#AppExeName}

[Files]
Source: "..\artifacts\CrashLens-release-final\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\CrashLens"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\CrashLens"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch CrashLens"; Flags: nowait postinstall skipifsilent
