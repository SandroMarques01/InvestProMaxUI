using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Domain.Entidades;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace InvestProMax.Infrastructure.Auth;

public class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options) => _options = options.Value;

    public int AccessTokenExpiracaoMinutos => _options.AccessTokenExpiracaoMinutos;

    public TokenGerado GerarAccessToken(Usuario usuario)
    {
        var expiraEmUtc = DateTime.UtcNow.AddMinutes(_options.AccessTokenExpiracaoMinutos);

        var claims = new[]
        {
            // "sub" é o claim que IUsuarioLogado (implementado na Api) lê para obter o UsuarioId
            // usado no isolamento por usuário (regra 10) — nunca um id vindo do cliente.
            new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, usuario.NomeUsuario),
            new Claim(JwtRegisteredClaimNames.Email, usuario.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.ChaveSecreta));
        var credenciais = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Emissor,
            audience: _options.Audiencia,
            claims: claims,
            expires: expiraEmUtc,
            signingCredentials: credenciais);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return new TokenGerado(tokenString, expiraEmUtc);
    }
}
