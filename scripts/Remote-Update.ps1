# =============================================================================
# Remote-Update.ps1 — Atualização Silenciosa do Agente de Checklist SENAI
# =============================================================================
# COMO USAR:
#   Execute este script como ADMINISTRADOR nas máquinas onde o agente já
#   está instalado para atualizar de QUALQUER versão anterior para a 2.0.2+.
#
#   Opção 1: Manual (um PC por vez)
#     Abrir PowerShell como Admin e rodar:
#       Set-ExecutionPolicy Bypass -Scope Process -Force
#       .\Remote-Update.ps1
#
#   Opção 2: Via rede (GPO / Script de logon de Admin)
#     Executar de forma remota em múltiplas máquinas via PsExec ou Invoke-Command.
#
# A partir da v2.0.2, o próprio agente se atualiza sozinho via GitHub Releases.
# Este script é necessário apenas para migrar as versões já instaladas.
# =============================================================================

$ErrorActionPreference = "SilentlyContinue"

# URL da Release mais recente no GitHub (api.github.com)
$GithubRepo   = "davimluiz/agente-cheklist"
$ApiUrl       = "https://api.github.com/repos/$GithubRepo/releases/latest"
$TempDir      = "$env:TEMP\ChecklistUpdate"

Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  Agente de Checklist SENAI — Atualização   " -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

# ─── 1. Verifica conectividade ────────────────────────────────────────────────
try {
    $null = Invoke-WebRequest -Uri "https://api.github.com" -UseBasicParsing -TimeoutSec 8
} catch {
    Write-Host "[ERRO] Sem conexão com a internet. Verifique a rede e tente novamente." -ForegroundColor Red
    exit 1
}

# ─── 2. Busca informações da release mais recente ─────────────────────────────
Write-Host "Buscando versão mais recente no GitHub..." -ForegroundColor Yellow

try {
    $headers  = @{ "User-Agent" = "ChecklistUpdater-Script" }
    $release  = Invoke-RestMethod -Uri $ApiUrl -Headers $headers -TimeoutSec 15
} catch {
    Write-Host "[ERRO] Não foi possível consultar o GitHub: $_" -ForegroundColor Red
    exit 1
}

$remoteVersion = $release.tag_name -replace '^v', ''
Write-Host "  Versão disponível no GitHub: v$remoteVersion" -ForegroundColor Green

# ─── 3. Localiza o asset instalador (Instalar_Agente_Checklist_SENAI.exe) ─────
$installerAsset = $release.assets | Where-Object { $_.name -match "Instalar_Agente" -and $_.name -match "\.exe$" }

if (-not $installerAsset) {
    # Fallback: qualquer .exe nos assets
    $installerAsset = $release.assets | Where-Object { $_.name -match "\.exe$" } | Select-Object -First 1
}

if (-not $installerAsset) {
    Write-Host "[ERRO] Nenhum instalador .exe encontrado nos assets da release." -ForegroundColor Red
    Write-Host "       Verifique se o arquivo foi anexado à Release no GitHub." -ForegroundColor Yellow
    exit 1
}

$downloadUrl  = $installerAsset.browser_download_url
$installerName = $installerAsset.name
Write-Host "  Instalador: $installerName" -ForegroundColor Cyan
Write-Host "  URL: $downloadUrl" -ForegroundColor DarkGray

# ─── 4. Download do instalador ────────────────────────────────────────────────
Write-Host ""
Write-Host "Baixando instalador..." -ForegroundColor Yellow

if (-not (Test-Path $TempDir)) { New-Item -ItemType Directory -Path $TempDir | Out-Null }
$destPath = "$TempDir\$installerName"

try {
    $webClient = New-Object System.Net.WebClient
    $webClient.DownloadFile($downloadUrl, $destPath)
} catch {
    Write-Host "[ERRO] Falha ao baixar o instalador: $_" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $destPath) -or (Get-Item $destPath).Length -lt 100KB) {
    Write-Host "[ERRO] Download inválido ou incompleto." -ForegroundColor Red
    exit 1
}

Write-Host "  Download concluído: $destPath" -ForegroundColor Green

# ─── 5. Encerra instâncias em execução da versão antiga ───────────────────────
Write-Host ""
Write-Host "Encerrando instâncias em execução do agente..." -ForegroundColor Yellow
Start-Process -FilePath "taskkill.exe" -ArgumentList "/F /IM ChecklistLogin.exe" -WindowStyle Hidden -Wait -ErrorAction SilentlyContinue

Start-Sleep -Milliseconds 1500

# ─── 6. Instala silenciosamente (/VERYSILENT — nada aparece para o usuário) ───
Write-Host "Instalando versão v$remoteVersion em segundo plano..." -ForegroundColor Yellow

try {
    $installProc = Start-Process -FilePath $destPath -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART" -Wait -PassThru -WindowStyle Hidden
    if ($installProc.ExitCode -ne 0) {
        Write-Host "[AVISO] Instalador retornou código $($installProc.ExitCode). Verifique manualmente." -ForegroundColor Yellow
    }
} catch {
    Write-Host "[ERRO] Falha na instalação: $_" -ForegroundColor Red
    exit 1
}

# ─── 7. Limpa arquivos temporários ────────────────────────────────────────────
Remove-Item -Path $TempDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "=============================================" -ForegroundColor Green
Write-Host "  ATUALIZAÇÃO CONCLUÍDA COM SUCESSO!         " -ForegroundColor Green
Write-Host "  Versão instalada: v$remoteVersion           " -ForegroundColor Green
Write-Host "  O agente agora se atualiza automaticamente." -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green
Write-Host ""
