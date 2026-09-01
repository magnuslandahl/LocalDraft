; Inno Setup script for the LocalDraft per-user Windows installer.
;
; LocalDraft keeps every document, recording, version, log and temporary file
; inside its own application folder. The installer therefore never asks for
; administrator rights and always installs into a writable per-user location,
; so the application root stays writable and nothing is ever stored in shared
; or machine-wide locations.
;
; Only ASCII is used in this file so the script compiles regardless of the
; encoding assumptions of the Inno Setup compiler.

#define AppName "LocalDraft"
#define AppPublisher "LocalDraft"
#define AppUrl "https://github.com/magnuslandahlapollo/LocalDraft"

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef AppNumericVersion
  #define AppNumericVersion "0.0.0"
#endif
#ifndef PackageRoot
  #define PackageRoot "..\dist\LocalDraft-Portable-win-x64"
#endif

[Setup]
AppId={{8F3C1D5A-7B24-4E96-9C0A-2D5E4F6B8A31}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
VersionInfoVersion={#AppNumericVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}
AppUpdatesURL={#AppUrl}/releases
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableDirPage=yes
DisableProgramGroupPage=yes
AllowNoIcons=yes
PrivilegesRequired=lowest
OutputBaseFilename=LocalDraft-Setup-win-x64
SetupIconFile=..\src\LocalDraft.App\Assets\LocalDraft.ico
UninstallDisplayIcon={app}\{#AppName}.exe
UninstallDisplayName={#AppName}
Compression=lzma2/normal
SolidCompression=no
WizardStyle=modern

[Languages]
Name: "swedish"; MessagesFile: "compiler:Languages\Swedish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
swedish.RemoveUserData=Vill du aven ta bort dina dokument, inspelningar, versioner och installningar?%n%nValj Nej om du vill behalla dem.
english.RemoveUserData=Do you also want to delete your documents, recordings, versions and settings?%n%nChoose No to keep them.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; The models are already compressed, so packing them again only wastes time.
Source: "{#PackageRoot}\Models\*"; DestDir: "{app}\Models"; Flags: ignoreversion recursesubdirs createallsubdirs nocompression
Source: "{#PackageRoot}\*"; DestDir: "{app}"; Excludes: "\Models\*"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppName}.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppName}.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppName}.exe"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{app}\Data');
    if DirExists(DataDir) then
    begin
      if MsgBox(ExpandConstant('{cm:RemoveUserData}'), mbConfirmation, MB_YESNO) = IDYES then
        DelTree(DataDir, True, True, True);
    end;
  end;
end;
