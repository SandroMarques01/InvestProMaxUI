using InvestProMax.Domain.Enums;

namespace InvestProMax.Application.Auth;

public sealed record LoginRequest(string NomeUsuario, string Senha);

public sealed record RefreshRequest(string RefreshToken);

public sealed record AlterarSenhaRequest(string SenhaAtual, string NovaSenha);

public sealed record CriarUsuarioRequest(string Nome, string Email, string NomeUsuario, string Senha);

public sealed record RegistrarUsuarioRequest(string Nome, string Email, string NomeUsuario, string Senha);

public sealed record UsuarioResponse(
    Guid Id,
    string Nome,
    string Email,
    string NomeUsuario,
    bool Ativo,
    StatusCadastroUsuario StatusCadastro = StatusCadastroUsuario.Aprovado,
    long? TelegramId = null,
    DateTime? CriadoEmUtc = null,
    DateTime? AprovadoEmUtc = null,
    bool PossuiGoogle = false);

public sealed record AuthConfigPublicaResponse(
    bool GoogleHabilitado,
    string TelegramBotUsername);

public sealed record TrocarCodigoGoogleRequest(string Codigo);

public sealed record StatusCadastroResponse(
    StatusCadastroUsuario StatusCadastro,
    string Email,
    string NomeUsuario,
    string Mensagem,
    string? CodigoValidacaoTelegram = null,
    string? LinkTelegramBot = null,
    AutenticacaoResponse? Autenticacao = null);

public sealed record ValidarTelegramRequest(
    string Codigo,
    long TelegramId,
    string? TelegramUsername = null,
    string? TelegramNome = null);

public sealed record ValidarTelegramResponse(
    bool Sucesso,
    string MensagemUsuario,
    Guid? UsuarioId,
    string? Nome,
    string? Email,
    string? NomeUsuario,
    long? TelegramId,
    IReadOnlyList<long> AdminTelegramIds);

public sealed record DecidirCadastroTelegramRequest(
    Guid UsuarioId,
    long AdminTelegramId,
    bool Aprovar);

public sealed record DecidirCadastroTelegramResponse(
    bool Sucesso,
    string MensagemAdmin,
    long? UsuarioTelegramId,
    string? MensagemUsuario);

/// <summary>
/// Resposta comum a /auth/login e /auth/refresh — sufixo Response conforme convenção (seção 12).
/// </summary>
public sealed record AutenticacaoResponse(
    string AccessToken,
    DateTime AccessTokenExpiraEmUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiraEmUtc);
