using InvestProMax.Application.Inventario;
using InvestProMax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using InventarioEntidade = InvestProMax.Domain.Entidades.Inventario;

namespace InvestProMax.Infrastructure.Repositories;

public class InventarioRepository : IInventarioRepository
{
    private readonly InvestProMaxDbContext _db;

    public InventarioRepository(InvestProMaxDbContext db) => _db = db;

    public async Task<(IReadOnlyList<InventarioEntidade> Itens, int Total)> ListarAsync(
        Guid usuarioId, InventarioFiltro filtro, int skip, int tamanhoPagina, CancellationToken ct = default)
    {
        var query = _db.Inventarios.Include(i => i.Ativo).Where(i => i.Ativo!.UsuarioId == usuarioId);

        if (filtro.AtivoId is not null)
        {
            query = query.Where(i => i.AtivoId == filtro.AtivoId);
        }
        if (filtro.Ano is not null)
        {
            query = query.Where(i => i.Data.Year == filtro.Ano);
        }
        if (filtro.Mes is not null)
        {
            query = query.Where(i => i.Data.Month == filtro.Mes);
        }

        var total = await query.CountAsync(ct);
        var itens = await query
            .OrderByDescending(i => i.Data)
            .ThenBy(i => i.AtivoId)
            .ThenBy(i => i.Id)
            .Skip(skip)
            .Take(tamanhoPagina)
            .ToListAsync(ct);

        return (itens, total);
    }

    public Task<InventarioEntidade?> ObterPorAtivoEMesAsync(Guid ativoId, int ano, int mes, CancellationToken ct = default) =>
        _db.Inventarios.FirstOrDefaultAsync(
            i => i.AtivoId == ativoId && i.Data.Year == ano && i.Data.Month == mes, ct);

    public async Task AdicionarAsync(InventarioEntidade inventario, CancellationToken ct = default)
    {
        await _db.Inventarios.AddAsync(inventario, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AtualizarAsync(InventarioEntidade inventario, CancellationToken ct = default)
    {
        _db.Inventarios.Update(inventario);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<InventarioEntidade>> ListarUltimoMesDoUsuarioAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var query = _db.Inventarios.Where(i => i.Ativo!.UsuarioId == usuarioId && i.StatusAtivo);

        var dataMaisRecente = await query.MaxAsync(i => (DateOnly?)i.Data, ct);
        if (dataMaisRecente is null)
        {
            return [];
        }

        return await query
            .Where(i => i.Data.Year == dataMaisRecente.Value.Year && i.Data.Month == dataMaisRecente.Value.Month)
            .ToListAsync(ct);
    }
}
