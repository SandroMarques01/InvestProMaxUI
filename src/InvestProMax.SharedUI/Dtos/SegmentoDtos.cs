namespace InvestProMax.SharedUI.Dtos;

public sealed record CriarSegmentoRequest(string Nome, string? Descricao);

public sealed record AtualizarSegmentoRequest(string Nome, string? Descricao, bool Ativo);

public sealed record SegmentoResponse(Guid Id, string Nome, string? Descricao, bool Ativo);
