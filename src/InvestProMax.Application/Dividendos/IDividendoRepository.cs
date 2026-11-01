using InvestProMax.Domain.Entidades;

namespace InvestProMax.Application.Dividendos;

public interface IDividendoRepository
{
    Task<Dividendo?> ObterPorIdAsync(Guid id, Guid usuarioId, CancellationToken ct = default);

    Task<(IReadOnlyList<Dividendo> Itens, int Total)> ListarAsync(
        Guid usuarioId, DividendoFiltro filtro, int skip, int tamanhoPagina, CancellationToken ct = default);

    /// <summary>Usada nas operações de troca/desdobramento (regras 3/4).</summary>
    Task<IReadOnlyList<Dividendo>> ListarPorPapelAsync(Guid papelId, CancellationToken ct = default);

    Task AdicionarAsync(Dividendo dividendo, CancellationToken ct = default);
    Task AtualizarAsync(Dividendo dividendo, CancellationToken ct = default);
    Task AtualizarEmLoteAsync(IEnumerable<Dividendo> dividendos, CancellationToken ct = default);
    Task RemoverAsync(Dividendo dividendo, CancellationToken ct = default);

    /// <summary>Regra 5: mesmo propósito do método homônimo em ITransacaoRepository.</summary>
    Task<HashSet<string>> ListarHashesImportadosAsync(Guid usuarioId, CancellationToken ct = default);

    /// <summary>Usada pelo dashboard (seção 8) para agregar dividendos recebidos por mês.</summary>
    Task<IReadOnlyList<Dividendo>> ListarTodosDoUsuarioAsync(Guid usuarioId, CancellationToken ct = default);

    Task AdicionarEmLoteAsync(IEnumerable<Dividendo> dividendos, CancellationToken ct = default);
}
