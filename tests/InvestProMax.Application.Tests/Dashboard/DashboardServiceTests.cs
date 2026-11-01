using FluentAssertions;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Application.Dashboard;
using InvestProMax.Application.Dividendos;
using InvestProMax.Application.Inventario;
using InvestProMax.Application.Papeis;
using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;
using Moq;
using Xunit;
using InventarioEntidade = InvestProMax.Domain.Entidades.Inventario;

namespace InvestProMax.Application.Tests.Dashboard;

public class DashboardServiceTests
{
    private readonly Mock<IPapelRepository> _papelRepository = new();
    private readonly Mock<IInventarioRepository> _inventarioRepository = new();
    private readonly Mock<IDividendoRepository> _dividendoRepository = new();
    private readonly Mock<IUsuarioLogado> _usuarioLogado = new();
    private readonly Guid _usuarioId = Guid.NewGuid();
    private readonly DashboardService _sut;

    public DashboardServiceTests()
    {
        _usuarioLogado.Setup(u => u.UsuarioId).Returns(_usuarioId);
        _sut = new DashboardService(
            _papelRepository.Object, _inventarioRepository.Object, _dividendoRepository.Object, _usuarioLogado.Object);

        _papelRepository.Setup(r => r.ListarTodosDoUsuarioAsync(_usuarioId, default))
            .ReturnsAsync((IReadOnlyList<Papel>)[]);
        _inventarioRepository.Setup(r => r.ListarUltimoMesDoUsuarioAsync(_usuarioId, default))
            .ReturnsAsync((IReadOnlyList<InventarioEntidade>)[]);
        _dividendoRepository.Setup(r => r.ListarTodosDoUsuarioAsync(_usuarioId, default))
            .ReturnsAsync((IReadOnlyList<Dividendo>)[]);
    }

    [Fact]
    public async Task PatrimonioTotal_soma_os_lancamentos_do_ultimo_mes_com_dados()
    {
        var data = new DateOnly(2026, 8, 1);
        _inventarioRepository.Setup(r => r.ListarUltimoMesDoUsuarioAsync(_usuarioId, default))
            .ReturnsAsync((IReadOnlyList<InventarioEntidade>)
            [
                new() { Valor = 1000m, Data = data },
                new() { Valor = 2500.50m, Data = data }
            ]);

        var resultado = await _sut.ObterAsync();

        resultado.PatrimonioTotal.Should().Be(3500.50m);
        resultado.PatrimonioAno.Should().Be(2026);
        resultado.PatrimonioMes.Should().Be(8);
    }

    [Fact]
    public async Task PatrimonioTotal_usa_carteira_quando_nunca_houve_lancamento_no_inventario()
    {
        var papel = new Papel
        {
            UsuarioId = _usuarioId,
            Codigo = "PETR4",
            TipoPapel = TipoPapel.Acao,
            CotacaoAtual = 40m,
            Transacoes = [new() { TipoTransacao = TipoTransacao.Compra, Quantidade = 100, ValorUnitario = 30m, Data = new DateOnly(2026, 1, 1) }]
        };
        _papelRepository.Setup(r => r.ListarTodosDoUsuarioAsync(_usuarioId, default))
            .ReturnsAsync((IReadOnlyList<Papel>)[papel]);

        var resultado = await _sut.ObterAsync();

        resultado.PatrimonioTotal.Should().Be(4000m);
        resultado.PatrimonioAno.Should().BeNull();
        resultado.PatrimonioMes.Should().BeNull();
        resultado.TotalInvestidoCarteira.Should().Be(3000m);
        resultado.TotalAtualCarteira.Should().Be(4000m);
        resultado.GanhoTotalCarteira.Should().Be(1000m);
    }

    [Fact]
    public async Task DividendosPorMes_cobre_os_ultimos_12_meses_incluindo_meses_sem_dividendo()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var mesPassado = new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(-1);

        _dividendoRepository.Setup(r => r.ListarTodosDoUsuarioAsync(_usuarioId, default))
            .ReturnsAsync((IReadOnlyList<Dividendo>)
            [
                new() { ValorRecebido = 50m, Data = mesPassado },
                new() { ValorRecebido = 30m, Data = mesPassado }
            ]);

        var resultado = await _sut.ObterAsync();

        resultado.DividendosPorMes.Should().HaveCount(12);
        resultado.DividendosPorMes.Should().Contain(d => d.Ano == mesPassado.Year && d.Mes == mesPassado.Month && d.Total == 80m);
        resultado.DividendosPorMes.Count(d => d.Total == 0).Should().Be(11);
        resultado.TotalDividendosHistorico.Should().Be(80m);
        resultado.DividendosNoMes.Should().Be(0m);
    }

    [Fact]
    public async Task DividendosPorMesETipo_agrupa_por_mes_e_tipo_de_papel()
    {
        var papelAcaoId = Guid.NewGuid();
        var papelFiiId = Guid.NewGuid();

        var papelAcao = new Papel { Id = papelAcaoId, UsuarioId = _usuarioId, Codigo = "VALE3", TipoPapel = TipoPapel.Acao };
        var papelFii = new Papel { Id = papelFiiId, UsuarioId = _usuarioId, Codigo = "HGLG11", TipoPapel = TipoPapel.FII };

        _papelRepository.Setup(r => r.ListarTodosDoUsuarioAsync(_usuarioId, default))
            .ReturnsAsync((IReadOnlyList<Papel>)[papelAcao, papelFii]);

        _dividendoRepository.Setup(r => r.ListarTodosDoUsuarioAsync(_usuarioId, default))
            .ReturnsAsync((IReadOnlyList<Dividendo>)
            [
                new() { PapelId = papelAcaoId, ValorRecebido = 120m, Data = new DateOnly(2026, 8, 15) },
                new() { PapelId = papelFiiId, ValorRecebido = 80m, Data = new DateOnly(2026, 8, 10) },
                new() { PapelId = papelAcaoId, ValorRecebido = 50m, Data = new DateOnly(2026, 7, 5) }
            ]);

        var resultado = await _sut.ObterAsync();

        resultado.DividendosPorMesETipo.Should().HaveCount(2);

        var agosto = resultado.DividendosPorMesETipo.First(d => d.Ano == 2026 && d.Mes == 8);
        agosto.Acoes.Should().Be(120m);
        agosto.Fiis.Should().Be(80m);
        agosto.Bdrs.Should().Be(0m);
        agosto.Etfs.Should().Be(0m);
        agosto.Total.Should().Be(200m);

        var julho = resultado.DividendosPorMesETipo.First(d => d.Ano == 2026 && d.Mes == 7);
        julho.Acoes.Should().Be(50m);
        julho.Fiis.Should().Be(0m);
        julho.Total.Should().Be(50m);
    }

    [Fact]
    public async Task EvolucaoCapital_calcula_compras_vendas_e_capital_acumulado()
    {
        var papel = new Papel
        {
            UsuarioId = _usuarioId,
            Codigo = "PETR4",
            TipoPapel = TipoPapel.Acao,
            CotacaoAtual = 35m,
            Transacoes =
            [
                new() { TipoTransacao = TipoTransacao.Compra, Quantidade = 100, ValorUnitario = 30m, Data = new DateOnly(2026, 1, 10) }, // +3000
                new() { TipoTransacao = TipoTransacao.Compra, Quantidade = 50, ValorUnitario = 32m, Data = new DateOnly(2026, 2, 15) },  // +1600
                new() { TipoTransacao = TipoTransacao.Venda, Quantidade = 20, ValorUnitario = 35m, Data = new DateOnly(2026, 2, 20) }    // -700
            ]
        };

        _papelRepository.Setup(r => r.ListarTodosDoUsuarioAsync(_usuarioId, default))
            .ReturnsAsync((IReadOnlyList<Papel>)[papel]);

        var resultado = await _sut.ObterAsync();

        resultado.EvolucaoCapital.Should().HaveCount(2);

        var jan = resultado.EvolucaoCapital[0];
        jan.Ano.Should().Be(2026);
        jan.Mes.Should().Be(1);
        jan.ComprasNoMes.Should().Be(3000m);
        jan.VendasNoMes.Should().Be(0m);
        jan.AporteLiquidoNoMes.Should().Be(3000m);
        jan.CapitalAcumulado.Should().Be(3000m);

        var fev = resultado.EvolucaoCapital[1];
        fev.Ano.Should().Be(2026);
        fev.Mes.Should().Be(2);
        fev.ComprasNoMes.Should().Be(1600m);
        fev.VendasNoMes.Should().Be(700m);
        fev.AporteLiquidoNoMes.Should().Be(900m);
        fev.CapitalAcumulado.Should().Be(3900m);
    }

    [Fact]
    public async Task AlocacaoPorTipoPapel_soma_posicao_vezes_cotacao_agrupada_por_tipo_e_ignora_papel_zerado()
    {
        var papelAcao = new Papel
        {
            UsuarioId = _usuarioId, Codigo = "PETR4", TipoPapel = TipoPapel.Acao, CotacaoAtual = 40m,
            Transacoes = [new() { TipoTransacao = TipoTransacao.Compra, Quantidade = 100, ValorUnitario = 30m, Data = new DateOnly(2026, 1, 1) }]
        };
        var papelFiiZerado = new Papel
        {
            UsuarioId = _usuarioId, Codigo = "HGLG11", TipoPapel = TipoPapel.FII, CotacaoAtual = 150m,
            Transacoes =
            [
                new() { TipoTransacao = TipoTransacao.Compra, Quantidade = 10, ValorUnitario = 140m, Data = new DateOnly(2026, 1, 1) },
                new() { TipoTransacao = TipoTransacao.Venda, Quantidade = 10, ValorUnitario = 150m, Data = new DateOnly(2026, 2, 1) }
            ]
        };
        _papelRepository.Setup(r => r.ListarTodosDoUsuarioAsync(_usuarioId, default))
            .ReturnsAsync((IReadOnlyList<Papel>)[papelAcao, papelFiiZerado]);

        var resultado = await _sut.ObterAsync();

        resultado.AlocacaoPorTipoPapel.Should().ContainSingle();
        resultado.AlocacaoPorTipoPapel[0].TipoPapel.Should().Be(TipoPapel.Acao);
        resultado.AlocacaoPorTipoPapel[0].Valor.Should().Be(100m * 40m);
    }
}

