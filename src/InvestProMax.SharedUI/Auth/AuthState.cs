using InvestProMax.SharedUI.Dtos;

namespace InvestProMax.SharedUI.Auth;

/// <summary>
/// Estado de sessão em memória (escopo por circuito/instância — registrado como Scoped na
/// SharedUI, ver DependencyInjection.cs). O InvestProMaxApiClient escreve aqui depois de
/// login/refresh bem-sucedido; o InvestProMaxAuthStateProvider lê daqui pra alimentar
/// AuthorizeView/[Authorize] do Blazor. A persistência entre execuções do app (o refresh
/// token sobrevivendo a um fechar/abrir) é responsabilidade do ITokenStorage de cada head,
/// não deste estado em memória.
/// </summary>
public sealed class AuthState
{
    public string? AccessToken { get; private set; }
    public DateTime? AccessTokenExpiraEmUtc { get; private set; }
    public string? RefreshToken { get; private set; }
    public string? NomeUsuario { get; private set; }

    public bool EstaAutenticado =>
        AccessToken is not null && AccessTokenExpiraEmUtc is not null && AccessTokenExpiraEmUtc > DateTime.UtcNow;

    /// <summary>Disparado sempre que a sessão muda (login, refresh, logout) — o
    /// InvestProMaxAuthStateProvider assina isso para notificar o Blazor.</summary>
    public event Action? MudancaDeEstado;

    public void DefinirSessao(AutenticacaoResponse autenticacao)
    {
        AccessToken = autenticacao.AccessToken;
        AccessTokenExpiraEmUtc = autenticacao.AccessTokenExpiraEmUtc;
        RefreshToken = autenticacao.RefreshToken;
        NomeUsuario = JwtClaimsHelper.LerClaims(autenticacao.AccessToken).GetValueOrDefault("unique_name");
        MudancaDeEstado?.Invoke();
    }

    /// <summary>
    /// Usado só na inicialização do app (ver IInvestProMaxApiClient.RestaurarSessaoAsync):
    /// carrega o refresh token persistido pelo ITokenStorage do head, sem ainda ter um access
    /// token válido — a primeira chamada autenticada (ou a própria RestaurarSessaoAsync) vai
    /// trocá-lo por uma sessão completa via /auth/refresh.
    /// </summary>
    public void DefinirRefreshTokenPendente(string refreshToken) => RefreshToken = refreshToken;

    public void LimparSessao()
    {
        AccessToken = null;
        AccessTokenExpiraEmUtc = null;
        RefreshToken = null;
        NomeUsuario = null;
        MudancaDeEstado?.Invoke();
    }
}
