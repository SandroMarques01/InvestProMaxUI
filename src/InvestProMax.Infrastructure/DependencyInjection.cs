using InvestProMax.Application.Ativos;
using InvestProMax.Application.Auth;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Application.Configuracao;
using InvestProMax.Application.Contas;
using InvestProMax.Application.Dividendos;
using InvestProMax.Application.Faturas;
using InvestProMax.Application.Inventario;
using InvestProMax.Application.Papeis;
using InvestProMax.Application.Segmentos;
using InvestProMax.Application.Transacoes;
using InvestProMax.Infrastructure.Auth;
using InvestProMax.Infrastructure.Excel;
using InvestProMax.Infrastructure.Persistence;
using InvestProMax.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InvestProMax.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("InvestProMaxDb");
        var supabaseConnectionString = configuration.GetConnectionString("SupabaseDb");

        services.AddSingleton<IDatabaseProviderService, DatabaseProviderService>();
        services.AddSingleton<ConexaoFalhaInterceptor>();
        services.AddSingleton<ReplicaSaveChangesInterceptor>();

        if (!string.IsNullOrWhiteSpace(supabaseConnectionString) && IsPostgreSql(supabaseConnectionString))
        {
            var normalizedSupabase = NormalizePostgresConnectionString(supabaseConnectionString);

            // Se connectionString (InvestProMaxDb) foi informada, registra o LocalDbContext (fallback no localhost)
            // Se for nula ou vazia (ex: no Render/Docker Linux onde o SQL Server LocalDB não existe),
            // registra apenas o SupabaseDbContext como banco único e principal.
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                services.AddDbContext<LocalDbContext>(options =>
                    options.UseSqlServer(connectionString));

                services.AddDbContext<SupabaseDbContext>((sp, options) =>
                {
                    options.UseNpgsql(normalizedSupabase);
                    options.AddInterceptors(
                        sp.GetRequiredService<ConexaoFalhaInterceptor>(),
                        sp.GetRequiredService<ReplicaSaveChangesInterceptor>());
                });

                services.AddHostedService<DatabaseHealthMonitorHostedService>();

                // O contexto principal injetado nos repositórios (InvestProMaxDbContext) é resolvido dinamicamente:
                // Por padrão usa Supabase (banco principal); se estiver inacessível, cai automaticamente para LocalDB (localhost).
                services.AddScoped<InvestProMaxDbContext>(sp =>
                {
                    var dbProvider = sp.GetRequiredService<IDatabaseProviderService>();
                    return dbProvider.GetActiveDbContext(sp);
                });
            }
            else
            {
                // Modo Produção / Docker (sem LocalDB)
                services.AddDbContext<SupabaseDbContext>(options =>
                    options.UseNpgsql(normalizedSupabase));

                services.AddScoped<InvestProMaxDbContext>(sp =>
                    sp.GetRequiredService<SupabaseDbContext>());
            }
        }
        else if (IsPostgreSql(connectionString))
        {
            var normalized = NormalizePostgresConnectionString(connectionString!);
            services.AddDbContext<SupabaseDbContext>(options =>
                options.UseNpgsql(normalized));
            services.AddScoped<InvestProMaxDbContext>(sp =>
                sp.GetRequiredService<SupabaseDbContext>());
        }
        else
        {
            services.AddDbContext<LocalDbContext>(options =>
                options.UseSqlServer(connectionString));
            services.AddScoped<InvestProMaxDbContext>(sp =>
                sp.GetRequiredService<LocalDbContext>());
        }

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SecaoConfiguracao));
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SecaoConfiguracao));

        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();

        services.AddScoped<IPapelRepository, PapelRepository>();
        services.AddScoped<ITransacaoRepository, TransacaoRepository>();
        services.AddScoped<IMovimentoAluguelRepository, MovimentoAluguelRepository>();
        services.AddScoped<IDividendoRepository, DividendoRepository>();
        services.AddScoped<ISegmentoRepository, SegmentoRepository>();
        services.AddScoped<IAtivoRepository, AtivoRepository>();
        services.AddScoped<IInventarioRepository, InventarioRepository>();
        services.AddScoped<IContaRepository, ContaRepository>();
        services.AddScoped<IFaturaRepository, FaturaRepository>();

        services.AddScoped<IExtratoB3Reader, ClosedXmlExtratoB3Reader>();
        services.AddScoped<ICotacoesReader, ClosedXmlCotacoesReader>();
        services.AddScoped<IBackupExcelWriter, ClosedXmlBackupExcelWriter>();
        services.AddScoped<IBackupExcelReader, ClosedXmlBackupExcelReader>();

        var mongoConnectionString = configuration.GetConnectionString("MongoDb");
        if (!string.IsNullOrWhiteSpace(mongoConnectionString))
        {
            var mongoUrl = new MongoDB.Driver.MongoUrl(mongoConnectionString);
            var mongoSettings = MongoDB.Driver.MongoClientSettings.FromUrl(mongoUrl);
            mongoSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);
            var mongoClient = new MongoDB.Driver.MongoClient(mongoSettings);

            services.AddSingleton<MongoDB.Driver.IMongoClient>(mongoClient);
            services.AddScoped<MongoDB.Driver.IMongoDatabase>(sp =>
            {
                var dbName = !string.IsNullOrWhiteSpace(mongoUrl.DatabaseName) ? mongoUrl.DatabaseName : "InvestProMax";
                return sp.GetRequiredService<MongoDB.Driver.IMongoClient>().GetDatabase(dbName);
            });
        }

        services.AddScoped<IMongoBackupService, MongoBackupService>();

        return services;
    }

    public static bool IsPostgreSql(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return false;
        return connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
            || connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase)
            || (connectionString.Contains("Port=", StringComparison.OrdinalIgnoreCase) && !connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase));
    }

    public static string NormalizePostgresConnectionString(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;

        if (raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) ||
            raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
        {
            var uriStr = raw;
            var schemeIdx = uriStr.IndexOf("://", StringComparison.Ordinal);
            var rest = uriStr.Substring(schemeIdx + 3);

            var pathIdx = rest.IndexOfAny(new[] { '/', '?' });
            var authority = pathIdx >= 0 ? rest.Substring(0, pathIdx) : rest;
            var pathAndQuery = pathIdx >= 0 ? rest.Substring(pathIdx) : "";

            var atIdx = authority.LastIndexOf('@');
            string user = "";
            string pass = "";
            string hostAndPort = authority;

            if (atIdx >= 0)
            {
                var userInfo = authority.Substring(0, atIdx);
                hostAndPort = authority.Substring(atIdx + 1);

                var colonIdx = userInfo.IndexOf(':');
                if (colonIdx >= 0)
                {
                    user = Uri.UnescapeDataString(userInfo.Substring(0, colonIdx));
                    pass = Uri.UnescapeDataString(userInfo.Substring(colonIdx + 1));
                }
                else
                {
                    user = Uri.UnescapeDataString(userInfo);
                }
            }

            var hpParts = hostAndPort.Split(':');
            var host = hpParts[0];
            var port = hpParts.Length > 1 && int.TryParse(hpParts[1], out var p) ? p : 5432;

            var dbName = "postgres";
            if (!string.IsNullOrEmpty(pathAndQuery))
            {
                var qIdx = pathAndQuery.IndexOf('?');
                var path = qIdx >= 0 ? pathAndQuery.Substring(0, qIdx) : pathAndQuery;
                var trimmed = path.TrimStart('/');
                if (!string.IsNullOrWhiteSpace(trimmed))
                    dbName = trimmed;
            }

            return $"Host={host};Port={port};Database={dbName};Username={user};Password={pass};Ssl Mode=Require;";
        }

        return raw;
    }
}
