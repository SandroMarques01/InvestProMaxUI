using InvestProMax.Application.Ativos;
using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;
using InvestProMax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InvestProMax.Infrastructure.Repositories;

public class AtivoRepository : IAtivoRepository
{
    private readonly InvestProMaxDbContext _db;
    private readonly SupabaseDbContext? _supabaseDb;
    private readonly LocalDbContext? _localDb;

    public AtivoRepository(
        InvestProMaxDbContext db,
        SupabaseDbContext? supabaseDb = null,
        LocalDbContext? localDb = null)
    {
        _db = db;
        _supabaseDb = supabaseDb;
        _localDb = localDb;
    }

    private IQueryable<Ativo> ComNavegacoes() => _db.Ativos.Include(a => a.HistoricoInventario);

    public Task<Ativo?> ObterPorIdAsync(Guid id, Guid usuarioId, CancellationToken ct = default) =>
        ComNavegacoes().FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == usuarioId, ct);

    public async Task<(IReadOnlyList<Ativo> Itens, int Total)> ListarAsync(
        Guid usuarioId, AtivoFiltro filtro, int skip, int tamanhoPagina, CancellationToken ct = default)
    {
        var query = _db.Ativos.Where(a => a.UsuarioId == usuarioId);

        if (filtro.TipoAtivo is not null)
        {
            query = query.Where(a => a.TipoAtivo == filtro.TipoAtivo);
        }
        if (filtro.StatusAtivo is not null)
        {
            query = query.Where(a => a.StatusAtivo == filtro.StatusAtivo);
        }

        var total = await query.CountAsync(ct);
        var itens = await query.OrderBy(a => a.Nome).Skip(skip).Take(tamanhoPagina).ToListAsync(ct);

        return (itens, total);
    }

    public async Task<IReadOnlyList<Ativo>> ListarPorOrigemAsync(Guid usuarioId, TipoOrigemAtivo tipoOrigem, CancellationToken ct = default) =>
        await _db.Ativos.Where(a => a.UsuarioId == usuarioId && a.TipoOrigem == tipoOrigem && a.StatusAtivo).ToListAsync(ct);

    public async Task AdicionarAsync(Ativo ativo, CancellationToken ct = default)
    {
        await _db.Ativos.AddAsync(ativo, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AtualizarAsync(Ativo ativo, CancellationToken ct = default)
    {
        _db.Ativos.Update(ativo);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoverAsync(Ativo ativo, CancellationToken ct = default)
    {
        _db.Ativos.Remove(ativo);
        await _db.SaveChangesAsync(ct);
    }

    public async Task ZerarPatrimonioDoUsuarioAsync(Guid usuarioId, CancellationToken ct = default)
    {
        await ZerarPatrimonioNoContextoAsync(_db, usuarioId, ct);

        foreach (var secundario in new InvestProMaxDbContext?[] { _supabaseDb, _localDb })
        {
            if (secundario is null || ReferenceEquals(secundario, _db))
            {
                continue;
            }

            try
            {
                await ZerarPatrimonioNoContextoAsync(secundario, usuarioId, ct);
            }
            catch
            {
                // Falha na base secundária não interrompe a operação na base principal.
            }
        }
    }

    private static async Task ZerarPatrimonioNoContextoAsync(InvestProMaxDbContext db, Guid usuarioId, CancellationToken ct)
    {
        var ativoIds = await db.Ativos.Where(a => a.UsuarioId == usuarioId).Select(a => a.Id).ToListAsync(ct);

        if (db.Database.IsRelational())
        {
            if (ativoIds.Count > 0)
            {
                await db.Inventarios.Where(i => ativoIds.Contains(i.AtivoId)).ExecuteDeleteAsync(ct);
            }

            await db.Ativos.Where(a => a.UsuarioId == usuarioId).ExecuteDeleteAsync(ct);
            return;
        }

        if (ativoIds.Count > 0)
        {
            db.Inventarios.RemoveRange(await db.Inventarios.Where(i => ativoIds.Contains(i.AtivoId)).ToListAsync(ct));
        }

        db.Ativos.RemoveRange(await db.Ativos.Where(a => a.UsuarioId == usuarioId).ToListAsync(ct));
        await db.SaveChangesAsync(ct);
    }
}
