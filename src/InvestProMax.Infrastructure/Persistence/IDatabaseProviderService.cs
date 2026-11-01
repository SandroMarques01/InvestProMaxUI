using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InvestProMax.Infrastructure.Persistence;

public enum TipoProvedorBanco
{
    Supabase,
    LocalDb
}

public interface IDatabaseProviderService
{
    TipoProvedorBanco ProvedorAtivo { get; }
    bool SupabaseEstaAcessivel { get; }
    Task<bool> VerificarAcessibilidadeSupabaseAsync(CancellationToken ct = default);
    void MarcarSupabaseInacessivel(Exception? ex = null);
    void MarcarSupabaseAcessivel();
    InvestProMaxDbContext GetActiveDbContext(IServiceProvider serviceProvider);
}

public sealed class DatabaseProviderService : IDatabaseProviderService
{
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly ILogger<DatabaseProviderService> _logger;
    private volatile bool _supabaseDisponivel = true;
    private DateTime _ultimoTesteSupabase = DateTime.MinValue;

    public DatabaseProviderService(
        ILogger<DatabaseProviderService> logger,
        IServiceScopeFactory? scopeFactory = null)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public TipoProvedorBanco ProvedorAtivo => _supabaseDisponivel ? TipoProvedorBanco.Supabase : TipoProvedorBanco.LocalDb;

    public bool SupabaseEstaAcessivel => _supabaseDisponivel;

    public async Task<bool> VerificarAcessibilidadeSupabaseAsync(CancellationToken ct = default)
    {
        if (_scopeFactory is null)
        {
            _supabaseDisponivel = false;
            return false;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(4)); // Timeout rápido de 4s para não bloquear requisições

            using var scope = _scopeFactory.CreateScope();
            var supabase = scope.ServiceProvider.GetService<SupabaseDbContext>();
            if (supabase is null)
            {
                _supabaseDisponivel = false;
                return false;
            }

            var ok = await supabase.Database.CanConnectAsync(cts.Token);
            _supabaseDisponivel = ok;
            _ultimoTesteSupabase = DateTime.UtcNow;

            if (ok)
            {
                _logger.LogInformation("Conexão com o Supabase (Banco Principal) verificada com sucesso.");
            }
            else
            {
                _logger.LogWarning("Supabase não respondeu à verificação de conexão. Utilizando fallback LocalDB.");
            }

            return ok;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Supabase inacessível durante verificação de conexão. Utilizando fallback LocalDB.");
            _supabaseDisponivel = false;
            _ultimoTesteSupabase = DateTime.UtcNow;
            return false;
        }
    }

    public void MarcarSupabaseInacessivel(Exception? ex = null)
    {
        if (_supabaseDisponivel)
        {
            _supabaseDisponivel = false;
            _ultimoTesteSupabase = DateTime.UtcNow;
            _logger.LogWarning(ex, "Supabase marcado como INACESSÍVEL devido a falha de conexão/operação. Alternando imediatamente para LocalDB (localhost).");
        }
    }

    public void MarcarSupabaseAcessivel()
    {
        if (!_supabaseDisponivel)
        {
            _supabaseDisponivel = true;
            _ultimoTesteSupabase = DateTime.UtcNow;
            _logger.LogInformation("Supabase marcado como ACESSÍVEL. Restaurando como Banco Principal para transações e consultas.");
        }
    }

    public InvestProMaxDbContext GetActiveDbContext(IServiceProvider sp)
    {
        if (_supabaseDisponivel)
        {
            var supabase = sp.GetService<SupabaseDbContext>();
            if (supabase is not null)
            {
                return supabase;
            }
        }

        var localDb = sp.GetService<LocalDbContext>();
        if (localDb is not null)
        {
            return localDb;
        }

        return sp.GetRequiredService<InvestProMaxDbContext>();
    }
}

