#Requires -Version 5.1

<#
.SYNOPSIS
Instala ou atualiza o Checklist SENAI silenciosamente, sempre como PORTO.

.DESCRIPTION
Execute como administrador elevado ou SYSTEM. Nao solicita elevacao/UAC.
Valida o download antes de interromper o agente. Repara a configuracao,
a tarefa de login e o auto-update. Abre no proximo login/desbloqueio.
-ValidateOnly verifica o pacote sem instalar ou encerrar processos.
#>

[CmdletBinding()]
param(
    [switch]$ValidateOnly
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repoBase = 'https://raw.githubusercontent.com/admregionalvitoria-sudo/agentechecklist/main'

$stageDir = Join-Path `
    ([IO.Path]::GetTempPath()) `
    ('ChecklistPorto-' + [guid]::NewGuid().ToString('N'))

$stageExe = Join-Path $stageDir 'ChecklistLogin.exe'

$logFile = Join-Path `
    ([IO.Path]::GetTempPath()) `
    'Checklist-PORTO-deploy.log'

$deploymentMutex = $null
$ownsMutex = $false
$replaced = $false
$hadExe = $false
$backupPath = $null
$exePath = $null
$exitCode = 1


function Write-DeployLog {
    param(
        [string]$Message
    )

    $line = '{0:o} {1}' -f (Get-Date), $Message

    Add-Content `
        -LiteralPath $logFile `
        -Value $line `
        -Encoding UTF8

    Write-Output $line
}


function Test-UsersModifyPermission {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    # BUILTIN\Users
    $usersSid = 'S-1-5-32-545'

    $acl = Get-Acl -LiteralPath $Path

    foreach ($accessRule in $acl.Access) {

        $ruleSid = $null

        try {

            # Caso a IdentityReference ja seja um SID
            if (
                $accessRule.IdentityReference -is
                [Security.Principal.SecurityIdentifier]
            ) {
                $ruleSid = $accessRule.IdentityReference.Value
            }
            else {

                # Tenta traduzir conta -> SID
                $ruleSid = (
                    $accessRule.IdentityReference.Translate(
                        [Security.Principal.SecurityIdentifier]
                    )
                ).Value
            }

        }
        catch {

            # Algumas ACLs podem conter contas antigas,
            # usuarios removidos ou dominios indisponiveis.
            # Isso nao deve derrubar toda a instalacao.
            Write-DeployLog (
                'Aviso: nao foi possivel resolver ACL de "{0}". Ignorando entrada.' `
                    -f $accessRule.IdentityReference.Value
            )

            continue
        }

        $hasModify = (
            (
                $accessRule.FileSystemRights `
                    -band `
                [Security.AccessControl.FileSystemRights]::Modify
            ) `
            -eq `
            [Security.AccessControl.FileSystemRights]::Modify
        )

        if (
            $ruleSid -eq $usersSid -and
            $accessRule.AccessControlType -eq 'Allow' -and
            $hasModify
        ) {
            return $true
        }
    }

    return $false
}


try {

    if (-not $ValidateOnly) {

        $principal = New-Object `
            Security.Principal.WindowsPrincipal(
                [Security.Principal.WindowsIdentity]::GetCurrent()
            )

        if (
            -not $principal.IsInRole(
                [Security.Principal.WindowsBuiltInRole]::Administrator
            )
        ) {
            $exitCode = 5

            throw (
                'Execute como administrador elevado ou SYSTEM. ' +
                'O script nao abre UAC nem contorna permissoes.'
            )
        }


        $deploymentMutex = New-Object `
            Threading.Mutex(
                $false,
                'Global\ChecklistPortoDeployment'
            )

        $ownsMutex = $deploymentMutex.WaitOne(0)

        if (-not $ownsMutex) {
            throw 'Outra instalacao do checklist ja esta em andamento.'
        }


        $dataDir = Join-Path `
            $env:ProgramData `
            'ChecklistLogin'

        New-Item `
            -ItemType Directory `
            -Path $dataDir `
            -Force |
            Out-Null

        $logFile = Join-Path `
            $dataDir `
            'deploy-PORTO.log'
    }


    Write-DeployLog 'Iniciando verificacao do pacote PORTO.'


    [Net.ServicePointManager]::SecurityProtocol = `
        [Net.ServicePointManager]::SecurityProtocol `
        -bor `
        [Net.SecurityProtocolType]::Tls12


    New-Item `
        -ItemType Directory `
        -Path $stageDir |
        Out-Null


    for ($attempt = 1; $attempt -le 3; $attempt++) {

        try {

            $nonce = [guid]::NewGuid().ToString('N')

            $manifest = Invoke-RestMethod `
                -Uri "$repoBase/release/manifest.json?deploy=$nonce" `
                -TimeoutSec 30


            if (
                $manifest.version -notmatch '^\d+\.\d+\.\d+$' `
                -or `
                $manifest.sha256 -notmatch '^[a-fA-F0-9]{64}$'
            ) {
                throw 'Manifesto invalido.'
            }


            Invoke-WebRequest `
                -UseBasicParsing `
                -Uri "$repoBase/release/ChecklistLogin.exe?deploy=$nonce" `
                -OutFile $stageExe `
                -TimeoutSec 120


            if (
                (Get-Item -LiteralPath $stageExe).Length `
                -lt 10240
            ) {
                throw 'Executavel incompleto.'
            }


            $downloadHash = (
                Get-FileHash `
                    -LiteralPath $stageExe `
                    -Algorithm SHA256
            ).Hash


            if ($downloadHash -ne $manifest.sha256) {
                throw (
                    'SHA-256 divergente; pacote nao sera executado.'
                )
            }


            $binaryVersion = `
                [Diagnostics.FileVersionInfo]::GetVersionInfo(
                    $stageExe
                ).FileVersion


            if (
                $binaryVersion `
                -ne `
                ($manifest.version + '.0')
            ) {
                throw (
                    'Versao do executavel diverge do manifesto.'
                )
            }


            break

        }
        catch {

            if ($attempt -eq 3) {
                throw
            }

            Write-DeployLog (
                "Download ainda nao consistente; tentativa $attempt."
            )

            Start-Sleep -Seconds 2
        }
    }


    Write-DeployLog (
        "Download, SHA-256 e versao $($manifest.version) validados."
    )


    if (-not $ValidateOnly) {

        $programFilesRoot = if ($env:ProgramW6432) {
            $env:ProgramW6432
        }
        else {
            $env:ProgramFiles
        }


        $installDir = Join-Path `
            $programFilesRoot `
            'ChecklistLogin'

        $exePath = Join-Path `
            $installDir `
            'ChecklistLogin.exe'

        $backupPath = Join-Path `
            $installDir `
            'ChecklistLogin.veyon-backup'


        $hadExe = Test-Path `
            -LiteralPath $exePath


        New-Item `
            -ItemType Directory `
            -Path $installDir `
            -Force |
            Out-Null


        $logDir = 'C:\Logs\Checklist'

        $configPath = Join-Path `
            $installDir `
            'config.json'


        if (Test-Path -LiteralPath $configPath) {

            try {

                $oldConfig = `
                    Get-Content `
                        -LiteralPath $configPath `
                        -Raw |
                    ConvertFrom-Json


                if (
                    $oldConfig.logFolderPath `
                    -and `
                    [IO.Path]::IsPathRooted(
                        $oldConfig.logFolderPath
                    )
                ) {
                    $logDir = $oldConfig.logFolderPath
                }

            }
            catch {

                Write-DeployLog (
                    'Configuracao antiga invalida; ' +
                    'usando pasta padrao de logs.'
                )
            }
        }


        if ($logDir.Contains('"')) {
            throw 'Pasta de logs invalida.'
        }


        if ($hadExe) {
            Write-DeployLog 'Atualizando instalacao existente.'
        }
        else {
            Write-DeployLog 'Instalando agente.'
        }


        # Encerra qualquer instancia em execucao
        Get-Process `
            -Name ChecklistLogin `
            -ErrorAction SilentlyContinue |
            Stop-Process `
                -Force `
                -ErrorAction Stop


        Start-Sleep -Milliseconds 500


        if ($hadExe) {

            Copy-Item `
                -LiteralPath $exePath `
                -Destination $backupPath `
                -Force
        }


        Copy-Item `
            -LiteralPath $stageExe `
            -Destination $exePath `
            -Force


        $replaced = $true


        $setup = Start-Process `
            -FilePath $exePath `
            -ArgumentList (
                '--install "{0}" "PORTO"' -f $logDir
            ) `
            -WindowStyle Hidden `
            -PassThru


        if (-not $setup.WaitForExit(60000)) {

            $setup.Kill()

            throw (
                'Tempo excedido ao configurar agente.'
            )
        }


        if ($setup.ExitCode -ne 0) {
            throw (
                "Configuracao retornou erro $($setup.ExitCode)."
            )
        }


        $installedConfig = `
            Get-Content `
                -LiteralPath $configPath `
                -Raw |
            ConvertFrom-Json


        if ($installedConfig.location -ne 'PORTO') {
            throw 'Unidade PORTO nao foi aplicada.'
        }


        $sharedConfigPath = Join-Path `
            $dataDir `
            'config.json'


        $sharedConfig = `
            Get-Content `
                -LiteralPath $sharedConfigPath `
                -Raw |
            ConvertFrom-Json


        if ($sharedConfig.location -ne 'PORTO') {
            throw (
                'Configuracao compartilhada PORTO nao foi aplicada.'
            )
        }


        $task = Get-ScheduledTask `
            -TaskName ChecklistLoginTask `
            -ErrorAction Stop


        $validTaskAction = @(
            $task.Actions |
            Where-Object {

                $_.Execute.Trim('"') `
                    -eq `
                    $exePath
            }
        ).Count -gt 0


        if (
            $task.State -eq 'Disabled' `
            -or `
            -not $validTaskAction
        ) {
            throw 'Tarefa de inicializacao invalida.'
        }


        $registry = `
            [Microsoft.Win32.RegistryKey]::OpenBaseKey(
                [Microsoft.Win32.RegistryHive]::LocalMachine,
                [Microsoft.Win32.RegistryView]::Registry64
            )


        $runKey = $registry.OpenSubKey(
            'SOFTWARE\Microsoft\Windows\CurrentVersion\Run'
        )


        try {

            $runValue = $null

            if ($runKey) {
                $runValue = $runKey.GetValue(
                    'ChecklistLogin'
                )
            }


            if (
                $null -eq $runValue `
                -or `
                $runValue.Trim('"') -ne $exePath
            ) {
                throw (
                    'Inicializacao no Windows nao foi registrada.'
                )
            }

        }
        finally {

            if ($runKey) {
                $runKey.Dispose()
            }

            $registry.Dispose()
        }


        #
        # VALIDACAO DE PERMISSAO
        #
        # Corrigido:
        # ACLs invalidas ou contas removidas nao derrubam mais
        # todo o instalador.
        #

        $usersCanUpdate = `
            Test-UsersModifyPermission `
                -Path $installDir


        if (-not $usersCanUpdate) {
            throw (
                'Permissao necessaria ao atualizador automatico ' +
                'nao foi aplicada.'
            )
        }


        Set-Content `
            -LiteralPath (
                Join-Path $installDir 'version.txt'
            ) `
            -Value $manifest.version `
            -Encoding ASCII


        Write-DeployLog (
            "SUCESSO: $($manifest.version), PORTO, " +
            'inicializacao e auto-update configurados. ' +
            'Abre no proximo login/desbloqueio.'
        )


        $replaced = $false


        if (
            Test-Path `
                -LiteralPath $backupPath
        ) {
            Remove-Item `
                -LiteralPath $backupPath `
                -Force
        }
    }


    $exitCode = 0

}
catch {

    Write-DeployLog (
        'ERRO: ' + $_.Exception.Message
    )


    if (
        $replaced `
        -and `
        $hadExe `
        -and `
        (Test-Path -LiteralPath $backupPath)
    ) {

        try {

            Copy-Item `
                -LiteralPath $backupPath `
                -Destination $exePath `
                -Force


            Write-DeployLog (
                'Executavel anterior restaurado.'
            )

        }
        catch {

            Write-DeployLog (
                'Falha ao restaurar executavel: ' +
                $_.Exception.Message
            )
        }
    }

}
finally {

    # Apenas arquivos exatos criados neste script;
    # nenhuma exclusao recursiva.

    if (
        Test-Path `
            -LiteralPath $stageExe
    ) {
        Remove-Item `
            -LiteralPath $stageExe `
            -Force `
            -ErrorAction SilentlyContinue
    }


    if (
        Test-Path `
            -LiteralPath $stageDir
    ) {
        Remove-Item `
            -LiteralPath $stageDir `
            -ErrorAction SilentlyContinue
    }


    if ($ownsMutex) {
        $deploymentMutex.ReleaseMutex()
    }


    if ($deploymentMutex) {
        $deploymentMutex.Dispose()
    }
}


exit $exitCode
