using InvestProMax.Domain.Entidades;

namespace InvestProMax.Application.Transacoes;

public interface ITransacaoRepository
{
    /// <summary>Já filtra por dono do Papel (isolamento por usuário — regra 10).</summary>
    Task<Transacao?> ObterPorIdAsync(Guid id, Guid usuarioId, CancellationToken ct = default);

    Task<(IReadOnlyList<Transacao> Itens, int Total)> ListarAsync(
        Guid usuarioId, TransacaoFiltro filtro, int skip, int tamanhoPagina, CancellationToken ct = default);

    /// <summary>Todas as transações do papel, sem paginação — usada para recalcular posição
    /// (regras 1/2) e nas operações de troca/desdobramento (regras 3/4).</summary>
    Task<IReadOnlyList<Transacao>> ListarPorPapelAsync(Guid papelId, CancellationToken ct = default);

    Task AdicionarAsync(Transacao transacao, CancellationToken ct = default);
    Task AtualizarAsync(Transacao transacao, CancellationToken ct = default);
    Task AtualizarEmLoteAsync(IEnumerable<Transacao> transacoes, CancellationToken ct = default);
    Task RemoverAsync(Transacao transacao, CancellationToken ct = default);

    /// <summary>Regra 5: hashes de linhas do extrato B3 já importadas para o usuário, usados
    /// para não duplicar lançamentos em reimportações do mesmo arquivo.</summary>
    Task<HashSet<string>> ListarHashesImportadosAsync(Guid usuarioId, CancellationToken ct = default);

    Task AdicionarEmLoteAsync(IEnumerable<Transacao> transacoes, CancellationToken ct = default);
}
