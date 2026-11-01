using InvestProMax.Application.Faturas;

namespace InvestProMax.Api.BackgroundServices;

/// <summary>
/// Seção 9: "um hosted service roda diariamente e aplica as regras 7 e 8 para todos os
/// usuários". A cada execução, cria um escopo de DI próprio (services scoped, como
/// InvestProMaxDbContext, não podem ser resolvidos direto do provider raiz de um
/// BackgroundService singleton).
///
/// ASSUNÇÃO SINALIZADA: "diariamente" não define um horário exato — implementado como um loop
/// que roda uma vez ao iniciar a Api e depois a cada 24h corridas, não agendado para um
/// horário fixo (ex.: sempre à meia-noite). Para produção, considerar trocar por um agendador
/// de verdade (Quartz.NET, Hangfire, ou um cron job externo chamando POST /faturas/gerar) se o
/// horário exato importar — está fora do escopo desta fase.
/// </summary>
public sealed class FaturaGeracaoHostedService : BackgroundService
{
    private static readonly TimeSpan IntervaloEntreExecucoes = TimeSpan.FromHours(24);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FaturaGeracaoHostedService> _logger;

    public FaturaGeracaoHostedService(IServiceProvider serviceProvider, ILogger<FaturaGeracaoHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ExecutarUmaVezAsync(stoppingToken);

            try
            {
                await Task.Delay(IntervaloEntreExecucoes, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Encerramento normal do serviço (aplicação sendo desligada) — não é uma falha.
            }
        }
    }

    private async Task ExecutarUmaVezAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var job = scope.ServiceProvider.GetRequiredService<IFaturaGeracaoJobService>();
            var resultado = await job.ExecutarParaTodosUsuariosAsync(ct);

            _logger.LogInformation(
                "Job diário de faturas concluído: {Usuarios} usuários, {Criadas} faturas criadas, {Atrasadas} reclassificadas como atrasadas.",
                resultado.UsuariosProcessados, resultado.FaturasCriadas, resultado.FaturasReclassificadasAtrasadas);
        }
        catch (Exception ex)
        {
            // Uma falha aqui não deve derrubar a Api — só registra e tenta de novo no próximo ciclo.
            _logger.LogError(ex, "Falha ao executar o job diário de geração de faturas.");
        }
    }
}
