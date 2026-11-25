; Open this script in Inno Setup after a Release build, or double-click Build installer.cmd.
; Explicit files only: never package a build folder with a wildcard.
#ifndef MainBin
  #define MainBin SourcePath + "..\LicenseKeyGenerator\bin\Release"
#endif
#ifndef LegacyBin
  #define LegacyBin SourcePath + "..\KeyGenerator\bin\Release"
#endif
#ifndef DocsDir
  #define DocsDir SourcePath
#endif
#ifndef PackageOutput
  #define PackageOutput SourcePath + "..\Output"
#endif
#if !FileExists(MainBin + "\LicenseKeyGenerator.exe")
  #error Build LicenseKeyGenerator.sln in Release first, or double-click Build installer.cmd.
#endif
#define VersionMS
#define VersionLS
#define AppVersion GetVersionNumbers(MainBin + "\LicenseKeyGenerator.exe", VersionMS, VersionLS)
#ifndef TestPackage
  #define ProductId "{EB657910-23A7-4DDC-8204-3A8DFB841B62}"
  #define ProductName "Quickhand POS LicenseKeyGenerator"
  #define PackageName "LicenseKeyGenerator-Setup-" + AppVersion
#else
  ; The smoke test uses a separate identity, no shortcuts, no uninstall registry.
  #define ProductId "{455E2C61-FF92-420F-8766-6E7B78850966}"
  #define ProductName "LicenseKeyGenerator isolated test"
  #define PackageName "LicenseKeyGenerator-Test-" + AppVersion
#endif

[Setup]
AppId={{#ProductId}
AppName={#ProductName}
AppVersion={#AppVersion}
AppPublisher=Quickhand POS
AppVerName={#ProductName} {#AppVersion}
VersionInfoVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\QuickhandPOS\LicenseKeyGenerator
DefaultGroupName=Quickhand POS LicenseKeyGenerator
PrivilegesRequired=lowest
MinVersion=10.0
WizardStyle=modern
DisableProgramGroupPage=yes
UsePreviousAppDir=yes
UsePreviousTasks=yes
UninstallDisplayIcon={app}\LicenseKeyGenerator.exe
UninstallLogMode=append
AppMutex=Local\QuickhandPOS-LicenseKeyGenerator,Local\QuickhandPOS-LegacyKeyGenerator
CloseApplications=no
RestartApplications=no
SetupLogging=yes
OutputDir={#PackageOutput}
OutputBaseFilename={#PackageName}
Compression=lzma2
SolidCompression=yes
DisableWelcomePage=no
#ifdef TestPackage
CreateUninstallRegKey=no
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[LangOptions]
DialogFontName=Segoe UI
DialogFontSize=12
WelcomeFontName=Segoe UI
WelcomeFontSize=16

[Tasks]
#ifndef TestPackage
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: checkedonce
#endif

[Files]
Source: "{#MainBin}\LicenseKeyGenerator.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MainBin}\LicenseKeyGenerator.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MainBin}\LicenseKeyGenerator.Core.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MainBin}\Newtonsoft.Json.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#LegacyBin}\KeyGenerator.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#LegacyBin}\KeyGenerator.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#DocsDir}\Usage.html"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#DocsDir}\ThirdPartyNotices.txt"; DestDir: "{app}"; Flags: ignoreversion

; No InstallDelete/UninstallDelete: files created by the operator must survive.
[Icons]
#ifndef TestPackage
Name: "{group}\LicenseKeyGenerator"; Filename: "{app}\LicenseKeyGenerator.exe"; WorkingDir: "{app}"
Name: "{group}\Legacy machine code"; Filename: "{app}\KeyGenerator.exe"; WorkingDir: "{app}"
Name: "{group}\Instructions"; Filename: "{app}\Usage.html"
Name: "{userdesktop}\LicenseKeyGenerator"; Filename: "{app}\LicenseKeyGenerator.exe"; WorkingDir: "{app}"; Tasks: desktopicon
#endif

[Run]
#ifndef TestPackage
Filename: "{app}\LicenseKeyGenerator.exe"; Description: "Open LicenseKeyGenerator"; Flags: nowait postinstall skipifsilent unchecked
#endif

[Code]
function InitializeSetup: Boolean;
begin
  Result := IsDotNetInstalled(net48, 0);
  if not Result then
    SuppressibleMsgBox('Microsoft .NET Framework 4.8 or later is required.'#13#10 +
      'Install it from Microsoft, then run this installer again.', mbError, MB_OK, IDOK);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ExistingMS, ExistingLS: Cardinal;
begin
  Result := '';
  if GetVersionNumbers(ExpandConstant('{app}\LicenseKeyGenerator.exe'), ExistingMS, ExistingLS) then
    if (ExistingMS > {#VersionMS}) or ((ExistingMS = {#VersionMS}) and (ExistingLS > {#VersionLS})) then
      Result := 'A newer version of LicenseKeyGenerator is already installed. Use a newer installer.';
end;
