namespace InvestProMax.SharedUI.Auth;

/// <summary>
/// A SharedUI não decide onde o refresh token fica persistido entre execuções — cada head
/// implementa isso do jeito apropriado pra sua plataforma (seção 9: "Web: HttpOnly cookie ou
/// localStorage... MAUI: SecureStorage"). Web NÃO deve usar localStorage para o refresh token
/// (fica exposto a XSS) — ASSUNÇÃO SINALIZADA: a especificação lista os dois como alternativas
/// sem decidir, mas dado que a seção 7 já pede JWT curto + refresh longo justamente para
/// mitigar roubo de token, o head Web (Fase 7) deveria preferir cookie HttpOnly+Secure
/// gerenciado pelo próprio host ASP.NET Core em vez de localStorage. Esta interface não impõe
/// isso — é uma decisão de implementação do head, não da SharedUI.
/// </summary>
public interface ITokenStorage
{
    Task<string?> ObterRefreshTokenAsync();
    Task SalvarRefreshTokenAsync(string refreshToken);
    Task LimparAsync();
}
