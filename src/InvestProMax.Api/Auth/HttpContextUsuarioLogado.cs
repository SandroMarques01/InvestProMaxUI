using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using InvestProMax.Application.Common.Interfaces;

namespace InvestProMax.Api.Auth;

public class HttpContextUsuarioLogado : IUsuarioLogado
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextUsuarioLogado(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    public bool EstaAutenticado => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public Guid UsuarioId
    {
        get
        {
            var valor = _httpContextAccessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (valor is null || !Guid.TryParse(valor, out var id))
            {
                throw new InvalidOperationException(
                    "UsuarioId não encontrado no token — este código só deve rodar atrás de [Authorize].");
            }

            return id;
        }
    }
}
