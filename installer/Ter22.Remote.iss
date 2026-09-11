#define MyAppName "Ter22 Remote"
#define MyAppVersion "0.1.1"
#define MyAppExeName "Ter22.Remote.exe"

[Setup]
AppId={{61B9F010-FA88-47AB-B884-853D5EBF5FC2}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=ter22
DefaultDirName={localappdata}\Programs\Ter22 Remote
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19045
OutputDir=..\artifacts\installer
OutputBaseFilename=Ter22-Remote-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
AppMutex=Local\Ter22.Remote
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "romanian"; MessagesFile: "Languages\Romanian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Creeaza o scurtatura pe desktop"; GroupDescription: "Optiuni:"; Flags: unchecked

[Files]
Source: "..\artifacts\windows-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Deschide Ter22 Remote"; Flags: nowait postinstall skipifsilent
