using System;
using System.Collections.Generic;
using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;
using InvestProMax.Infrastructure.Persistence;
using InvestProMax.Infrastructure.Repositories;
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Repositorios_ao_zerar_carteira_e_base_apagam_em_Supabase_e_LocalDb_apenas_do_usuario_solicitante(
        bool supabaseComoPrincipal)
    {
        var sufixo = Guid.NewGuid().ToString("N");
        var localOpts = new DbContextOptionsBuilder<LocalDbContext>()
            .UseInMemoryDatabase($"Zerar_Local_{sufixo}")
            .Options;
        var supabaseOpts = new DbContextOptionsBuilder<SupabaseDbContext>()
            .UseInMemoryDatabase($"Zerar_Supabase_{sufixo}")
            .Options;

        await using var localDb = new LocalDbContext(localOpts);
        await using var supabaseDb = new SupabaseDbContext(supabaseOpts);

        var usuarioA = Guid.NewGuid();
        var usuarioB = Guid.NewGuid();

        PopularDadosNosDoisBancos(supabaseDb, usuarioA, usuarioB);
        PopularDadosNosDoisBancos(localDb, usuarioA, usuarioB);

        InvestProMaxDbContext contextoAtivo = supabaseComoPrincipal ? supabaseDb : localDb;
        var papelRepo = new PapelRepository(contextoAtivo, supabaseDb, localDb);
        var ativoRepo = new AtivoRepository(contextoAtivo, supabaseDb, localDb);
        var contaRepo = new ContaRepository(contextoAtivo, supabaseDb, localDb);

        // Act
        await papelRepo.ZerarCarteiraDoUsuarioAsync(usuarioA);
        await ativoRepo.ZerarPatrimonioDoUsuarioAsync(usuarioA);
        await contaRepo.ZerarContasDoUsuarioAsync(usuarioA);

        // Assert — ambos os bancos zeraram para usuarioA e preservaram usuarioB
        var papelAId = IdPapel(usuarioA);
        var ativoAId = IdAtivo(usuarioA);
        var contaAId = IdConta(usuarioA);
        var faturaAId = IdFatura(usuarioA);

        var papelBId = IdPapel(usuarioB);
        var ativoBId = IdAtivo(usuarioB);
        var contaBId = IdConta(usuarioB);
        var faturaBId = IdFatura(usuarioB);

        foreach (InvestProMaxDbContext db in new InvestProMaxDbContext[] { supabaseDb, localDb })
        {
            Assert.Equal(0, await db.Papeis.CountAsync(p => p.UsuarioId == usuarioA));
            Assert.Equal(0, await db.Transacoes.CountAsync(t => t.PapelId == papelAId));
            Assert.Equal(0, await db.Dividendos.CountAsync(d => d.PapelId == papelAId));
            Assert.Equal(0, await db.MovimentosAluguel.CountAsync(m => m.PapelId == papelAId));
            Assert.Equal(0, await db.Ativos.CountAsync(a => a.UsuarioId == usuarioA));
            Assert.Equal(0, await db.Inventarios.CountAsync(i => i.AtivoId == ativoAId));
            Assert.Equal(0, await db.Contas.CountAsync(c => c.UsuarioId == usuarioA));
            Assert.Equal(0, await db.Faturas.CountAsync(f => f.ContaId == contaAId));
            Assert.Equal(0, await db.Pagamentos.CountAsync(p => p.FaturaId == faturaAId));

            Assert.Equal(1, await db.Papeis.CountAsync(p => p.UsuarioId == usuarioB));
            Assert.Equal(1, await db.Transacoes.CountAsync(t => t.PapelId == papelBId));
            Assert.Equal(1, await db.Dividendos.CountAsync(d => d.PapelId == papelBId));
            Assert.Equal(1, await db.MovimentosAluguel.CountAsync(m => m.PapelId == papelBId));
            Assert.Equal(1, await db.Ativos.CountAsync(a => a.UsuarioId == usuarioB));
            Assert.Equal(1, await db.Inventarios.CountAsync(i => i.AtivoId == ativoBId));
            Assert.Equal(1, await db.Contas.CountAsync(c => c.UsuarioId == usuarioB));
            Assert.Equal(1, await db.Faturas.CountAsync(f => f.ContaId == contaBId));
            Assert.Equal(1, await db.Pagamentos.CountAsync(p => p.FaturaId == faturaBId));
        }

        static Guid IdPapel(Guid usuarioId) => Deterministico(usuarioId, 1);
        static Guid IdAtivo(Guid usuarioId) => Deterministico(usuarioId, 2);
        static Guid IdConta(Guid usuarioId) => Deterministico(usuarioId, 3);
        static Guid IdFatura(Guid usuarioId) => Deterministico(usuarioId, 4);

        static Guid Deterministico(Guid usuarioId, byte marcador)
        {
            var bytes = usuarioId.ToByteArray();
            bytes[0] = (byte)(bytes[0] ^ marcador);
            bytes[15] = marcador;
            return new Guid(bytes);
        }

        static void PopularDadosNosDoisBancos(InvestProMaxDbContext db, Guid usuarioA, Guid usuarioB)
        {
            foreach (var (usuarioId, codigo) in new[] { (usuarioA, "PETR4"), (usuarioB, "VALE3") })
            {
                var papelId = IdPapel(usuarioId);
                var ativoId = IdAtivo(usuarioId);
                var contaId = IdConta(usuarioId);
                var faturaId = IdFatura(usuarioId);

                db.Papeis.Add(new Papel { Id = papelId, UsuarioId = usuarioId, Codigo = codigo, Nome = codigo, TipoPapel = TipoPapel.Acao });
                db.Transacoes.Add(new Transacao { Id = Deterministico(usuarioId, 11), PapelId = papelId, TipoTransacao = TipoTransacao.Compra, Quantidade = 10, ValorUnitario = 30, Data = new DateOnly(2026, 1, 10) });
                db.Dividendos.Add(new Dividendo { Id = Deterministico(usuarioId, 12), PapelId = papelId, TipoDividendo = TipoDividendo.Dividendo, ValorRecebido = 15, Quantidade = 10, Data = new DateOnly(2026, 2, 10) });
                db.MovimentosAluguel.Add(new MovimentoAluguel { Id = Deterministico(usuarioId, 13), PapelId = papelId, Tipo = TipoMovimentoAluguel.Saida, Quantidade = 5, Data = new DateOnly(2026, 3, 10) });

                db.Ativos.Add(new Ativo { Id = ativoId, UsuarioId = usuarioId, Nome = $"Ativo {codigo}", TipoAtivo = TipoAtivo.RendaFixa });
                db.Inventarios.Add(new Inventario { Id = Deterministico(usuarioId, 21), AtivoId = ativoId, Valor = 1000, Data = new DateOnly(2026, 4, 1) });

                db.Contas.Add(new Conta { Id = contaId, UsuarioId = usuarioId, Nome = $"Conta {codigo}", ValorEstimado = 100, DiaVencimento = 10 });
                db.Faturas.Add(new Fatura { Id = faturaId, ContaId = contaId, Competencia = new DateOnly(2026, 5, 1), DataVencimento = new DateOnly(2026, 5, 10), ValorPrevisto = 100 });
                db.Pagamentos.Add(new Pagamento { Id = Deterministico(usuarioId, 31), FaturaId = faturaId, ValorPago = 100, Data = new DateOnly(2026, 5, 9) });
            }

            db.SaveChanges();
        }
    }
}
