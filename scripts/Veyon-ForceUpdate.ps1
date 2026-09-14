<#
.SYNOPSIS
    Atualização forçada do Agente de Checklist SENAI via Veyon
.DESCRIPTION
    Execute este script via Veyon em todos os PCs do laboratório de uma vez.
    Baixa o ChecklistLogin.exe do GitHub, encerra a instância atual,
    substitui o executável e reinicia o agente — tudo silenciosamente.
.USO NO VEYON
    1. Abra o Veyon Master
    2. Selecione todos os computadores (Ctrl+A)
    3. Menu: Computadores > Executar programa / Rodar script
    4. Cole o caminho deste script OU use o one-liner abaixo diretamente
#>

# ─── CONFIGURAÇÕES ────────────────────────────────────────────────────────────
$ExeUrl     = "https://raw.githubusercontent.com/admregionalvitoria-sudo/agentechecklist/main/release/ChecklistLogin.exe"
$InstallDir = "C:\Program Files\ChecklistLogin"
$ExePath    = "$InstallDir\ChecklistLogin.exe"
$TempPath   = "$env:TEMP\ChecklistLogin_update.exe"
# ─────────────────────────────────────────────────────────────────────────────

$ErrorActionPreference = "SilentlyContinue"

# 1. Encerra o processo atual silenciosamente
Get-Process -Name "ChecklistLogin" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 1000

# 2. Baixa o novo executável do GitHub
try {
    $wc = New-Object System.Net.WebClient
    $wc.DownloadFile($ExeUrl, $TempPath)
} catch {
    exit 1
}

# Valida o download (mínimo 10KB)
if (-not (Test-Path $TempPath) -or (Get-Item $TempPath).Length -lt 10240) {
    Remove-Item $TempPath -Force -ErrorAction SilentlyContinue
    exit 1
}

# 3. Substitui o executável
try {
    # Renomeia o antigo para .old (Windows não permite sobrescrever exe em uso,
    # mas permite renomear — o processo já foi encerrado no passo 1)
    if (Test-Path $ExePath) {
        $oldPath = "$ExePath.old"
        Remove-Item $oldPath -Force -ErrorAction SilentlyContinue
        Rename-Item -Path $ExePath -NewName "$ExePath.old" -Force
    }

    # Move o novo exe para o lugar
    Move-Item -Path $TempPath -Destination $ExePath -Force
} catch {
    # Tenta cópia direta como fallback
    Copy-Item -Path $TempPath -Destination $ExePath -Force
    Remove-Item $TempPath -Force -ErrorAction SilentlyContinue
}

# 4. Reinicia o agente silenciosamente
Start-Process -FilePath $ExePath -WindowStyle Hidden -ErrorAction SilentlyContinue

exit 0
