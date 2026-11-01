using InvestProMax.Application.Faturas;
using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;
using InvestProMax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InvestProMax.Infrastructure.Repositories;

public class FaturaRepository : IFaturaRepository
{
    private readonly InvestProMaxDbContext _db;

    public FaturaRepository(InvestProMaxDbContext db) => _db = db;

    private IQueryable<Fatura> ComNavegacoes() => _db.Faturas.Include(f => f.Conta).Include(f => f.Pagamento);

    public Task<Fatura?> ObterPorIdAsync(Guid id, Guid usuarioId, CancellationToken ct = default) =>
        ComNavegacoes().FirstOrDefaultAsync(f => f.Id == id && f.Conta!.UsuarioId == usuarioId, ct);

    public async Task<(IReadOnlyList<Fatura> Itens, int Total)> ListarAsync(
        Guid usuarioId, FaturaFiltro filtro, int skip, int tamanhoPagina, CancellationToken ct = default)
    {
        var query = ComNavegacoes().Where(f => f.Conta!.UsuarioId == usuarioId);

        if (filtro.Status is not null)
        {
            query = query.Where(f => f.Status == filtro.Status);
        }
        if (filtro.Competencia is not null)
        {
            query = query.Where(f => f.Competencia == filtro.Competencia);
        }

        var total = await query.CountAsync(ct);
        var itens = await query
            .OrderByDescending(f => f.DataVencimento)
            .ThenBy(f => f.Id)
            .Skip(skip)
            .Take(tamanhoPagina)
            .ToListAsync(ct);

        return (itens, total);
    }

    public Task<Fatura?> ObterPorContaECompetenciaAsync(Guid contaId, DateOnly competencia, CancellationToken ct = default) =>
        _db.Faturas.FirstOrDefaultAsync(f => f.ContaId == contaId && f.Competencia == competencia, ct);

    public async Task<IReadOnlyList<Fatura>> ListarPendentesVencidasAsync(Guid usuarioId, DateOnly hoje, CancellationToken ct = default) =>
        await _db.Faturas
            .Where(f => f.Conta!.UsuarioId == usuarioId && f.Status == StatusFatura.Pendente && f.DataVencimento < hoje)
            .ToListAsync(ct);

    public async Task AdicionarAsync(Fatura fatura, CancellationToken ct = default)
    {
        await _db.Faturas.AddAsync(fatura, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AtualizarAsync(Fatura fatura, CancellationToken ct = default)
    {
        _db.Faturas.Update(fatura);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AtualizarEmLoteAsync(IEnumerable<Fatura> faturas, CancellationToken ct = default)
    {
        _db.Faturas.UpdateRange(faturas);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AdicionarPagamentoAsync(Pagamento pagamento, CancellationToken ct = default)
    {
        await _db.Pagamentos.AddAsync(pagamento, ct);
        await _db.SaveChangesAsync(ct);
    }
}
