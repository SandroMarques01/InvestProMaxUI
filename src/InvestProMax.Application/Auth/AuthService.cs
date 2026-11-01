using InvestProMax.Application.Common.Exceptions;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Domain.Entidades;
using Microsoft.Extensions.Options;

namespace InvestProMax.Application.Auth;

public sealed class AuthService : IAuthService
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly AuthOptions _options;

    public AuthService(
        IUsuarioRepository usuarioRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IOptions<AuthOptions> options)
    {
        _usuarioRepository = usuarioRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _options = options.Value;
    }

    public async Task<AutenticacaoResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var usuario = await _usuarioRepository.ObterPorNomeUsuarioAsync(request.NomeUsuario, ct);

        if (usuario is null || !usuario.Ativo || !_passwordHasher.Verificar(request.Senha, usuario.SenhaHash))
        {
            // Mensagem deliberadamente genérica: não revelar se o problema foi o usuário ou a senha.
            throw new NaoAutorizadoException("Usuário ou senha inválidos.");
        }

        return await EmitirParDeTokensAsync(usuario, ct);
    }

    public async Task<AutenticacaoResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var hash = GeradorDeRefreshToken.CalcularHash(request.RefreshToken);
        var tokenExistente = await _refreshTokenRepository.ObterPorHashAsync(hash, ct);

        if (tokenExistente is null)
        {
            throw new NaoAutorizadoException("Refresh token inválido ou expirado.");
        }

        // Detecção de reuso: um token já revogado sendo reapresentado é sinal de possível
        // comprometimento (ex.: token roubado sendo usado depois que o dono já rotacionou).
        // Revoga todos os tokens ativos do usuário para forçar novo login em todos os
        // dispositivos, em vez de só recusar esta tentativa isolada.
        if (tokenExistente.RevogadoEmUtc is not null)
        {
            await _refreshTokenRepository.RevogarTodosDoUsuarioAsync(tokenExistente.UsuarioId, ct);
            throw new NaoAutorizadoException("Refresh token inválido ou expirado.");
        }

        if (!tokenExistente.EstaAtivo)
        {
            throw new NaoAutorizadoException("Refresh token inválido ou expirado.");
        }

        var usuario = await _usuarioRepository.ObterPorIdAsync(tokenExistente.UsuarioId, ct);
        if (usuario is null || !usuario.Ativo)
        {
            throw new NaoAutorizadoException("Usuário inválido.");
        }

        var resposta = await EmitirParDeTokensAsync(usuario, ct, tokenParaRevogar: tokenExistente);
        return resposta;
    }

    public async Task AlterarSenhaAsync(Guid usuarioId, AlterarSenhaRequest request, CancellationToken ct = default)
    {
        var usuario = await _usuarioRepository.ObterPorIdAsync(usuarioId, ct);
        if (usuario is null)
        {
            throw new NaoAutorizadoException();
        }

        if (!_passwordHasher.Verificar(request.SenhaAtual, usuario.SenhaHash))
        {
            throw new NaoAutorizadoException("Senha atual incorreta.");
        }

        usuario.SenhaHash = _passwordHasher.Hash(request.NovaSenha);
        await _usuarioRepository.AtualizarAsync(usuario, ct);
    }

    public async Task<UsuarioResponse> CriarUsuarioAsync(CriarUsuarioRequest request, CancellationToken ct = default)
    {
        if (await _usuarioRepository.ExisteNomeUsuarioAsync(request.NomeUsuario, ct))
        {
            throw new RegraDeNegocioException("Nome de usuário já está em uso.");
        }

        if (await _usuarioRepository.ExisteEmailAsync(request.Email, ct))
        {
            throw new RegraDeNegocioException("E-mail já está em uso.");
        }

        var usuario = new Usuario
        {
            Nome = request.Nome.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            NomeUsuario = request.NomeUsuario.Trim(),
            SenhaHash = _passwordHasher.Hash(request.Senha),
            Ativo = true
        };

        await _usuarioRepository.AdicionarAsync(usuario, ct);

        return new UsuarioResponse(usuario.Id, usuario.Nome, usuario.Email, usuario.NomeUsuario, usuario.Ativo);
    }

    private async Task<AutenticacaoResponse> EmitirParDeTokensAsync(
        Usuario usuario, CancellationToken ct, RefreshToken? tokenParaRevogar = null)
    {
        var accessToken = _tokenService.GerarAccessToken(usuario);

        var refreshTokenPlano = GeradorDeRefreshToken.GerarTokenPlano();
        var novoRefreshToken = new RefreshToken
        {
            UsuarioId = usuario.Id,
            TokenHash = GeradorDeRefreshToken.CalcularHash(refreshTokenPlano),
            CriadoEmUtc = DateTime.UtcNow,
            ExpiraEmUtc = DateTime.UtcNow.AddDays(_options.RefreshTokenDuracaoDias)
        };

        await _refreshTokenRepository.AdicionarAsync(novoRefreshToken, ct);

        if (tokenParaRevogar is not null)
        {
            tokenParaRevogar.RevogadoEmUtc = DateTime.UtcNow;
            tokenParaRevogar.SubstituidoPorId = novoRefreshToken.Id;
            await _refreshTokenRepository.AtualizarAsync(tokenParaRevogar, ct);
        }

        return new AutenticacaoResponse(
            accessToken.Token,
            accessToken.ExpiraEmUtc,
            refreshTokenPlano,
            novoRefreshToken.ExpiraEmUtc);
    }
}
