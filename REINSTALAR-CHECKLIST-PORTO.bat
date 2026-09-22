@echo off
setlocal
set "CHECKLIST_DEPLOY_SOURCE=%~f0"
powershell.exe -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop';$s=Join-Path $env:TEMP ('ChecklistDeploy-'+[guid]::NewGuid().ToString('N')+'.ps1');try{$t=[IO.File]::ReadAllText($env:CHECKLIST_DEPLOY_SOURCE);$m='#==POWERSHELL==';$t=$t.Substring($t.LastIndexOf($m)+$m.Length);[IO.File]::WriteAllText($s,$t,[Text.Encoding]::UTF8);& $s}catch{Add-Content (Join-Path $env:TEMP 'Checklist-PORTO-bootstrap.log') $_;exit 1}finally{Remove-Item -LiteralPath $s -Force -ErrorAction SilentlyContinue}"
exit /b %errorlevel%
#==POWERSHELL==
#Requires -Version 5.1
<#
.SYNOPSIS
Reinstalacao limpa e silenciosa do Checklist SENAI, unidade PORTO.
.DESCRIPTION
Executar elevado ou SYSTEM. -ValidateOnly apenas baixa e valida o pacote.
Remove dados/configuracoes/caches e logs nas pastas padrao do agente.
Pastas de instalacao personalizadas devem ser informadas em -LegacyDirectory.
Nao inicia a interface nem reinicia o computador. Nao possui rollback.
#>
[CmdletBinding()]
param([switch]$ValidateOnly, [string[]]$LegacyDirectory = @())
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$stage = Join-Path ([IO.Path]::GetTempPath()) ('ChecklistClean-' + [guid]::NewGuid().ToString('N'))
$log = Join-Path ([IO.Path]::GetTempPath()) 'Checklist-PORTO-clean.log'
$mutex = $null
$ownsMutex = $false
$exitCode = 1
function Write-Log([string]$Message) {
    $line = '{0:o} {1}' -f (Get-Date), $Message
    Add-Content -LiteralPath $log -Value $line -Encoding UTF8
    Write-Output $line
}
function Get-SafeDirectory([string]$Path) {
    if (-not [IO.Path]::IsPathRooted($Path) -or $Path.StartsWith('\\')) {
        throw "Somente pastas locais absolutas: $Path"
    }
    $full = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    # Nunca aceitar uma raiz, perfil ou pasta generica como alvo de exclusao.
    if ((Split-Path $full -Leaf) -notin @('ChecklistLogin', 'Agente de Checklist SENAI', 'Agente Checklist SENAI') -and $full -ne 'C:\Logs\Checklist') {
        throw "Pasta nao reconhecida como exclusiva do agente: $full"
    }
    Assert-NoLink $full
    return $full
}
function Assert-NoLink([string]$Path) {
    $current = $Path
    while ($current) {
        if (Test-Path -LiteralPath $current) {
            if ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Link/junction nao permitido na limpeza: $current"
            }
        }
        $parent = Split-Path $current -Parent
        if ($parent -eq $current) { break }
        $current = $parent
    }
}
function Assert-Tree([string]$Path) {
    Assert-NoLink $Path
    foreach ($item in Get-ChildItem -LiteralPath $Path -Force) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Link/junction encontrado: $($item.FullName)" }
        if ($item.PSIsContainer) { Assert-Tree $item.FullName }
    }
}
try {
    if (-not $ValidateOnly) {
        $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
        $principal = New-Object Security.Principal.WindowsPrincipal($identity)
        if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
            $exitCode = 5
            throw 'Execute elevado ou como SYSTEM. Nao sera exibido UAC.'
        }
        $mutex = New-Object Threading.Mutex($false, 'Global\ChecklistPortoDeployment')
        try { $ownsMutex = $mutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $ownsMutex = $true }
        if (-not $ownsMutex) { throw 'Outra implantacao esta em andamento.' }
        $log = Join-Path $env:ProgramData 'Checklist-PORTO-clean.log'
    }
    Write-Log 'Baixando pacote antes de qualquer remocao.'
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    New-Item -ItemType Directory -Path $stage | Out-Null
    # Fixar manifesto e binario no mesmo commit evita corrida com publicacoes.
    $head = Invoke-RestMethod -Uri 'https://api.github.com/repos/admregionalvitoria-sudo/agentechecklist/commits/main' -Headers @{ 'User-Agent' = 'Checklist-Porto-Deploy' } -TimeoutSec 60
    if ($head.sha -notmatch '^[a-f0-9]{40}$') { throw 'Commit invalido.' }
    $base = 'https://raw.githubusercontent.com/admregionalvitoria-sudo/agentechecklist/' + $head.sha
    $manifest = Invoke-RestMethod -Uri "$base/release/manifest.json" -TimeoutSec 60
    if ($manifest.version -notmatch '^\d+\.\d+\.\d+$' -or $manifest.sha256 -notmatch '^[a-fA-F0-9]{64}$') { throw 'Manifesto invalido.' }
    $package = Join-Path $stage 'ChecklistLogin.exe'
    Invoke-WebRequest -UseBasicParsing -Uri "$base/release/ChecklistLogin.exe" -OutFile $package -TimeoutSec 180
    if ((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -ne $manifest.sha256) { throw 'SHA-256 divergente.' }
    if ([Diagnostics.FileVersionInfo]::GetVersionInfo($package).FileVersion -ne ($manifest.version + '.0')) { throw 'Versao divergente.' }
    Write-Log "Pacote $($manifest.version) validado; commit $($head.sha)."
    if ($ValidateOnly) { $exitCode = 0; return }

    $programRoot = if ($env:ProgramW6432) { $env:ProgramW6432 } else { $env:ProgramFiles }
    $installDir = Join-Path $programRoot 'ChecklistLogin'
    $exe = Join-Path $installDir 'ChecklistLogin.exe'
    $profiles = @(Get-CimInstance Win32_UserProfile | Where-Object { $_.LocalPath -and (Test-Path -LiteralPath $_.LocalPath) })
    $targets = @($installDir, (Join-Path $env:ProgramData 'ChecklistLogin'), 'C:\Logs\Checklist')
    foreach ($root in @($env:ProgramFiles, ${env:ProgramFiles(x86)})) {
        if ($root) { $targets += Join-Path $root 'ChecklistLogin' }
    }
    foreach ($profile in $profiles) {
        foreach ($relative in @('AppData\Local\ChecklistLogin', 'AppData\Roaming\ChecklistLogin', 'AppData\Local\Programs\ChecklistLogin')) {
            $targets += Join-Path $profile.LocalPath $relative
        }
    }
    $targets += $LegacyDirectory
    $targets = @($targets | ForEach-Object { Get-SafeDirectory $_ } | Sort-Object -Unique)
    # Validar todos os alvos ANTES de interromper/remover a instalacao existente.
    foreach ($target in $targets) {
        if (Test-Path -LiteralPath $target) { Assert-Tree $target }
        if ($PSCommandPath.StartsWith($target + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Execute este script fora das pastas que serao removidas.' }
        Write-Log "Alvo de limpeza: $target"
    }
    $uninstallKeys = @(
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{8F1E4A3B-D295-46F2-98C0-A7513C089F12}_is1',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{8F1E4A3B-D295-46F2-98C0-A7513C089F12}_is1'
    )
    foreach ($key in $uninstallKeys) {
        if (Test-Path -LiteralPath $key) {
            $old = Get-ItemProperty -LiteralPath $key
            if ($old.InstallLocation) {
                $oldDir = Get-SafeDirectory $old.InstallLocation
                if ($oldDir -notin $targets) { throw "Instalacao personalizada encontrada. Inclua -LegacyDirectory '$oldDir'." }
            }
        }
    }
    $tasks = @(Get-ScheduledTask | Where-Object {
        $_.TaskName -eq 'ChecklistLoginTask' -or @($_.Actions | Where-Object { $_.Execute -match '(?i)(^|[\\/])ChecklistLogin\.exe"?$' }).Count -gt 0
    })
    foreach ($task in $tasks) {
        Disable-ScheduledTask -InputObject $task | Out-Null
        Stop-ScheduledTask -InputObject $task
        Unregister-ScheduledTask -InputObject $task -Confirm:$false
    }
    Get-Process -Name ChecklistLogin -ErrorAction SilentlyContinue | Stop-Process -Force
    # Remover inicializacao da maquina e de todos os perfis, inclusive desconectados.
    $runSuffix = 'Software\Microsoft\Windows\CurrentVersion\Run'
    foreach ($path in @("HKLM:\$runSuffix", 'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run')) {
        if (Test-Path -LiteralPath $path) {
            $key = Get-Item -LiteralPath $path
            if ($key.GetValueNames() -contains 'ChecklistLogin') { Remove-ItemProperty -LiteralPath $path -Name ChecklistLogin }
        }
    }
    foreach ($profile in $profiles) {
        $hive = $profile.SID
        $loadedByUs = $false
        try {
            if (-not (Test-Path -LiteralPath "Registry::HKEY_USERS\$hive")) {
                $ntuser = Join-Path $profile.LocalPath 'NTUSER.DAT'
                if (-not (Test-Path -LiteralPath $ntuser)) { continue }
                $hive = 'ChecklistDeploy_' + [guid]::NewGuid().ToString('N')
                & reg.exe load "HKU\$hive" $ntuser | Out-Null
                if ($LASTEXITCODE -ne 0) { throw "Falha ao abrir perfil $($profile.LocalPath)" }
                $loadedByUs = $true
            }
            $userKey = [Microsoft.Win32.Registry]::Users.OpenSubKey("$hive\$runSuffix", $true)
            if ($userKey) { try { $userKey.DeleteValue('ChecklistLogin', $false) } finally { $userKey.Dispose() } }
        } finally {
            if ($loadedByUs) {
                & reg.exe unload "HKU\$hive" | Out-Null
                if ($LASTEXITCODE -ne 0) { throw "Falha ao fechar hive $hive" }
            }
        }
    }
    $startupDirs = @([Environment]::GetFolderPath('CommonStartup'))
    foreach ($profile in $profiles) { $startupDirs += Join-Path $profile.LocalPath 'AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup' }
    foreach ($dir in $startupDirs) {
        foreach ($name in @('Agente de Checklist SENAI.lnk', 'ChecklistLogin.lnk')) {
            $link = Join-Path $dir $name
            Assert-NoLink $dir
            if (Test-Path -LiteralPath $link) { Remove-Item -LiteralPath $link -Force }
        }
    }
    foreach ($target in $targets) {
        if (Test-Path -LiteralPath $target) {
            $resolved = (Resolve-Path -LiteralPath $target).ProviderPath.TrimEnd('\')
            if ($resolved -ne $target) { throw "Alvo resolvido inesperado: $resolved" }
            Assert-Tree $target
            Remove-Item -LiteralPath $resolved -Recurse -Force
            Write-Log "Removido: $resolved"
        }
    }
    foreach ($key in $uninstallKeys) { if (Test-Path -LiteralPath $key) { Remove-Item -LiteralPath $key -Recurse -Force } }
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
    Copy-Item -LiteralPath $package -Destination $exe
    $setup = Start-Process -FilePath $exe -ArgumentList '--install "C:\Logs\Checklist" "PORTO"' -WindowStyle Hidden -PassThru
    if (-not $setup.WaitForExit(60000)) { $setup.Kill(); throw 'Tempo excedido ao configurar.' }
    if ($setup.ExitCode -ne 0) { throw "Instalacao falhou: $($setup.ExitCode)" }
    foreach ($configPath in @((Join-Path $installDir 'config.json'), (Join-Path $env:ProgramData 'ChecklistLogin\config.json'))) {
        $config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
        if ($config.location -ne 'PORTO' -or $config.logFolderPath -ne 'C:\Logs\Checklist') { throw "Configuracao invalida: $configPath" }
    }
    $task = Get-ScheduledTask -TaskName ChecklistLoginTask
    if ($task.State -eq 'Disabled' -or @($task.Actions | Where-Object { $_.Execute.Trim('"') -eq $exe }).Count -eq 0) { throw 'Tarefa invalida.' }
    if ((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -ne $manifest.sha256) { throw 'Hash instalado invalido.' }
    $usersSid = New-Object Security.Principal.SecurityIdentifier('S-1-5-32-545')
    $rules = (Get-Acl -LiteralPath $installDir).GetAccessRules($true, $true, [Security.Principal.SecurityIdentifier])
    if (@($rules | Where-Object { $_.IdentityReference -eq $usersSid -and $_.AccessControlType -eq 'Allow' -and ($_.FileSystemRights -band [Security.AccessControl.FileSystemRights]::Modify) -eq [Security.AccessControl.FileSystemRights]::Modify }).Count -eq 0) { throw 'Permissao de auto-update ausente.' }
    Set-Content -LiteralPath (Join-Path $installDir 'version.txt') -Value $manifest.version -Encoding ASCII
    Write-Log "SUCESSO: $($manifest.version), PORTO. Checklist abre no proximo login/desbloqueio."
    $exitCode = 0
} catch {
    Write-Log ('ERRO: ' + $_.Exception.Message)
} finally {
    # Excluir somente o arquivo e a pasta temporaria criados por esta execucao.
    $tempExe = Join-Path $stage 'ChecklistLogin.exe'
    if (Test-Path -LiteralPath $tempExe) { Remove-Item -LiteralPath $tempExe -Force -ErrorAction SilentlyContinue }
    if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -ErrorAction SilentlyContinue }
    if ($ownsMutex) { $mutex.ReleaseMutex() }
    if ($mutex) { $mutex.Dispose() }
}
exit $exitCode
