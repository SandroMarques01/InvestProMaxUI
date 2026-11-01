namespace InvestProMax.Application.Auth;

/// <summary>Configuração de autenticação vinda de appsettings (seção "Auth"). Ver Program.cs da Api.</summary>
public sealed class AuthOptions
{
    public const string SecaoConfiguracao = "Auth";

    /// <summary>Duração do refresh token. O access token é curto (15–30 min) por definição da seção 7
    /// e sua duração é responsabilidade do ITokenService/JWT, não desta classe.</summary>
    public int RefreshTokenDuracaoDias { get; set; } = 7;
}
