using System.Net.Http.Json;
using InvestProMax.Api.Auth;
using InvestProMax.Application.Auth;
using Microsoft.Extensions.Options;

namespace InvestProMax.Api.Services;

public sealed class NotificadorTelegramService : INotificadorTelegramService
{
    private readonly HttpClient _http;
    private readonly IntegracaoN8nOptions _n8nOptions;
    private readonly IntegracaoTelegramOptions _telegramOptions;
    private readonly ILogger<NotificadorTelegramService> _logger;

    public NotificadorTelegramService(
        HttpClient http,
        IOptions<IntegracaoN8nOptions> n8nOptions,
        IOptions<IntegracaoTelegramOptions> telegramOptions,
        ILogger<NotificadorTelegramService> logger)
    {
        _http = http;
        _n8nOptions = n8nOptions.Value;
        _telegramOptions = telegramOptions.Value;
        _logger = logger;
    }

    public string BotUsername =>
        string.IsNullOrWhiteSpace(_telegramOptions.BotUsername)
            ? "MarkX_InvestProMax_MarquesFC_bot"
            : _telegramOptions.BotUsername.Trim().TrimStart('@');

    public string MontarLinkValidacao(string codigo) =>
        $"https://t.me/{BotUsername}?start={Uri.EscapeDataString(codigo)}";

    public IReadOnlyList<long> ObterAdminsConfigurados() =>
        _telegramOptions.AdminTelegramId is > 0
            ? [_telegramOptions.AdminTelegramId.Value]
            : [];

    public async Task NotificarUsuarioAsync(long telegramId, string mensagem, CancellationToken ct = default)
    {
        if (telegramId <= 0 || string.IsNullOrWhiteSpace(mensagem) || string.IsNullOrWhiteSpace(_n8nOptions.WebhookNotificarTelegramUrl))
        {
            return;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _n8nOptions.WebhookNotificarTelegramUrl)
            {
                Content = JsonContent.Create(new { telegramId, mensagem })
            };

            if (!string.IsNullOrWhiteSpace(_n8nOptions.Segredo))
            {
                request.Headers.Add("X-Segredo", _n8nOptions.Segredo);
            }

            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Webhook de notificação Telegram retornou HTTP {StatusCode} para TelegramId {TelegramId}.",
                    (int)response.StatusCode, telegramId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao enviar notificação Telegram via n8n para TelegramId {TelegramId}.", telegramId);
        }
    }
}
