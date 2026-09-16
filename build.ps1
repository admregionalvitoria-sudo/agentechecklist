param([switch]$ExecutableOnly)
# PowerShell script to build ChecklistLogin application and create the single unified installer
$ErrorActionPreference = "Stop"

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  Compilando Agente de Checklist SENAI   " -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

$PSScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$CscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $CscPath)) {
    $CscPath = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\Roslyn\csc.exe"
}
$IsccCandidates = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Antigravity IDE\resources\app\node_modules\innosetup\bin\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Antigravity IDE\_\resources\app\node_modules\innosetup\bin\ISCC.exe"
)
$IsccPath = $IsccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
$SourceCs = "$PSScriptRoot\src\ChecklistLogin\StandaloneApp.cs"
$BinDir = "$PSScriptRoot\bin"
$TargetExe = "$BinDir\ChecklistLogin.exe"
$InstallerIss = "$PSScriptRoot\installer\setup.iss"
$FinalInstaller = "$PSScriptRoot\Instalar_Agente_Checklist_SENAI.exe"
$ReleaseDir = "$PSScriptRoot\release"
$VersionFile = "$PSScriptRoot\version.txt"

if (-not (Test-Path $BinDir)) { New-Item -ItemType Directory -Path $BinDir | Out-Null }
if (-not (Test-Path $ReleaseDir)) { New-Item -ItemType Directory -Path $ReleaseDir | Out-Null }

# 2. Compilar C# Standalone Application na pasta temporária bin\
Write-Host "Compilando executável do agente C# WPF..." -ForegroundColor Yellow
if (Test-Path $CscPath) {
    & $CscPath /target:winexe /out:"$TargetExe" /nologo "/resource:$PSScriptRoot\logo\logo.png,logo.png" /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationFramework.dll" /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll" /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll" /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Xaml.dll" /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Web.Extensions.dll" /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll" /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Core.dll" /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Drawing.dll" /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll" "$SourceCs" "$PSScriptRoot\src\ChecklistLogin\ModernUi.cs" "/resource:$PSScriptRoot\logo\helpdesk-qr.png,helpdesk-qr.png"
    if ($LASTEXITCODE -ne 0) { throw "Falha na compilacao do agente." }
    Write-Host "Executável base compilado com logo embutida!" -ForegroundColor Green
} else {
    Write-Error "Compilador Roslyn csc.exe não encontrado em: $CscPath"
}

# 3. Extrair versão atual do StandaloneApp.cs e atualizar version.txt + release/
$versionMatch = Select-String -Path $SourceCs -Pattern 'CurrentVersion\s*=\s*"([\d.]+)"' | Select-Object -First 1
if ($versionMatch) {
    $currentVersion = $versionMatch.Matches[0].Groups[1].Value
    Write-Host "Versão detectada: $currentVersion" -ForegroundColor Cyan

    # Atualiza version.txt (usado pelo auto-updater nas máquinas)
    Set-Content -Path $VersionFile -Value $currentVersion -Encoding UTF8
    Write-Host "version.txt atualizado: $currentVersion" -ForegroundColor Green

    # Copia o exe compilado para release/ (baixado pelas máquinas no auto-update)
    Copy-Item -Path $TargetExe -Destination "$ReleaseDir\ChecklistLogin.exe" -Force
    $releaseHash = (Get-FileHash -LiteralPath "$ReleaseDir\ChecklistLogin.exe" -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath "$ReleaseDir\ChecklistLogin.exe.sha256" -Value $releaseHash -Encoding ASCII
    @{ version = $currentVersion; sha256 = $releaseHash } | ConvertTo-Json | Set-Content -LiteralPath "$ReleaseDir\manifest.json" -Encoding ASCII
    Write-Host "release\ChecklistLogin.exe copiado para auto-update!" -ForegroundColor Green
} else {
    Write-Warning "Não foi possível detectar CurrentVersion no código-fonte."
}

if ($ExecutableOnly) { Write-Host "Executavel, versao e hashes preparados."; exit 0 }

# 4. Gerar Instalador Único via Inno Setup
Write-Host "Gerando instalador único executável via Inno Setup..." -ForegroundColor Yellow
if (Test-Path $IsccPath) {
    & $IsccPath "$InstallerIss" | Out-Null
    
    if ($LASTEXITCODE -ne 0) { throw "Falha ao gerar instalador." }

    if (Test-Path $FinalInstaller) {
        Write-Host "=========================================" -ForegroundColor Green
        Write-Host "  INSTALADOR ÚNICO GERADO COM SUCESSO!   " -ForegroundColor Green
        Write-Host "  Arquivo: $FinalInstaller" -ForegroundColor Green
        Write-Host "=========================================" -ForegroundColor Green
        Write-Host ""
        Write-Host ">>> PRÓXIMO PASSO: Publicar atualização para as máquinas" -ForegroundColor Magenta
        Write-Host "    git add ." -ForegroundColor White
        Write-Host "    git commit -m 'feat: versao $currentVersion'" -ForegroundColor White
        Write-Host "    git push" -ForegroundColor White
        Write-Host "    As máquinas se atualizam automaticamente na próxima conexão!" -ForegroundColor Green
    } else {
        Write-Error "Falha ao gerar o arquivo de instalação final."
    }
} else {
    Write-Error "Inno Setup Compiler (ISCC.exe) não encontrado em: $IsccPath"
}
