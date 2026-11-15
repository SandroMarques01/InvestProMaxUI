using FluentAssertions;
using InvestProMax.Application.Ativos;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Application.Configuracao;
using InvestProMax.Application.Contas;
using InvestProMax.Application.Papeis;
using Moq;
using Xunit;

namespace InvestProMax.Application.Tests.Configuracao;

public class ConfiguracaoServiceTests
{
    private readonly Mock<IPapelRepository> _papelRepository = new(MockBehavior.Strict);
    private readonly Mock<IAtivoRepository> _ativoRepository = new(MockBehavior.Strict);
    private readonly Mock<IContaRepository> _contaRepository = new(MockBehavior.Strict);
    private readonly Mock<IMongoBackupService> _mongoBackupService = new(MockBehavior.Strict);
    private readonly Mock<IUsuarioLogado> _usuarioLogado = new();
    private readonly Guid _usuarioId = Guid.NewGuid();

    public ConfiguracaoServiceTests()
    {
        _usuarioLogado.Setup(u => u.UsuarioId).Returns(_usuarioId);
    }

    [Fact]
    public async Task ZerarCarteiraAsync_deve_zerar_mongo_antes_do_repositorio_apenas_para_o_usuario_logado()
    {
        var ordem = new List<string>();
        _mongoBackupService
            .Setup(m => m.ZerarCarteiraDoUsuarioAsync(_usuarioId, It.IsAny<CancellationToken>()))
            .Callback(() => ordem.Add("Mongo"))
            .Returns(Task.CompletedTask);
        _papelRepository
            .Setup(r => r.ZerarCarteiraDoUsuarioAsync(_usuarioId, It.IsAny<CancellationToken>()))
            .Callback(() => ordem.Add("CarteiraRepo"))
            .Returns(Task.CompletedTask);

        var sut = new ConfiguracaoService(
            _papelRepository.Object,
            _ativoRepository.Object,
            _contaRepository.Object,
            _usuarioLogado.Object,
            _mongoBackupService.Object);

        await sut.ZerarCarteiraAsync();

        ordem.Should().Equal("Mongo", "CarteiraRepo");
        _mongoBackupService.Verify(m => m.ZerarCarteiraDoUsuarioAsync(_usuarioId, It.IsAny<CancellationToken>()), Times.Once);
        _papelRepository.Verify(r => r.ZerarCarteiraDoUsuarioAsync(_usuarioId, It.IsAny<CancellationToken>()), Times.Once);
        _ativoRepository.VerifyNoOtherCalls();
        _contaRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ZerarCarteiraAsync_sem_mongo_configurado_deve_zerar_carteira_no_repositorio()
    {
        _papelRepository
            .Setup(r => r.ZerarCarteiraDoUsuarioAsync(_usuarioId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new ConfiguracaoService(
            _papelRepository.Object,
            _ativoRepository.Object,
            _contaRepository.Object,
            _usuarioLogado.Object);

        await sut.ZerarCarteiraAsync();

        _papelRepository.Verify(r => r.ZerarCarteiraDoUsuarioAsync(_usuarioId, It.IsAny<CancellationToken>()), Times.Once);
        _ativoRepository.VerifyNoOtherCalls();
        _contaRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ZerarBaseAsync_deve_zerar_mongo_antes_dos_repositorios_apenas_para_o_usuario_logado()
    {
        var ordem = new List<string>();
        _mongoBackupService
            .Setup(m => m.ZerarBaseDoUsuarioAsync(_usuarioId, It.IsAny<CancellationToken>()))
            .Callback(() => ordem.Add("Mongo"))
            .Returns(Task.CompletedTask);
        _papelRepository
            .Setup(r => r.ZerarCarteiraDoUsuarioAsync(_usuarioId, It.IsAny<CancellationToken>()))
            .Callback(() => ordem.Add("CarteiraRepo"))
            .Returns(Task.CompletedTask);
        _ativoRepository
            .Setup(r => r.ZerarPatrimonioDoUsuarioAsync(_usuarioId, It.IsAny<CancellationToken>()))
            .Callback(() => ordem.Add("AtivoRepo"))
            .Returns(Task.CompletedTask);
        _contaRepository
            .Setup(r => r.ZerarContasDoUsuarioAsync(_usuarioId, It.IsAny<CancellationToken>()))
            .Callback(() => ordem.Add("ContaRepo"))
            .Returns(Task.CompletedTask);

        var sut = new ConfiguracaoService(
            _papelRepository.Object,
            _ativoRepository.Object,
            _contaRepository.Object,
            _usuarioLogado.Object,
            _mongoBackupService.Object);

        await sut.ZerarBaseAsync();

        ordem.Should().Equal("Mongo", "CarteiraRepo", "AtivoRepo", "ContaRepo");
        _mongoBackupService.Verify(m => m.ZerarBaseDoUsuarioAsync(_usuarioId, It.IsAny<CancellationToken>()), Times.Once);
        _papelRepository.Verify(r => r.ZerarCarteiraDoUsuarioAsync(_usuarioId, It.IsAny<CancellationToken>()), Times.Once);
        _ativoRepository.Verify(r => r.ZerarPatrimonioDoUsuarioAsync(_usuarioId, It.IsAny<CancellationToken>()), Times.Once);
        _contaRepository.Verify(r => r.ZerarContasDoUsuarioAsync(_usuarioId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ZerarBaseAsync_sem_mongo_configurado_deve_zerar_todos_os_repositorios()
    {
        _papelRepository
            .Setup(r => r.ZerarCarteiraDoUsuarioAsync(_usuarioId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _ativoRepository
            .Setup(r => r.ZerarPatrimonioDoUsuarioAsync(_usuarioId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _contaRepository
            .Setup(r => r.ZerarContasDoUsuarioAsync(_usuarioId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new ConfiguracaoService(
            _papelRepository.Object,
            _ativoRepository.Object,
            _contaRepository.Object,
            _usuarioLogado.Object);

        await sut.ZerarBaseAsync();

        _papelRepository.Verify(r => r.ZerarCarteiraDoUsuarioAsync(_usuarioId, It.IsAny<CancellationToken>()), Times.Once);
        _ativoRepository.Verify(r => r.ZerarPatrimonioDoUsuarioAsync(_usuarioId, It.IsAny<CancellationToken>()), Times.Once);
        _contaRepository.Verify(r => r.ZerarContasDoUsuarioAsync(_usuarioId, It.IsAny<CancellationToken>()), Times.Once);
    }
}

