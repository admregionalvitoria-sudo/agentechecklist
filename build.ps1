# PowerShell script to build ChecklistLogin application and create the single unified installer
$ErrorActionPreference = "Stop"

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host " Compilando Agente de Checklist SENAI   " -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

$PSScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$CscPath = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\Roslyn\csc.exe"
$IsccPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
$SourceCs = "$PSScriptRoot\src\ChecklistLogin\StandaloneApp.cs"
$BinDir = "$PSScriptRoot\bin"
$TargetExe = "$BinDir\ChecklistLogin.exe"
$InstallerIss = "$PSScriptRoot\installer\setup.iss"
$FinalInstaller = "$PSScriptRoot\Instalar_Agente_Checklist_SENAI.exe"

# 1. Limpar instaladores/executáveis legados da raiz
Write-Host "Limpando arquivos legados da raiz..." -ForegroundColor Yellow
Remove-Item -Path "$PSScriptRoot\ChecklistLogin.exe" -Force -ErrorAction SilentlyContinue
Remove-Item -Path "$PSScriptRoot\Instalar_Agente_Checklist.exe" -Force -ErrorAction SilentlyContinue
Remove-Item -Path "$PSScriptRoot\Output" -Recurse -Force -ErrorAction SilentlyContinue

if (-not (Test-Path $BinDir)) { New-Item -ItemType Directory -Path $BinDir | Out-Null }

# 2. Compilar C# Standalone Application na pasta temporária bin\
Write-Host "Compilando executável do agente C# WPF..." -ForegroundColor Yellow
if (Test-Path $CscPath) {
    & $CscPath /target:winexe /out:"$TargetExe" /nologo "/resource:$PSScriptRoot\logo\logo.png,logo.png" /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationFramework.dll" /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll" /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll" /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Xaml.dll" /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll" /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Core.dll" /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Drawing.dll" /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll" "$SourceCs"
    Write-Host "Executável base compilado com logo embutida!" -ForegroundColor Green
} else {
    Write-Error "Compilador Roslyn csc.exe não encontrado em: $CscPath"
}

# 3. Gerar Instalador Único via Inno Setup
Write-Host "Gerando instalador único executável via Inno Setup..." -ForegroundColor Yellow
if (Test-Path $IsccPath) {
    & $IsccPath "$InstallerIss" | Out-Null
    
    # Limpar a pasta temporária bin\ para que o único .exe na raiz seja o Instalador Final
    Remove-Item -Path $BinDir -Recurse -Force -ErrorAction SilentlyContinue

    if (Test-Path $FinalInstaller) {
        Write-Host "=========================================" -ForegroundColor Green
        Write-Host " INSTALADOR ÚNICO GERADO COM SUCESSO!   " -ForegroundColor Green
        Write-Host " Arquivo: $FinalInstaller" -ForegroundColor Green
        Write-Host "=========================================" -ForegroundColor Green
    } else {
        Write-Error "Falha ao gerar o arquivo de instalação final."
    }
} else {
    Write-Error "Inno Setup Compiler (ISCC.exe) não encontrado em: $IsccPath"
}


