# Script para inicializar API e Front-end Web simultaneamente
Write-Host "Iniciando InvestProMax.Api (https://localhost:7199)..." -ForegroundColor Green
$apiProcess = Start-Process dotnet -ArgumentList "run --project src/InvestProMax.Api" -PassThru

Start-Sleep -Seconds 2

Write-Host "Iniciando InvestProMax.Web..." -ForegroundColor Cyan
$webProcess = Start-Process dotnet -ArgumentList "run --project src/InvestProMax.Web" -PassThru

Write-Host "`nAmbos os processos foram iniciados com sucesso!" -ForegroundColor Yellow
Write-Host "Back-end API: PID $($apiProcess.Id)"
Write-Host "Front-end Web: PID $($webProcess.Id)"
Write-Host "Pressione qualquer tecla ou feche as janelas para finalizar."
