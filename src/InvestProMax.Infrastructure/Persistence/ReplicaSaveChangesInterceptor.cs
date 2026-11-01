using System.Runtime.CompilerServices;
using InvestProMax.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InvestProMax.Infrastructure.Persistence;

public sealed class ReplicaSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReplicaSaveChangesInterceptor> _logger;

    private readonly ConditionalWeakTable<DbContext, List<MudancaPendente>> _mudancasPorContexto = new();

    public ReplicaSaveChangesInterceptor(
        IServiceScopeFactory scopeFactory,
        ILogger<ReplicaSaveChangesInterceptor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is SupabaseDbContext ctx && !DatabaseDualSyncInitializer.SincronizacaoEmAndamento)
        {
            var mudancas = CapturarMudancas(ctx);
            _mudancasPorContexto.AddOrUpdate(ctx, mudancas);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is SupabaseDbContext ctx && _mudancasPorContexto.TryGetValue(ctx, out var mudancas))
        {
            _mudancasPorContexto.Remove(ctx);
            if (mudancas is { Count: > 0 })
            {
                await ReplicarParaLocalDbAsync(mudancas, cancellationToken);
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context is not null)
        {
            _mudancasPorContexto.Remove(eventData.Context);
        }
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            _mudancasPorContexto.Remove(eventData.Context);
        }
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private static List<MudancaPendente> CapturarMudancas(InvestProMaxDbContext ctx)
    {
        ctx.ChangeTracker.DetectChanges();

        var lista = new List<MudancaPendente>();
        foreach (var entry in ctx.ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            {
                var clone = ClonarEntidadeSemNavegacao(entry.Entity);
                if (clone is not null)
                {
                    lista.Add(new MudancaPendente(entry.State, clone, ObterOrdemEntidade(clone, entry.State)));
                }
            }
        }

        return lista.OrderBy(m => m.Ordem).ToList();
    }

    private async Task ReplicarParaLocalDbAsync(List<MudancaPendente> mudancas, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var localDb = scope.ServiceProvider.GetService<LocalDbContext>();
            if (localDb is null) return;

            foreach (var mudanca in mudancas)
            {
                await AplicarMudancaAsync(localDb, mudanca, ct);
            }

            await localDb.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Falha ao replicar alterações em tempo real do Supabase para o LocalDB ({Count} operação(ões)). Os dados serão reconciliados na sincronização periódica/inicialização.",
                mudancas.Count);
        }
    }

    private static async Task AplicarMudancaAsync(InvestProMaxDbContext destino, MudancaPendente mudanca, CancellationToken ct)
    {
        var entidade = mudanca.Entidade;

        if (entidade is PapelSegmento ps)
        {
            var existe = await destino.PapelSegmentos
                .AsNoTracking()
                .AnyAsync(x => x.PapelId == ps.PapelId && x.SegmentoId == ps.SegmentoId, ct);

            if (mudanca.Estado == EntityState.Deleted)
            {
                if (existe)
                {
                    destino.PapelSegmentos.Remove(ps);
                }
            }
            else if (!existe)
            {
                await destino.PapelSegmentos.AddAsync(ps, ct);
            }

            return;
        }

        var idProp = entidade.GetType().GetProperty("Id");
        if (idProp?.GetValue(entidade) is not Guid id)
        {
            return;
        }

        var existente = await destino.FindAsync(entidade.GetType(), [id], ct);

        if (mudanca.Estado == EntityState.Deleted)
        {
            if (existente is not null)
            {
                destino.Remove(existente);
            }
        }
        else if (existente is null)
        {
            await destino.AddAsync(entidade, ct);
        }
        else
        {
            destino.Entry(existente).CurrentValues.SetValues(entidade);
        }
    }

    private static int ObterOrdemEntidade(object entidade, EntityState estado)
    {
        var nivelHierarquia = entidade switch
        {
            Usuario => 1,
            Segmento => 1,
            Papel => 2,
            Ativo => 2,
            Conta => 2,
            RefreshToken => 2,
            PapelSegmento => 3,
            Transacao => 3,
            Dividendo => 3,
            MovimentoAluguel => 3,
            Inventario => 3,
            Fatura => 3,
            Pagamento => 4,
            _ => 5
        };

        // Em deleções, filhos devem ser removidos antes dos pais.
        return estado == EntityState.Deleted ? 100 - nivelHierarquia : nivelHierarquia;
    }

    public static object? ClonarEntidadeSemNavegacao(object entidade) => entidade switch
    {
        Usuario u => new Usuario
        {
            Id = u.Id,
            Nome = u.Nome,
            Email = u.Email,
            NomeUsuario = u.NomeUsuario,
            SenhaHash = u.SenhaHash,
            Ativo = u.Ativo,
            TelegramId = u.TelegramId
        },
        Segmento s => new Segmento
        {
            Id = s.Id,
            Nome = s.Nome,
            Descricao = s.Descricao,
            Ativo = s.Ativo
        },
        Papel p => new Papel
        {
            Id = p.Id,
            UsuarioId = p.UsuarioId,
            Codigo = p.Codigo,
            Nome = p.Nome,
            TipoPapel = p.TipoPapel,
            CotacaoAtual = p.CotacaoAtual,
            CotacaoAtualizadaEm = NormalizarUtc(p.CotacaoAtualizadaEm),
            FonteCotacao = p.FonteCotacao,
            Descricao = p.Descricao,
            Ativo = p.Ativo
        },
        PapelSegmento ps => new PapelSegmento
        {
            PapelId = ps.PapelId,
            SegmentoId = ps.SegmentoId
        },
        Transacao t => new Transacao
        {
            Id = t.Id,
            PapelId = t.PapelId,
            TipoTransacao = t.TipoTransacao,
            Quantidade = t.Quantidade,
            ValorUnitario = t.ValorUnitario,
            Data = t.Data,
            Descricao = t.Descricao,
            Historico = t.Historico,
            OrigemImportacaoHash = t.OrigemImportacaoHash
        },
        Dividendo d => new Dividendo
        {
            Id = d.Id,
            PapelId = d.PapelId,
            TipoDividendo = d.TipoDividendo,
            ValorRecebido = d.ValorRecebido,
            Quantidade = d.Quantidade,
            Data = d.Data,
            Descricao = d.Descricao,
            Historico = d.Historico,
            OrigemImportacaoHash = d.OrigemImportacaoHash
        },
        MovimentoAluguel m => new MovimentoAluguel
        {
            Id = m.Id,
            PapelId = m.PapelId,
            Tipo = m.Tipo,
            Quantidade = m.Quantidade,
            Data = m.Data,
            Descricao = m.Descricao,
            OrigemImportacaoHash = m.OrigemImportacaoHash
        },
        Ativo a => new Ativo
        {
            Id = a.Id,
            UsuarioId = a.UsuarioId,
            Nome = a.Nome,
            TipoAtivo = a.TipoAtivo,
            TipoOrigem = a.TipoOrigem,
            TipoPapelVinculado = a.TipoPapelVinculado,
            Descricao = a.Descricao,
            StatusAtivo = a.StatusAtivo
        },
        Inventario i => new Inventario
        {
            Id = i.Id,
            AtivoId = i.AtivoId,
            Valor = i.Valor,
            Data = i.Data,
            Descricao = i.Descricao,
            StatusAtivo = i.StatusAtivo
        },
        Conta c => new Conta
        {
            Id = c.Id,
            UsuarioId = c.UsuarioId,
            Nome = c.Nome,
            ValorEstimado = c.ValorEstimado,
            DiaVencimento = c.DiaVencimento,
            Recorrencia = c.Recorrencia,
            NumeroParcelas = c.NumeroParcelas,
            Categoria = c.Categoria,
            Descricao = c.Descricao,
            StatusAtivo = c.StatusAtivo
        },
        Fatura f => new Fatura
        {
            Id = f.Id,
            ContaId = f.ContaId,
            Competencia = f.Competencia,
            DataVencimento = f.DataVencimento,
            ValorPrevisto = f.ValorPrevisto,
            Status = f.Status
        },
        Pagamento pg => new Pagamento
        {
            Id = pg.Id,
            FaturaId = pg.FaturaId,
            ValorPago = pg.ValorPago,
            Data = pg.Data,
            FormaPagamento = pg.FormaPagamento,
            Descricao = pg.Descricao
        },
        RefreshToken rt => new RefreshToken
        {
            Id = rt.Id,
            UsuarioId = rt.UsuarioId,
            TokenHash = rt.TokenHash,
            CriadoEmUtc = NormalizarUtc(rt.CriadoEmUtc),
            ExpiraEmUtc = NormalizarUtc(rt.ExpiraEmUtc),
            RevogadoEmUtc = NormalizarUtc(rt.RevogadoEmUtc),
            SubstituidoPorId = rt.SubstituidoPorId
        },
        _ => null
    };

    public static DateTime NormalizarUtc(DateTime dt) =>
        dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);

    public static DateTime? NormalizarUtc(DateTime? dt) =>
        dt.HasValue ? NormalizarUtc(dt.Value) : null;

    private sealed record MudancaPendente(EntityState Estado, object Entidade, int Ordem);
}

