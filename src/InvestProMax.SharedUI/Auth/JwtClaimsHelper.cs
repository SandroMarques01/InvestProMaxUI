using System.Text;
using System.Text.Json;

namespace InvestProMax.SharedUI.Auth;

/// <summary>
/// Decodifica só o payload de um JWT (sem validar assinatura — a Api já validou; isto é só
/// para exibir "nome_usuario" na UI). Evita puxar System.IdentityModel.Tokens.Jwt inteiro só
/// pra esse uso cosmético.
/// </summary>
internal static class JwtClaimsHelper
{
    public static IReadOnlyDictionary<string, string> LerClaims(string accessToken)
    {
        var partes = accessToken.Split('.');
        if (partes.Length != 3)
        {
            return new Dictionary<string, string>();
        }

        try
        {
            var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(partes[1]));
            using var documento = JsonDocument.Parse(payloadJson);

            var claims = new Dictionary<string, string>();
            foreach (var propriedade in documento.RootElement.EnumerateObject())
            {
                claims[propriedade.Name] = propriedade.Value.ToString();
            }
            return claims;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var texto = input.Replace('-', '+').Replace('_', '/');
        var resto = texto.Length % 4;
        if (resto > 0)
        {
            texto += new string('=', 4 - resto);
        }
        return Convert.FromBase64String(texto);
    }
}
