using InvestProMax.Application.Papeis;
using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;
using InvestProMax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InvestProMax.Infrastructure.Repositories;

public class PapelRepository : IPapelRepository
{
    private readonly InvestProMaxDbContext _db;
    private readonly SupabaseDbContext? _supabaseDb;
    private readonly LocalDbContext? _localDb;

    public PapelRepository(
        InvestProMaxDbContext db,
        SupabaseDbContext? supabaseDb = null,
        LocalDbContext? localDb = null)
    {
        _db = db;
        _supabaseDb = supabaseDb;
        _localDb = localDb;
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

        foreach (var secundario in new InvestProMaxDbContext?[] { _supabaseDb, _localDb })
        {
            if (secundario is null || ReferenceEquals(secundario, _db))
            {
                continue;
            }

            try
            {
                await ZerarCarteiraNoContextoAsync(secundario, usuarioId, ct);
            }
            catch
            {
                // Falha na base secundária não interrompe a operação na base principal.
            }
        }
    }

    private static async Task ZerarCarteiraNoContextoAsync(InvestProMaxDbContext db, Guid usuarioId, CancellationToken ct)
    {
        var papelIds = await db.Papeis.Where(p => p.UsuarioId == usuarioId).Select(p => p.Id).ToListAsync(ct);

        if (db.Database.IsRelational())
        {
            if (papelIds.Count > 0)
            {
                await db.Transacoes.Where(t => papelIds.Contains(t.PapelId)).ExecuteDeleteAsync(ct);
                await db.Dividendos.Where(d => papelIds.Contains(d.PapelId)).ExecuteDeleteAsync(ct);
                await db.MovimentosAluguel.Where(m => papelIds.Contains(m.PapelId)).ExecuteDeleteAsync(ct);
                await db.PapelSegmentos.Where(ps => papelIds.Contains(ps.PapelId)).ExecuteDeleteAsync(ct);
            }

            await db.Papeis.Where(p => p.UsuarioId == usuarioId).ExecuteDeleteAsync(ct);
            return;
        }

        if (papelIds.Count > 0)
        {
            db.Transacoes.RemoveRange(await db.Transacoes.Where(t => papelIds.Contains(t.PapelId)).ToListAsync(ct));
            db.Dividendos.RemoveRange(await db.Dividendos.Where(d => papelIds.Contains(d.PapelId)).ToListAsync(ct));
            db.MovimentosAluguel.RemoveRange(await db.MovimentosAluguel.Where(m => papelIds.Contains(m.PapelId)).ToListAsync(ct));
            db.PapelSegmentos.RemoveRange(await db.PapelSegmentos.Where(ps => papelIds.Contains(ps.PapelId)).ToListAsync(ct));
        }

        db.Papeis.RemoveRange(await db.Papeis.Where(p => p.UsuarioId == usuarioId).ToListAsync(ct));
        await db.SaveChangesAsync(ct);
    }
}
