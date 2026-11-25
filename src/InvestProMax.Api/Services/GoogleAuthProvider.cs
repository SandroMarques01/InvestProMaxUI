using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using InvestProMax.Api.Auth;
using InvestProMax.Application.Auth;
using InvestProMax.Application.Common.Exceptions;
using Microsoft.Extensions.Options;

namespace InvestProMax.Api.Services;

public sealed class GoogleAuthProvider : IGoogleAuthProvider
{
    private readonly HttpClient _http;
    private readonly GoogleAuthOptions _options;
    private readonly ILogger<GoogleAuthProvider> _logger;

    public GoogleAuthProvider(
        HttpClient http,
        IOptions<GoogleAuthOptions> options,
        ILogger<GoogleAuthProvider> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public bool EstaConfigurado =>
        !string.IsNullOrWhiteSpace(_options.ClientId)
        && !string.IsNullOrWhiteSpace(_options.ClientSecret);

    public string GerarUrlAutorizacao(string redirectUri, string state)
    {
        if (!EstaConfigurado)
        {
            throw new RegraDeNegocioException("Login com Google não está configurado no servidor.");
        }

        return "https://accounts.google.com/o/oauth2/v2/auth"
            + $"?client_id={Uri.EscapeDataString(_options.ClientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
            + "&response_type=code"
            + $"&scope={Uri.EscapeDataString("openid email profile")}"
            + "&prompt=select_account"
            + $"&state={Uri.EscapeDataString(state)}";
    }

    public async Task<GoogleUsuarioInfo> TrocarCodigoPorUsuarioAsync(
        string code,
        string redirectUri,
        CancellationToken ct = default)
    {
        if (!EstaConfigurado)
        {
            throw new RegraDeNegocioException("Login com Google não está configurado no servidor.");
        }

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        });

        using var tokenResponse = await _http.PostAsync("https://oauth2.googleapis.com/token", form, ct);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            var erro = await tokenResponse.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Falha ao trocar código OAuth do Google. HTTP {StatusCode}: {Erro}",
                (int)tokenResponse.StatusCode, erro);
            throw new RegraDeNegocioException("Não foi possível concluir a autenticação com o Google.");
        }

        var tokenPayload = await tokenResponse.Content.ReadFromJsonAsync<GoogleTokenPayload>(cancellationToken: ct);
        if (string.IsNullOrWhiteSpace(tokenPayload?.AccessToken))
        {
            throw new RegraDeNegocioException("Resposta de autenticação do Google sem access token.");
        }

        using var userInfoReq = new HttpRequestMessage(HttpMethod.Get, "https://openidconnect.googleapis.com/v1/userinfo");
        userInfoReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenPayload.AccessToken);

        using var userInfoResp = await _http.SendAsync(userInfoReq, ct);
        if (!userInfoResp.IsSuccessStatusCode)
        {
            _logger.LogWarning("Falha ao obter userinfo do Google. HTTP {StatusCode}", (int)userInfoResp.StatusCode);
            throw new RegraDeNegocioException("Não foi possível obter os dados do perfil Google.");
        }

        var userInfo = await userInfoResp.Content.ReadFromJsonAsync<GoogleUserInfoPayload>(cancellationToken: ct);
        if (userInfo is null || string.IsNullOrWhiteSpace(userInfo.Sub) || string.IsNullOrWhiteSpace(userInfo.Email))
        {
            throw new RegraDeNegocioException("Dados de identificação do Google incompletos.");
        }

        if (!userInfo.EmailVerified)
        {
            throw new RegraDeNegocioException("O e-mail da conta Google precisa estar verificado.");
        }

        var nome = string.IsNullOrWhiteSpace(userInfo.Name)
            ? userInfo.Email.Split('@')[0]
            : userInfo.Name.Trim();

        return new GoogleUsuarioInfo(userInfo.Sub.Trim(), userInfo.Email.Trim().ToLowerInvariant(), nome, userInfo.EmailVerified);
    }

    private sealed class GoogleTokenPayload
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
    }

    private sealed class GoogleUserInfoPayload
    {
        [JsonPropertyName("sub")]
        public string? Sub { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("email_verified")]
        public bool EmailVerified { get; set; }
    }
}
