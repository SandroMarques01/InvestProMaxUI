namespace InvestProMax.Application.Cotacoes;

public interface IDisparadorAtualizacaoCotacoes
{
    bool EstaConfigurado { get; }
    Task DispararAsync(DateTime solicitadoEm, CancellationToken ct = default);
}
