namespace InvestProMax.Application.Cotacoes;

public sealed record CotacaoAtivoWebhookResult(
    bool Atualizado,
    decimal? Cotacao,
    DateTime? DataHoraCotacao,
    string? Fonte);

public interface IDisparadorAtualizacaoCotacoes
{
    bool EstaConfigurado { get; }
    bool EstaConfiguradoPorAtivo { get; }
    Task DispararAsync(DateTime solicitadoEm, CancellationToken ct = default);
    Task<CotacaoAtivoWebhookResult?> DispararPorAtivoAsync(string codigo, CancellationToken ct = default);
}
