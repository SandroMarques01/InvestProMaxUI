using InvestProMax.Application.Papeis;
using InvestProMax.Domain.Entidades;
using InvestProMax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InvestProMax.Infrastructure.Repositories;

public class MovimentoAluguelRepository : IMovimentoAluguelRepository
{
    private readonly InvestProMaxDbContext _db;

    public MovimentoAluguelRepository(InvestProMaxDbContext db) => _db = db;

    public Task<MovimentoAluguel?> ObterPorIdAsync(Guid id, Guid usuarioId, CancellationToken ct = default) =>
        _db.MovimentosAluguel.FirstOrDefaultAsync(m => m.Id == id && m.Papel!.UsuarioId == usuarioId, ct);

    public async Task<HashSet<string>> ListarHashesImportadosAsync(Guid usuarioId, CancellationToken ct = default) =>
        (await _db.MovimentosAluguel
            .Where(m => m.Papel!.UsuarioId == usuarioId && m.OrigemImportacaoHash != null)
            .Select(m => m.OrigemImportacaoHash!)
            .ToListAsync(ct))
        .ToHashSet();

    public async Task<IReadOnlyList<MovimentoAluguel>> ListarPorPapelAsync(Guid papelId, CancellationToken ct = default) =>
        await _db.MovimentosAluguel.Where(m => m.PapelId == papelId).ToListAsync(ct);

    public async Task AdicionarEmLoteAsync(IEnumerable<MovimentoAluguel> movimentos, CancellationToken ct = default)
    {
        await _db.MovimentosAluguel.AddRangeAsync(movimentos, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AtualizarEmLoteAsync(IEnumerable<MovimentoAluguel> movimentos, CancellationToken ct = default)
    {
        _db.MovimentosAluguel.UpdateRange(movimentos);
        await _db.SaveChangesAsync(ct);
    }
}
