using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InvestProMax.Infrastructure.Persistence;

public sealed class DatabaseHealthMonitorHostedService : BackgroundService
{
    private readonly IDatabaseProviderService _dbProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseHealthMonitorHostedService> _logger;
    private readonly TimeSpan _intervaloVerificacao = TimeSpan.FromSeconds(30);

    public DatabaseHealthMonitorHostedService(
        IDatabaseProviderService dbProvider,
        IServiceScopeFactory scopeFactory,
        ILogger<DatabaseHealthMonitorHostedService> logger)
    {
        _dbProvider = dbProvider;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_intervaloVerificacao, stoppingToken);

                // Só precisa testar se o Supabase estiver marcado como inacessível no momento
                if (!_dbProvider.SupabaseEstaAcessivel)
                {
                    _logger.LogInformation("Tentando reconectar ao Supabase (Banco Principal)...");
                    var reconectou = await _dbProvider.VerificarAcessibilidadeSupabaseAsync(stoppingToken);

                    if (reconectou)
                    {
                        _logger.LogInformation("Conexão com o Supabase restabelecida. Sincronizando dados com o LocalDB...");

                        using var scope = _scopeFactory.CreateScope();
                        var localDb = scope.ServiceProvider.GetRequiredService<LocalDbContext>();
                        var supabaseDb = scope.ServiceProvider.GetRequiredService<SupabaseDbContext>();

                        await DatabaseDualSyncInitializer.SincronizarBasesAsync(localDb, supabaseDb, _logger, stoppingToken);
                        _dbProvider.MarcarSupabaseAcessivel();
                        _logger.LogInformation("Supabase sincronizado e restaurado como Banco Principal.");
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Erro na verificação de saúde do banco de dados em segundo plano.");
            }
        }
    }
}

