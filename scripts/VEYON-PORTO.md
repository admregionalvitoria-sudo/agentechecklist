# Instalar ou atualizar como PORTO via Veyon

O arquivo `Veyon-InstallOrUpdate-PORTO.ps1` instala diretamente o aplicativo Windows `ChecklistLogin.exe`. Não depende do instalador antigo nem abre assistentes. Requer Windows com PowerShell 5.1 e .NET Framework 4.x/WPF.

## Comando

No recurso **Executar programa** do Veyon, use o comando abaixo **em contexto de administrador elevado ou SYSTEM**:

```powershell
powershell.exe -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; [Net.ServicePointManager]::SecurityProtocol=[Net.ServicePointManager]::SecurityProtocol -bor 3072; $s=Join-Path $env:TEMP 'Veyon-InstallOrUpdate-PORTO.ps1'; Invoke-WebRequest -UseBasicParsing 'https://raw.githubusercontent.com/admregionalvitoria-sudo/agentechecklist/main/scripts/Veyon-InstallOrUpdate-PORTO.ps1' -OutFile $s; & $s"
```

Se a versão do Veyon separar programa e argumentos, use `powershell.exe` como programa e o restante como argumentos.

O Veyon pode executar programas no contexto do usuário conectado. Se esse usuário não tiver um token elevado, a instalação silenciosa em Program Files não será possível: o script retorna código 5, sem solicitar UAC. Nesse caso distribua pelo mecanismo administrativo já disponível na escola (por exemplo, script de inicialização do computador por GPO ou ferramenta de implantação executada como SYSTEM). Não é necessário nem indicado colocar uma senha no comando.

Referência do recurso: [manual do Veyon](https://docs.veyon.io/en/latest/user/features.html#run-program).

## O que configura

- Consulta o manifesto e baixa o executável do repositório oficial solicitado; verifica SHA-256 e versão antes de encerrar o agente existente.
- Instala em `%ProgramW6432%\ChecklistLogin` (Program Files nativo), ou atualiza nesse local.
- Define a unidade **PORTO**, inclusive se antes estava em outra unidade. Mantém a pasta de logs existente quando válida; em instalações novas usa `C:\Logs\Checklist`.
- Executa o modo `--install`, configura o JSON local e compartilhado, a tarefa `ChecklistLoginTask`, o início automático no Windows e a permissão usada pelo atualizador existente.
- Verifica tarefa, unidade, registro e permissão. Se a atualização falhar depois da substituição, tenta restaurar o executável anterior; registra a falha para nova execução. Esse retorno não desfaz todas as configurações já aplicadas.
- Não reinicia o computador nem inicia a interface durante a implantação. O checklist abre no próximo login, conexão de sessão ou desbloqueio.

## Diagnóstico

Log administrativo: `%ProgramData%\ChecklistLogin\deploy-PORTO.log`.
Sem elevação: `%TEMP%\Checklist-PORTO-deploy.log`.
Códigos: `0` sucesso; `5` falta de elevação; `1` download/configuração/validação falhou.

É possível testar somente o download com `powershell.exe -NoProfile -File .\Veyon-InstallOrUpdate-PORTO.ps1 -ValidateOnly`. Isso não instala nem encerra o agente.

## Próximas versões

Execute `build.ps1 -ExecutableOnly` e publique **juntos** `version.txt`, `release/ChecklistLogin.exe`, `release/ChecklistLogin.exe.sha256` e `release/manifest.json`. O atualizador continua consultando os mesmos endereços de `main`; a partir de 2.1.1 também verifica o hash. Ele troca o arquivo em segundo plano e a nova versão passa a executar na próxima inicialização do processo.

As regras Firebase precisam ser publicadas separadamente para ativar a personalização remota. A instalação e o checklist padrão continuam funcionando sem essa publicação.
