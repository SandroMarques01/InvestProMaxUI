namespace InvestProMax.SharedUI.Dtos;

public sealed record CriarPapelRequest(
    string Codigo,
    string Nome,
    TipoPapel TipoPapel,
    decimal CotacaoAtual,
    string? Descricao,
    IReadOnlyList<Guid>? SegmentoIds);

public sealed record AtualizarPapelRequest(
    string Nome,
    TipoPapel TipoPapel,
    decimal CotacaoAtual,
    string? Descricao,
    IReadOnlyList<Guid>? SegmentoIds);

public sealed record PapelResponse(
    Guid Id,
    string Codigo,
    string Nome,
    TipoPapel TipoPapel,
    decimal CotacaoAtual,
    string? Descricao,
    bool Ativo,
    decimal QuantidadeAtual,
    decimal PrecoMedio,
    IReadOnlyList<Guid> SegmentoIds,
    decimal QuantidadeEmAluguel = 0,
    DateTime? CotacaoAtualizadaEm = null,
    string? FonteCotacao = null);

public sealed record TrocarPapelRequest(Guid PapelDestinoId);

public sealed record DesdobramentoRequest(decimal Fator, DateOnly Data, TipoEventoPapel Tipo);
