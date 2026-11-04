; Inno Setup 6 Script for KiteWin (Windows 11)
; Conforms to BAWA per-user installation standards: zero UAC prompt, clean uninstall.

#define MyAppName "KiteWin"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Antigravity Team"
#define MyAppURL "https://github.com/tasonai/KiteWindows"
#define MyAppExeName "KiteWin.exe"
#define MyAppSourceDir "..\src\KiteWin\bin\Release\net10.0-windows10.0.26100.0\win-x64\publish"

[Setup]
; Basic Application Info
AppId={{5E6F7A8B-9C0D-1E2F-3A4B-5C6D7E8F9A0B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; Per-user installation: installs into %LOCALAPPDATA%\Programs\KiteWin without admin rights
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename={#MyAppName}-{#MyAppVersion}-Setup
SetupIconFile=..\src\KiteWin\Assets\AppIcon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

; Modern Compression
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

; Architecture: 64-bit Windows 10/11
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Copy all published files and subdirectories
Source: "{#MyAppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\src\KiteWin\Assets\AppIcon.ico"; DestDir: "{app}\Assets"; Flags: ignoreversion

[Icons]
; Start Menu and Desktop shortcuts with high-res icon
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\AppIcon.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\AppIcon.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
