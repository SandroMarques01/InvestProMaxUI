namespace InvestProMax.Application.Cotacoes;

public sealed record TickersResponse(
    DateTime GeradoEm,
    IReadOnlyList<string> Tickers);

public sealed record CotacaoRecebidaItem(
    string Codigo,
    decimal? Cotacao,
    DateTime? DataHoraCotacao);

public sealed record FalhaCotacaoItem(
    string Codigo,
    string Motivo);

public sealed record ReceberCotacoesRequest(
    string Fonte,
    IReadOnlyList<CotacaoRecebidaItem>? Cotacoes = null,
    IReadOnlyList<FalhaCotacaoItem>? Falhas = null);

public sealed record ItemCotacaoIgnorada(
    string Codigo,
    string Motivo);

public sealed record ReceberCotacoesResponse(
    int Recebidas,
    int Atualizadas,
    IReadOnlyList<string> NaoEncontradas,
    IReadOnlyList<ItemCotacaoIgnorada> Ignoradas,
    int FalhasInformadas,
    DateTime AtualizadoEm);

public sealed record SolicitarAtualizacaoResponse(
    DateTime SolicitadoEm);
