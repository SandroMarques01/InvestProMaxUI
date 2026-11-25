using FluentAssertions;
using InvestProMax.Application.Auth;
using InvestProMax.Application.Common.Exceptions;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;
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

    [Fact]
    public async Task Login_remove_espacos_nas_extremidades_do_nome_de_usuario()
    {
        var usuario = UsuarioValido();
        _usuarioRepository.Setup(r => r.ObterPorNomeUsuarioAsync("joao", default)).ReturnsAsync(usuario);
        _passwordHasher.Setup(p => p.Verificar("senha-correta", usuario.SenhaHash)).Returns(true);
        _tokenService.Setup(t => t.GerarAccessToken(usuario))
            .Returns(new TokenGerado("access-token-falso", DateTime.UtcNow.AddMinutes(20)));

        var resposta = await _sut.LoginAsync(new LoginRequest("  joao  ", "senha-correta"));

        resposta.AccessToken.Should().Be("access-token-falso");
        _usuarioRepository.Verify(r => r.ObterPorNomeUsuarioAsync("joao", default), Times.Once);
    }

    [Fact]
    public async Task Registrar_cria_usuario_pendente_telegram_e_bloqueia_login_ate_aprovacao_admin()
    {
        Usuario? usuarioSalvo = null;
        _usuarioRepository.Setup(r => r.ExisteNomeUsuarioAsync("maria", default)).ReturnsAsync(false);
        _usuarioRepository.Setup(r => r.ExisteEmailAsync("maria@teste.com", default)).ReturnsAsync(false);
        _passwordHasher.Setup(p => p.Hash("Senha@123")).Returns("hash-maria");
        _usuarioRepository.Setup(r => r.AdicionarAsync(It.IsAny<Usuario>(), default))
            .Callback<Usuario, CancellationToken>((u, _) => usuarioSalvo = u)
            .Returns(Task.CompletedTask);

        var notificador = new Mock<INotificadorTelegramService>();
        notificador.Setup(n => n.MontarLinkValidacao(It.IsAny<string>()))
            .Returns<string>(c => $"https://t.me/MarkX_InvestProMax_MarquesFC_bot?start={c}");
        notificador.Setup(n => n.ObterAdminsConfigurados()).Returns([999999L]);

        var sut = new AuthService(
            _usuarioRepository.Object, _refreshTokenRepository.Object,
            _passwordHasher.Object, _tokenService.Object,
            Options.Create(new AuthOptions { RefreshTokenDuracaoDias = 7 }),
            notificador.Object);

        var registro = await sut.RegistrarAsync(new RegistrarUsuarioRequest("Maria", "maria@teste.com", "maria", "Senha@123"));

        registro.StatusCadastro.Should().Be(StatusCadastroUsuario.PendenteTelegram);
        registro.CodigoValidacaoTelegram.Should().StartWith("ipm_");
        registro.LinkTelegramBot.Should().Contain(registro.CodigoValidacaoTelegram);
        usuarioSalvo.Should().NotBeNull();
        usuarioSalvo!.Ativo.Should().BeFalse();

        // Tentativa de login antes da validação no Telegram -> CadastroPendenteException(PendenteTelegram)
        _usuarioRepository.Setup(r => r.ObterPorNomeUsuarioAsync("maria", default)).ReturnsAsync(usuarioSalvo);
        _passwordHasher.Setup(p => p.Verificar("Senha@123", "hash-maria")).Returns(true);

        var acaoAntesTelegram = () => sut.LoginAsync(new LoginRequest("maria", "Senha@123"));
        var ex1 = await acaoAntesTelegram.Should().ThrowAsync<CadastroPendenteException>();
        ex1.Which.StatusCadastro.Should().Be(StatusCadastroUsuario.PendenteTelegram);

        // Validação no Telegram -> muda para PendenteAprovacaoAdmin e salva TelegramId
        _usuarioRepository.Setup(r => r.ObterPorCodigoTelegramAsync(registro.CodigoValidacaoTelegram!, default))
            .ReturnsAsync(usuarioSalvo);
        _usuarioRepository.Setup(r => r.ListarTelegramIdsAdminsAsync(default)).ReturnsAsync([]);

        var respTelegram = await sut.ValidarTelegramAsync(new ValidarTelegramRequest(registro.CodigoValidacaoTelegram!, 555123456L));
        respTelegram.Sucesso.Should().BeTrue();
        respTelegram.TelegramId.Should().Be(555123456L);
        respTelegram.AdminTelegramIds.Should().Contain(999999L);
        usuarioSalvo.StatusCadastro.Should().Be(StatusCadastroUsuario.PendenteAprovacaoAdmin);

        // Admin aprova -> libera usuário e notifica no Telegram
        _usuarioRepository.Setup(r => r.ObterPorIdAsync(usuarioSalvo.Id, default)).ReturnsAsync(usuarioSalvo);
        var aprovado = await sut.AprovarUsuarioAsync(usuarioSalvo.Id);

        aprovado.Ativo.Should().BeTrue();
        aprovado.StatusCadastro.Should().Be(StatusCadastroUsuario.Aprovado);
        notificador.Verify(n => n.NotificarUsuarioAsync(555123456L, It.Is<string>(m => m.Contains("aprovado")), default), Times.Once);

        // Login liberado após aprovação
        _tokenService.Setup(t => t.GerarAccessToken(usuarioSalvo))
            .Returns(new TokenGerado("jwt-ok", DateTime.UtcNow.AddMinutes(20)));
        var loginOk = await sut.LoginAsync(new LoginRequest("maria", "Senha@123"));
        loginOk.AccessToken.Should().Be("jwt-ok");
    }

    [Fact]
    public async Task AutenticarComGoogle_cria_usuario_pendente_quando_novo_e_libera_quando_aprovado()
    {
        Usuario? usuarioSalvo = null;
        _usuarioRepository.Setup(r => r.ObterPorGoogleSubjectAsync("sub-123", default)).ReturnsAsync((Usuario?)null);
        _usuarioRepository.Setup(r => r.ObterPorEmailAsync("joao.silva@gmail.com", default)).ReturnsAsync((Usuario?)null);
        _usuarioRepository.Setup(r => r.ExisteNomeUsuarioAsync("joao.silva", default)).ReturnsAsync(false);
        _passwordHasher.Setup(p => p.Hash(It.IsAny<string>())).Returns("hash-random");
        _usuarioRepository.Setup(r => r.AdicionarAsync(It.IsAny<Usuario>(), default))
            .Callback<Usuario, CancellationToken>((u, _) => usuarioSalvo = u)
            .Returns(Task.CompletedTask);

        var respNovo = await _sut.AutenticarComGoogleAsync(new GoogleUsuarioInfo("sub-123", "joao.silva@gmail.com", "João Silva", true));

        respNovo.StatusCadastro.Should().Be(StatusCadastroUsuario.PendenteTelegram);
        respNovo.Autenticacao.Should().BeNull();
        respNovo.CodigoValidacaoTelegram.Should().StartWith("ipm_");
        usuarioSalvo!.GoogleSubject.Should().Be("sub-123");
        usuarioSalvo.Ativo.Should().BeFalse();

        // Aprovado posteriormente -> login Google emite JWT
        usuarioSalvo.StatusCadastro = StatusCadastroUsuario.Aprovado;
        usuarioSalvo.Ativo = true;
        _usuarioRepository.Setup(r => r.ObterPorGoogleSubjectAsync("sub-123", default)).ReturnsAsync(usuarioSalvo);
        _tokenService.Setup(t => t.GerarAccessToken(usuarioSalvo))
            .Returns(new TokenGerado("jwt-google", DateTime.UtcNow.AddMinutes(20)));

        var respAprovado = await _sut.AutenticarComGoogleAsync(new GoogleUsuarioInfo("sub-123", "joao.silva@gmail.com", "João Silva", true));
        respAprovado.StatusCadastro.Should().Be(StatusCadastroUsuario.Aprovado);
        respAprovado.Autenticacao!.AccessToken.Should().Be("jwt-google");
    }
}
