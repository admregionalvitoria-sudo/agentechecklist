# PowerShell script to register ChecklistLogin task in Windows Task Scheduler
param(
    [string]$ExePath = "C:\Program Files\ChecklistLogin\ChecklistLogin.exe",
    [string]$TaskName = "ChecklistLoginTask",
    [string]$LogFolderPath = "C:\Logs\Checklist"
)

try {
    Write-Host "Configurando tarefa agendada '$TaskName' para executar no logon de qualquer usuário..." -ForegroundColor Cyan

    if (-not (Test-Path $ExePath)) {
        Write-Warning "Executável não encontrado em: $ExePath. O registro continuará, mas verifique o caminho."
    }

    # Ensure log directory exists and grant write access to Authenticated Users
    if (-not (Test-Path $LogFolderPath)) {
        New-Item -ItemType Directory -Path $LogFolderPath -Force | Out-Null
    }

    Write-Host "Configurando permissões de escrita na pasta de logs: $LogFolderPath" -ForegroundColor Cyan
    $Acl = Get-Acl $LogFolderPath
    $Ar = New-Object System.Security.AccessControl.FileSystemAccessRule("Users", "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow")
    $Acl.SetAccessRule($Ar)
    Set-Acl -Path $LogFolderPath -AclObject $Acl

    # Define Scheduled Task Action
    $Action = New-ScheduledTaskAction -Execute $ExePath

    # Define Scheduled Task Trigger (At logon for Any User)
    $Trigger = New-ScheduledTaskTrigger -AtLogOn

    # Define Scheduled Task Principal (Run with Highest Privileges)
    $Principal = New-ScheduledTaskPrincipal -GroupId "BUILTIN\Users" -RunLevel Highest

    # Define Scheduled Task Settings
    $Settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -Priority 1

    # Register Task
    Register-ScheduledTask -TaskName $TaskName -Action $Action -Trigger $Trigger -Principal $Principal -Settings $Settings -Force | Out-Null

    Write-Host "Tarefa '$TaskName' registrada com sucesso!" -ForegroundColor Green
}
catch {
    Write-Error "Falha ao registrar a tarefa agendada: $_"
    exit 1
}
