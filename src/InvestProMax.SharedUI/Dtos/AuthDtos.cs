namespace InvestProMax.SharedUI.Dtos;

public sealed record LoginRequest(string NomeUsuario, string Senha);

public sealed record RefreshRequest(string RefreshToken);

public sealed record AlterarSenhaRequest(string SenhaAtual, string NovaSenha);

public sealed record CriarUsuarioRequest(string Nome, string Email, string NomeUsuario, string Senha);

public sealed record UsuarioResponse(Guid Id, string Nome, string Email, string NomeUsuario, bool Ativo);

public sealed record AutenticacaoResponse(
    string AccessToken,
    DateTime AccessTokenExpiraEmUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiraEmUtc);
