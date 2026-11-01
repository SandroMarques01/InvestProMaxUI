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
    public string Segredo { get; set; } = string.Empty;
}
