namespace InvestProMax.Api.Auth;

public sealed class IntegracaoCotacoesOptions
{
    public const string SecaoConfiguracao = "Integracao:Cotacoes";

    public string ApiKey { get; set; } = string.Empty;
    public string UsuarioId { get; set; } = string.Empty;
}

public sealed class IntegracaoN8nOptions
{
    public const string SecaoConfiguracao = "Integracao:N8n";

    public string WebhookUrl { get; set; } = string.Empty;
    public string WebhookCotacaoAtivoUrl { get; set; } = string.Empty;
    public string WebhookNotificarTelegramUrl { get; set; } = string.Empty;
    public string Segredo { get; set; } = string.Empty;
}

public sealed class IntegracaoTelegramOptions
{
    public const string SecaoConfiguracao = "Integracao:Telegram";

    public string BotUsername { get; set; } = "MarkX_InvestProMax_MarquesFC_bot";
    public long? AdminTelegramId { get; set; }
}

public sealed class GoogleAuthOptions
{
    public const string SecaoConfiguracao = "Auth:Google";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}
