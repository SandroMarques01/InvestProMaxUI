namespace InvestProMax.SharedUI.Dtos;

public sealed record CriarAtivoRequest(
    string Nome, TipoAtivo TipoAtivo, TipoOrigemAtivo TipoOrigem, TipoPapel? TipoPapelVinculado, string? Descricao);

public sealed record AtualizarAtivoRequest(
    string Nome, TipoAtivo TipoAtivo, TipoOrigemAtivo TipoOrigem, TipoPapel? TipoPapelVinculado,
    string? Descricao, bool StatusAtivo);

public sealed record AtivoResponse(
    Guid Id, string Nome, TipoAtivo TipoAtivo, TipoOrigemAtivo TipoOrigem, TipoPapel? TipoPapelVinculado,
    string? Descricao, bool StatusAtivo);
