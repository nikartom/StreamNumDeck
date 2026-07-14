#ifndef MyAppVersion
  #define MyAppVersion "0.2.0-alpha.1"
#endif
#ifndef MySourceDir
  #error MySourceDir must point to the portable application directory.
#endif

#define MyAppName "StreamNumDeck"
#define MyAppPublisher "Nikolay Karchevskiy"
#define MyAppExeName "StreamNumDeck.exe"

[Setup]
AppId={{7568F934-048E-4E96-8D84-51EC4CB6BC30}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/nikartom/StreamNumDeck
AppSupportURL=https://github.com/nikartom/StreamNumDeck/issues
AppUpdatesURL=https://github.com/nikartom/StreamNumDeck/releases
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\artifacts\wpf
OutputBaseFilename=StreamNumDeck-{#MyAppVersion}-Setup
SetupIconFile=..\src\StreamNumDeck.App\Assets\AppIcon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
MinVersion=10.0.22000
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

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
