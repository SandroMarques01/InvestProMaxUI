namespace InvestProMax.SharedUI.Dtos;

public sealed record CriarContaRequest(
    string Nome, decimal ValorEstimado, int DiaVencimento, Recorrencia Recorrencia,
    int? NumeroParcelas, string? Categoria, string? Descricao);

public sealed record AtualizarContaRequest(
    string Nome, decimal ValorEstimado, int DiaVencimento, Recorrencia Recorrencia,
    int? NumeroParcelas, string? Categoria, string? Descricao, bool StatusAtivo);

public sealed record ContaResponse(
    Guid Id, string Nome, decimal ValorEstimado, int DiaVencimento, Recorrencia Recorrencia,
    int? NumeroParcelas, string? Categoria, string? Descricao, bool StatusAtivo);

public sealed record PagamentoResponse(Guid Id, decimal ValorPago, DateOnly Data, string? FormaPagamento, string? Descricao);

public sealed record FaturaResponse(
    Guid Id, Guid ContaId, string ContaNome, DateOnly Competencia, DateOnly DataVencimento,
    decimal ValorPrevisto, StatusFatura Status, PagamentoResponse? Pagamento);

public sealed record PagarFaturaRequest(decimal ValorPago, DateOnly Data, string? FormaPagamento, string? Descricao);

public sealed record GerarFaturasResponse(int FaturasCriadas);
