using FluentAssertions;
using InvestProMax.Application.Common.Exceptions;
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

public class PapelServiceTests
{
    private readonly Mock<IPapelRepository> _papelRepository = new();
    private readonly Mock<ITransacaoRepository> _transacaoRepository = new();
    private readonly Mock<IMovimentoAluguelRepository> _movimentoAluguelRepository = new();
    private readonly Mock<IDividendoRepository> _dividendoRepository = new();
    private readonly Mock<ISegmentoRepository> _segmentoRepository = new();
    private readonly Mock<IUsuarioLogado> _usuarioLogado = new();
    private readonly Guid _usuarioId = Guid.NewGuid();
    private readonly PapelService _sut;

    public PapelServiceTests()
    {
        _usuarioLogado.Setup(u => u.UsuarioId).Returns(_usuarioId);
        _movimentoAluguelRepository.Setup(r => r.ListarPorPapelAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync(new List<MovimentoAluguel>());
        _sut = new PapelService(
            _papelRepository.Object, _transacaoRepository.Object, _movimentoAluguelRepository.Object, _dividendoRepository.Object,
            _segmentoRepository.Object, _usuarioLogado.Object);
    }

    private Papel NovoPapel(string codigo) => new() { UsuarioId = _usuarioId, Codigo = codigo, Nome = codigo, Ativo = true };

    // ---------- Regra 3: troca de papel ----------

    [Fact]
    public async Task Trocar_com_mesmo_id_de_origem_e_destino_lanca_RegraDeNegocio()
    {
        var id = Guid.NewGuid();
        var acao = () => _sut.TrocarAsync(id, new TrocarPapelRequest(id));

        await acao.Should().ThrowAsync<RegraDeNegocioException>();
    }

    [Fact]
    public async Task Trocar_realoca_transacoes_e_dividendos_para_o_destino_e_desativa_a_origem()
    {
        var origem = NovoPapel("ABCD3");
        var destino = NovoPapel("ABCD4");

        var transacaoDaOrigem = new Transacao { PapelId = origem.Id, TipoTransacao = TipoTransacao.Compra, Quantidade = 10, ValorUnitario = 5 };
        var dividendoDaOrigem = new Dividendo { PapelId = origem.Id };

        _papelRepository.Setup(r => r.ObterPorIdAsync(origem.Id, _usuarioId, default)).ReturnsAsync(origem);
        _papelRepository.Setup(r => r.ObterPorIdAsync(destino.Id, _usuarioId, default)).ReturnsAsync(destino);
        _transacaoRepository.Setup(r => r.ListarPorPapelAsync(origem.Id, default))
            .ReturnsAsync(new List<Transacao> { transacaoDaOrigem });
        _dividendoRepository.Setup(r => r.ListarPorPapelAsync(origem.Id, default))
            .ReturnsAsync(new List<Dividendo> { dividendoDaOrigem });
        _transacaoRepository.Setup(r => r.ListarPorPapelAsync(destino.Id, default))
            .ReturnsAsync(new List<Transacao> { transacaoDaOrigem }); // já realocada, mesmo objeto

        await _sut.TrocarAsync(origem.Id, new TrocarPapelRequest(destino.Id));

        transacaoDaOrigem.PapelId.Should().Be(destino.Id);
        dividendoDaOrigem.PapelId.Should().Be(destino.Id);
        origem.Ativo.Should().BeFalse();
        _transacaoRepository.Verify(r => r.AtualizarEmLoteAsync(
            It.Is<IEnumerable<Transacao>>(l => l.Contains(transacaoDaOrigem)), default), Times.Once);
        _dividendoRepository.Verify(r => r.AtualizarEmLoteAsync(
            It.Is<IEnumerable<Dividendo>>(l => l.Contains(dividendoDaOrigem)), default), Times.Once);
    }

    // ---------- Regra 4: desdobramento / grupamento ----------

    [Fact]
    public async Task Desdobramento_multiplica_quantidade_e_divide_valor_unitario_de_transacoes_anteriores_a_data_e_marca_historico()
    {
        var papel = NovoPapel("XYZW3");
        var transacaoAntiga = new Transacao
        {
            PapelId = papel.Id, TipoTransacao = TipoTransacao.Compra,
            Quantidade = 100, ValorUnitario = 20m, Data = new DateOnly(2026, 1, 1), Historico = false
        };

        _papelRepository.Setup(r => r.ObterPorIdAsync(papel.Id, _usuarioId, default)).ReturnsAsync(papel);
        _transacaoRepository.Setup(r => r.ListarPorPapelAsync(papel.Id, default))
            .ReturnsAsync(new List<Transacao> { transacaoAntiga });
        _dividendoRepository.Setup(r => r.ListarPorPapelAsync(papel.Id, default))
            .ReturnsAsync(new List<Dividendo>());

        await _sut.DesdobrarAsync(papel.Id, new DesdobramentoRequest(2m, new DateOnly(2026, 6, 1), TipoEventoPapel.Desdobramento));

        transacaoAntiga.Quantidade.Should().Be(200);
        transacaoAntiga.ValorUnitario.Should().Be(10m);
        transacaoAntiga.Historico.Should().BeTrue();
    }

    [Fact]
    public async Task Desdobramento_nao_altera_transacoes_com_data_igual_ou_posterior_ao_evento()
    {
        var papel = NovoPapel("XYZW3");
        var transacaoPosterior = new Transacao
        {
            PapelId = papel.Id, TipoTransacao = TipoTransacao.Compra,
            Quantidade = 100, ValorUnitario = 20m, Data = new DateOnly(2026, 6, 1), Historico = false
        };

        _papelRepository.Setup(r => r.ObterPorIdAsync(papel.Id, _usuarioId, default)).ReturnsAsync(papel);
        _transacaoRepository.Setup(r => r.ListarPorPapelAsync(papel.Id, default))
            .ReturnsAsync(new List<Transacao> { transacaoPosterior });
        _dividendoRepository.Setup(r => r.ListarPorPapelAsync(papel.Id, default))
            .ReturnsAsync(new List<Dividendo>());

        await _sut.DesdobrarAsync(papel.Id, new DesdobramentoRequest(2m, new DateOnly(2026, 6, 1), TipoEventoPapel.Desdobramento));

        transacaoPosterior.Quantidade.Should().Be(100);
        transacaoPosterior.ValorUnitario.Should().Be(20m);
        transacaoPosterior.Historico.Should().BeFalse();
    }

    [Fact]
    public async Task Grupamento_aplica_o_inverso_do_desdobramento()
    {
        var papel = NovoPapel("WXYZ3");
        var transacaoAntiga = new Transacao
        {
            PapelId = papel.Id, TipoTransacao = TipoTransacao.Compra,
            Quantidade = 1000, ValorUnitario = 2m, Data = new DateOnly(2026, 1, 1), Historico = false
        };

        _papelRepository.Setup(r => r.ObterPorIdAsync(papel.Id, _usuarioId, default)).ReturnsAsync(papel);
        _transacaoRepository.Setup(r => r.ListarPorPapelAsync(papel.Id, default))
            .ReturnsAsync(new List<Transacao> { transacaoAntiga });
        _dividendoRepository.Setup(r => r.ListarPorPapelAsync(papel.Id, default))
            .ReturnsAsync(new List<Dividendo>());

        // Grupamento 10:1
        await _sut.DesdobrarAsync(papel.Id, new DesdobramentoRequest(10m, new DateOnly(2026, 6, 1), TipoEventoPapel.Grupamento));

        transacaoAntiga.Quantidade.Should().Be(100);
        transacaoAntiga.ValorUnitario.Should().Be(20m);
        transacaoAntiga.Historico.Should().BeTrue();
    }

    [Fact]
    public async Task Desdobramento_de_dividendo_multiplica_quantidade_mas_nao_altera_o_valor_recebido()
    {
        var papel = NovoPapel("XYZW3");
        var dividendoAntigo = new Dividendo
        {
            PapelId = papel.Id, Quantidade = 100, ValorRecebido = 50m, Data = new DateOnly(2026, 1, 1), Historico = false
        };

        _papelRepository.Setup(r => r.ObterPorIdAsync(papel.Id, _usuarioId, default)).ReturnsAsync(papel);
        _transacaoRepository.Setup(r => r.ListarPorPapelAsync(papel.Id, default)).ReturnsAsync(new List<Transacao>());
        _dividendoRepository.Setup(r => r.ListarPorPapelAsync(papel.Id, default))
            .ReturnsAsync(new List<Dividendo> { dividendoAntigo });

        await _sut.DesdobrarAsync(papel.Id, new DesdobramentoRequest(2m, new DateOnly(2026, 6, 1), TipoEventoPapel.Desdobramento));

        dividendoAntigo.Quantidade.Should().Be(200);
        dividendoAntigo.ValorRecebido.Should().Be(50m); // dinheiro recebido é fato histórico, não recalcula
        dividendoAntigo.Historico.Should().BeTrue();
    }

    // ---------- Desdobramento não pode ser aplicado duas vezes ----------

    [Fact]
    public async Task Desdobramento_repetido_para_a_mesma_data_e_recusado()
    {
        var papel = NovoPapel("XYZW3");
        var transacao = new Transacao
        {
            PapelId = papel.Id, TipoTransacao = TipoTransacao.Compra,
            Quantidade = 100, ValorUnitario = 20m, Data = new DateOnly(2026, 1, 1)
        };

        _papelRepository.Setup(r => r.ObterPorIdAsync(papel.Id, _usuarioId, default)).ReturnsAsync(papel);
        _transacaoRepository.Setup(r => r.ListarPorPapelAsync(papel.Id, default)).ReturnsAsync(new List<Transacao> { transacao });
        _dividendoRepository.Setup(r => r.ListarPorPapelAsync(papel.Id, default)).ReturnsAsync(new List<Dividendo>());

        var pedido = new DesdobramentoRequest(2m, new DateOnly(2026, 6, 1), TipoEventoPapel.Desdobramento);
        await _sut.DesdobrarAsync(papel.Id, pedido);

        var segunda = async () => await _sut.DesdobrarAsync(papel.Id, pedido);

        await segunda.Should().ThrowAsync<RegraDeNegocioException>();
        transacao.Quantidade.Should().Be(200); // não dobrou de novo
    }

    // ---------- Detalhe do papel (fórmulas do MinhasFinancas) ----------

    [Fact]
    public async Task Detalhe_calcula_evolucao_preco_medio_do_dividendo_yield_on_cost_e_dy_como_no_sistema_antigo()
    {
        var papel = NovoPapel("ABCD3");
        papel.CotacaoAtual = 12m;
        papel.Transacoes = new List<Transacao>
        {
            new() { PapelId = papel.Id, TipoTransacao = TipoTransacao.Compra, Quantidade = 100, ValorUnitario = 10m, Data = new DateOnly(2026, 1, 1) },
            new() { PapelId = papel.Id, TipoTransacao = TipoTransacao.Compra, Quantidade = 100, ValorUnitario = 20m, Data = new DateOnly(2026, 2, 1) }
        };
        papel.Dividendos = new List<Dividendo>
        {
            new() { PapelId = papel.Id, TipoDividendo = TipoDividendo.Dividendo, ValorRecebido = 50m, Quantidade = 100, Data = new DateOnly(2026, 3, 1) }
        };
        _papelRepository.Setup(r => r.ObterPorIdAsync(papel.Id, _usuarioId, default)).ReturnsAsync(papel);

        var detalhe = await _sut.ObterDetalheAsync(papel.Id);

        detalhe.Transacoes.Select(t => t.Evolucao).Should().Equal(1000m, 3000m);
        detalhe.Transacoes.Select(t => t.EvolucaoAtual).Should().Equal(1200m, 2400m);
        detalhe.Transacoes[0].GanhoUnitario.Should().Be(2m);

        var d = detalhe.Dividendos.Single();
        d.ValorUnitario.Should().Be(0.5m);
        d.PrecoMedio.Should().Be(10m);                 // só a 1ª compra cobre as 100 do provento
        d.YieldOnCostPercentual.Should().Be(5m);       // 50 / (10 × 100) × 100
        d.DyPercentual.Should().BeApproximately(4.1667m, 0.0001m); // 0,5 / 12 × 100
        d.Evolucao.Should().Be(50m);
        d.EvolucaoPercentual.Should().Be(5m);
    }
}
