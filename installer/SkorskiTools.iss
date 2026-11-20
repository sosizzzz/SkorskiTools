; Skorski Tools installer (Inno Setup 6).
; Two scopes are available from a single installer:
;   - Current user: %LOCALAPPDATA%\Programs\Skorski Tools (no admin rights)
;   - All users:    C:\Program Files\Skorski Tools (elevation requested via UAC)
; Choosing "All users" restarts this installer with administrator rights.

#define AppName "Skorski Tools"
#define AppVersion "1.0.0"
#define AppPublisher "sosizzzz"

[Setup]
AppId={{8F6C1D42-5A3E-4B17-9C2D-7E1A0B4F6D89}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={code:GetDefaultDir}
DefaultGroupName={#AppName}
OutputDir=output
OutputBaseFilename=SkorskiTools-Setup
SetupIconFile=..\src\SkorskiTools.App\Assets\app.ico
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=lowest
DisableDirPage=yes
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\SkorskiTools.App.exe
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: ".gitkeep"

[Icons]
Name: "{group}\Skorski Tools"; Filename: "{app}\SkorskiTools.App.exe"
Name: "{autodesktop}\Skorski Tools"; Filename: "{app}\SkorskiTools.App.exe"; Tasks: desktopicon

[Code]
var
  ScopePage: TWizardPage;
  CurrentUserRadio: TRadioButton;
  AllUsersRadio: TRadioButton;

function HasAllUsersParam: Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 1 to ParamCount do
    if CompareText(ParamStr(I), '/ALLUSERS') = 0 then
      Result := True;
end;

function GetDefaultDir(Param: String): String;
begin
  { Evaluated by Setup before InitializeWizard, so read the parameter directly. }
  if HasAllUsersParam then
    Result := ExpandConstant('{pf}\Skorski Tools')
  else
    Result := ExpandConstant('{localappdata}\Programs\Skorski Tools');
end;

procedure InitializeWizard;
begin
  { The elevated instance already knows the chosen scope. }
  if HasAllUsersParam then
    Exit;

  ScopePage := CreateCustomPage(
    wpWelcome, 'Installation scope', 'Choose who will use Skorski Tools.');

  CurrentUserRadio := TRadioButton.Create(ScopePage);
  CurrentUserRadio.Parent := ScopePage.Surface;
  CurrentUserRadio.Left := 8;
  CurrentUserRadio.Top := 16;
  CurrentUserRadio.Width := ScopePage.Surface.Width - 16;
  CurrentUserRadio.Height := 24;
  CurrentUserRadio.Caption := 'Current user (recommended) - no administrator rights';
  CurrentUserRadio.Checked := True;

  AllUsersRadio := TRadioButton.Create(ScopePage);
  AllUsersRadio.Parent := ScopePage.Surface;
  AllUsersRadio.Left := 8;
  AllUsersRadio.Top := 52;
  AllUsersRadio.Width := ScopePage.Surface.Width - 16;
  AllUsersRadio.Height := 48;
  AllUsersRadio.Caption := 'All users - requires administrator rights' + #13#10 +
    'Windows will ask for confirmation to continue.';
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;

  if (CurPageID = ScopePage.ID) and AllUsersRadio.Checked then
  begin
    if not ShellExec(
        'runas',
        ExpandConstant('{srcexe}'),
        '/ALLUSERS',
        '',
        SW_SHOW,
        ewWaitUntilTerminated,
        ResultCode) then
      MsgBox('The installer could not restart with administrator rights.', mbError, MB_OK);

    Abort;
  end;
end;
