; FleetAutomate Windows installer script.
; Compile with Inno Setup:
;   ISCC.exe installer\FleetAutomate.iss

#define MyAppName "FleetAutomate"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "FleetAutomate"
#define MyAppExeName "FleetAutomate.exe"
#define RepoRoot AddBackslash(SourcePath) + ".."
#define PublishDir RepoRoot + "\src\FleetAutomate.Application\bin\Release\net8.0-windows\win-x64\publish"
#define OutputDir RepoRoot + "\website\releases"

[Setup]
AppId={{D7B8B357-6C18-4B8C-8999-91EF6E994F0C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=FleetAutomate-{#MyAppVersion}-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile={#RepoRoot}\src\FleetAutomate.Application\Icons\AppIcon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
