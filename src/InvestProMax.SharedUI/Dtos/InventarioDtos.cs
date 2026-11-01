namespace InvestProMax.SharedUI.Dtos;

public sealed record InventarioResponse(Guid Id, Guid AtivoId, decimal Valor, DateOnly Data, string? Descricao, bool StatusAtivo);

public sealed record ItemLancamentoLoteRequest(Guid AtivoId, decimal Valor, string? Descricao);

public sealed record LancamentoLoteRequest(int Ano, int Mes, IReadOnlyList<ItemLancamentoLoteRequest> Itens);

public sealed record LancamentoLoteResponse(int Criados, int Atualizados);

public sealed record ItemSugestaoCalculada(
    Guid AtivoId, string AtivoNome, decimal ValorSugerido, decimal QuantidadeTotal = 0, decimal QuantidadeEmAluguel = 0);

public sealed record SugestaoCalculadaResponse(int Ano, int Mes, IReadOnlyList<ItemSugestaoCalculada> Itens);
