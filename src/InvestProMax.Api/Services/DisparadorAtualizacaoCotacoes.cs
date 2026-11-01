using System.Net.Http.Json;
using InvestProMax.Api.Auth;
using InvestProMax.Application.Cotacoes;
using Microsoft.Extensions.Options;

namespace InvestProMax.Api.Services;

public sealed class DisparadorAtualizacaoCotacoes : IDisparadorAtualizacaoCotacoes
{
    private readonly HttpClient _http;
    private readonly IntegracaoN8nOptions _n8nOptions;
    private readonly ILogger<DisparadorAtualizacaoCotacoes> _logger;

    public DisparadorAtualizacaoCotacoes(
        HttpClient http,
        IOptions<IntegracaoN8nOptions> n8nOptions,
        ILogger<DisparadorAtualizacaoCotacoes> logger)
    {
        _http = http;
        _n8nOptions = n8nOptions.Value;
        _logger = logger;
    }

    public bool EstaConfigurado => !string.IsNullOrWhiteSpace(_n8nOptions.WebhookUrl);

    public async Task DispararAsync(DateTime solicitadoEm, CancellationToken ct = default)
    {
        if (!EstaConfigurado)
        {
            throw new InvalidOperationException("Webhook do n8n não está configurado.");
        }

        var payload = new
        {
            forcar = false,
            solicitadoEm = solicitadoEm.ToString("O")
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _n8nOptions.WebhookUrl)
        {
            Content = JsonContent.Create(payload)
        };

        if (!string.IsNullOrWhiteSpace(_n8nOptions.Segredo))
        {
            request.Headers.Add("X-Segredo", _n8nOptions.Segredo);
        }

        _logger.LogInformation("Disparando webhook do n8n em {WebhookUrl} para atualização de cotações solicitada em {SolicitadoEm}",
            _n8nOptions.WebhookUrl, solicitadoEm);

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
}
