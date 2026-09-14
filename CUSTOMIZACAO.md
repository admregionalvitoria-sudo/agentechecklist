# Agente 2.3.0 — Cloudinary

O agente lê a personalização em https://log-acesso.vercel.app/api/appearance e usa o Cloudinary donpjw2ed para fotos, vídeos, textos e configuração por unidade. Não é preciso publicar regras novas do Firebase. O login atual do painel permanece intacto.

O painel é um repositório independente em painel-log-acesso/. Siga painel-log-acesso/CLOUDINARY.md para configurar as credenciais do servidor na Vercel e autorizar o UID do administrador. Sem essas variáveis, a publicação é recusada e o agente mantém o conteúdo local ou o padrão.

O carrossel vertical 9:16 aceita até seis mídias, com título e duração entre 5 e 60 segundos. Vídeos MP4 são exibidos sem som. O QR code padrão é https://helpdeskalunosenai.vercel.app/. O envio das respostas ao Google Sheets é preservado.

## Executável e atualização automática

O executável atual está em release/ChecklistLogin.exe. Publique com version.txt, release/ChecklistLogin.exe.sha256 e release/manifest.json, gerados por build.ps1 -ExecutableOnly. A atualização pelo GitHub continua ativa; a nova versão passa a executar na próxima abertura do processo.

O instalador antigo Instalar_Agente_Checklist_SENAI.exe não foi regenerado: falta Inno Setup nesta máquina. Use scripts/Veyon-InstallOrUpdate-PORTO.ps1 para instalar ou atualizar silenciosamente e fixar a unidade PORTO; veja scripts/VEYON-PORTO.md.

## Cache e validação

Configurações ficam em %LOCALAPPDATA%/ChecklistLogin/appearance-UNIDADE.json. As mídias ficam na subpasta media e são baixadas em segundo plano; falhas não impedem o checklist. A configuração da unidade tem precedência sobre global. O agente preserva respostas ao aplicar uma atualização visual.

Validação: build C# WPF, scripts/Test-Appearance.cs, renderização 1366x768, validação de respostas e preservação das respostas durante atualização. O painel tem testes de QR, serialização, URLs permitidas e bloqueio de publicação sem autenticação. A reprodução real de vídeo e a publicação na conta Cloudinary devem ser conferidas em uma máquina piloto após configurar a Vercel.
