using InvestProMax.Application.Dividendos;
using InvestProMax.Domain.Entidades;
using InvestProMax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InvestProMax.Infrastructure.Repositories;

public class DividendoRepository : IDividendoRepository
{
    private readonly InvestProMaxDbContext _db;

    public DividendoRepository(InvestProMaxDbContext db) => _db = db;

    public Task<Dividendo?> ObterPorIdAsync(Guid id, Guid usuarioId, CancellationToken ct = default) =>
        _db.Dividendos.Include(d => d.Papel)
            .FirstOrDefaultAsync(d => d.Id == id && d.Papel!.UsuarioId == usuarioId, ct);

    public async Task<(IReadOnlyList<Dividendo> Itens, int Total)> ListarAsync(
        Guid usuarioId, DividendoFiltro filtro, int skip, int tamanhoPagina, CancellationToken ct = default)
    {
        var query = _db.Dividendos.Include(d => d.Papel).Where(d => d.Papel!.UsuarioId == usuarioId);

        if (filtro.PapelId is not null)
        {
            query = query.Where(d => d.PapelId == filtro.PapelId);
        }
        if (filtro.TipoPapel is not null)
        {
            query = query.Where(d => d.Papel!.TipoPapel == filtro.TipoPapel);
        }
        if (filtro.DataInicio is not null)
        {
            query = query.Where(d => d.Data >= filtro.DataInicio);
        }
        if (filtro.DataFim is not null)
        {
            query = query.Where(d => d.Data <= filtro.DataFim);
        }

        var total = await query.CountAsync(ct);
        var itens = await query
            .OrderByDescending(d => d.Data)
            .ThenBy(d => d.Id)
            .Skip(skip)
            .Take(tamanhoPagina)
            .ToListAsync(ct);

        return (itens, total);
    }

    public async Task<IReadOnlyList<Dividendo>> ListarPorPapelAsync(Guid papelId, CancellationToken ct = default) =>
        await _db.Dividendos.Where(d => d.PapelId == papelId).ToListAsync(ct);

    public async Task AdicionarAsync(Dividendo dividendo, CancellationToken ct = default)
    {
        await _db.Dividendos.AddAsync(dividendo, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AtualizarAsync(Dividendo dividendo, CancellationToken ct = default)
    {
        _db.Dividendos.Update(dividendo);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AtualizarEmLoteAsync(IEnumerable<Dividendo> dividendos, CancellationToken ct = default)
    {
        _db.Dividendos.UpdateRange(dividendos);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoverAsync(Dividendo dividendo, CancellationToken ct = default)
    {
        _db.Dividendos.Remove(dividendo);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<HashSet<string>> ListarHashesImportadosAsync(Guid usuarioId, CancellationToken ct = default) =>
        (await _db.Dividendos
            .Where(d => d.Papel!.UsuarioId == usuarioId && d.OrigemImportacaoHash != null)
            .Select(d => d.OrigemImportacaoHash!)
            .ToListAsync(ct))
        .ToHashSet();

    public async Task<IReadOnlyList<Dividendo>> ListarTodosDoUsuarioAsync(Guid usuarioId, CancellationToken ct = default) =>
        await _db.Dividendos.Where(d => d.Papel!.UsuarioId == usuarioId).ToListAsync(ct);

    public async Task AdicionarEmLoteAsync(IEnumerable<Dividendo> dividendos, CancellationToken ct = default)
    {
        await _db.Dividendos.AddRangeAsync(dividendos, ct);
        await _db.SaveChangesAsync(ct);
    }
}
