; Script de Inno Setup 6 para Alba Studio 2.0.0 (Windows x64)
; Genera un instalador guiado sin requerir .NET SDK ni herramientas previas en el equipo destino.

#ifndef MyAppVersion
  #define MyAppVersion "2.0.0"
#endif
#ifndef SourceStageDir
  #define SourceStageDir "..\..\dist\stage-win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\dist"
#endif

[Setup]
AppId={{8D6E4A21-9A3C-4B28-A1E7-4F92C510A200}
AppName=Alba Studio
AppVersion={#MyAppVersion}
AppVerName=Alba Studio {#MyAppVersion}
AppPublisher=CynexStudios
AppPublisherURL=https://github.com/CynexStudios/alba-ecosistema
AppSupportURL=https://github.com/CynexStudios/alba-ecosistema/issues
AppUpdatesURL=https://github.com/CynexStudios/alba-ecosistema/releases
DefaultDirName={autopf}\Alba Studio
DefaultGroupName=Alba Studio
AllowNoIcons=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir={#OutputDir}
OutputBaseFilename=AlbaStudio-{#MyAppVersion}-win-x64-setup
SetupIconFile=..\..\assets\icons\alba-studio.ico
UninstallDisplayIcon={app}\ide\Alba.IDE.exe
WizardImageFile=..\..\assets\branding\installer-sidebar.bmp
WizardSmallImageFile=..\..\assets\branding\installer-small.bmp
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
ChangesAssociations=yes
ChangesEnvironment=yes

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "assoc_alb"; Description: "Asociar archivos de código Alba (.alb) con Alba Studio"; GroupDescription: "Integración con el sistema:"
Name: "add_path"; Description: "Agregar el comando 'alba' (CLI) al PATH"; GroupDescription: "Integración con el sistema:"

[Files]
Source: "{#SourceStageDir}\ide\*"; DestDir: "{app}\ide"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceStageDir}\cli\*"; DestDir: "{app}\cli"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceStageDir}\addons\*"; DestDir: "{app}\addons"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceStageDir}\docs\*"; DestDir: "{app}\docs"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceStageDir}\examples\*"; DestDir: "{app}\examples"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceStageDir}\alba-host.exe"; DestDir: "{app}"; Flags: ignoreversion; Check: FileExists(ExpandConstant('{#SourceStageDir}\alba-host.exe'))

[Icons]
Name: "{group}\Alba Studio"; Filename: "{app}\ide\Alba.IDE.exe"; IconFilename: "{app}\ide\Alba.IDE.exe"
Name: "{group}\Documentación de Alba"; Filename: "{app}\docs\01_introduccion_y_filosofia.md"
Name: "{group}\Desinstalar Alba Studio"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Alba Studio"; Filename: "{app}\ide\Alba.IDE.exe"; Tasks: desktopicon

[Registry]
; Asociación de archivos .alb con Alba Studio
Root: HKA; Subkey: "Software\Classes\.alb"; ValueType: string; ValueName: ""; ValueData: "AlbaStudio.ArchivoAlba"; Flags: uninsdeletevalue; Tasks: assoc_alb
Root: HKA; Subkey: "Software\Classes\AlbaStudio.ArchivoAlba"; ValueType: string; ValueName: ""; ValueData: "Programa en lenguaje Alba"; Flags: uninsdeletekey; Tasks: assoc_alb
Root: HKA; Subkey: "Software\Classes\AlbaStudio.ArchivoAlba\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\ide\Alba.IDE.exe,0"; Tasks: assoc_alb
Root: HKA; Subkey: "Software\Classes\AlbaStudio.ArchivoAlba\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\ide\Alba.IDE.exe"" ""%1"""; Tasks: assoc_alb

; Registro de CLI en PATH del usuario sin tocar datos personales
Root: HKCU; Subkey: "Environment"; ValueType: expandsz; ValueName: "Path"; ValueData: "{olddata};{app}\cli"; Tasks: add_path; Check: NeedsAddPath(ExpandConstant('{app}\cli'))

[Run]
Filename: "{app}\ide\Alba.IDE.exe"; Description: "{cm:LaunchProgram,Alba Studio}"; Flags: nowait postinstall skipifsilent

[Code]
function NeedsAddPath(Param: string): boolean;
var
  OrigPath: string;
begin
  if not RegQueryStringValue(HKEY_CURRENT_USER, 'Environment', 'Path', OrigPath) then
  begin
    Result := True;
    exit;
  end;
  Result := Pos(';' + Uppercase(Param) + ';', ';' + Uppercase(OrigPath) + ';') = 0;
end;
