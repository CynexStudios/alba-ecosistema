; Script de Inno Setup 6 para Xynex Code Editor & Cibao v5.0.0 (Windows x64)
; Genera un instalador gráfico completo sin requerir .NET SDK previo en el equipo destino.

#ifndef MyAppVersion
  #define MyAppVersion "5.0.0"
#endif
#ifndef SourceStageDir
  #define SourceStageDir "..\..\dist\stage-win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\dist"
#endif

[Setup]
AppId={{8D6E4A21-9A3C-4B28-A1E7-4F92C510A200}
AppName=Xynex Code Editor
AppVersion={#MyAppVersion}
AppVerName=Xynex Code Editor {#MyAppVersion} (Cibao)
AppPublisher=Jaderson Liriano (CynexStudios)
AppPublisherURL=https://github.com/CynexStudios/alba-ecosistema
AppSupportURL=https://github.com/CynexStudios/alba-ecosistema/issues
AppUpdatesURL=https://github.com/CynexStudios/alba-ecosistema/releases
DefaultDirName={autopf}\Xynex Code Editor
DefaultGroupName=Xynex Code Editor
AllowNoIcons=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir={#OutputDir}
OutputBaseFilename=XynexCodeEditor-{#MyAppVersion}-win-x64-setup
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
Name: "assoc_cibao"; Description: "Asociar archivos Cibao (.cibao y .alb) con Xynex Code Editor"; GroupDescription: "Integración con el sistema:"
Name: "add_path"; Description: "Agregar los comandos 'cibao' y 'alba' (CLI) al PATH"; GroupDescription: "Integración con el sistema:"

[Files]
Source: "{#SourceStageDir}\ide\*"; DestDir: "{app}\ide"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceStageDir}\cli\*"; DestDir: "{app}\cli"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceStageDir}\addons\*"; DestDir: "{app}\addons"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceStageDir}\docs\*"; DestDir: "{app}\docs"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceStageDir}\examples\*"; DestDir: "{app}\examples"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceStageDir}\alba-host.exe"; DestDir: "{app}"; Flags: ignoreversion; Check: FileExists(ExpandConstant('{#SourceStageDir}\alba-host.exe'))

[Icons]
Name: "{group}\Xynex Code Editor"; Filename: "{app}\ide\Alba.IDE.exe"; IconFilename: "{app}\ide\Alba.IDE.exe"
Name: "{group}\Manual del Lenguaje Cibao"; Filename: "{app}\docs\03_manual_del_lenguaje.md"
Name: "{group}\Desinstalar Xynex Code Editor"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Xynex Code Editor"; Filename: "{app}\ide\Alba.IDE.exe"; Tasks: desktopicon

[Registry]
; Asociación de archivos principales .cibao con Xynex Code Editor
Root: HKA; Subkey: "Software\Classes\.cibao"; ValueType: string; ValueName: ""; ValueData: "XynexCodeEditor.ArchivoCibao"; Flags: uninsdeletevalue; Tasks: assoc_cibao
Root: HKA; Subkey: "Software\Classes\XynexCodeEditor.ArchivoCibao"; ValueType: string; ValueName: ""; ValueData: "Programa en lenguaje Cibao"; Flags: uninsdeletekey; Tasks: assoc_cibao
Root: HKA; Subkey: "Software\Classes\XynexCodeEditor.ArchivoCibao\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\ide\Alba.IDE.exe,0"; Tasks: assoc_cibao
Root: HKA; Subkey: "Software\Classes\XynexCodeEditor.ArchivoCibao\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\ide\Alba.IDE.exe"" ""%1"""; Tasks: assoc_cibao

; Asociación compatible .alb con Xynex Code Editor
Root: HKA; Subkey: "Software\Classes\.alb"; ValueType: string; ValueName: ""; ValueData: "XynexCodeEditor.ArchivoAlba"; Flags: uninsdeletevalue; Tasks: assoc_cibao
Root: HKA; Subkey: "Software\Classes\XynexCodeEditor.ArchivoAlba"; ValueType: string; ValueName: ""; ValueData: "Programa Cibao / Alba"; Flags: uninsdeletekey; Tasks: assoc_cibao
Root: HKA; Subkey: "Software\Classes\XynexCodeEditor.ArchivoAlba\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\ide\Alba.IDE.exe,0"; Tasks: assoc_cibao
Root: HKA; Subkey: "Software\Classes\XynexCodeEditor.ArchivoAlba\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\ide\Alba.IDE.exe"" ""%1"""; Tasks: assoc_cibao

; Registro de CLI en PATH del usuario sin tocar datos personales
Root: HKCU; Subkey: "Environment"; ValueType: expandsz; ValueName: "Path"; ValueData: "{olddata};{app}\cli"; Tasks: add_path; Check: NeedsAddPath(ExpandConstant('{app}\cli'))

[Run]
Filename: "{app}\ide\Alba.IDE.exe"; Description: "{cm:LaunchProgram,Xynex Code Editor}"; Flags: nowait postinstall skipifsilent

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
