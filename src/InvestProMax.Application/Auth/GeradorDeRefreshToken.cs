using System.Security.Cryptography;

namespace InvestProMax.Application.Auth;

/// <summary>
/// Gera o valor bruto do refresh token (enviado ao cliente uma única vez) e calcula o hash
/// que é efetivamente persistido — o mesmo princípio de nunca guardar o segredo em texto puro
/// aplicado à senha (seção 7) vale aqui.
/// </summary>
internal static class GeradorDeRefreshToken
{
    public static string GerarTokenPlano()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    public static string CalcularHash(string tokenPlano)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(tokenPlano);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
