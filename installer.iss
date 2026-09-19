; SnapNote Studio Inno Setup Script

#define MyAppName "SnapNote Studio"
#define MyAppVersion "4.1.0"
#define MyAppPublisher "SnapNote Studio Team"
#define MyAppExeName "SnapNoteStudio.exe"
#define MyAppDescription "Screenshot capture and annotation tool"

[Setup]
AppId={{B3F7A2D1-8E4C-4F5A-9D6B-1C2E3F4A5B6C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppSupportURL=https://github.com/snapnote-studio
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=installer_output
OutputBaseFilename=SnapNoteStudio_Setup_{#MyAppVersion}
SetupIconFile=Resources\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
#ifdef NOUIACCESS
PrivilegesRequired=lowest
#else
; uiAccess executables must be installed in a secure location such as Program Files.
PrivilegesRequired=admin
#endif
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#ifdef SIGN
SignTool=snapnotesign
SignedUninstaller=yes
#endif

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{#MyAppName} をアンインストール"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[InstallDelete]
Type: files; Name: "{userstartup}\{#MyAppName}.lnk"

[UninstallDelete]
Type: files; Name: "{userstartup}\{#MyAppName}.lnk"

[UninstallRun]
Filename: "reg.exe"; Parameters: "delete ""HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"" /v ""SnapNoteStudio"" /f"; Flags: runhidden; RunOnceId: "RemoveStartupRegistryValue"

[Run]
; uiAccess executables must be launched through ShellExecute/AppInfo.
Filename: "{app}\{#MyAppExeName}"; Description: "{#MyAppName} を起動"; Flags: nowait postinstall skipifsilent shellexec
