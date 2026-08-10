; CAD Work Assistant - Inno Setup script (Milestone 8 sections 118-121).
; This file is pure ASCII on purpose (including the [Code] section's dialog text) - Inno Setup 6's
; script encoding auto-detection depends on a UTF-8 BOM being present in the .iss file, which this
; repo's file-writing tools do not add, so any non-ASCII character here risks silent corruption
; under the system's default code page. Standard installer screens (Welcome/Ready/Finish) still
; render in Korean via the Korean.isl language file below - only this script's OWN custom text
; (the AutoCAD-running warning) stays in English to keep the whole file encoding-safe.
;
; scripts/build-release.ps1 invokes ISCC.exe with
; /DAppVersion=<Directory.Build.props CwaVersion> /DSourceDir=<publish output>
; so the version is never hand-maintained in two places (sections 124-125). The defaults below are
; only a fallback for compiling this file directly with ISCC.exe during manual testing.
#ifndef AppVersion
  #define AppVersion "0.8.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish\desktop"
#endif
; BundleDir is where ISCC reads the bundle FILES from (an absolute path from build-release.ps1).
; BundleFolderName is the folder NAME the bundle is installed under inside ApplicationPlugins -
; these must stay separate: reusing BundleDir for both once put the whole absolute source path
; into the destination folder name (confirmed by an actual install log showing Setup trying to
; create "...\ApplicationPlugins\c:\Users\...\installer\CADWorkAssistant.bundle\PackageContents.xml"
; as a literal path segment, which aborted the copy).
#ifndef BundleDir
  #define BundleDir "CADWorkAssistant.bundle"
#endif
#define BundleFolderName "CADWorkAssistant.bundle"
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

#define AppName "CAD Work Assistant"
#define AppPublisher "TRSN CLARUS"
#define AppExeName "CADWorkAssistant.Desktop.exe"
; AppMutex makes Setup/Uninstall detect a running instance and ask the user to close it first
; (section 146) - this name must exactly match the mutex App.xaml.cs creates on startup.
#define AppMutexName "Global\CADWorkAssistant.SingleInstance"

[Setup]
; AppId stays fixed across versions (section 139) - Windows uses it to identify the installed
; program entry and upgrade target. Do not regenerate it.
AppId={{4F321868-99A6-487E-9B1C-9681A6AF63D9}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppMutex={#AppMutexName}
; No administrator privileges required (section 132) - the AutoCAD ApplicationPlugins folder is
; already per-user (%APPDATA%\Autodesk\ApplicationPlugins), so installing Desktop per-user too lets
; users without admin rights on a company PC install the whole product.
PrivilegesRequired=lowest
; {autopf} (Program Files) normally needs admin rights - that would silently conflict with
; PrivilegesRequired=lowest and abort the copy step with an Access Denied Abort/Retry/Ignore box
; (confirmed by actually running the installer: exit code 5, no files copied). Use the per-user
; equivalent instead, matching PrivilegesRequired=lowest end to end.
DefaultDirName={localappdata}\Programs\{#AppName}
UsePreviousAppDir=yes
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=CADWorkAssistant-Setup-{#AppVersion}-x64
Compression=lzma2/ultra
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\src\CADWorkAssistant.Desktop\Assets\AppIcon.ico
UninstallDisplayIcon={app}\{#AppExeName}
WizardStyle=modern
; No decorative installer skin (section 144) - Inno's native wizard as-is.
DisableWelcomePage=no
DisableReadyPage=no

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Full self-contained Desktop publish output - scripts/build-release.ps1 populates SourceDir via
; `dotnet publish -r win-x64 --self-contained true` before calling ISCC.exe. This also stages the
; built user manual PDF at SourceDir\Documentation\CAD_Work_Assistant_User_Guide_ko-KR.pdf
; (Milestone 13 Part B section 115) - recursesubdirs already copies it to {app}\Documentation\
; alongside the exe, so no separate [Files] line is needed for it.
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

; AutoCAD Bundle - placed in the per-user ApplicationPlugins folder and autoloaded when AutoCAD
; starts (section 116). Copying it does not fail even if AutoCAD is not installed (section 117) -
; it just sits there unloaded.
Source: "{#BundleDir}\PackageContents.xml"; DestDir: "{userappdata}\Autodesk\ApplicationPlugins\{#BundleFolderName}"; Flags: ignoreversion
Source: "{#BundleDir}\Contents\Windows\*"; DestDir: "{userappdata}\Autodesk\ApplicationPlugins\{#BundleFolderName}\Contents\Windows"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Tasks]
; Desktop shortcut is optional (section 141) - off by default.
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Run]
; Never launches AutoCAD (section 145) - Desktop only, and only if the user opts in.
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Only removes the executables/DLLs - user data (%LOCALAPPDATA%\CADWorkAssistant\) is never touched
; (sections 135-137: uninstalling the program must not delete project data). The Bundle is removed
; deliberately, unlike user data - it holds no user data (just the manifest + DLLs), and leaving a
; stale DLL behind would make AutoCAD try to load a plugin version that no longer matches Desktop.
Type: filesandordirs; Name: "{userappdata}\Autodesk\ApplicationPlugins\{#BundleFolderName}"

[Code]
function IsAutoCadRunning(): Boolean;
var
  ResultCode: Integer;
  TempFile: string;
  Lines: TArrayOfString;
begin
  Result := False;
  TempFile := ExpandConstant('{tmp}\cwa_acad_check.txt');
  if Exec(ExpandConstant('{cmd}'), '/C tasklist /FI "IMAGENAME eq acad.exe" /NH > "' + TempFile + '"',
          '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if LoadStringsFromFile(TempFile, Lines) then
    begin
      if (GetArrayLength(Lines) > 0) and (Pos('acad.exe', Lowercase(Lines[0])) > 0) then
        Result := True;
    end;
  end;
end;

function WarnIfAutoCadRunning(): Boolean;
begin
  Result := True;
  if IsAutoCadRunning() then
  begin
    // Never force-kill AutoCAD (section 147) - whether to proceed anyway is the user's call.
    // Copying files usually still succeeds even with AutoCAD open (the plugin DLL it already
    // loaded is not locked once loaded into its process) - but the newly installed version will
    // not take effect until the next AutoCAD restart.
    Result := MsgBox('AutoCAD appears to be running.' + #13#10 +
      'Closing AutoCAD first makes the plugin update more reliable.' + #13#10#13#10 +
      'Continue anyway?', mbConfirmation, MB_YESNO) = IDYES;
  end;
end;

function InitializeSetup(): Boolean;
begin
  Result := WarnIfAutoCadRunning();
end;

function InitializeUninstall(): Boolean;
begin
  Result := WarnIfAutoCadRunning();
end;
