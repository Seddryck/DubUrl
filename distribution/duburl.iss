#define AppName "DubUrl"
#define AppPublisher "Seddryck"
#define AppExeName "duburl.exe"

#ifndef AppVersion
  #define AppVersion "0.0.0-local"
#endif
#ifndef RuntimeIdentifier
  #define RuntimeIdentifier "win-x64"
#endif
#ifndef TargetFramework
  #define TargetFramework "net10.0"
#endif
#ifndef TargetArchitecture
  #define TargetArchitecture "x64compatible"
#endif
#ifndef PublishDirectory
  #define PublishDirectory ".\bin\publish\net10.0\win-x64"
#endif
#ifndef OutputDirectory
  #define OutputDirectory ".\bin\installers"
#endif
#ifndef OutputBaseFilename
  #define OutputBaseFilename "duburl-" + AppVersion + "-" + TargetFramework + "-" + RuntimeIdentifier + "-setup"
#endif
#ifndef IconFile
  #define IconFile "..\misc\icon\duburl.ico"
#endif

[Setup]
AppId={{70F327F4-2497-4A5B-B4D3-C09F32F16045}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/Seddryck/DubUrl
AppSupportURL=https://github.com/Seddryck/DubUrl/issues
AppUpdatesURL=https://github.com/Seddryck/DubUrl/releases
DefaultDirName={autopf}\DubUrl
DefaultGroupName=DubUrl
OutputDir={#OutputDirectory}
OutputBaseFilename={#OutputBaseFilename}
SetupIconFile={#IconFile}
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed={#TargetArchitecture}
ArchitecturesInstallIn64BitMode={#TargetArchitecture}
PrivilegesRequired=admin
ChangesEnvironment=yes

[Tasks]
Name: "addtopath"; Description: "Add DubUrl to the system PATH"; Flags: checkedonce

[Files]
Source: "{#PublishDirectory}\DubUrl.Cli.exe"; DestDir: "{app}"; DestName: "duburl.exe"; Flags: ignoreversion
Source: "{#PublishDirectory}\*"; DestDir: "{app}"; Excludes: "DubUrl.Cli.exe"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\DubUrl Command Prompt"; Filename: "{cmd}"; Parameters: "/K cd /d ""{app}"""

[Code]
const
  EnvironmentKey = 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment';
  RequiredDotNetMajorVersion = 10;

function NormalizePathEntry(Value: string): string;
begin Result := RemoveBackslashUnlessRoot(Trim(Value)); end;

function PathContains(CurrentPath, Entry: string): Boolean;
var A, B: string;
begin
  A := Uppercase(CurrentPath); B := Uppercase(NormalizePathEntry(Entry));
  StringChangeEx(A, '/', '\', True); StringChangeEx(B, '/', '\', True);
  Result := Pos(';' + B + ';', ';' + A + ';') > 0;
end;

procedure AddToPath(Entry: string);
var Value: string;
begin
  if not RegQueryStringValue(HKEY_LOCAL_MACHINE, EnvironmentKey, 'Path', Value) then Value := '';
  if PathContains(Value, Entry) then Exit;
  if (Value <> '') and (Value[Length(Value)] <> ';') then Value := Value + ';';
  RegWriteExpandStringValue(HKEY_LOCAL_MACHINE, EnvironmentKey, 'Path', Value + NormalizePathEntry(Entry));
end;

procedure RemoveFromPath(Entry: string);
var Value, Remaining, Updated, Item, Target: string; P: Integer;
begin
  if not RegQueryStringValue(HKEY_LOCAL_MACHINE, EnvironmentKey, 'Path', Value) then Exit;
  Remaining := Value; Updated := ''; Target := Uppercase(NormalizePathEntry(Entry));
  while Remaining <> '' do begin
    P := Pos(';', Remaining);
    if P > 0 then begin Item := Copy(Remaining, 1, P - 1); Delete(Remaining, 1, P); end
    else begin Item := Remaining; Remaining := ''; end;
    Item := Trim(Item);
    if (Item <> '') and (Uppercase(NormalizePathEntry(Item)) <> Target) then begin
      if Updated <> '' then Updated := Updated + ';';
      Updated := Updated + Item;
    end;
  end;
  RegWriteExpandStringValue(HKEY_LOCAL_MACHINE, EnvironmentKey, 'Path', Updated);
end;

function RuntimeArchitecture(): string;
begin if '{#RuntimeIdentifier}' = 'win-arm64' then Result := 'arm64' else Result := 'x64'; end;

function HasRuntime(): Boolean;
var Key: string; Names: TArrayOfString; I: Integer;
begin
  Result := False;
  Key := 'SOFTWARE\dotnet\Setup\InstalledVersions\' + RuntimeArchitecture() + '\sharedfx\Microsoft.NETCore.App';
  if not RegGetValueNames(HKEY_LOCAL_MACHINE_32, Key, Names) then Exit;
  for I := 0 to GetArrayLength(Names) - 1 do
    if Pos(IntToStr(RequiredDotNetMajorVersion) + '.', Names[I]) = 1 then begin Result := True; Exit; end;
end;

function InitializeSetup(): Boolean;
begin
  Result := HasRuntime();
  if not Result then MsgBox('DubUrl requires Microsoft .NET 10 Runtime (' + Uppercase(RuntimeArchitecture()) + '). Install it and run setup again.', mbError, MB_OK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin if (CurStep = ssPostInstall) and WizardIsTaskSelected('addtopath') then AddToPath(ExpandConstant('{app}')); end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin if CurUninstallStep = usUninstall then RemoveFromPath(ExpandConstant('{app}')); end;
