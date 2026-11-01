namespace InvestProMax.Application.Cotacoes;

public interface IAtualizacaoCotacoesService
{
    Task<TickersResponse> ListarTickersAsync(int desatualizadasHaMinutos = 0, CancellationToken ct = default);
    Task<ReceberCotacoesResponse> ReceberLoteAsync(ReceberCotacoesRequest request, CancellationToken ct = default);
}
