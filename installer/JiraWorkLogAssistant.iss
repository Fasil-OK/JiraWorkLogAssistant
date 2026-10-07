; Inno Setup script for Jira WorkLog Assistant
; Builds a self-contained, per-user installer (no admin, no .NET prerequisite).

#define MyAppName "Jira WorkLog Assistant"
#define MyAppVersion "2.2.0"
#define MyAppPublisher "Jira WorkLog Assistant"
#define MyAppExeName "JiraWorkLogAssistant.exe"

[Setup]
; A unique GUID identifies this app for upgrades/uninstall. Keep it stable across versions.
AppId={{8F3C1E92-7B4D-4A1C-9E2F-5A6B7C8D9E01}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\JiraWorkLogAssistant
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; Per-user install -> no administrator rights required.
PrivilegesRequired=lowest
OutputDir=..\publish\installer
OutputBaseFilename=JiraWorkLogAssistant-Setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"
Name: "startupicon"; Description: "Start {#MyAppName} automatically when Windows starts"; GroupDescription: "Startup:"

[Files]
; Everything from the self-contained publish folder.
Source: "..\publish\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Optional run-at-startup (per-user). Mirrors the in-app "Launch on Windows Startup" setting.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; \
    ValueName: "JiraWorkLogAssistant"; ValueData: """{app}\{#MyAppExeName}"""; \
    Flags: uninsdeletevalue; Tasks: startupicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Remove the local app data (database, logs, settings) on uninstall.
Type: filesandordirs; Name: "{localappdata}\JiraWorkLogAssistant"
