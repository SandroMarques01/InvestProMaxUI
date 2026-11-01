using InvestProMax.Application.Segmentos;
using InvestProMax.Domain.Entidades;
using InvestProMax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InvestProMax.Infrastructure.Repositories;

public class SegmentoRepository : ISegmentoRepository
{
    private readonly InvestProMaxDbContext _db;

    public SegmentoRepository(InvestProMaxDbContext db) => _db = db;

    public Task<Segmento?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Segmentos.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<Segmento?> ObterPorNomeAsync(string nome, CancellationToken ct = default) =>
        _db.Segmentos.FirstOrDefaultAsync(s => s.Nome == nome, ct);

    public async Task<(IReadOnlyList<Segmento> Itens, int Total)> ListarAsync(
        bool? ativo, int skip, int tamanhoPagina, CancellationToken ct = default)
    {
        var query = _db.Segmentos.AsQueryable();
        if (ativo is not null)
        {
            query = query.Where(s => s.Ativo == ativo);
        }

        var total = await query.CountAsync(ct);
        var itens = await query.OrderBy(s => s.Nome).Skip(skip).Take(tamanhoPagina).ToListAsync(ct);

        return (itens, total);
    }

    public async Task<bool> ExistemTodosAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var lista = ids.Distinct().ToList();
        if (lista.Count == 0)
        {
            return true;
        }

        var encontrados = await _db.Segmentos.CountAsync(s => lista.Contains(s.Id), ct);
        return encontrados == lista.Count;
    }

    public async Task AdicionarAsync(Segmento segmento, CancellationToken ct = default)
    {
        await _db.Segmentos.AddAsync(segmento, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AtualizarAsync(Segmento segmento, CancellationToken ct = default)
    {
        _db.Segmentos.Update(segmento);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoverAsync(Segmento segmento, CancellationToken ct = default)
    {
        _db.Segmentos.Remove(segmento);
        await _db.SaveChangesAsync(ct);
    }
}
