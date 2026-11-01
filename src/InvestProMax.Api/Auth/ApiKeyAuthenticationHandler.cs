using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace InvestProMax.Api.Auth;

public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";
    public const string HeaderName = "X-Api-Key";
}

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IntegracaoCotacoesOptions _cotacoesOptions;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<IntegracaoCotacoesOptions> cotacoesOptions)
        : base(options, logger, encoder)
    {
        _cotacoesOptions = cotacoesOptions.Value;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationOptions.HeaderName, out var headerValues) ||
            string.IsNullOrWhiteSpace(headerValues))
        {
            return Task.FromResult(AuthenticateResult.Fail("Chave de API não informada."));
        }

        var chaveInformada = headerValues.ToString().Trim();
        var chaveConfigurada = _cotacoesOptions.ApiKey;

        if (string.IsNullOrWhiteSpace(chaveConfigurada))
        {
            return Task.FromResult(AuthenticateResult.Fail("Chave de API não configurada no servidor."));
        }

        var chaveInformadaBytes = Encoding.UTF8.GetBytes(chaveInformada);
        var chaveConfiguradaBytes = Encoding.UTF8.GetBytes(chaveConfigurada);

        if (chaveInformadaBytes.Length != chaveConfiguradaBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(chaveInformadaBytes, chaveConfiguradaBytes))
        {
            return Task.FromResult(AuthenticateResult.Fail("Chave de API inválida."));
        }

        if (!Guid.TryParse(_cotacoesOptions.UsuarioId, out var usuarioId))
        {
            return Task.FromResult(AuthenticateResult.Fail("UsuarioId da integração não configurado ou inválido."));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()),
            new Claim(ClaimTypes.AuthenticationMethod, ApiKeyAuthenticationOptions.DefaultScheme)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = (int)HttpStatusCode.Unauthorized;
        Response.ContentType = "application/problem+json";

        var problemDetails = new ProblemDetails
        {
            Status = (int)HttpStatusCode.Unauthorized,
            Title = "Chave de API ausente ou inválida.",
            Instance = Request.Path
        };

        await Response.WriteAsJsonAsync(problemDetails, options: null, contentType: "application/problem+json");
    }

    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = (int)HttpStatusCode.Forbidden;
        Response.ContentType = "application/problem+json";

        var problemDetails = new ProblemDetails
        {
            Status = (int)HttpStatusCode.Forbidden,
            Title = "Você não tem permissão para acessar este recurso.",
            Instance = Request.Path
        };

        await Response.WriteAsJsonAsync(problemDetails, options: null, contentType: "application/problem+json");
    }
}
