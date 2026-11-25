namespace InvestProMax.Application.Auth;

public sealed record GoogleUsuarioInfo(
    string Subject,
    string Email,
    string Nome,
    bool EmailVerificado);

public interface IGoogleAuthProvider
{
    bool EstaConfigurado { get; }
    string GerarUrlAutorizacao(string redirectUri, string state);
    Task<GoogleUsuarioInfo> TrocarCodigoPorUsuarioAsync(string code, string redirectUri, CancellationToken ct = default);
}
