using FluentAssertions;
using InvestProMax.Application.Common.Exceptions;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Application.Configuracao;
using InvestProMax.Application.Dividendos;
using InvestProMax.Application.Papeis;
using InvestProMax.Application.Segmentos;
using InvestProMax.Application.Transacoes;
using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;
using Moq;
using Xunit;

namespace InvestProMax.Application.Tests.Configuracao;

public class BackupServiceTests
{
    private readonly Mock<IBackupExcelWriter> _writer = new();
    private readonly Mock<IBackupExcelReader> _reader = new();
    private readonly Mock<IPapelRepository> _papelRepository = new();
    private readonly Mock<ITransacaoRepository> _transacaoRepository = new();
    private readonly Mock<IDividendoRepository> _dividendoRepository = new();
    private readonly Mock<ISegmentoRepository> _segmentoRepository = new();
    private readonly Mock<IMovimentoAluguelRepository> _movimentoAluguelRepository = new();
    private readonly Mock<IUsuarioLogado> _usuarioLogado = new();
    private readonly Guid _usuarioId = Guid.NewGuid();
    private readonly BackupService _sut;

    public BackupServiceTests()
    {
        _usuarioLogado.Setup(u => u.UsuarioId).Returns(_usuarioId);
        _sut = new BackupService(
            _writer.Object, _reader.Object, _papelRepository.Object, _transacaoRepository.Object,
            _dividendoRepository.Object, _movimentoAluguelRepository.Object, _segmentoRepository.Object, _usuarioLogado.Object);
    }

    [Fact]
    public async Task Restaurar_papel_com_id_inexistente_cria_um_novo_com_o_mesmo_id()
    {
        var idDoBackup = Guid.NewGuid();
        var linha = new LinhaBackupPapel(idDoBackup, "PETR4", "Petrobras", TipoPapel.Acao, 30m, null, true, []);

        _reader.Setup(r => r.Ler(It.IsAny<Stream>()))
            .Returns(new BackupDados([linha], [], []));
        _papelRepository.Setup(r => r.ObterPorIdAsync(idDoBackup, _usuarioId, default)).ReturnsAsync((Papel?)null);

        var resposta = await _sut.RestaurarAsync(new MemoryStream());

        resposta.PapeisCriados.Should().Be(1);
        resposta.PapeisAtualizados.Should().Be(0);
        _papelRepository.Verify(r => r.AdicionarAsync(It.Is<Papel>(p => p.Id == idDoBackup && p.Codigo == "PETR4"), default), Times.Once);
    }

    [Fact]
    public async Task Restaurar_papel_com_id_ja_existente_atualiza_em_vez_de_duplicar()
    {
        var papelExistente = new Papel { UsuarioId = _usuarioId, Codigo = "PETR4", Nome = "Nome antigo", CotacaoAtual = 10m };
        var linha = new LinhaBackupPapel(papelExistente.Id, "PETR4", "Nome novo", TipoPapel.Acao, 99m, null, true, []);

        _reader.Setup(r => r.Ler(It.IsAny<Stream>()))
            .Returns(new BackupDados([linha], [], []));
        _papelRepository.Setup(r => r.ObterPorIdAsync(papelExistente.Id, _usuarioId, default)).ReturnsAsync(papelExistente);

        var resposta = await _sut.RestaurarAsync(new MemoryStream());

        resposta.PapeisCriados.Should().Be(0);
        resposta.PapeisAtualizados.Should().Be(1);
        papelExistente.Nome.Should().Be("Nome novo");
        papelExistente.CotacaoAtual.Should().Be(99m);
        _papelRepository.Verify(r => r.AdicionarAsync(It.IsAny<Papel>(), default), Times.Never);
    }

    [Fact]
    public async Task Transacao_que_referencia_papel_fora_da_aba_Papel_lanca_RegraDeNegocio()
    {
        var linhaTransacao = new LinhaBackupTransacao(
            Guid.NewGuid(), "CODIGO-INEXISTENTE", TipoTransacao.Compra, 10, 5, new DateOnly(2026, 1, 1), null, false);

        _reader.Setup(r => r.Ler(It.IsAny<Stream>()))
            .Returns(new BackupDados([], [linhaTransacao], []));

        var acao = () => _sut.RestaurarAsync(new MemoryStream());

        await acao.Should().ThrowAsync<RegraDeNegocioException>();
    }
}
