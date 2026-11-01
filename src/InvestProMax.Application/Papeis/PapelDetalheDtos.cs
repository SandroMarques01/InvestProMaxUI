using InvestProMax.Domain.Enums;

namespace InvestProMax.Application.Papeis;

/// <summary>Tela de detalhe do papel (equivalente a Papel/Detalhes do MinhasFinancas).</summary>
public sealed record PapelDetalheResponse(
    PapelResponse Papel,
    IReadOnlyList<TransacaoDetalheItem> Transacoes,
    IReadOnlyList<DividendoDetalheItem> Dividendos);

public sealed record TransacaoDetalheItem(
    Guid Id,
    DateOnly Data,
    TipoTransacao TipoTransacao,
    decimal ValorUnitario,
    decimal Quantidade,
    decimal Total,
    decimal Evolucao,
    decimal CotacaoAtual,
    decimal GanhoUnitario,
    decimal EvolucaoAtual,
    string? Descricao);

public sealed record DividendoDetalheItem(
    Guid Id,
    DateOnly Data,
    TipoDividendo TipoDividendo,
    decimal ValorRecebido,
    decimal Quantidade,
    decimal ValorUnitario,
    decimal PrecoMedio,
    decimal YieldOnCostPercentual,
    decimal CotacaoAtual,
    decimal DyPercentual,
    decimal Evolucao,
    decimal EvolucaoPercentual,
    string? Descricao);
