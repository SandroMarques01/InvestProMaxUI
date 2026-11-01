using InvestProMax.Domain.Entidades;

namespace InvestProMax.Application.Papeis;

public interface IMovimentoAluguelRepository
{
    Task<MovimentoAluguel?> ObterPorIdAsync(Guid id, Guid usuarioId, CancellationToken ct = default);
    Task<HashSet<string>> ListarHashesImportadosAsync(Guid usuarioId, CancellationToken ct = default);
    Task<IReadOnlyList<MovimentoAluguel>> ListarPorPapelAsync(Guid papelId, CancellationToken ct = default);
    Task AdicionarEmLoteAsync(IEnumerable<MovimentoAluguel> movimentos, CancellationToken ct = default);
    Task AtualizarEmLoteAsync(IEnumerable<MovimentoAluguel> movimentos, CancellationToken ct = default);
}
