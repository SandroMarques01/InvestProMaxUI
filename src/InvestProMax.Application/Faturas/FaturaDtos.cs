using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;

namespace InvestProMax.Application.Faturas;

public sealed record PagamentoResponse(Guid Id, decimal ValorPago, DateOnly Data, string? FormaPagamento, string? Descricao);

public sealed record FaturaResponse(
    Guid Id,
    Guid ContaId,
    string ContaNome,
    DateOnly Competencia,
    DateOnly DataVencimento,
    decimal ValorPrevisto,
    StatusFatura Status,
    PagamentoResponse? Pagamento);

public sealed record FaturaFiltro(StatusFatura? Status, DateOnly? Competencia);

public sealed record PagarFaturaRequest(decimal ValorPago, DateOnly Data, string? FormaPagamento, string? Descricao);

public sealed record GerarFaturasResponse(int FaturasCriadas);

public interface IFaturaRepository
{
    Task<Fatura?> ObterPorIdAsync(Guid id, Guid usuarioId, CancellationToken ct = default);

    Task<(IReadOnlyList<Fatura> Itens, int Total)> ListarAsync(
        Guid usuarioId, FaturaFiltro filtro, int skip, int tamanhoPagina, CancellationToken ct = default);

    /// <summary>Regra 7 — checagem de idempotência: já existe fatura desta conta nesta competência?</summary>
    Task<Fatura?> ObterPorContaECompetenciaAsync(Guid contaId, DateOnly competencia, CancellationToken ct = default);

    /// <summary>Regra 8 — candidatas a reclassificar como Atrasada.</summary>
    Task<IReadOnlyList<Fatura>> ListarPendentesVencidasAsync(Guid usuarioId, DateOnly hoje, CancellationToken ct = default);

    Task AdicionarAsync(Fatura fatura, CancellationToken ct = default);
    Task AtualizarAsync(Fatura fatura, CancellationToken ct = default);
    Task AtualizarEmLoteAsync(IEnumerable<Fatura> faturas, CancellationToken ct = default);

    Task AdicionarPagamentoAsync(Pagamento pagamento, CancellationToken ct = default);
}
