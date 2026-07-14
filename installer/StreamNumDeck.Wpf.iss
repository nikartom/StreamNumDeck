#ifndef MyAppVersion
  #define MyAppVersion "1.2.0"
#endif
#ifndef MyFileVersion
  #define MyFileVersion "1.2.0.0"
#endif
#ifndef MySourceDir
  #error MySourceDir must point to the portable application directory.
#endif
#ifndef MyOutputDir
  #error MyOutputDir must point to the release artifacts directory.
#endif

#define MyAppName "StreamNumDeck"
#define MyAppPublisher "nikartom"
#define MyAppExeName "StreamNumDeck.exe"

[Setup]
AppId={{7568F934-048E-4E96-8D84-51EC4CB6BC30}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
VersionInfoVersion={#MyFileVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} installer
VersionInfoCopyright=Copyright (C) 2026 {#MyAppPublisher}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/nikartom/StreamNumDeck
AppSupportURL=https://github.com/nikartom/StreamNumDeck/issues
AppUpdatesURL=https://github.com/nikartom/StreamNumDeck/releases
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#MyOutputDir}
OutputBaseFilename=StreamNumDeck-{#MyAppVersion}-Setup
SetupIconFile=..\src\StreamNumDeck.App\Assets\AppIcon.ico
LicenseFile={#MySourceDir}\LICENSE.md
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
MinVersion=10.0.18362
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#MySourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "StreamNumDeck"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
