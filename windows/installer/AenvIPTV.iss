; AENV IPTV — Windows installer (Inno Setup 6)
; Built by build-installer.ps1. Produces a standard setup wizard:
;   Welcome → License → Install location → Start menu folder → Additional tasks → Ready → Install → Finish
; Silent install (IT / Store requirement): AENV-IPTV-Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
; Silent uninstall:                        "%ProgramFiles%\AENV IPTV\unins000.exe" /VERYSILENT

#define MyAppName "AENV IPTV"
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#define MyAppPublisher "Vusal Aliyev"
#define MyAppExeName "AenvIPTV.exe"
#ifndef MyAppURL
  #define MyAppURL ""
#endif

[Setup]
; Never change AppId, so updates are recognised as the same program
AppId={{85138C5C-CADA-431A-937F-39A2920022E4}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppCopyright=© 2026 {#MyAppPublisher}
#if MyAppURL != ""
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
#endif
VersionInfoVersion={#MyAppVersion}
VersionInfoProductVersion={#MyAppVersion}
VersionInfoProductName={#MyAppName}
VersionInfoDescription={#MyAppName} Setup
VersionInfoCompany={#MyAppPublisher}
VersionInfoCopyright=© 2026 {#MyAppPublisher}

; ---- Standard wizard pages ----
WizardStyle=modern
WizardSizePercent=100
DisableWelcomePage=no
LicenseFile=..\Legal\LICENSE.txt
DisableDirPage=no
DisableProgramGroupPage=no
DisableReadyPage=no
DisableFinishedPage=no
AllowNoIcons=yes
; Always ask for the language first, even when Windows already uses one of them
ShowLanguageDialog=yes
LanguageDetectionMethod=uilanguage
WizardImageFile=images\wizard-100.bmp,images\wizard-125.bmp,images\wizard-150.bmp,images\wizard-200.bmp
WizardSmallImageFile=images\wizard-small-100.bmp,images\wizard-small-125.bmp,images\wizard-small-150.bmp,images\wizard-small-200.bmp
SetupIconFile=..\Assets\AenvIPTV.ico

; ---- Location, privileges, platform ----
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
UsePreviousAppDir=yes
UsePreviousGroup=yes
; Per-user install by default (no admin prompt); the user can choose "install for all users"
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

; ---- Behaviour ----
CloseApplications=yes
RestartApplications=no
SetupMutex=AenvIptvSetupMutex
UsedUserAreasWarning=no
SetupLogging=yes

; ---- Output ----
OutputDir=..\dist
OutputBaseFilename=AENV-IPTV-Setup-{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes

; ---- Apps & features entry ----
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "german";  MessagesFile: "compiler:Languages\German.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[InstallDelete]
; Remove shortcuts and binaries left by earlier app names (VAEN IPTV, Ekran)
Type: files; Name: "{autoprograms}\VAEN IPTV.lnk"
Type: files; Name: "{autodesktop}\VAEN IPTV.lnk"
Type: files; Name: "{autoprograms}\Ekran.lnk"
Type: files; Name: "{autodesktop}\Ekran.lnk"
Type: files; Name: "{app}\VaenIPTV.*"
Type: files; Name: "{app}\Ekran.*"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "IPTV player"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Program folder leftovers only. User data (playlists, favorites) is kept so a reinstall keeps the library.
Type: filesandordirs; Name: "{app}\libvlc"

[Code]
{ Use the installer language as the app's initial language (unless one is already set) }
procedure CurStepChanged(CurStep: TSetupStep);
var
  F, Code: String;
begin
  if CurStep = ssPostInstall then
  begin
    F := ExpandConstant('{userappdata}\AenvIPTV\settings.json');
    if not FileExists(F) then
    begin
      Code := 'en';
      if ActiveLanguage = 'turkish' then Code := 'tr';
      if ActiveLanguage = 'german' then Code := 'de';
      if ActiveLanguage = 'spanish' then Code := 'es';
      ForceDirectories(ExtractFileDir(F));
      SaveStringToFile(F, '{"lang":"' + Code + '"}', False);
    end;
  end;
end;
