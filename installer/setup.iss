; Inno Setup Script - Instalador Único do Agente de Checklist SENAI
#define MyAppName "Agente de Checklist SENAI"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "SENAI - Serviço Nacional de Aprendizagem Industrial"
#define MyAppExeName "ChecklistLogin.exe"

[Setup]
AppId={{8F1E4A3B-D295-46F2-98C0-A7513C089F12}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\ChecklistLogin
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

; Gera o executável único de instalação na raiz do projeto
OutputDir={#SourcePath}\..
OutputBaseFilename=Instalar_Agente_Checklist_SENAI
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

; REQUISITO OBRIGATÓRIO DE ELEVAÇÃO DE ADMINISTRADOR (UAC) PARA TODOS OS USUÁRIOS
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"

[Files]
Source: "{#SourcePath}\..\bin\ChecklistLogin.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourcePath}\..\logo\logo.png"; DestDir: "{app}\logo"; Flags: ignoreversion

[Registry]
; Garantia 1: Inicialização em HKLM Run (Inicia para TODOS os usuários no Logon)
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ChecklistLogin"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue

[Icons]
; Garantia 2: Atalho na pasta de Inicialização Comum para Todos os Usuários
Name: "{commonstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Run]
; Garantia 3: Registra a tarefa agendada e permissões de pasta silenciosamente (SEM janela do PowerShell)
Filename: "{app}\{#MyAppExeName}"; Parameters: "--install ""{code:GetLogFolderPath}"""; Flags: runhidden waituntilterminated

[UninstallRun]
; Remove a tarefa agendada silenciosamente ao desinstalar
Filename: "{app}\{#MyAppExeName}"; Parameters: "--uninstall"; Flags: runhidden waituntilterminated

[Code]
var
  LogDirPage: TInputDirWizardPage;

procedure InitializeWizard;
begin
  LogDirPage := CreateInputDirPage(
    wpSelectDir,
    'Selecione a Pasta de Logs',
    'Onde os arquivos de log do checklist deverão ser salvos?',
    'Selecione a pasta onde os registros (.txt) do checklist SENAI serão armazenados para TODOS os usuários:',
    False,
    ''
  );
  LogDirPage.Add('');
  LogDirPage.Values[0] := 'C:\Logs\Checklist';
end;

function GetLogFolderPath(Param: String): String;
begin
  Result := LogDirPage.Values[0];
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ConfigContent: String;
  ConfigFilePath: String;
  EscapedPath: String;
begin
  if CurStep = ssPostInstall then
  begin
    EscapedPath := GetLogFolderPath('');
    StringChangeEx(EscapedPath, '\', '\\', True);

    ConfigContent := '{' + #13#10 +
                     '  "logFolderPath": "' + EscapedPath + '"' + #13#10 +
                     '}';

    ConfigFilePath := ExpandConstant('{app}\config.json');
    SaveStringToFile(ConfigFilePath, ConfigContent, False);
  end;
end;
