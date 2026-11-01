using InvestProMax.Domain.Enums;

namespace InvestProMax.Application.Papeis;

public sealed record PapelGeralItemResponse(
    Guid Id,
    string Codigo,
    string Nome,
    TipoPapel TipoPapel,
    decimal QuantidadeTotal,
    decimal PrecoMedio,
    decimal TotalCompra,
    decimal PercentualCompra,
    decimal CotacaoAtual,
    decimal TotalAtual,
    decimal PercentualTotal,
    decimal ValorizacaoPercentual,
    decimal GanhoUnitario,
    decimal GanhoTotal,
    decimal TotalDividendos,
    decimal PercentualDividendos,
    bool Ativo,
    decimal QuantidadeEmAluguel = 0,
    DateTime? CotacaoAtualizadaEm = null);

public sealed record PapeisGeralResponse(
    IReadOnlyList<PapelGeralItemResponse> Itens,
    decimal TotalInvestido,
    decimal TotalAtual,
    decimal TotalDividendos,
    decimal GanhoTotal,
    decimal ValorizacaoPercentualGeral,
    decimal DyTotalPercentual = 0);
