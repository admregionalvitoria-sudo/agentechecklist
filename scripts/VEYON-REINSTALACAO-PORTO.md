# Reinstalacao limpa PORTO via Veyon

Use `Veyon-CleanInstall-PORTO.ps1` para remover a instalacao antiga e instalar a versao atual de `main` no GitHub, com unidade **PORTO**.

## Executar

Abra VEYON-COMANDO-PORTO.txt, copie a unica linha inteira e cole no campo **Iniciar aplicativo** do Veyon. Nao copie o arquivo para os computadores. O comando contem o script compactado e o extrai automaticamente em uma pasta temporaria durante a execucao.

O GitHub e consultado somente para baixar o agente ja publicado e seu manifesto. Nenhum script precisa ser publicado e nenhuma alteracao e enviada ao repositorio.

A execucao precisa ocorrer como **administrador elevado ou SYSTEM**. Abrir o Veyon Master como administrador nao garante que o processo remoto esteja elevado. Sem privilegios, o script retorna 5; nao abre UAC. A instalacao ocorre sem janelas e o checklist aparece no proximo login/desbloqueio, sem reiniciar o PC.

## Limpeza

- Encerra `ChecklistLogin.exe`, remove tarefas do agente, entradas `ChecklistLogin` de inicializacao da maquina e dos perfis, atalhos conhecidos de inicializacao e o registro do instalador Inno Setup antigo.
- Remove `ChecklistLogin` de Program Files (64 e 32 bits), ProgramData e AppData Local/Roaming/Local\Programs dos perfis existentes, incluindo caches e configuracoes.
- **Apaga tambem os logs antigos em `C:\Logs\Checklist` e os logs contidos nas pastas removidas.** A nova instalacao recria a pasta padrao de logs.
- Pastas personalizadas nao sao localizadas por uma busca indiscriminada no disco. Para diretorios adicionais exclusivos do agente, use `-LegacyDirectory 'D:\Apps\ChecklistLogin'`. O nome final deve ser `ChecklistLogin`, `Agente de Checklist SENAI` ou `Agente Checklist SENAI`. Outros locais de logs e copias avulsas do EXE fora dos locais descritos nao sao removidos automaticamente.
- Recusa junctions/links simbolicos e alvos genericos. O script deve ficar fora das pastas removidas.

Baixa manifesto e EXE do mesmo commit, confere SHA-256 e versao **antes da limpeza**. Reinstala usando o modo oficial `--install`, valida PORTO, configuracao compartilhada, tarefa, inicializacao, hash e permissao de auto-update. Nao depende do instalador grafico. Nao ha rollback dos dados apagados; uma falha apos a limpeza fica no log e exige nova execucao.

## Conferencia

Log: `%ProgramData%\Checklist-PORTO-clean.log`. Sem elevacao ou em validacao: `%TEMP%\Checklist-PORTO-clean.log`.

Retornos: `0` sucesso; `5` falta de elevacao; `1` falha (consulte o log).

Teste de download sem alterar a instalacao:

```text
powershell.exe -NoProfile -NonInteractive -File "C:\Windows\Temp\Veyon-CleanInstall-PORTO.ps1" -ValidateOnly
```

Validado localmente: sintaxe PowerShell e download real da versao 2.4.5, com SHA-256 e versao conferidos. A remocao/instalacao deve ser conferida primeiro em um computador piloto do laboratorio. O arquivo VEYON-COMANDO-PORTO.txt contem o comando autonomo para copiar. Nenhuma alteracao foi publicada no GitHub.


