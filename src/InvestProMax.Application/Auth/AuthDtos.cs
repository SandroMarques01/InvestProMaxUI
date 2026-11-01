namespace InvestProMax.Application.Auth;

public sealed record LoginRequest(string NomeUsuario, string Senha);

public sealed record RefreshRequest(string RefreshToken);

public sealed record AlterarSenhaRequest(string SenhaAtual, string NovaSenha);

public sealed record CriarUsuarioRequest(string Nome, string Email, string NomeUsuario, string Senha);

public sealed record UsuarioResponse(Guid Id, string Nome, string Email, string NomeUsuario, bool Ativo);

/// <summary>
/// Resposta comum a /auth/login e /auth/refresh — sufixo Response conforme convenção (seção 12).
/// </summary>
public sealed record AutenticacaoResponse(
    string AccessToken,
    DateTime AccessTokenExpiraEmUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiraEmUtc);
