using FluentAssertions;
using InvestProMax.Application.Auth;
using InvestProMax.Application.Common.Exceptions;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Domain.Entidades;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace InvestProMax.Application.Tests.Auth;

public class AuthServiceTests
{
    private readonly Mock<IUsuarioRepository> _usuarioRepository = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepository = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        var options = Options.Create(new AuthOptions { RefreshTokenDuracaoDias = 7 });

        _sut = new AuthService(
            _usuarioRepository.Object,
            _refreshTokenRepository.Object,
            _passwordHasher.Object,
            _tokenService.Object,
            options);
    }

    private static Usuario UsuarioValido() => new()
    {
        NomeUsuario = "joao",
        SenhaHash = "hash-armazenado",
        Ativo = true
    };

    [Fact]
    public async Task Login_com_credenciais_corretas_retorna_par_de_tokens()
    {
        var usuario = UsuarioValido();
        _usuarioRepository.Setup(r => r.ObterPorNomeUsuarioAsync("joao", default)).ReturnsAsync(usuario);
        _passwordHasher.Setup(p => p.Verificar("senha-correta", usuario.SenhaHash)).Returns(true);
        _tokenService.Setup(t => t.GerarAccessToken(usuario))
            .Returns(new TokenGerado("access-token-falso", DateTime.UtcNow.AddMinutes(20)));

        var resposta = await _sut.LoginAsync(new LoginRequest("joao", "senha-correta"));

        resposta.AccessToken.Should().Be("access-token-falso");
        resposta.RefreshToken.Should().NotBeNullOrWhiteSpace();
        _refreshTokenRepository.Verify(r => r.AdicionarAsync(It.IsAny<RefreshToken>(), default), Times.Once);
    }

    [Fact]
    public async Task Login_com_senha_errada_lanca_NaoAutorizado()
    {
        var usuario = UsuarioValido();
        _usuarioRepository.Setup(r => r.ObterPorNomeUsuarioAsync("joao", default)).ReturnsAsync(usuario);
        _passwordHasher.Setup(p => p.Verificar("senha-errada", usuario.SenhaHash)).Returns(false);

        var acao = () => _sut.LoginAsync(new LoginRequest("joao", "senha-errada"));

        await acao.Should().ThrowAsync<NaoAutorizadoException>();
    }

    [Fact]
    public async Task Login_com_usuario_inexistente_lanca_NaoAutorizado_sem_revelar_o_motivo()
    {
        _usuarioRepository.Setup(r => r.ObterPorNomeUsuarioAsync("fantasma", default)).ReturnsAsync((Usuario?)null);

        var acao = () => _sut.LoginAsync(new LoginRequest("fantasma", "qualquer"));

        await acao.Should().ThrowAsync<NaoAutorizadoException>();
    }

    [Fact]
    public async Task Login_com_usuario_inativo_lanca_NaoAutorizado()
    {
        var usuario = UsuarioValido();
        usuario.Ativo = false;
        _usuarioRepository.Setup(r => r.ObterPorNomeUsuarioAsync("joao", default)).ReturnsAsync(usuario);

        var acao = () => _sut.LoginAsync(new LoginRequest("joao", "qualquer"));

        await acao.Should().ThrowAsync<NaoAutorizadoException>();
    }

    [Fact]
    public async Task AlterarSenha_com_senha_atual_correta_atualiza_o_hash()
    {
        var usuario = UsuarioValido();
        _usuarioRepository.Setup(r => r.ObterPorIdAsync(usuario.Id, default)).ReturnsAsync(usuario);
        _passwordHasher.Setup(p => p.Verificar("senha-atual", usuario.SenhaHash)).Returns(true);
        _passwordHasher.Setup(p => p.Hash("senha-nova")).Returns("novo-hash");

        await _sut.AlterarSenhaAsync(usuario.Id, new AlterarSenhaRequest("senha-atual", "senha-nova"));

        usuario.SenhaHash.Should().Be("novo-hash");
        _usuarioRepository.Verify(r => r.AtualizarAsync(usuario, default), Times.Once);
    }

    [Fact]
    public async Task AlterarSenha_com_senha_atual_errada_lanca_NaoAutorizado_e_nao_atualiza_nada()
    {
        var usuario = UsuarioValido();
        _usuarioRepository.Setup(r => r.ObterPorIdAsync(usuario.Id, default)).ReturnsAsync(usuario);
        _passwordHasher.Setup(p => p.Verificar("senha-errada", usuario.SenhaHash)).Returns(false);

        var acao = () => _sut.AlterarSenhaAsync(usuario.Id, new AlterarSenhaRequest("senha-errada", "senha-nova"));

        await acao.Should().ThrowAsync<NaoAutorizadoException>();
        _usuarioRepository.Verify(r => r.AtualizarAsync(It.IsAny<Usuario>(), default), Times.Never);
    }

    [Fact]
    public async Task Refresh_com_token_ja_revogado_detecta_reuso_e_revoga_todos_os_tokens_do_usuario()
    {
        var usuarioId = Guid.NewGuid();
        var tokenRevogado = new RefreshToken
        {
            UsuarioId = usuarioId,
            TokenHash = "hash-qualquer",
            CriadoEmUtc = DateTime.UtcNow.AddDays(-2),
            ExpiraEmUtc = DateTime.UtcNow.AddDays(5),
            RevogadoEmUtc = DateTime.UtcNow.AddDays(-1)
        };
        _refreshTokenRepository
            .Setup(r => r.ObterPorHashAsync(It.IsAny<string>(), default))
            .ReturnsAsync(tokenRevogado);

        var acao = () => _sut.RefreshAsync(new RefreshRequest("token-plano-qualquer"));

        await acao.Should().ThrowAsync<NaoAutorizadoException>();
        _refreshTokenRepository.Verify(r => r.RevogarTodosDoUsuarioAsync(usuarioId, default), Times.Once);
    }

    [Fact]
    public async Task Refresh_com_token_expirado_mas_nunca_revogado_lanca_NaoAutorizado_sem_revogar_em_cascata()
    {
        var tokenExpirado = new RefreshToken
        {
            UsuarioId = Guid.NewGuid(),
            TokenHash = "hash-qualquer",
            CriadoEmUtc = DateTime.UtcNow.AddDays(-10),
            ExpiraEmUtc = DateTime.UtcNow.AddDays(-1),
            RevogadoEmUtc = null
        };
        _refreshTokenRepository
            .Setup(r => r.ObterPorHashAsync(It.IsAny<string>(), default))
            .ReturnsAsync(tokenExpirado);

        var acao = () => _sut.RefreshAsync(new RefreshRequest("token-plano-qualquer"));

        await acao.Should().ThrowAsync<NaoAutorizadoException>();
        _refreshTokenRepository.Verify(
            r => r.RevogarTodosDoUsuarioAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task Refresh_com_token_valido_rotaciona_e_revoga_apenas_o_token_usado()
    {
        var usuario = UsuarioValido();
        var tokenValido = new RefreshToken
        {
            UsuarioId = usuario.Id,
            TokenHash = "hash-qualquer",
            CriadoEmUtc = DateTime.UtcNow.AddDays(-1),
            ExpiraEmUtc = DateTime.UtcNow.AddDays(6),
            RevogadoEmUtc = null
        };
        _refreshTokenRepository
            .Setup(r => r.ObterPorHashAsync(It.IsAny<string>(), default))
            .ReturnsAsync(tokenValido);
        _usuarioRepository.Setup(r => r.ObterPorIdAsync(usuario.Id, default)).ReturnsAsync(usuario);
        _tokenService.Setup(t => t.GerarAccessToken(usuario))
            .Returns(new TokenGerado("novo-access-token", DateTime.UtcNow.AddMinutes(20)));

        var resposta = await _sut.RefreshAsync(new RefreshRequest("token-plano-qualquer"));

        resposta.AccessToken.Should().Be("novo-access-token");
        tokenValido.RevogadoEmUtc.Should().NotBeNull();
        tokenValido.SubstituidoPorId.Should().NotBeNull();
        _refreshTokenRepository.Verify(
            r => r.RevogarTodosDoUsuarioAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task CriarUsuario_com_dados_validos_salva_usuario_com_hash_e_retorna_response()
    {
        var request = new CriarUsuarioRequest("Novo Usuario", "novo@teste.com", "novousuario", "SenhaForte123!");
        _usuarioRepository.Setup(r => r.ExisteNomeUsuarioAsync(request.NomeUsuario, default)).ReturnsAsync(false);
        _usuarioRepository.Setup(r => r.ExisteEmailAsync(request.Email, default)).ReturnsAsync(false);
        _passwordHasher.Setup(p => p.Hash(request.Senha)).Returns("hash-seguro");

        var resposta = await _sut.CriarUsuarioAsync(request);

        resposta.Should().NotBeNull();
        resposta.Nome.Should().Be("Novo Usuario");
        resposta.Email.Should().Be("novo@teste.com");
        resposta.NomeUsuario.Should().Be("novousuario");
        resposta.Ativo.Should().BeTrue();

        _usuarioRepository.Verify(r => r.AdicionarAsync(
            It.Is<Usuario>(u => u.Nome == "Novo Usuario" && u.Email == "novo@teste.com" && u.NomeUsuario == "novousuario" && u.SenhaHash == "hash-seguro" && u.Ativo),
            default), Times.Once);
    }

    [Fact]
    public async Task CriarUsuario_com_nome_usuario_duplicado_lanca_RegraDeNegocioException()
    {
        var request = new CriarUsuarioRequest("Novo Usuario", "novo@teste.com", "usuarioexistente", "SenhaForte123!");
        _usuarioRepository.Setup(r => r.ExisteNomeUsuarioAsync(request.NomeUsuario, default)).ReturnsAsync(true);

        var acao = () => _sut.CriarUsuarioAsync(request);

        await acao.Should().ThrowAsync<RegraDeNegocioException>().WithMessage("*Nome de usuário*");
        _usuarioRepository.Verify(r => r.AdicionarAsync(It.IsAny<Usuario>(), default), Times.Never);
    }

    [Fact]
    public async Task CriarUsuario_com_email_duplicado_lanca_RegraDeNegocioException()
    {
        var request = new CriarUsuarioRequest("Novo Usuario", "existente@teste.com", "novousuario", "SenhaForte123!");
        _usuarioRepository.Setup(r => r.ExisteNomeUsuarioAsync(request.NomeUsuario, default)).ReturnsAsync(false);
        _usuarioRepository.Setup(r => r.ExisteEmailAsync(request.Email, default)).ReturnsAsync(true);

        var acao = () => _sut.CriarUsuarioAsync(request);

        await acao.Should().ThrowAsync<RegraDeNegocioException>().WithMessage("*E-mail*");
        _usuarioRepository.Verify(r => r.AdicionarAsync(It.IsAny<Usuario>(), default), Times.Never);
    }
}
