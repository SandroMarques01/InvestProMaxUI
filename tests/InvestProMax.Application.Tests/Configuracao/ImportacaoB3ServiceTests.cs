using FluentAssertions;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Application.Configuracao;
using InvestProMax.Application.Dividendos;
using InvestProMax.Application.Papeis;
using InvestProMax.Application.Transacoes;
using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;
using Moq;
using Xunit;

namespace InvestProMax.Application.Tests.Configuracao;

public class ImportacaoB3ServiceTests
{
    private readonly Mock<IExtratoB3Reader> _reader = new();
    private readonly Mock<IPapelRepository> _papelRepository = new();
    private readonly Mock<ITransacaoRepository> _transacaoRepository = new();
    private readonly Mock<IDividendoRepository> _dividendoRepository = new();
    private readonly Mock<IMovimentoAluguelRepository> _movimentoAluguelRepository = new();
    private readonly Mock<IUsuarioLogado> _usuarioLogado = new();
    private readonly Guid _usuarioId = Guid.NewGuid();
    private readonly ImportacaoB3Service _sut;

    public ImportacaoB3ServiceTests()
    {
        _usuarioLogado.Setup(u => u.UsuarioId).Returns(_usuarioId);
        _transacaoRepository.Setup(r => r.ListarHashesImportadosAsync(_usuarioId, default)).ReturnsAsync(new HashSet<string>());
        _dividendoRepository.Setup(r => r.ListarHashesImportadosAsync(_usuarioId, default)).ReturnsAsync(new HashSet<string>());

        _movimentoAluguelRepository.Setup(r => r.ListarHashesImportadosAsync(_usuarioId, default)).ReturnsAsync(new HashSet<string>());

        _sut = new ImportacaoB3Service(
            _reader.Object, _papelRepository.Object, _transacaoRepository.Object, _dividendoRepository.Object,
            _movimentoAluguelRepository.Object, _usuarioLogado.Object);
    }

    private static LinhaExtratoB3 LinhaTransferencia(string entradaSaida = "Credito", decimal quantidade = 100, decimal? precoUnitario = 30m, decimal valorOperacao = 3000m) =>
        new(entradaSaida, new DateOnly(2026, 3, 10), "Transferência - Liquidação", "PETR4 - PETROLEO BRASILEIRO S.A. PETROBRAS", quantidade, precoUnitario, valorOperacao);

    [Fact]
    public async Task Linha_de_compra_nova_cria_papel_e_transacao()
    {
        _reader.Setup(r => r.Ler(It.IsAny<Stream>())).Returns(new List<LinhaExtratoB3> { LinhaTransferencia() });
        _papelRepository.Setup(r => r.ObterPorCodigoAsync(_usuarioId, "PETR4", default)).ReturnsAsync((Papel?)null);

        List<Transacao>? transacoesCriadas = null;
        _transacaoRepository.Setup(r => r.AdicionarEmLoteAsync(It.IsAny<IEnumerable<Transacao>>(), default))
            .Callback<IEnumerable<Transacao>, CancellationToken>((t, _) => transacoesCriadas = t.ToList())
            .Returns(Task.CompletedTask);

        var resposta = await _sut.ImportarAsync(new MemoryStream());

        resposta.PapeisCriados.Should().Be(1);
        resposta.TransacoesCriadas.Should().Be(1);
        _papelRepository.Verify(r => r.AdicionarAsync(
            It.Is<Papel>(p => p.Codigo == "PETR4" && p.Nome.Contains("PETROBRAS") && p.UsuarioId == _usuarioId), default), Times.Once);

        transacoesCriadas.Should().ContainSingle();
        transacoesCriadas![0].TipoTransacao.Should().Be(TipoTransacao.Compra); // Credito → Compra
        transacoesCriadas[0].ValorUnitario.Should().Be(30m);
        transacoesCriadas[0].OrigemImportacaoHash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Linha_de_venda_por_saida_e_classificada_corretamente()
    {
        _reader.Setup(r => r.Ler(It.IsAny<Stream>())).Returns(new List<LinhaExtratoB3> { LinhaTransferencia(entradaSaida: "Debito") });
        _papelRepository.Setup(r => r.ObterPorCodigoAsync(_usuarioId, "PETR4", default)).ReturnsAsync((Papel?)null);

        List<Transacao>? transacoesCriadas = null;
        _transacaoRepository.Setup(r => r.AdicionarEmLoteAsync(It.IsAny<IEnumerable<Transacao>>(), default))
            .Callback<IEnumerable<Transacao>, CancellationToken>((t, _) => transacoesCriadas = t.ToList())
            .Returns(Task.CompletedTask);

        await _sut.ImportarAsync(new MemoryStream());

        transacoesCriadas![0].TipoTransacao.Should().Be(TipoTransacao.Venda);
    }

    [Fact]
    public async Task Papel_ja_existente_e_reaproveitado_em_vez_de_criar_um_novo()
    {
        var papelExistente = new Papel { UsuarioId = _usuarioId, Codigo = "PETR4", Nome = "Petrobras", Ativo = true };
        _reader.Setup(r => r.Ler(It.IsAny<Stream>())).Returns(new List<LinhaExtratoB3> { LinhaTransferencia() });
        _papelRepository.Setup(r => r.ObterPorCodigoAsync(_usuarioId, "PETR4", default)).ReturnsAsync(papelExistente);

        var resposta = await _sut.ImportarAsync(new MemoryStream());

        resposta.PapeisCriados.Should().Be(0);
        _papelRepository.Verify(r => r.AdicionarAsync(It.IsAny<Papel>(), default), Times.Never);
    }

    [Fact]
    public async Task Duas_linhas_identicas_no_mesmo_arquivo_geram_apenas_uma_transacao_regra_5()
    {
        var linha = LinhaTransferencia();
        _reader.Setup(r => r.Ler(It.IsAny<Stream>())).Returns(new List<LinhaExtratoB3> { linha, linha });
        _papelRepository.Setup(r => r.ObterPorCodigoAsync(_usuarioId, "PETR4", default)).ReturnsAsync((Papel?)null);

        var resposta = await _sut.ImportarAsync(new MemoryStream());

        resposta.TransacoesCriadas.Should().Be(1);
        resposta.LinhasIgnoradasDuplicadas.Should().Be(1);
    }

    [Fact]
    public async Task Linha_cujo_hash_ja_foi_importado_anteriormente_e_ignorada()
    {
        var linha = LinhaTransferencia();
        var hashJaImportado = HashDeLinhaImportada.CalcularParaLinhaB3(linha);

        _reader.Setup(r => r.Ler(It.IsAny<Stream>())).Returns(new List<LinhaExtratoB3> { linha });
        _transacaoRepository.Setup(r => r.ListarHashesImportadosAsync(_usuarioId, default))
            .ReturnsAsync(new HashSet<string> { hashJaImportado });

        var resposta = await _sut.ImportarAsync(new MemoryStream());

        resposta.TransacoesCriadas.Should().Be(0);
        resposta.LinhasIgnoradasDuplicadas.Should().Be(1);
        _papelRepository.Verify(r => r.ObterPorCodigoAsync(It.IsAny<Guid>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task Dividendo_cria_registro_de_dividendo_nao_transacao()
    {
        var linha = new LinhaExtratoB3("Credito", new DateOnly(2026, 4, 1), "Dividendo", "PETR4 - PETROLEO BRASILEIRO S.A. PETROBRAS", 100, null, 45.50m);
        _reader.Setup(r => r.Ler(It.IsAny<Stream>())).Returns(new List<LinhaExtratoB3> { linha });
        _papelRepository.Setup(r => r.ObterPorCodigoAsync(_usuarioId, "PETR4", default)).ReturnsAsync((Papel?)null);

        List<Dividendo>? dividendosCriados = null;
        _dividendoRepository.Setup(r => r.AdicionarEmLoteAsync(It.IsAny<IEnumerable<Dividendo>>(), default))
            .Callback<IEnumerable<Dividendo>, CancellationToken>((d, _) => dividendosCriados = d.ToList())
            .Returns(Task.CompletedTask);

        var resposta = await _sut.ImportarAsync(new MemoryStream());

        resposta.DividendosCriados.Should().Be(1);
        resposta.TransacoesCriadas.Should().Be(0);
        dividendosCriados.Should().ContainSingle();
        dividendosCriados![0].TipoDividendo.Should().Be(TipoDividendo.Dividendo);
        dividendosCriados[0].ValorRecebido.Should().Be(45.50m);
    }

    [Fact]
    public async Task Movimentacao_desconhecida_e_contada_como_nao_reconhecida_e_nao_falha_a_importacao()
    {
        var linha = new LinhaExtratoB3("Credito", new DateOnly(2026, 4, 1), "Algo Que Não Existe", "PETR4 - PETROLEO", 1, 1, 1);
        _reader.Setup(r => r.Ler(It.IsAny<Stream>())).Returns(new List<LinhaExtratoB3> { linha });

        var resposta = await _sut.ImportarAsync(new MemoryStream());

        resposta.LinhasIgnoradasNaoReconhecidas.Should().Be(1);
        resposta.TransacoesCriadas.Should().Be(0);
        resposta.DividendosCriados.Should().Be(0);
    }

    // ---------- Aluguel de ações: o papel continua na carteira ----------

    private static LinhaExtratoB3 LinhaAluguel(string entradaSaida, DateOnly data, decimal quantidade, decimal? preco) =>
        new(entradaSaida, data, "Transferência - Liquidação", "BBSE3 - BB SEGURIDADE PARTICIPACOES S.A.", quantidade, preco, preco is null ? 0 : preco.Value * quantidade);

    [Fact]
    public async Task Saida_e_devolucao_de_aluguel_geram_movimentos_e_nenhuma_transacao()
    {
        _reader.Setup(r => r.Ler(It.IsAny<Stream>())).Returns(new List<LinhaExtratoB3>
        {
            // fora de ordem de propósito: o serviço ordena por data antes de parear
            LinhaAluguel("Credito", new DateOnly(2026, 9, 10), 100, 28.5m),
            LinhaAluguel("Debito", new DateOnly(2026, 9, 1), 100, null)
        });
        _papelRepository.Setup(r => r.ObterPorCodigoAsync(_usuarioId, "BBSE3", default)).ReturnsAsync((Papel?)null);

        List<MovimentoAluguel>? movimentos = null;
        _movimentoAluguelRepository.Setup(r => r.AdicionarEmLoteAsync(It.IsAny<IEnumerable<MovimentoAluguel>>(), default))
            .Callback<IEnumerable<MovimentoAluguel>, CancellationToken>((m, _) => movimentos = m.ToList())
            .Returns(Task.CompletedTask);

        var resposta = await _sut.ImportarAsync(new MemoryStream());

        resposta.TransacoesCriadas.Should().Be(0);
        resposta.MovimentosAluguelCriados.Should().Be(2);
        movimentos!.Select(m => m.Tipo).Should().Equal(TipoMovimentoAluguel.Saida, TipoMovimentoAluguel.Devolucao);
        _transacaoRepository.Verify(r => r.AdicionarEmLoteAsync(It.IsAny<IEnumerable<Transacao>>(), default), Times.Never);
    }

    [Fact]
    public async Task Credito_com_preco_sem_aluguel_aberto_continua_sendo_compra()
    {
        _reader.Setup(r => r.Ler(It.IsAny<Stream>())).Returns(new List<LinhaExtratoB3>
        {
            LinhaAluguel("Credito", new DateOnly(2026, 9, 10), 100, 28.5m)
        });
        _papelRepository.Setup(r => r.ObterPorCodigoAsync(_usuarioId, "BBSE3", default)).ReturnsAsync((Papel?)null);

        var resposta = await _sut.ImportarAsync(new MemoryStream());

        resposta.TransacoesCriadas.Should().Be(1);
        resposta.MovimentosAluguelCriados.Should().Be(0);
    }

    [Fact]
    public async Task Quantidade_que_excede_o_emprestado_na_devolucao_vira_compra_so_do_excedente()
    {
        _reader.Setup(r => r.Ler(It.IsAny<Stream>())).Returns(new List<LinhaExtratoB3>
        {
            LinhaAluguel("Debito", new DateOnly(2026, 9, 1), 100, null),
            LinhaAluguel("Credito", new DateOnly(2026, 9, 10), 130, 28.5m)
        });
        _papelRepository.Setup(r => r.ObterPorCodigoAsync(_usuarioId, "BBSE3", default)).ReturnsAsync((Papel?)null);

        List<Transacao>? transacoes = null;
        _transacaoRepository.Setup(r => r.AdicionarEmLoteAsync(It.IsAny<IEnumerable<Transacao>>(), default))
            .Callback<IEnumerable<Transacao>, CancellationToken>((t, _) => transacoes = t.ToList())
            .Returns(Task.CompletedTask);

        await _sut.ImportarAsync(new MemoryStream());

        transacoes.Should().ContainSingle().Which.Quantidade.Should().Be(30);
    }

    [Fact]
    public async Task Emprestimo_com_valor_pequeno_vira_dividendo_do_tipo_aluguel_e_com_valor_de_preco_e_ignorado()
    {
        _reader.Setup(r => r.Ler(It.IsAny<Stream>())).Returns(new List<LinhaExtratoB3>
        {
            new("Credito", new DateOnly(2026, 8, 5), "Empréstimo", "GOAU4 - METALURGICA GERDAU S.A.", 100, null, 1.37m),
            new("Credito", new DateOnly(2026, 9, 1), "Empréstimo", "BPAC11 - BANCO BTG PACTUAL S.A.", 40, null, 2184.80m)
        });
        _papelRepository.Setup(r => r.ObterPorCodigoAsync(_usuarioId, It.IsAny<string>(), default)).ReturnsAsync((Papel?)null);

        List<Dividendo>? dividendos = null;
        _dividendoRepository.Setup(r => r.AdicionarEmLoteAsync(It.IsAny<IEnumerable<Dividendo>>(), default))
            .Callback<IEnumerable<Dividendo>, CancellationToken>((d, _) => dividendos = d.ToList())
            .Returns(Task.CompletedTask);

        var resposta = await _sut.ImportarAsync(new MemoryStream());

        dividendos.Should().ContainSingle().Which.TipoDividendo.Should().Be(TipoDividendo.Aluguel);
        resposta.LinhasIgnoradasNaoReconhecidas.Should().Be(1);
    }

    [Fact]
    public async Task Saida_e_devolucao_no_mesmo_dia_com_credito_antes_do_debito_no_arquivo_processa_saida_primeiro()
    {
        var mesmaData = new DateOnly(2026, 9, 24);
        _reader.Setup(r => r.Ler(It.IsAny<Stream>())).Returns(new List<LinhaExtratoB3>
        {
            LinhaAluguel("Credito", mesmaData, 109, 38.86m),
            LinhaAluguel("Debito", mesmaData, 109, null)
        });
        _papelRepository.Setup(r => r.ObterPorCodigoAsync(_usuarioId, "BBSE3", default)).ReturnsAsync((Papel?)null);

        List<MovimentoAluguel>? movimentos = null;
        _movimentoAluguelRepository.Setup(r => r.AdicionarEmLoteAsync(It.IsAny<IEnumerable<MovimentoAluguel>>(), default))
            .Callback<IEnumerable<MovimentoAluguel>, CancellationToken>((m, _) => movimentos = m.ToList())
            .Returns(Task.CompletedTask);

        var resposta = await _sut.ImportarAsync(new MemoryStream());

        resposta.TransacoesCriadas.Should().Be(0);
        resposta.MovimentosAluguelCriados.Should().Be(2);
        movimentos!.Select(m => m.Tipo).Should().Equal(TipoMovimentoAluguel.Saida, TipoMovimentoAluguel.Devolucao);
    }

    [Fact]
    public void Hash_de_linha_importada_independe_da_cultura_da_thread()
    {
        var linha = new LinhaExtratoB3("Credito", new DateOnly(2026, 10, 7), "Transferência - Liquidação", "GOGL34 - ALPHABET INC.", 36m, 144.35m, 5196.6m);
        var culturaOriginal = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
            var hashPtBr = HashDeLinhaImportada.CalcularParaLinhaB3(linha);

            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            var hashInvariant = HashDeLinhaImportada.CalcularParaLinhaB3(linha);

            hashInvariant.Should().Be(hashPtBr);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = culturaOriginal;
        }
    }
}
