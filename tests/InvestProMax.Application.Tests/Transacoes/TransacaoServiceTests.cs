using FluentAssertions;
using InvestProMax.Application.Common.Exceptions;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Application.Papeis;
using InvestProMax.Application.Transacoes;
using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;
using Moq;
using Xunit;

namespace InvestProMax.Application.Tests.Transacoes;

public class TransacaoServiceTests
{
    private readonly Mock<ITransacaoRepository> _transacaoRepository = new();
    private readonly Mock<IPapelRepository> _papelRepository = new();
    private readonly Mock<IUsuarioLogado> _usuarioLogado = new();
    private readonly Guid _usuarioId = Guid.NewGuid();
    private readonly TransacaoService _sut;

    public TransacaoServiceTests()
    {
        _usuarioLogado.Setup(u => u.UsuarioId).Returns(_usuarioId);
        _sut = new TransacaoService(_transacaoRepository.Object, _papelRepository.Object, _usuarioLogado.Object);
    }

    private Papel NovoPapel(bool ativo) => new() { UsuarioId = _usuarioId, Codigo = "TEST3", Nome = "Teste", Ativo = ativo };

    [Fact]
    public async Task Vender_toda_a_posicao_desativa_o_papel_automaticamente_regra_2()
    {
        var papel = NovoPapel(ativo: true);
        var compraExistente = new Transacao { PapelId = papel.Id, TipoTransacao = TipoTransacao.Compra, Quantidade = 100, ValorUnitario = 10, Data = new DateOnly(2026, 1, 1) };
        var vendaNova = new Transacao { Id = Guid.NewGuid(), PapelId = papel.Id, TipoTransacao = TipoTransacao.Venda, Quantidade = 100, ValorUnitario = 15, Data = new DateOnly(2026, 2, 1) };

        _papelRepository.Setup(r => r.ObterPorIdAsync(papel.Id, _usuarioId, default)).ReturnsAsync(papel);
        _transacaoRepository.Setup(r => r.ObterPorIdAsync(vendaNova.Id, _usuarioId, default)).ReturnsAsync(vendaNova);
        // Após a venda ser persistida, a lista completa do papel já reflete as duas transações.
        _transacaoRepository.Setup(r => r.ListarPorPapelAsync(papel.Id, default))
            .ReturnsAsync(new List<Transacao> { compraExistente, vendaNova });

        await _sut.AtualizarAsync(vendaNova.Id, new AtualizarTransacaoRequest(
            vendaNova.TipoTransacao, vendaNova.Quantidade, vendaNova.ValorUnitario, vendaNova.Data, null));

        papel.Ativo.Should().BeFalse();
        _papelRepository.Verify(r => r.AtualizarAsync(papel, default), Times.Once);
    }

    [Fact]
    public async Task Comprar_depois_de_zerado_reativa_o_papel_automaticamente()
    {
        var papel = NovoPapel(ativo: false);
        var novaCompra = new Transacao { PapelId = papel.Id, TipoTransacao = TipoTransacao.Compra, Quantidade = 50, ValorUnitario = 30, Data = new DateOnly(2026, 3, 1) };

        _papelRepository.Setup(r => r.ObterPorIdAsync(papel.Id, _usuarioId, default)).ReturnsAsync(papel);
        _transacaoRepository.Setup(r => r.ListarPorPapelAsync(papel.Id, default))
            .ReturnsAsync(new List<Transacao> { novaCompra });

        await _sut.CriarAsync(new CriarTransacaoRequest(
            papel.Id, TipoTransacao.Compra, 50, 30, new DateOnly(2026, 3, 1), null));

        papel.Ativo.Should().BeTrue();
    }

    [Fact]
    public async Task Comprar_papel_ja_ativo_nao_dispara_atualizacao_desnecessaria_do_papel()
    {
        var papel = NovoPapel(ativo: true);
        var compraAnterior = new Transacao { PapelId = papel.Id, TipoTransacao = TipoTransacao.Compra, Quantidade = 100, ValorUnitario = 10, Data = new DateOnly(2026, 1, 1) };
        var novaCompra = new Transacao { PapelId = papel.Id, TipoTransacao = TipoTransacao.Compra, Quantidade = 50, ValorUnitario = 12, Data = new DateOnly(2026, 2, 1) };

        _papelRepository.Setup(r => r.ObterPorIdAsync(papel.Id, _usuarioId, default)).ReturnsAsync(papel);
        _transacaoRepository.Setup(r => r.ListarPorPapelAsync(papel.Id, default))
            .ReturnsAsync(new List<Transacao> { compraAnterior, novaCompra });

        await _sut.CriarAsync(new CriarTransacaoRequest(
            papel.Id, TipoTransacao.Compra, 50, 12, new DateOnly(2026, 2, 1), null));

        papel.Ativo.Should().BeTrue();
        _papelRepository.Verify(r => r.AtualizarAsync(It.IsAny<Papel>(), default), Times.Never);
    }

    [Fact]
    public async Task Criar_transacao_para_papel_inexistente_lanca_NaoEncontrado()
    {
        var papelId = Guid.NewGuid();
        _papelRepository.Setup(r => r.ObterPorIdAsync(papelId, _usuarioId, default)).ReturnsAsync((Papel?)null);

        var acao = () => _sut.CriarAsync(new CriarTransacaoRequest(
            papelId, TipoTransacao.Compra, 10, 5, new DateOnly(2026, 1, 1), null));

        await acao.Should().ThrowAsync<NaoEncontradoException>();
    }
}
