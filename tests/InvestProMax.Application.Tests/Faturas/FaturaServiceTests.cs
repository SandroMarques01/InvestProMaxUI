using FluentAssertions;
using InvestProMax.Application.Common.Exceptions;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Application.Contas;
using InvestProMax.Application.Faturas;
using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;
using Moq;
using Xunit;

namespace InvestProMax.Application.Tests.Faturas;

public class FaturaServiceTests
{
    private readonly Mock<IFaturaRepository> _faturaRepository = new();
    private readonly Mock<IContaRepository> _contaRepository = new();
    private readonly Mock<IUsuarioLogado> _usuarioLogado = new();
    private readonly Guid _usuarioId = Guid.NewGuid();
    private readonly FaturaService _sut;

    public FaturaServiceTests()
    {
        _usuarioLogado.Setup(u => u.UsuarioId).Returns(_usuarioId);
        _sut = new FaturaService(_faturaRepository.Object, _contaRepository.Object, _usuarioLogado.Object);
    }

    [Fact]
    public async Task Pagar_fatura_pendente_cria_pagamento_e_muda_status_para_paga_regra_8()
    {
        var fatura = new Fatura { Status = StatusFatura.Pendente, DataVencimento = new DateOnly(2026, 6, 10) };
        _faturaRepository.Setup(r => r.ObterPorIdAsync(fatura.Id, _usuarioId, default)).ReturnsAsync(fatura);

        var resposta = await _sut.PagarAsync(fatura.Id, new PagarFaturaRequest(120m, new DateOnly(2026, 6, 9), "Pix", null));

        fatura.Status.Should().Be(StatusFatura.Paga);
        resposta.Status.Should().Be(StatusFatura.Paga);
        _faturaRepository.Verify(r => r.AdicionarPagamentoAsync(
            It.Is<Pagamento>(p => p.FaturaId == fatura.Id && p.ValorPago == 120m && p.FormaPagamento == "Pix"), default), Times.Once);
        _faturaRepository.Verify(r => r.AtualizarAsync(fatura, default), Times.Once);
    }

    [Fact]
    public async Task Pagar_fatura_ja_paga_lanca_RegraDeNegocio_e_nao_cria_segundo_pagamento()
    {
        var fatura = new Fatura { Status = StatusFatura.Paga, DataVencimento = new DateOnly(2026, 6, 10) };
        _faturaRepository.Setup(r => r.ObterPorIdAsync(fatura.Id, _usuarioId, default)).ReturnsAsync(fatura);

        var acao = () => _sut.PagarAsync(fatura.Id, new PagarFaturaRequest(120m, new DateOnly(2026, 6, 9), null, null));

        await acao.Should().ThrowAsync<RegraDeNegocioException>();
        _faturaRepository.Verify(r => r.AdicionarPagamentoAsync(It.IsAny<Pagamento>(), default), Times.Never);
    }

    [Fact]
    public async Task Pagar_fatura_cancelada_lanca_RegraDeNegocio()
    {
        var fatura = new Fatura { Status = StatusFatura.Cancelada, DataVencimento = new DateOnly(2026, 6, 10) };
        _faturaRepository.Setup(r => r.ObterPorIdAsync(fatura.Id, _usuarioId, default)).ReturnsAsync(fatura);

        var acao = () => _sut.PagarAsync(fatura.Id, new PagarFaturaRequest(120m, new DateOnly(2026, 6, 9), null, null));

        await acao.Should().ThrowAsync<RegraDeNegocioException>();
    }

    [Fact]
    public async Task Pagar_fatura_inexistente_lanca_NaoEncontrado()
    {
        var faturaId = Guid.NewGuid();
        _faturaRepository.Setup(r => r.ObterPorIdAsync(faturaId, _usuarioId, default)).ReturnsAsync((Fatura?)null);

        var acao = () => _sut.PagarAsync(faturaId, new PagarFaturaRequest(120m, new DateOnly(2026, 6, 9), null, null));

        await acao.Should().ThrowAsync<NaoEncontradoException>();
    }

    [Fact]
    public async Task GerarFaturas_delega_para_o_usuario_autenticado_na_competencia_atual()
    {
        var conta = new Conta { UsuarioId = _usuarioId, Nome = "Aluguel", ValorEstimado = 1500m, DiaVencimento = 5, StatusAtivo = true };
        _contaRepository.Setup(r => r.ListarAtivasDoUsuarioAsync(_usuarioId, default)).ReturnsAsync(new List<Conta> { conta });
        _faturaRepository.Setup(r => r.ObterPorContaECompetenciaAsync(conta.Id, It.IsAny<DateOnly>(), default)).ReturnsAsync((Fatura?)null);

        var resposta = await _sut.GerarFaturasAsync();

        resposta.FaturasCriadas.Should().Be(1);
        _faturaRepository.Verify(r => r.AdicionarAsync(It.Is<Fatura>(f => f.ContaId == conta.Id), default), Times.Once);
    }
}
