using FluentAssertions;
using FluentValidation;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Application.Cotacoes;
using InvestProMax.Application.Papeis;
using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;
using Moq;
using Xunit;

namespace InvestProMax.Application.Tests.Cotacoes;

public class AtualizacaoCotacoesServiceTests
{
    private readonly Mock<IPapelRepository> _papelRepository = new();
    private readonly Mock<IUsuarioLogado> _usuarioLogado = new();
    private readonly Guid _usuarioId = Guid.NewGuid();
    private readonly AtualizacaoCotacoesService _sut;
    private readonly ReceberCotacoesRequestValidator _validator = new();

    public AtualizacaoCotacoesServiceTests()
    {
        _usuarioLogado.Setup(u => u.UsuarioId).Returns(_usuarioId);
        _sut = new AtualizacaoCotacoesService(_papelRepository.Object, _usuarioLogado.Object);
    }

    private Papel CriarPapelComPosicao(string codigo, decimal quantidade, decimal cotacaoAtual = 10m, DateTime? cotacaoAtualizadaEm = null, string? fonte = null)
    {
        var papel = new Papel
        {
            Id = Guid.NewGuid(),
            UsuarioId = _usuarioId,
            Codigo = codigo,
            Nome = codigo,
            CotacaoAtual = cotacaoAtual,
            CotacaoAtualizadaEm = cotacaoAtualizadaEm,
            FonteCotacao = fonte,
            Ativo = quantidade > 0
        };

        if (quantidade > 0)
        {
            papel.Transacoes.Add(new Transacao
            {
                Id = Guid.NewGuid(),
                PapelId = papel.Id,
                Data = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
                TipoTransacao = TipoTransacao.Compra,
                Quantidade = quantidade,
                ValorUnitario = cotacaoAtual
            });
        }

        return papel;
    }

    [Fact]
    public async Task Lote_atualiza_CotacaoAtual_CotacaoAtualizadaEm_e_FonteCotacao()
    {
        var papel = CriarPapelComPosicao("PETR4", 100, cotacaoAtual: 30m);
        _papelRepository.Setup(r => r.ListarTodosDoUsuarioAsync(_usuarioId, default))
            .ReturnsAsync(new List<Papel> { papel });

        var dataCotacao = DateTime.UtcNow;
        var request = new ReceberCotacoesRequest(
            Fonte: "brapi",
            Cotacoes: new List<CotacaoRecebidaItem>
            {
                new("PETR4", 38.52m, dataCotacao)
            });

        var resultado = await _sut.ReceberLoteAsync(request);

        resultado.Recebidas.Should().Be(1);
        resultado.Atualizadas.Should().Be(1);
        resultado.NaoEncontradas.Should().BeEmpty();
        resultado.Ignoradas.Should().BeEmpty();

        papel.CotacaoAtual.Should().Be(38.52m);
        papel.CotacaoAtualizadaEm.Should().Be(dataCotacao);
        papel.FonteCotacao.Should().Be("brapi");

        _papelRepository.Verify(r => r.AtualizarAsync(papel, default), Times.Once);
    }

    [Fact]
    public async Task Codigo_com_sufixo_12_13_e_em_minusculas_e_normalizado()
    {
        var papel = CriarPapelComPosicao("GAME11", 50, cotacaoAtual: 9m);
        _papelRepository.Setup(r => r.ListarTodosDoUsuarioAsync(_usuarioId, default))
            .ReturnsAsync(new List<Papel> { papel });

        var request = new ReceberCotacoesRequest(
            Fonte: "brapi",
            Cotacoes: new List<CotacaoRecebidaItem>
            {
                new("  game12  ", 9.50m, DateTime.UtcNow)
            });

        var resultado = await _sut.ReceberLoteAsync(request);

        resultado.Atualizadas.Should().Be(1);
        papel.CotacaoAtual.Should().Be(9.50m);
    }

    [Fact]
    public async Task Cotacao_zero_negativa_ou_nula_vai_para_ignoradas_e_nao_altera_o_papel()
    {
        var papel = CriarPapelComPosicao("VALE3", 100, cotacaoAtual: 60m);
        _papelRepository.Setup(r => r.ListarTodosDoUsuarioAsync(_usuarioId, default))
            .ReturnsAsync(new List<Papel> { papel });

        var request = new ReceberCotacoesRequest(
            Fonte: "brapi",
            Cotacoes: new List<CotacaoRecebidaItem>
            {
                new("VALE3", 0m, DateTime.UtcNow),
                new("VALE3", -5m, DateTime.UtcNow),
                new("VALE3", null, DateTime.UtcNow)
            });

        var resultado = await _sut.ReceberLoteAsync(request);

        resultado.Recebidas.Should().Be(3);
        resultado.Atualizadas.Should().Be(0);
        resultado.Ignoradas.Should().HaveCount(3);
        resultado.Ignoradas.Should().OnlyContain(i => i.Motivo == "cotação inválida");

        papel.CotacaoAtual.Should().Be(60m);
        _papelRepository.Verify(r => r.AtualizarAsync(It.IsAny<Papel>(), default), Times.Never);
    }

    [Fact]
    public async Task Cotacao_com_data_anterior_a_gravada_e_ignorada()
    {
        var dataAtual = DateTime.UtcNow;
        var papel = CriarPapelComPosicao("BBSE3", 100, cotacaoAtual: 30m, cotacaoAtualizadaEm: dataAtual);
        _papelRepository.Setup(r => r.ListarTodosDoUsuarioAsync(_usuarioId, default))
            .ReturnsAsync(new List<Papel> { papel });

        var request = new ReceberCotacoesRequest(
            Fonte: "brapi",
            Cotacoes: new List<CotacaoRecebidaItem>
            {
                new("BBSE3", 32m, dataAtual.AddHours(-1))
            });

        var resultado = await _sut.ReceberLoteAsync(request);

        resultado.Atualizadas.Should().Be(0);
        resultado.Ignoradas.Should().ContainSingle(i => i.Codigo == "BBSE3");
        papel.CotacaoAtual.Should().Be(30m);
        _papelRepository.Verify(r => r.AtualizarAsync(It.IsAny<Papel>(), default), Times.Never);
    }

    [Fact]
    public async Task Codigo_inexistente_aparece_em_naoEncontradas()
    {
        _papelRepository.Setup(r => r.ListarTodosDoUsuarioAsync(_usuarioId, default))
            .ReturnsAsync(new List<Papel>());

        var request = new ReceberCotacoesRequest(
            Fonte: "brapi",
            Cotacoes: new List<CotacaoRecebidaItem>
            {
                new("ABCD3", 15m, DateTime.UtcNow)
            });

        var resultado = await _sut.ReceberLoteAsync(request);

        resultado.Recebidas.Should().Be(1);
        resultado.Atualizadas.Should().Be(0);
        resultado.NaoEncontradas.Should().ContainSingle(c => c == "ABCD3");
    }

    [Fact]
    public async Task Reenvio_do_mesmo_lote_e_idempotente()
    {
        var papel = CriarPapelComPosicao("ITUB4", 100, cotacaoAtual: 30m);
        _papelRepository.Setup(r => r.ListarTodosDoUsuarioAsync(_usuarioId, default))
            .ReturnsAsync(new List<Papel> { papel });

        var dataCotacao = DateTime.UtcNow;
        var request = new ReceberCotacoesRequest(
            Fonte: "brapi",
            Cotacoes: new List<CotacaoRecebidaItem>
            {
                new("ITUB4", 35m, dataCotacao)
            });

        var primeiroResultado = await _sut.ReceberLoteAsync(request);
        primeiroResultado.Atualizadas.Should().Be(1);
        papel.CotacaoAtual.Should().Be(35m);

        var segundoResultado = await _sut.ReceberLoteAsync(request);
        segundoResultado.Atualizadas.Should().Be(1);
        papel.CotacaoAtual.Should().Be(35m);
        papel.CotacaoAtualizadaEm.Should().Be(dataCotacao);
    }

    [Fact]
    public void Lote_vazio_e_lote_de_501_itens_falham_na_validacao()
    {
        var loteVazio = new ReceberCotacoesRequest(Fonte: "brapi", Cotacoes: new List<CotacaoRecebidaItem>());
        var validacaoVazio = _validator.Validate(loteVazio);
        validacaoVazio.IsValid.Should().BeFalse();
        validacaoVazio.Errors.Should().Contain(e => e.ErrorMessage.Contains("vazio"));

        var itens501 = Enumerable.Range(1, 501)
            .Select(i => new CotacaoRecebidaItem($"TICK{i}", 10m, DateTime.UtcNow))
            .ToList();
        var lote501 = new ReceberCotacoesRequest(Fonte: "brapi", Cotacoes: itens501);
        var validacao501 = _validator.Validate(lote501);
        validacao501.IsValid.Should().BeFalse();
        validacao501.Errors.Should().Contain(e => e.ErrorMessage.Contains("500"));

        var semFonte = new ReceberCotacoesRequest(
            Fonte: "",
            Cotacoes: new List<CotacaoRecebidaItem> { new("PETR4", 30m, DateTime.UtcNow) });
        var validacaoSemFonte = _validator.Validate(semFonte);
        validacaoSemFonte.IsValid.Should().BeFalse();
        validacaoSemFonte.Errors.Should().Contain(e => e.ErrorMessage.Contains("obrigatória"));
    }

    [Fact]
    public async Task ListarTickers_so_quantidade_maior_que_zero_e_filtro_desatualizadasHaMinutos_exclui_recem_atualizados()
    {
        var agora = DateTime.UtcNow;

        var papelComPosicao = CriarPapelComPosicao("PETR4", 100, cotacaoAtualizadaEm: agora.AddMinutes(-40));
        var papelRecemAtualizado = CriarPapelComPosicao("VALE3", 50, cotacaoAtualizadaEm: agora.AddMinutes(-10));
        var papelSemPosicao = CriarPapelComPosicao("MGLU3", 0, cotacaoAtualizadaEm: agora.AddMinutes(-60));
        var papelNuncaAtualizado = CriarPapelComPosicao("BBDC4", 80, cotacaoAtualizadaEm: null);

        _papelRepository.Setup(r => r.ListarTodosDoUsuarioAsync(_usuarioId, default))
            .ReturnsAsync(new List<Papel>
            {
                papelComPosicao,
                papelRecemAtualizado,
                papelSemPosicao,
                papelNuncaAtualizado
            });

        // Papel com aluguel (quantidade comprada 100, movimento de aluguel 50)
        var papelComAluguel = CriarPapelComPosicao("ALUG3", 100, cotacaoAtualizadaEm: null);
        papelComAluguel.MovimentosAluguel.Add(new MovimentoAluguel
        {
            Id = Guid.NewGuid(),
            PapelId = papelComAluguel.Id,
            Tipo = TipoMovimentoAluguel.Saida,
            Quantidade = 50,
            Data = DateOnly.FromDateTime(agora.AddDays(-2))
        });

        _papelRepository.Setup(r => r.ListarTodosDoUsuarioAsync(_usuarioId, default))
            .ReturnsAsync(new List<Papel>
            {
                papelComPosicao,
                papelRecemAtualizado,
                papelSemPosicao,
                papelNuncaAtualizado,
                papelComAluguel
            });

        // desatualizadasHaMinutos = 0 -> todos com posicao > 0 (incluindo papel em aluguel)
        var todosComPosicao = await _sut.ListarTickersAsync(desatualizadasHaMinutos: 0);
        todosComPosicao.Tickers.Should().BeEquivalentTo(new[] { "ALUG3", "BBDC4", "PETR4", "VALE3" });

        // desatualizadasHaMinutos = 30 -> exclui recém-atualizado (VALE3, atualizado há 10 min)
        var desatualizados30 = await _sut.ListarTickersAsync(desatualizadasHaMinutos: 30);
        desatualizados30.Tickers.Should().BeEquivalentTo(new[] { "ALUG3", "BBDC4", "PETR4" });

        // Garante que a consulta foi filtrada pelo usuarioId autenticado
        _papelRepository.Verify(r => r.ListarTodosDoUsuarioAsync(_usuarioId, default), Times.AtLeastOnce);
        var outroUsuarioId = Guid.NewGuid();
        _papelRepository.Verify(r => r.ListarTodosDoUsuarioAsync(outroUsuarioId, default), Times.Never);
    }
}
