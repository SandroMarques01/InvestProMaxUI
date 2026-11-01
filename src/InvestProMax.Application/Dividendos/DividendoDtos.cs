using InvestProMax.Domain.Enums;

namespace InvestProMax.Application.Dividendos;

public sealed record CriarDividendoRequest(
    Guid PapelId,
    TipoDividendo TipoDividendo,
    decimal ValorRecebido,
    decimal Quantidade,
    DateOnly Data,
    string? Descricao);

public sealed record AtualizarDividendoRequest(
    TipoDividendo TipoDividendo,
    decimal ValorRecebido,
    decimal Quantidade,
    DateOnly Data,
    string? Descricao);

public sealed record DividendoResponse(
    Guid Id,
    Guid PapelId,
    TipoDividendo TipoDividendo,
    decimal ValorRecebido,
    decimal Quantidade,
    DateOnly Data,
    string? Descricao,
    bool Historico);

public sealed record DividendoFiltro(
    Guid? PapelId = null,
    TipoPapel? TipoPapel = null,
    DateOnly? DataInicio = null,
    DateOnly? DataFim = null);
