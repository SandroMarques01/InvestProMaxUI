using InvestProMax.Domain.Enums;

namespace InvestProMax.Application.Transacoes;

public sealed record CriarTransacaoRequest(
    Guid PapelId,
    TipoTransacao TipoTransacao,
    decimal Quantidade,
    decimal ValorUnitario,
    DateOnly Data,
    string? Descricao);

public sealed record AtualizarTransacaoRequest(
    TipoTransacao TipoTransacao,
    decimal Quantidade,
    decimal ValorUnitario,
    DateOnly Data,
    string? Descricao);

public sealed record TransacaoResponse(
    Guid Id,
    Guid PapelId,
    TipoTransacao TipoTransacao,
    decimal Quantidade,
    decimal ValorUnitario,
    DateOnly Data,
    string? Descricao,
    bool Historico);

public sealed record TransacaoFiltro(
    Guid? PapelId = null,
    TipoPapel? TipoPapel = null,
    DateOnly? DataInicio = null,
    DateOnly? DataFim = null);
