; Inno Setup script for Your4Sellers.Desktop
; Build with: iscc.exe /DMyAppVersion=1.2.3 /DMyPublishDir="C:\path\to\publish" /DMyIconPath="C:\path\to\icon.ico" your4sellers.iss

#define MyAppName "Your4Sellers"
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#ifndef MyPublishDir
  #define MyPublishDir "C:\\invalid\\publish"
#endif
#ifndef MyIconPath
  #define MyIconPath AddBackslash(MyPublishDir) + "wwwroot\\icon.ico"
#endif

#define MyAppPublisher "4SELLERS GmbH"
#define MyAppURL "https://www.4sellers.de"
#define MyAppExeName "Your4Sellers.Desktop.exe"
#define MyAppVersionSafe StringChange(MyAppVersion, ".", "_")

[Setup]
AppId={{7F314032-6B66-4A37-A9E0-9F5E2F1E74B1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
ChangesAssociations=no
DisableWelcomePage=yes
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyMemo=yes
DisableReadyPage=yes
DisableFinishedPage=yes
ShowLanguageDialog=no
; Output auch auf das K:-Laufwerk legen (analog zur Toolbox)
OutputDir=K:\Programme\Your4Sellers
OutputBaseFilename=Installer_{#MyAppName}_{#MyAppVersionSafe}
SetupIconFile={#MyIconPath}
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
UsePreviousLanguage=no
UsePreviousTasks=no

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#MyPublishDir}\\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\\{#MyAppName}"; Filename: "{app}\\{#MyAppExeName}"

[Run]
; Start the application automatically after installation
Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Flags: nowait
