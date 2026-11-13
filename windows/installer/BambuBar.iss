; Beta Monitor for Windows: the installer (ledger P5.1, O10). Inno Setup 6.3 or later, per user, no admin: installs to
; %LOCALAPPDATA%\Programs\BetaMonitor. One installer per architecture, built from the install image CI stages in
; build\windows\<rid>\ (dotnet publish + ffmpeg\ + Rainmeter\ + Skins\ + LICENSE.txt + THIRD-PARTY-NOTICES.txt):
;
;   ISCC /DAppVersion=3.0.0 /DArch=x64 /DSourceDir=<repo>\build\windows\win-x64 /DOutputDir=<repo>\build\installer BambuBar.iss
;
; AppVersion comes from windows\tools\version.ps1 (project.yml's MARKETING_VERSION, the one version line). Arch is x64
; or arm64; both carry the x64 Rainmeter, which runs under emulation on ARM64 (D9).
;
; Silent updates: /VERYSILENT /SUPPRESSMSGBOXES /NORESTART. A running Beta Monitor from this folder, and the Rainmeter
; it started from Rainmeter\, are stopped before the files are replaced; after a silent install the app is started
; again (with --background when it had been started with it, i.e. at sign-in), after an interactive one the last
; page offers to open it.
; Uninstall stops both, removes the betamonitor:// and AppUserModelID keys, the sign-in (Run) entry and the .rmskin
; association our Rainmeter registers, and keeps the user's data (%APPDATA%\BetaMonitor, %LOCALAPPDATA%\BetaMonitor,
; the access codes and license in Credential Manager) unless the user says so, or /PURGE=1 on a silent uninstall.
;
; Signing (ledger section 9, not yet): sign BetaMonitor.exe in the image first, then pass a sign tool named "betamonitor" and define
; SignTool, and Inno signs the installer and its uninstaller:
;   ISCC "/Sbetamonitor=signtool sign /fd SHA256 /f <pfx> /p <password> /tr http://timestamp.digicert.com /td SHA256 $f" /DSignTool=betamonitor ...

#ifndef AppVersion
  #error Pass /DAppVersion=X.Y.Z (windows\tools\version.ps1)
#endif
#ifndef Arch
  #error Pass /DArch=x64 or /DArch=arm64
#endif
#ifndef SourceDir
  #error Pass /DSourceDir=<the install image, build\windows\win-x64 or win-arm64>
#endif
#ifndef OutputDir
  #define OutputDir "..\..\build\installer"
#endif

#if Arch == "x64"
  #define Architectures "x64os"
#elif Arch == "arm64"
  #define Architectures "arm64"
#else
  #error Arch must be x64 or arm64
#endif

; The same as Notifications\AppIdentity.cs: the Start menu shortcut carries it so Windows ties the shortcut, the
; taskbar button and the toasts together (P3.7).
#define AppUserModelId "BetaMonitor.App"

[Setup]
; Never change AppId: updates find the installed copy by it. (Beta Monitor has its own, so it installs beside Bambu Bar.)
AppId={{40AB2714-4329-4530-81BD-5ECDC7D268D0}
AppName=Beta Monitor
AppVersion={#AppVersion}
AppVerName=Beta Monitor {#AppVersion}
AppPublisher=Beta Monitor
AppPublisherURL=https://github.com/yigitozsoy/printpulse
AppSupportURL=https://github.com/yigitozsoy/printpulse/issues
AppUpdatesURL=https://github.com/yigitozsoy/printpulse/releases
AppCopyright=Beta Monitor, based on Bambu Bar (C) 2026 NINA LABS, LLC. MIT License.
VersionInfoVersion={#AppVersion}.0
VersionInfoProductName=Beta Monitor
VersionInfoDescription=Beta Monitor Setup
PrivilegesRequired=lowest
DefaultDirName={autopf}\BetaMonitor
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
ArchitecturesAllowed={#Architectures}
ArchitecturesInstallIn64BitMode={#Architectures}
MinVersion=10.0.18362
; We stop our own processes (PrepareToInstall); Restart Manager would only ask about others.
CloseApplications=no
RestartIfNeededByRun=no
WizardStyle=modern
; The welcome and finish pages carry the art (installer\art\make-art.py: the printer at work on a green glow), the
; other pages the app icon; Inno picks the size for the screen's scale. The welcome page is on so the art is seen.
WizardImageFile=art\wizard-*.bmp
WizardSmallImageFile=art\small-*.bmp
DisableWelcomePage=no
SetupIconFile=..\src\BambuBar.Windows\Assets\BetaMonitor.ico
UninstallDisplayIcon={app}\BetaMonitor.exe
UninstallDisplayName=Beta Monitor
OutputDir={#OutputDir}
OutputBaseFilename=BetaMonitor-{#AppVersion}-windows-{#Arch}
Compression=lzma2/max
SolidCompression=yes
#ifdef SignTool
SignTool={#SignTool}
SignedUninstaller=yes
#endif

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"

[Messages]
WelcomeLabel1=Welcome to Beta Monitor
WelcomeLabel2=Your Bambu Lab printers in the Windows tray: the print's progress at a glance, the camera, filament, alerts, and widgets for your desktop.%n%nBeta Monitor {#AppVersion} installs just for you; no administrator needed.
FinishedHeadingLabel=Beta Monitor is ready
FinishedLabel=Beta Monitor lives in the tray, by the clock. If Windows tucked its icon away, click ^ there and drag it onto the taskbar.
FinishedLabelNoIcons=Beta Monitor lives in the tray, by the clock. If Windows tucked its icon away, click ^ there and drag it onto the taskbar.

[CustomMessages]
en.RemoveUserData=Also remove your Beta Monitor settings, print history and widgets from this PC?%n%nPrinter access codes stay in Windows Credential Manager.

[InstallDelete]
; Folders the image owns wholesale: an update replaces them instead of leaving old files behind.
Type: filesandordirs; Name: "{app}\Assets"
Type: filesandordirs; Name: "{app}\ffmpeg"
Type: filesandordirs; Name: "{app}\Rainmeter"
Type: filesandordirs; Name: "{app}\Skins"

[Files]
Source: "{#SourceDir}\*"; Excludes: "*.pdb"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Beta Monitor"; Filename: "{app}\BetaMonitor.exe"; AppUserModelID: "{#AppUserModelId}"

[Registry]
; betamonitor:// links (widget clicks, toasts), as Platform\Registration.cs writes them too.
Root: HKCU; Subkey: "Software\Classes\betamonitor"; ValueType: string; ValueName: ""; ValueData: "URL:Beta Monitor"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\betamonitor"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\betamonitor\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\BetaMonitor.exe"",0"
Root: HKCU; Subkey: "Software\Classes\betamonitor\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\BetaMonitor.exe"" ""%1"""
; The AppUserModelID's name and icon for toasts, as Notifications\AppIdentity.cs writes them too.
Root: HKCU; Subkey: "Software\Classes\AppUserModelId\{#AppUserModelId}"; ValueType: string; ValueName: "DisplayName"; ValueData: "Beta Monitor"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\AppUserModelId\{#AppUserModelId}"; ValueType: string; ValueName: "IconUri"; ValueData: "{app}\AppIcon.png"

[Run]
; After a silent update: back as it was (the tray only when it had been started that way).
Filename: "{app}\BetaMonitor.exe"; Parameters: "{code:RelaunchParameters}"; Flags: nowait; Check: RelaunchAfterSilentInstall
Filename: "{app}\BetaMonitor.exe"; Description: "{cm:LaunchProgram,BetaMonitor}"; Flags: postinstall nowait skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
var
  WasRunning: Boolean;
  WasInTray: Boolean;

{ Ends every process called Name whose executable is inside Folder (with its trailing backslash), so a Beta Monitor or a
  Rainmeter run from anywhere else is left alone. Returns how many; InTray is set when one was started with
  --background. }
function StopProcesses(const Name, Folder: String; var InTray: Boolean): Integer;
var
  Locator, Service, Items, Item: Variant;
  I: Integer;
  Path, Line: String;
begin
  Result := 0;
  try
    Locator := CreateOleObject('WbemScripting.SWbemLocator');
    Service := Locator.ConnectServer('.', 'root\CIMV2');
    Items := Service.ExecQuery('SELECT ProcessId, ExecutablePath, CommandLine FROM Win32_Process WHERE Name = ''' + Name + '''');
    for I := 0 to Items.Count - 1 do
    begin
      Item := Items.ItemIndex(I);
      if not VarIsNull(Item.ExecutablePath) then
      begin
        Path := Item.ExecutablePath;
        if CompareText(Copy(Path, 1, Length(Folder)), Folder) = 0 then
        begin
          if not VarIsNull(Item.CommandLine) then
          begin
            Line := Item.CommandLine;
            if Pos('--background', Line) > 0 then InTray := True;
          end;
          Log('Stopping ' + Path);
          Item.Terminate(0);
          Result := Result + 1;
        end;
      end;
    end;
  except
    Log('Could not list or stop ' + Name + ': ' + GetExceptionMessage);
  end;
end;

{ Beta Monitor first (it would restart its Rainmeter), then our Rainmeter. }
function StopOurProcesses(var InTray: Boolean): Boolean;
var
  Folder: String;
  Ignored: Boolean;
begin
  Folder := AddBackslash(ExpandConstant('{app}'));
  Result := StopProcesses('BetaMonitor.exe', Folder, InTray) > 0;
  if StopProcesses('Rainmeter.exe', Folder + 'Rainmeter\', Ignored) > 0 then Result := True;
  if Result then Sleep(1000);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  WasInTray := False;
  WasRunning := StopOurProcesses(WasInTray);
  Result := '';
end;

{ The app's own updater quits before the installer gets this far and passes /RELAUNCH=1 so it comes back. }
function RelaunchAfterSilentInstall: Boolean;
begin
  Result := WizardSilent and (WasRunning or (ExpandConstant('{param:RELAUNCH|0}') = '1'));
end;

function RelaunchParameters(Param: String): String;
begin
  if WasInTray then Result := '--background' else Result := '';
end;

{ Our Rainmeter registers .rmskin for this user when nothing else has (it runs with its settings outside its own
  folder): remove that only when it points into our folder. }
procedure RemoveRmskinAssociation;
var
  Command, Folder: String;
begin
  Folder := Lowercase(AddBackslash(ExpandConstant('{app}')) + 'Rainmeter\');
  if RegQueryStringValue(HKCU, 'Software\Classes\Rainmeter.SkinInstaller\shell\open\command', '', Command) and
     (Pos(Folder, Lowercase(Command)) > 0) then
  begin
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\Rainmeter.SkinInstaller');
    if RegQueryStringValue(HKCU, 'Software\Classes\.rmskin', '', Command) and (Command = 'Rainmeter.SkinInstaller') then
      RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\.rmskin');
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Ignored, Purge: Boolean;
begin
  if CurUninstallStep = usUninstall then
  begin
    StopOurProcesses(Ignored);
    { Opening at sign-in (Platform\Registration.cs) and Task Manager's on/off state for it. }
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Beta Monitor');
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run', 'Beta Monitor');
    RemoveRmskinAssociation;
  end;
  if CurUninstallStep = usPostUninstall then
  begin
    if UninstallSilent then
      Purge := ExpandConstant('{param:PURGE|0}') = '1'
    else
      Purge := MsgBox(CustomMessage('RemoveUserData'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES;
    if Purge then
    begin
      DelTree(ExpandConstant('{userappdata}\BetaMonitor'), True, True, True);
      DelTree(ExpandConstant('{localappdata}\BetaMonitor'), True, True, True);
    end;
  end;
end;
