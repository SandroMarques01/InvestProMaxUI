using InvestProMax.Application.Transacoes;
using InvestProMax.Domain.Entidades;
using InvestProMax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InvestProMax.Infrastructure.Repositories;

public class TransacaoRepository : ITransacaoRepository
{
    private readonly InvestProMaxDbContext _db;

    public TransacaoRepository(InvestProMaxDbContext db) => _db = db;

    public Task<Transacao?> ObterPorIdAsync(Guid id, Guid usuarioId, CancellationToken ct = default) =>
        _db.Transacoes.Include(t => t.Papel)
            .FirstOrDefaultAsync(t => t.Id == id && t.Papel!.UsuarioId == usuarioId, ct);

    public async Task<(IReadOnlyList<Transacao> Itens, int Total)> ListarAsync(
        Guid usuarioId, TransacaoFiltro filtro, int skip, int tamanhoPagina, CancellationToken ct = default)
    {
        var query = _db.Transacoes.Include(t => t.Papel).Where(t => t.Papel!.UsuarioId == usuarioId);

        if (filtro.PapelId is not null)
        {
            query = query.Where(t => t.PapelId == filtro.PapelId);
        }
        if (filtro.TipoPapel is not null)
        {
            query = query.Where(t => t.Papel!.TipoPapel == filtro.TipoPapel);
        }
        if (filtro.DataInicio is not null)
        {
            query = query.Where(t => t.Data >= filtro.DataInicio);
        }
        if (filtro.DataFim is not null)
        {
            query = query.Where(t => t.Data <= filtro.DataFim);
        }

        var total = await query.CountAsync(ct);
        var itens = await query
            .OrderByDescending(t => t.Data)
            .ThenBy(t => t.Id)
            .Skip(skip)
            .Take(tamanhoPagina)
            .ToListAsync(ct);

        return (itens, total);
    }

    public async Task<IReadOnlyList<Transacao>> ListarPorPapelAsync(Guid papelId, CancellationToken ct = default) =>
        await _db.Transacoes.Where(t => t.PapelId == papelId).ToListAsync(ct);

    public async Task AdicionarAsync(Transacao transacao, CancellationToken ct = default)
    {
        await _db.Transacoes.AddAsync(transacao, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AtualizarAsync(Transacao transacao, CancellationToken ct = default)
    {
        _db.Transacoes.Update(transacao);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AtualizarEmLoteAsync(IEnumerable<Transacao> transacoes, CancellationToken ct = default)
    {
        _db.Transacoes.UpdateRange(transacoes);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoverAsync(Transacao transacao, CancellationToken ct = default)
    {
        _db.Transacoes.Remove(transacao);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<HashSet<string>> ListarHashesImportadosAsync(Guid usuarioId, CancellationToken ct = default) =>
        (await _db.Transacoes
            .Where(t => t.Papel!.UsuarioId == usuarioId && t.OrigemImportacaoHash != null)
            .Select(t => t.OrigemImportacaoHash!)
            .ToListAsync(ct))
        .ToHashSet();

    public async Task AdicionarEmLoteAsync(IEnumerable<Transacao> transacoes, CancellationToken ct = default)
    {
        await _db.Transacoes.AddRangeAsync(transacoes, ct);
        await _db.SaveChangesAsync(ct);
    }
}
