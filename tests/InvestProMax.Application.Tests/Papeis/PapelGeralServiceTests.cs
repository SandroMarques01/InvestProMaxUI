using FluentAssertions;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Application.Dividendos;
using InvestProMax.Application.Papeis;
using InvestProMax.Application.Segmentos;
using InvestProMax.Application.Transacoes;
using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;
using Moq;
using Xunit;

namespace InvestProMax.Application.Tests.Papeis;

public class PapelGeralServiceTests
{
    private readonly Mock<IPapelRepository> _papelRepository = new();
    private readonly Mock<ITransacaoRepository> _transacaoRepository = new();
    private readonly Mock<IMovimentoAluguelRepository> _movimentoAluguelRepository = new();
    private readonly Mock<IDividendoRepository> _dividendoRepository = new();
    private readonly Mock<ISegmentoRepository> _segmentoRepository = new();
    private readonly Mock<IUsuarioLogado> _usuarioLogado = new();

    private readonly PapelService _sut;
    private readonly Guid _usuarioId = Guid.NewGuid();

    public PapelGeralServiceTests()
    {
        _usuarioLogado.Setup(u => u.UsuarioId).Returns(_usuarioId);
        _sut = new PapelService(
            _papelRepository.Object,
            _transacaoRepository.Object,
            _movimentoAluguelRepository.Object,
            _dividendoRepository.Object,
            _segmentoRepository.Object,
            _usuarioLogado.Object);
    }

    [Fact]
    public async Task ObterPapeisGeralAsync_CalculaPosicoesTotaisEGanhosCorretamente()
    {
        var papelId = Guid.NewGuid();
        var papel = new Papel
        {
            Id = papelId,
            UsuarioId = _usuarioId,
            Codigo = "PETR4",
            Nome = "Petrobras",
            TipoPapel = TipoPapel.Acao,
            CotacaoAtual = 40.00m,
            Ativo = true,
            Transacoes = new List<Transacao>
            {
                new() { PapelId = papelId, TipoTransacao = TipoTransacao.Compra, Quantidade = 100, ValorUnitario = 30.00m, Data = new DateOnly(2026, 1, 10) },
                new() { PapelId = papelId, TipoTransacao = TipoTransacao.Compra, Quantidade = 100, ValorUnitario = 34.00m, Data = new DateOnly(2026, 2, 10) }
                // Preco médio = (100*30 + 100*34) / 200 = 6400 / 200 = 32.00
            },
            Dividendos = new List<Dividendo>
            {
                new() { PapelId = papelId, Quantidade = 200, ValorRecebido = 160.00m, Data = new DateOnly(2026, 3, 1) }
            }
        };

        _papelRepository.Setup(r => r.ListarTodosDoUsuarioAsync(_usuarioId, default))
            .ReturnsAsync(new List<Papel> { papel });

        var resultado = await _sut.ObterPapeisGeralAsync();

        resultado.Should().NotBeNull();
        resultado.Itens.Should().HaveCount(1);

        var item = resultado.Itens[0];
        item.Codigo.Should().Be("PETR4");
        item.QuantidadeTotal.Should().Be(200);
        item.PrecoMedio.Should().Be(32.00m);
        item.TotalCompra.Should().Be(6400.00m);
        item.CotacaoAtual.Should().Be(40.00m);
        item.TotalAtual.Should().Be(8000.00m);
        item.GanhoUnitario.Should().Be(8.00m);
        item.GanhoTotal.Should().Be(1600.00m);
        item.ValorizacaoPercentual.Should().Be(25.00m); // ((8000 / 6400) * 100) - 100 = 25%
        item.TotalDividendos.Should().Be(160.00m);
        item.PercentualDividendos.Should().Be(2.5m); // (160 / 6400) * 100 = 2.5%
        item.PercentualCompra.Should().Be(100.00m);
        item.PercentualTotal.Should().Be(100.00m);

        resultado.TotalInvestido.Should().Be(6400.00m);
        resultado.TotalAtual.Should().Be(8000.00m);
        resultado.TotalDividendos.Should().Be(160.00m);
        resultado.GanhoTotal.Should().Be(1600.00m);
        resultado.ValorizacaoPercentualGeral.Should().Be(25.00m);
    }

    [Fact]
    public async Task ObterPapeisGeralAsync_TrataDivisaoPorZero_QuandoSemValores()
    {
        var papel = new Papel
        {
            Id = Guid.NewGuid(),
            UsuarioId = _usuarioId,
            Codigo = "VAZIO",
            Nome = "Papel Sem Transações",
            TipoPapel = TipoPapel.Acao,
            CotacaoAtual = 10.00m,
            Ativo = true,
            Transacoes = new List<Transacao>(),
            Dividendos = new List<Dividendo>()
        };

        _papelRepository.Setup(r => r.ListarTodosDoUsuarioAsync(_usuarioId, default))
            .ReturnsAsync(new List<Papel> { papel });

        var resultado = await _sut.ObterPapeisGeralAsync(somenteAtivos: null);

        resultado.Should().NotBeNull();
        resultado.Itens.Should().HaveCount(1);
        var item = resultado.Itens[0];
        item.QuantidadeTotal.Should().Be(0);
        item.PrecoMedio.Should().Be(0);
        item.TotalCompra.Should().Be(0);
        item.ValorizacaoPercentual.Should().Be(0);
        resultado.ValorizacaoPercentualGeral.Should().Be(0);
    }

    [Fact]
    public async Task ObterPapeisGeralAsync_FiltroSomenteAtivos_FiltraCorretamente()
    {
        var ativoId = Guid.NewGuid();
        var papelAtivo = new Papel
        {
            Id = ativoId,
            UsuarioId = _usuarioId,
            Codigo = "ATIVO3",
            CotacaoAtual = 10m,
            Transacoes = new List<Transacao>
            {
                new() { PapelId = ativoId, TipoTransacao = TipoTransacao.Compra, Quantidade = 100, ValorUnitario = 10m, Data = new DateOnly(2026, 1, 1) }
            }
        };

        var encerradoId = Guid.NewGuid();
        var papelEncerrado = new Papel
        {
            Id = encerradoId,
            UsuarioId = _usuarioId,
            Codigo = "ENCERRADO3",
            CotacaoAtual = 10m,
            Transacoes = new List<Transacao>
            {
                new() { PapelId = encerradoId, TipoTransacao = TipoTransacao.Compra, Quantidade = 100, ValorUnitario = 10m, Data = new DateOnly(2026, 1, 1) },
                new() { PapelId = encerradoId, TipoTransacao = TipoTransacao.Venda, Quantidade = 100, ValorUnitario = 12m, Data = new DateOnly(2026, 1, 2) }
            }
        };

        _papelRepository.Setup(r => r.ListarTodosDoUsuarioAsync(_usuarioId, default))
            .ReturnsAsync(new List<Papel> { papelAtivo, papelEncerrado });

        var apenasAtivos = await _sut.ObterPapeisGeralAsync(somenteAtivos: true);
        apenasAtivos.Itens.Should().HaveCount(1);
        apenasAtivos.Itens[0].Codigo.Should().Be("ATIVO3");

        var apenasEncerrados = await _sut.ObterPapeisGeralAsync(somenteAtivos: false);
        apenasEncerrados.Itens.Should().HaveCount(1);
        apenasEncerrados.Itens[0].Codigo.Should().Be("ENCERRADO3");

        var todos = await _sut.ObterPapeisGeralAsync(somenteAtivos: null);
        todos.Itens.Should().HaveCount(2);
    }

    [Fact]
    public async Task ObterPapeisGeralAsync_FiltroDataFinal_ConsideraApenasEventosAteAData()
    {
        var papelId = Guid.NewGuid();
        var papel = new Papel
        {
            Id = papelId,
            UsuarioId = _usuarioId,
            Codigo = "VALE3",
            CotacaoAtual = 70m,
            Transacoes = new List<Transacao>
            {
                new() { PapelId = papelId, TipoTransacao = TipoTransacao.Compra, Quantidade = 100, ValorUnitario = 60m, Data = new DateOnly(2026, 1, 15) },
                new() { PapelId = papelId, TipoTransacao = TipoTransacao.Compra, Quantidade = 100, ValorUnitario = 80m, Data = new DateOnly(2026, 6, 15) }
            },
            Dividendos = new List<Dividendo>
            {
                new() { PapelId = papelId, Quantidade = 100, ValorRecebido = 50m, Data = new DateOnly(2026, 2, 1) },
                new() { PapelId = papelId, Quantidade = 200, ValorRecebido = 100m, Data = new DateOnly(2026, 7, 1) }
            }
        };

        _papelRepository.Setup(r => r.ListarTodosDoUsuarioAsync(_usuarioId, default))
            .ReturnsAsync(new List<Papel> { papel });

        // Corte em 31/03/2026: deve considerar apenas a primeira compra (100 a R$ 60) e primeiro dividendo (R$ 50)
        var dataCorte = new DateOnly(2026, 3, 31);
        var resultado = await _sut.ObterPapeisGeralAsync(dataFinal: dataCorte);

        resultado.Itens.Should().HaveCount(1);
        var item = resultado.Itens[0];
        item.QuantidadeTotal.Should().Be(100);
        item.PrecoMedio.Should().Be(60m);
        item.TotalCompra.Should().Be(6000m);
        item.TotalDividendos.Should().Be(50m);
    }
}
