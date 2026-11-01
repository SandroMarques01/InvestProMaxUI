namespace InvestProMax.Infrastructure.Auth;

/// <summary>Configuração de JWT vinda de appsettings (seção "Jwt"). Ver Program.cs da Api.</summary>
public sealed class JwtOptions
{
    public const string SecaoConfiguracao = "Jwt";

    /// <summary>
    /// Chave simétrica usada para assinar o token. NUNCA deve ficar em appsettings.json versionado —
    /// vem de appsettings.Development.json (fora do controle de versão) em dev, e de variável de
    /// ambiente/secret manager em produção (seção 7).
    /// </summary>
    public string ChaveSecreta { get; set; } = string.Empty;

    public string Emissor { get; set; } = "InvestProMax.Api";
    public string Audiencia { get; set; } = "InvestProMax.Clientes";

    /// <summary>Vida curta, conforme seção 7 (15–30 min). Default: 20.</summary>
    public int AccessTokenExpiracaoMinutos { get; set; } = 20;
}
