using System;
using System.Collections.Generic;
using InvestProMax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace InvestProMax.IntegrationTests;

public class DatabaseProviderServiceTests
{
    [Fact]
    public void DatabaseProviderService_IniciaComSupabase_QuandoConfigurado()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<LocalDbContext>(opts =>
            opts.UseInMemoryDatabase("Test_Local_Db"));
        services.AddDbContext<SupabaseDbContext>(opts =>
            opts.UseInMemoryDatabase("Test_Supabase_Db"));

        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var service = new DatabaseProviderService(NullLogger<DatabaseProviderService>.Instance, scopeFactory);

        // Assert
        Assert.Equal(TipoProvedorBanco.Supabase, service.ProvedorAtivo);
        Assert.True(service.SupabaseEstaAcessivel);

        using var scope = scopeFactory.CreateScope();
        var activeDb = service.GetActiveDbContext(scope.ServiceProvider);
        Assert.IsType<SupabaseDbContext>(activeDb);
    }

    [Fact]
    public void MarcarSupabaseInacessivel_AlternaParaLocalDb()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<LocalDbContext>(opts =>
            opts.UseInMemoryDatabase("Test_Local_Db2"));
        services.AddDbContext<SupabaseDbContext>(opts =>
            opts.UseInMemoryDatabase("Test_Supabase_Db2"));

        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var service = new DatabaseProviderService(NullLogger<DatabaseProviderService>.Instance, scopeFactory);

        // Act - Simula queda do Supabase
        service.MarcarSupabaseInacessivel(new TimeoutException("Supabase indisponível"));

        // Assert
        Assert.Equal(TipoProvedorBanco.LocalDb, service.ProvedorAtivo);
        Assert.False(service.SupabaseEstaAcessivel);

        using (var scope = scopeFactory.CreateScope())
        {
            var activeDb = service.GetActiveDbContext(scope.ServiceProvider);
            Assert.IsType<LocalDbContext>(activeDb);
        }

        // Act - Simula restauração de conectividade
        service.MarcarSupabaseAcessivel();

        // Assert
        Assert.Equal(TipoProvedorBanco.Supabase, service.ProvedorAtivo);
        Assert.True(service.SupabaseEstaAcessivel);

        using (var scope = scopeFactory.CreateScope())
        {
            var activeDbRestaurado = service.GetActiveDbContext(scope.ServiceProvider);
            Assert.IsType<SupabaseDbContext>(activeDbRestaurado);
        }
    }

    [Fact]
    public void AddInfrastructure_ComValidacaoDeEscopo_ResolveSupabaseSemExcecao()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDatabaseProviderService, DatabaseProviderService>();
        services.AddSingleton<ConexaoFalhaInterceptor>();
        services.AddSingleton<ReplicaSaveChangesInterceptor>();

        services.AddDbContext<LocalDbContext>(opts =>
            opts.UseInMemoryDatabase("LocalDb_ScopeTest"));

        services.AddDbContext<SupabaseDbContext>((sp, opts) =>
        {
            opts.UseInMemoryDatabase("Supabase_ScopeTest");
            opts.AddInterceptors(
                sp.GetRequiredService<ConexaoFalhaInterceptor>(),
                sp.GetRequiredService<ReplicaSaveChangesInterceptor>());
        });

        services.AddScoped<InvestProMaxDbContext>(sp =>
        {
            var dbProvider = sp.GetRequiredService<IDatabaseProviderService>();
            return dbProvider.GetActiveDbContext(sp);
        });

        // Act - Constrói o container com validação estrita de escopo (como no ASP.NET Core em Development)
        using var sp = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        // Assert - Não dispara exceção de escopo e resolve perfeitamente
        using var scope = sp.CreateScope();
        var activeDb = scope.ServiceProvider.GetRequiredService<InvestProMaxDbContext>();
        Assert.NotNull(activeDb);
        Assert.IsType<SupabaseDbContext>(activeDb);
    }
}
