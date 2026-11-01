using FluentAssertions;
using InvestProMax.Application.Ativos;
using InvestProMax.Application.Common.Exceptions;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Application.Inventario;
using InvestProMax.Application.Papeis;
using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;
using Moq;
using Xunit;
using InventarioEntidade = InvestProMax.Domain.Entidades.Inventario;

namespace InvestProMax.Application.Tests.Inventario;

public class InventarioServiceTests
{
    private readonly Mock<IInventarioRepository> _inventarioRepository = new();
    private readonly Mock<IAtivoRepository> _ativoRepository = new();
    private readonly Mock<IPapelRepository> _papelRepository = new();
    private readonly Mock<IUsuarioLogado> _usuarioLogado = new();
    private readonly Guid _usuarioId = Guid.NewGuid();
    private readonly InventarioService _sut;

    public InventarioServiceTests()
    {
        _usuarioLogado.Setup(u => u.UsuarioId).Returns(_usuarioId);
        _sut = new InventarioService(_inventarioRepository.Object, _ativoRepository.Object, _papelRepository.Object, _usuarioLogado.Object);
    }

    private Ativo NovoAtivo(TipoOrigemAtivo origem = TipoOrigemAtivo.Manual) =>
        new() { UsuarioId = _usuarioId, Nome = "Poupança", TipoAtivo = TipoAtivo.RendaFixa, TipoOrigem = origem };

    [Fact]
    public async Task LancarLote_para_ativo_sem_lancamento_no_mes_cria_um_novo()
    {
        var ativo = NovoAtivo();
        _ativoRepository.Setup(r => r.ObterPorIdAsync(ativo.Id, _usuarioId, default)).ReturnsAsync(ativo);
        _inventarioRepository.Setup(r => r.ObterPorAtivoEMesAsync(ativo.Id, 2026, 6, default)).ReturnsAsync((InventarioEntidade?)null);

        var resposta = await _sut.LancarLoteAsync(new LancamentoLoteRequest(
            2026, 6, [new ItemLancamentoLoteRequest(ativo.Id, 15000m, null)]));

        resposta.Criados.Should().Be(1);
        resposta.Atualizados.Should().Be(0);
        _inventarioRepository.Verify(r => r.AdicionarAsync(
            It.Is<InventarioEntidade>(i => i.AtivoId == ativo.Id && i.Valor == 15000m && i.Data == new DateOnly(2026, 6, 1)), default), Times.Once);
    }

    [Fact]
    public async Task LancarLote_para_ativo_com_lancamento_existente_no_mes_atualiza_em_vez_de_duplicar()
    {
        var ativo = NovoAtivo();
        var existente = new InventarioEntidade { AtivoId = ativo.Id, Valor = 1000m, Data = new DateOnly(2026, 6, 1) };

        _ativoRepository.Setup(r => r.ObterPorIdAsync(ativo.Id, _usuarioId, default)).ReturnsAsync(ativo);
        _inventarioRepository.Setup(r => r.ObterPorAtivoEMesAsync(ativo.Id, 2026, 6, default)).ReturnsAsync(existente);

        var resposta = await _sut.LancarLoteAsync(new LancamentoLoteRequest(
            2026, 6, [new ItemLancamentoLoteRequest(ativo.Id, 1500m, "Ajustado")]));

        resposta.Criados.Should().Be(0);
        resposta.Atualizados.Should().Be(1);
        existente.Valor.Should().Be(1500m);
        _inventarioRepository.Verify(r => r.AdicionarAsync(It.IsAny<InventarioEntidade>(), default), Times.Never);
    }

    [Fact]
    public async Task LancarLote_com_ativo_que_nao_pertence_ao_usuario_lanca_NaoEncontrado()
    {
        var ativoId = Guid.NewGuid();
        _ativoRepository.Setup(r => r.ObterPorIdAsync(ativoId, _usuarioId, default)).ReturnsAsync((Ativo?)null);

        var acao = () => _sut.LancarLoteAsync(new LancamentoLoteRequest(2026, 6, [new ItemLancamentoLoteRequest(ativoId, 100m, null)]));

        await acao.Should().ThrowAsync<NaoEncontradoException>();
    }

    [Fact]
    public async Task SugestaoCalculada_soma_quantidade_vezes_cotacao_dos_papeis_do_tipo_vinculado_regra_6()
    {
        var ativoCalculado = NovoAtivo(TipoOrigemAtivo.Calculado);
        ativoCalculado.TipoPapelVinculado = TipoPapel.FII;

        var papel1 = new Papel { UsuarioId = _usuarioId, Codigo = "HGLG11", CotacaoAtual = 170m };
        papel1.Transacoes.Add(new Transacao { PapelId = papel1.Id, TipoTransacao = TipoTransacao.Compra, Quantidade = 10, ValorUnitario = 150m, Data = new DateOnly(2026, 1, 1) });

        var papel2 = new Papel { UsuarioId = _usuarioId, Codigo = "MXRF11", CotacaoAtual = 10m };
        papel2.Transacoes.Add(new Transacao { PapelId = papel2.Id, TipoTransacao = TipoTransacao.Compra, Quantidade = 500, ValorUnitario = 9m, Data = new DateOnly(2026, 1, 1) });

        _ativoRepository.Setup(r => r.ListarPorOrigemAsync(_usuarioId, TipoOrigemAtivo.Calculado, default))
            .ReturnsAsync(new List<Ativo> { ativoCalculado });
        _papelRepository.Setup(r => r.ListarPorTipoAsync(_usuarioId, TipoPapel.FII, default))
            .ReturnsAsync(new List<Papel> { papel1, papel2 });

        var resposta = await _sut.ObterSugestaoCalculadaAsync(2026, 6);

        resposta.Itens.Should().ContainSingle();
        // 10 * 170 + 500 * 10 = 1700 + 5000 = 6700
        resposta.Itens[0].ValorSugerido.Should().Be(6700m);
        resposta.Itens[0].AtivoId.Should().Be(ativoCalculado.Id);
    }

    [Fact]
    public async Task SugestaoCalculada_sem_nenhum_ativo_calculado_retorna_lista_vazia()
    {
        _ativoRepository.Setup(r => r.ListarPorOrigemAsync(_usuarioId, TipoOrigemAtivo.Calculado, default))
            .ReturnsAsync(new List<Ativo>());

        var resposta = await _sut.ObterSugestaoCalculadaAsync(2026, 6);

        resposta.Itens.Should().BeEmpty();
        _papelRepository.Verify(r => r.ListarPorTipoAsync(It.IsAny<Guid>(), It.IsAny<TipoPapel>(), default), Times.Never);
    }
}
