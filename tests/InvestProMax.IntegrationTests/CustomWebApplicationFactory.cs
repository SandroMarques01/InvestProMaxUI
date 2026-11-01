using InvestProMax.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InvestProMax.IntegrationTests;

/// <summary>
/// ASSUNÇÃO SINALIZADA: a especificação (seção 11) exige teste de integração de isolamento
/// por usuário, mas não define infraestrutura de teste. O ambiente onde este pacote foi
/// escrito não tem SQL Server disponível (mesma limitação do README principal), então estes
/// testes trocam o provider EF Core por InMemory, com um nome de banco único por fábrica
/// (isolando cada classe de teste). Isso valida o pipeline real (rotas HTTP, autenticação JWT,
/// filtragem por UsuarioId nos repositórios) — só não valida comportamento específico do SQL
/// Server, como o computed column Ano/Mes de Inventario (coberto pela configuração do EF, não
/// por este teste). Rode contra SQL Server real antes de considerar a suíte completa.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _nomeDoBanco = $"InvestProMaxTestes_{Guid.NewGuid()}";
    private readonly InMemoryDatabaseRoot _root = new();
    private readonly IServiceProvider _inMemoryProvider = new ServiceCollection()
        .AddEntityFrameworkInMemoryDatabase()
        .BuildServiceProvider();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" (não "Development") para que o Program.cs não tente rodar
        // Database.MigrateAsync() (incompatível com o provider InMemory) nem o seed de
        // usuário de desenvolvimento — o seed de usuários destes testes é feito manualmente
        // em cada teste, com UsuarioId conhecido.
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:ChaveSecreta", "chave-de-teste-apenas-para-os-testes-de-integracao-32-bytes+");
        builder.UseSetting("Jwt:Emissor", "InvestProMax.Api.Testes");
        builder.UseSetting("Jwt:Audiencia", "InvestProMax.Clientes.Testes");
        builder.UseSetting("ConnectionStrings:InvestProMaxDb", "Server=(localdb)\\mssqllocaldb;Database=dummy;Trusted_Connection=True;");
        builder.UseSetting("ConnectionStrings:SupabaseDb", "");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:ChaveSecreta"] = "chave-de-teste-apenas-para-os-testes-de-integracao-32-bytes+",
                ["Jwt:Emissor"] = "InvestProMax.Api.Testes",
                ["Jwt:Audiencia"] = "InvestProMax.Clientes.Testes",
                ["Jwt:AccessTokenExpiracaoMinutos"] = "20",
                ["Auth:RefreshTokenDuracaoDias"] = "7",
                ["ConnectionStrings:InvestProMaxDb"] = "unused-because-provider-is-replaced-below",
                ["ConnectionStrings:SupabaseDb"] = "",
                ["Cors:OrigensPermitidas:0"] = "https://localhost"
            });
        });

        builder.ConfigureServices(services =>
        {
            var descritores = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType == typeof(DbContextOptions<InvestProMaxDbContext>) ||
                d.ServiceType == typeof(DbContextOptions<SupabaseDbContext>) ||
                d.ServiceType == typeof(DbContextOptions<LocalDbContext>) ||
                d.ServiceType == typeof(IDbContextFactory<SupabaseDbContext>) ||
                d.ServiceType == typeof(IDbContextFactory<LocalDbContext>) ||
                d.ServiceType == typeof(ReplicaSaveChangesInterceptor) ||
                d.ServiceType == typeof(ConexaoFalhaInterceptor) ||
                d.ServiceType == typeof(InvestProMaxDbContext) ||
                d.ServiceType == typeof(SupabaseDbContext) ||
                d.ServiceType == typeof(LocalDbContext) ||
                d.ImplementationType == typeof(InvestProMax.Infrastructure.Persistence.DatabaseHealthMonitorHostedService) ||
                d.ServiceType == typeof(InvestProMax.Infrastructure.Persistence.DatabaseHealthMonitorHostedService)).ToList();

            foreach (var d in descritores)
            {
                services.Remove(d);
            }

            services.AddDbContext<InvestProMaxDbContext>(options =>
            {
                options.UseInMemoryDatabase(_nomeDoBanco, _root);
                options.UseInternalServiceProvider(_inMemoryProvider);
            });
        });
    }
}
