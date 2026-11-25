using System.Net.Http.Json;
using System.Text.Json;
using InvestProMax.Api.Auth;
using InvestProMax.Application.Cotacoes;
using Microsoft.Extensions.Options;

namespace InvestProMax.Api.Services;

public sealed class DisparadorAtualizacaoCotacoes : IDisparadorAtualizacaoCotacoes
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
    public bool EstaConfiguradoPorAtivo => !string.IsNullOrWhiteSpace(_n8nOptions.WebhookCotacaoAtivoUrl);

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

        using var request = CriarRequisicaoPost(_n8nOptions.WebhookUrl, payload);

        _logger.LogInformation("Disparando webhook do n8n em {WebhookUrl} para atualização de cotações solicitada em {SolicitadoEm}",
            _n8nOptions.WebhookUrl, solicitadoEm);

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<CotacaoAtivoWebhookResult?> DispararPorAtivoAsync(string codigo, CancellationToken ct = default)
    {
        if (!EstaConfiguradoPorAtivo || string.IsNullOrWhiteSpace(codigo))
        {
            return null;
        }

        var ticker = codigo.Trim().ToUpperInvariant();
        var payload = new
        {
            ticker,
            solicitadoEm = DateTime.UtcNow.ToString("O")
        };

        try
        {
            using var request = CriarRequisicaoPost(_n8nOptions.WebhookCotacaoAtivoUrl, payload);

            _logger.LogInformation("Disparando webhook n8n de cotação unitária em {WebhookUrl} para o ticker {Ticker}",
                _n8nOptions.WebhookCotacaoAtivoUrl, ticker);

            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Webhook de cotação unitária retornou HTTP {StatusCode} para o ticker {Ticker}.",
                    (int)response.StatusCode, ticker);
                return null;
            }

            if (response.Content.Headers.ContentLength == 0)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<CotacaoAtivoWebhookResult>(JsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao consultar cotação unitária no n8n para o ticker {Ticker}. Cadastro mantido normalmente.", ticker);
            return null;
        }
    }

    private HttpRequestMessage CriarRequisicaoPost(string url, object payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload)
        };

        if (!string.IsNullOrWhiteSpace(_n8nOptions.Segredo))
        {
            request.Headers.Add("X-Segredo", _n8nOptions.Segredo);
        }

        return request;
    }
}
