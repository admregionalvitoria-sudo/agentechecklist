# Agente 2.1.1 — personalização remota

O painel está clonado como repositório independente em `painel-log-acesso/` (ignorado no Git do agente). Veja `painel-log-acesso/CUSTOMIZACAO.md` para publicação do painel e das regras Firebase.

O executável atualizado está em `release/ChecklistLogin.exe`. Publique esse arquivo, `version.txt`, `release/ChecklistLogin.exe.sha256` e `release/manifest.json` juntos no repositório utilizado pelo atualizador, após publicar as regras e validar em uma máquina piloto. O push do código não publica automaticamente as regras do Firebase; elas exigem a etapa descrita no guia do painel.

O instalador `Instalar_Agente_Checklist_SENAI.exe` existente NÃO foi regenerado: Inno Setup não está instalado nesta máquina. `installer/setup.iss` já indica 2.1.1; para gerar o instalador, disponibilize Inno Setup no caminho configurado em `build.ps1` e execute o build. O build também exige .NET Framework com bibliotecas WPF e System.Web.Extensions.

Configurações: título, subtítulo, aviso, cor do subtítulo/progresso e imagem PNG/JPEG incorporada, por unidade ou global. O agente consulta o Firestore em segundo plano ao iniciar/reabrir, mantendo as respostas em andamento. O cache fica em `%LOCALAPPDATA%/ChecklistLogin/appearance-UNIDADE.json`, por usuário. Em uma conta sem cache e sem rede, usa a interface padrão.

Validação realizada: compilação do agente com csc, teste `scripts/Test-Appearance.cs`, TypeScript e build de produção do painel. A integração com Firebase e máquinas dos alunos ainda requer a publicação e um teste piloto.

Para repetir o teste do agente depois de compilar `bin/ChecklistLogin.exe`: compile `scripts/Test-Appearance.cs` como console, referenciando `bin/ChecklistLogin.exe`, salve o teste em `bin/TestAppearance.exe` e execute-o. Ele não abre o checklist nem instala o agente.

## Instalação silenciosa PORTO

Use `scripts/Veyon-InstallOrUpdate-PORTO.ps1` do repositório do agente. Ele instala diretamente o executável atualizado, sem depender do instalador antigo. Consulte `scripts/VEYON-PORTO.md` para o comando, requisitos e diagnóstico. Para recompilar somente o agente e seus hashes, execute `./build.ps1 -ExecutableOnly`.
