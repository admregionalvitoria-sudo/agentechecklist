; Inno Setup Script - Instalador Único do Agente de Checklist SENAI
#define MyAppName "Agente de Checklist SENAI"
#define MyAppVersion "2.1.1"
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

; Garantir encerramento automático de instâncias em execução da aplicação
CloseApplications=yes
CloseApplicationsFilter=*ChecklistLogin*
RestartApplications=no

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

[InstallDelete]
Type: filesandordirs; Name: "{app}\ChecklistLogin.exe"
Type: filesandordirs; Name: "{app}\logo"

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
; Garantia 3: Registra a tarefa agendada, concede permissões de pasta e habilita auto-update em futuras execuções
Filename: "{app}\{#MyAppExeName}"; Parameters: "--install ""{code:GetLogFolderPath}"" ""{code:GetSelectedLocation}"""; Flags: runhidden waituntilterminated
Filename: "{app}\{#MyAppExeName}"; Flags: nowait postinstall skipifsilent; Description: "Iniciar Agente de Checklist SENAI agora"

[UninstallRun]
; Remove a tarefa agendada silenciosamente ao desinstalar
Filename: "{app}\{#MyAppExeName}"; Parameters: "--uninstall"; Flags: runhidden waituntilterminated

[Code]
var
  LogDirPage: TInputDirWizardPage;
  LocationPage: TInputOptionWizardPage;

function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  // Forçar o encerramento de qualquer processo ChecklistLogin.exe legado rodando em segundo plano
  Exec('taskkill.exe', '/F /IM ChecklistLogin.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := True;
end;

procedure InitializeWizard;
begin
  LocationPage := CreateInputOptionPage(
    wpSelectDir,
    'Selecione a Unidade / Local de Instalação',
    'Para qual local ou dispositivo este checklist está sendo instalado?',
    'Selecione a opção desejada para direcionar os registros de acesso para a aba correspondente da planilha online:',
    True,
    False
  );
  LocationPage.Add('PORTO');
  LocationPage.Add('NOTEBOOK PORTO');
  LocationPage.Add('BEIRA MAR');
  LocationPage.Add('NOTEBOOK BEIRA MAR');
  LocationPage.SelectedValueIndex := 0;

  LogDirPage := CreateInputDirPage(
    LocationPage.ID,
    'Selecione a Pasta de Logs',
    'Onde os arquivos de log do checklist deverão ser salvos?',
    'Selecione a pasta onde os registros (.txt) do checklist SENAI serão armazenados para TODOS os usuários:',
    False,
    ''
  );
  LogDirPage.Add('');
  LogDirPage.Values[0] := 'C:\Logs\Checklist';
end;

function GetSelectedLocation(Param: String): String;
begin
  case LocationPage.SelectedValueIndex of
    0: Result := 'PORTO';
    1: Result := 'NOTEBOOK PORTO';
    2: Result := 'BEIRA MAR';
    3: Result := 'NOTEBOOK BEIRA MAR';
  else
    Result := 'PORTO';
  end;
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
  SelectedLoc: String;
  ResultCode: Integer;
begin
  if CurStep = ssPreInstall then
  begin
    // Garantir novo encerramento antes de copiar os novos arquivos
    Exec('taskkill.exe', '/F /IM ChecklistLogin.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;

  if CurStep = ssPostInstall then
  begin
    EscapedPath := GetLogFolderPath('');
    StringChangeEx(EscapedPath, '\', '\\', True);
    SelectedLoc := GetSelectedLocation('');

    ConfigContent := '{' + #13#10 +
                     '  "logFolderPath": "' + EscapedPath + '",' + #13#10 +
                     '  "location": "' + SelectedLoc + '",' + #13#10 +
                     '  "googleWebhookUrl": "https://script.google.com/macros/s/AKfycbyvVnnAmbv_zVtjBilNd8qu5S4LWfN_K6QZga-aE5j3UKs3NOmSBHn1SKjaCCOeSrpA/exec"' + #13#10 +
                     '}';

    ConfigFilePath := ExpandConstant('{app}\config.json');
    SaveStringToFile(ConfigFilePath, ConfigContent, False);
  end;
end;
