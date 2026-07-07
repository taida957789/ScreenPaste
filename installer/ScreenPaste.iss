; Inno Setup script for ScreenPaste — per-user install (no admin).
; Version is injected from CI:  ISCC /DMyAppVersion=1.2.3 installer\ScreenPaste.iss

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#define MyAppName "ScreenPaste"
#define MyAppExeName "ScreenPaste.exe"
#define MyAppPublisher "taida957789"
#define MyAppURL "https://github.com/taida957789/ScreenPaste"

[Setup]
AppId={{8F3A6C21-5B7E-4D9A-9C2F-1E4B7A6D3C88}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
VersionInfoVersion={#MyAppVersion}

; Per-user install under %APPDATA%\ScreenPaste — no administrator rights required.
PrivilegesRequired=lowest
DefaultDirName={userappdata}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

OutputDir=..\dist
OutputBaseFilename=ScreenPaste-{#MyAppVersion}-setup
SetupIconFile=..\src\ScreenPaste\Assets\app.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Tasks]
Name: "desktopicon"; Description: "建立桌面捷徑"; GroupDescription: "捷徑:"
Name: "startup"; Description: "開機時自動啟動 ScreenPaste"; GroupDescription: "選項:"; Flags: unchecked

[Files]
Source: "..\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\解除安裝 {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Optional run-at-startup (per-user Run key). The app's own tray toggle also manages this.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ScreenPaste"; ValueData: """{app}\{#MyAppExeName}"""; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "立即啟動 {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Best-effort: close a running instance before uninstalling.
Filename: "{cmd}"; Parameters: "/C taskkill /IM {#MyAppExeName} /F"; Flags: runhidden; RunOnceId: "KillApp"
