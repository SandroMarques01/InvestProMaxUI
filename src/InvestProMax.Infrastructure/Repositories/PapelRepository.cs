using InvestProMax.Application.Papeis;
using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;
using InvestProMax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InvestProMax.Infrastructure.Repositories;

public class PapelRepository : IPapelRepository
{
    private readonly InvestProMaxDbContext _db;
    private readonly IDbContextFactory<SupabaseDbContext>? _supabaseFactory;

    public PapelRepository(InvestProMaxDbContext db, IDbContextFactory<SupabaseDbContext>? supabaseFactory = null)
    {
        _db = db;
        _supabaseFactory = supabaseFactory;
    }

    private IQueryable<Papel> ComNavegacoes() => _db.Papeis
        .Include(p => p.Transacoes)
        .Include(p => p.Dividendos)
        .Include(p => p.MovimentosAluguel)
        .Include(p => p.PapelSegmentos).ThenInclude(ps => ps.Segmento)
        .AsSplitQuery(); // 4 coleções: evita explosão cartesiana

    public Task<Papel?> ObterPorIdAsync(Guid id, Guid usuarioId, CancellationToken ct = default) =>
        ComNavegacoes().FirstOrDefaultAsync(p => p.Id == id && p.UsuarioId == usuarioId, ct);

    public async Task<(IReadOnlyList<Papel> Itens, int Total)> ListarAsync(
        Guid usuarioId, PapelFiltro filtro, int skip, int tamanhoPagina, CancellationToken ct = default)
    {
        var query = ComNavegacoes().Where(p => p.UsuarioId == usuarioId);

        if (filtro.TipoPapel is not null)
        {
            query = query.Where(p => p.TipoPapel == filtro.TipoPapel);
        }
        if (filtro.Ativo is not null)
        {
            query = query.Where(p => p.Ativo == filtro.Ativo);
        }

        var total = await query.CountAsync(ct);
        var itens = await query.OrderBy(p => p.Codigo).Skip(skip).Take(tamanhoPagina).ToListAsync(ct);

        return (itens, total);
    }

    public Task<bool> ExisteCodigoAsync(Guid usuarioId, string codigo, Guid? ignorarId = null, CancellationToken ct = default) =>
        _db.Papeis.AnyAsync(p => p.UsuarioId == usuarioId && p.Codigo == codigo && p.Id != ignorarId, ct);

    public Task<Papel?> ObterPorCodigoAsync(Guid usuarioId, string codigo, CancellationToken ct = default) =>
        ComNavegacoes().FirstOrDefaultAsync(p => p.UsuarioId == usuarioId && p.Codigo == codigo, ct);

    public async Task AdicionarAsync(Papel papel, CancellationToken ct = default)
    {
        await _db.Papeis.AddAsync(papel, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AtualizarAsync(Papel papel, CancellationToken ct = default)
    {
        _db.Papeis.Update(papel);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoverAsync(Papel papel, CancellationToken ct = default)
    {
        _db.Papeis.Remove(papel);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Papel>> ListarTodosDoUsuarioAsync(Guid usuarioId, CancellationToken ct = default) =>
        await ComNavegacoes().Where(p => p.UsuarioId == usuarioId).OrderBy(p => p.Codigo).ToListAsync(ct);

    public async Task<IReadOnlyList<Papel>> ListarPorTipoAsync(Guid usuarioId, TipoPapel tipoPapel, CancellationToken ct = default) =>
        await ComNavegacoes().Where(p => p.UsuarioId == usuarioId && p.TipoPapel == tipoPapel).ToListAsync(ct);

    public async Task ZerarCarteiraDoUsuarioAsync(Guid usuarioId, CancellationToken ct = default)
    {
        // Apagado nesta ordem por causa dos OnDelete Restrict propositais nas configurations
        // (evita cascata acidental em qualquer outro fluxo que não seja este).
        await ZerarCarteiraNoContextoAsync(_db, usuarioId, ct);

        if (_supabaseFactory is not null)
        {
            try
            {
                await using var supabase = await _supabaseFactory.CreateDbContextAsync(ct);
                await ZerarCarteiraNoContextoAsync(supabase, usuarioId, ct);
            }
            catch
            {
                // Falha na base secundária não interrompe a operação na base principal.
            }
        }
    }

    private static async Task ZerarCarteiraNoContextoAsync(InvestProMaxDbContext db, Guid usuarioId, CancellationToken ct)
    {
        var papeis = await db.Papeis.Where(p => p.UsuarioId == usuarioId).Select(p => p.Id).ToListAsync(ct);

        await db.Transacoes.Where(t => papeis.Contains(t.PapelId)).ExecuteDeleteAsync(ct);
        await db.Dividendos.Where(d => papeis.Contains(d.PapelId)).ExecuteDeleteAsync(ct);
        await db.MovimentosAluguel.Where(m => papeis.Contains(m.PapelId)).ExecuteDeleteAsync(ct);
        await db.PapelSegmentos.Where(ps => papeis.Contains(ps.PapelId)).ExecuteDeleteAsync(ct);
        await db.Papeis.Where(p => p.UsuarioId == usuarioId).ExecuteDeleteAsync(ct);
    }
}
