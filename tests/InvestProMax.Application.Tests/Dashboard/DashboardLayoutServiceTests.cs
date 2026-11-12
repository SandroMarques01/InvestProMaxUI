using FluentAssertions;
using InvestProMax.Application.Common.Exceptions;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Application.Dashboard;
using Moq;
using Xunit;

namespace InvestProMax.Application.Tests.Dashboard;

public class DashboardLayoutServiceTests
{
    private readonly Mock<IDashboardLayoutRepository> _repository = new();
    private readonly Mock<IUsuarioLogado> _usuarioLogado = new();
    private readonly Guid _usuarioId = Guid.NewGuid();
    private readonly DashboardLayoutService _sut;

    public DashboardLayoutServiceTests()
    {
        _usuarioLogado.Setup(u => u.UsuarioId).Returns(_usuarioId);
        _sut = new DashboardLayoutService(_repository.Object, _usuarioLogado.Object);
    }

    [Fact]
    public async Task ObterAsync_retorna_layout_padrao_quando_nao_ha_layout_salvo()
    {
        _repository.Setup(r => r.ObterAsync(_usuarioId, default)).ReturnsAsync((DashboardLayoutDto?)null);

        var resultado = await _sut.ObterAsync();

        resultado.Kpis.Should().Equal(DashboardLayoutService.KpisPadrao);
        resultado.Cards.Should().Equal(DashboardLayoutService.CardsPadrao);
    }

    [Fact]
    public async Task ObterAsync_retorna_layout_salvo_do_usuario()
    {
        var salvo = new DashboardLayoutDto(
            DashboardLayoutService.KpisPadrao.Reverse().ToList(),
            DashboardLayoutService.CardsPadrao.Reverse().ToList());
        _repository.Setup(r => r.ObterAsync(_usuarioId, default)).ReturnsAsync(salvo);

        var resultado = await _sut.ObterAsync();

        resultado.Should().BeEquivalentTo(salvo);
    }

    [Fact]
    public async Task SalvarAsync_persiste_layout_valido_com_blocos_reordenados()
    {
        var novoLayout = new DashboardLayoutDto(
            ["dividendos-mes", "patrimonio-total", "total-atual", "total-investido", "ganho-perda"],
            ["top10-papeis", "alocacao-tipo", "dividendos-recebidos", "aumento-capital", "dividendos-por-tipo"]);

        var resultado = await _sut.SalvarAsync(novoLayout);

        resultado.Should().BeEquivalentTo(novoLayout);
        _repository.Verify(r => r.SalvarAsync(_usuarioId, novoLayout, default), Times.Once);
    }

    [Fact]
    public async Task SalvarAsync_rejeita_layout_com_bloco_faltando_ou_desconhecido()
    {
        var invalido = new DashboardLayoutDto(
            ["patrimonio-total", "total-atual"],
            DashboardLayoutService.CardsPadrao);

        var acao = () => _sut.SalvarAsync(invalido);

        await acao.Should().ThrowAsync<RegraDeNegocioException>();
        _repository.Verify(r => r.SalvarAsync(It.IsAny<Guid>(), It.IsAny<DashboardLayoutDto>(), default), Times.Never);
    }
}

