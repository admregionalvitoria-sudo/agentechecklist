# PowerShell script to unregister ChecklistLogin task from Windows Task Scheduler
param(
    [string]$TaskName = "ChecklistLoginTask"
)

try {
    Write-Host "Removendo tarefa agendada '$TaskName'..." -ForegroundColor Cyan

    $TaskExists = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    if ($TaskExists) {
        Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
        Write-Host "Tarefa '$TaskName' removida com sucesso!" -ForegroundColor Green
    } else {
        Write-Host "A tarefa '$TaskName' não foi encontrada." -ForegroundColor Yellow
    }
}
catch {
    Write-Error "Falha ao remover a tarefa agendada: $_"
    exit 1
}
