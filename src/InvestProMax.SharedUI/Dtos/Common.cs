namespace InvestProMax.SharedUI.Dtos;

public sealed record PaginaResultado<T>(IReadOnlyList<T> Itens, int Total, int Page, int PageSize);

/// <summary>Espelha o suficiente do RFC 7807 (ProblemDetails) que a Api devolve em erro (seção 8).</summary>
public sealed record ProblemDetailsResponse(string? Title, string? Detail, int? Status);
