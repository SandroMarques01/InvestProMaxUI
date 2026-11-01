namespace InvestProMax.SharedUI.Dtos;

public sealed record CriarTransacaoRequest(
    Guid PapelId, TipoTransacao TipoTransacao, decimal Quantidade, decimal ValorUnitario, DateOnly Data, string? Descricao);

public sealed record AtualizarTransacaoRequest(
    TipoTransacao TipoTransacao, decimal Quantidade, decimal ValorUnitario, DateOnly Data, string? Descricao);

public sealed record TransacaoResponse(
    Guid Id, Guid PapelId, TipoTransacao TipoTransacao, decimal Quantidade, decimal ValorUnitario,
    DateOnly Data, string? Descricao, bool Historico);
