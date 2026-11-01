using FluentAssertions;
using InvestProMax.Application.Ativos;
using InvestProMax.Application.Common.Exceptions;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;
using Moq;
using Xunit;

namespace InvestProMax.Application.Tests.Ativos;

public class AtivoServiceTests
{
    private readonly Mock<IAtivoRepository> _repository = new();
    private readonly Mock<IUsuarioLogado> _usuarioLogado = new();
    private readonly Guid _usuarioId = Guid.NewGuid();
    private readonly AtivoService _sut;

    public AtivoServiceTests()
    {
        _usuarioLogado.Setup(u => u.UsuarioId).Returns(_usuarioId);
        _sut = new AtivoService(_repository.Object, _usuarioLogado.Object);
    }

    [Fact]
    public async Task Remover_ativo_com_historico_de_inventario_lanca_RegraDeNegocio()
    {
        var ativo = new Ativo { UsuarioId = _usuarioId, Nome = "Poupança" };
        ativo.HistoricoInventario.Add(new InvestProMax.Domain.Entidades.Inventario { AtivoId = ativo.Id, Valor = 100, Data = new DateOnly(2026, 1, 1) });
        _repository.Setup(r => r.ObterPorIdAsync(ativo.Id, _usuarioId, default)).ReturnsAsync(ativo);

        var acao = () => _sut.RemoverAsync(ativo.Id);

        await acao.Should().ThrowAsync<RegraDeNegocioException>();
        _repository.Verify(r => r.RemoverAsync(It.IsAny<Ativo>(), default), Times.Never);
    }

    [Fact]
    public async Task Remover_ativo_sem_historico_remove_normalmente()
    {
        var ativo = new Ativo { UsuarioId = _usuarioId, Nome = "Poupança" };
        _repository.Setup(r => r.ObterPorIdAsync(ativo.Id, _usuarioId, default)).ReturnsAsync(ativo);

        await _sut.RemoverAsync(ativo.Id);

        _repository.Verify(r => r.RemoverAsync(ativo, default), Times.Once);
    }
}

public class AtivoValidatorsTests
{
    private readonly CriarAtivoRequestValidator _sut = new();

    [Fact]
    public void Origem_Calculado_sem_TipoPapelVinculado_e_invalido()
    {
        var request = new CriarAtivoRequest("Ações via carteira", TipoAtivo.RendaVariavel, TipoOrigemAtivo.Calculado, null, null);

        var resultado = _sut.Validate(request);

        resultado.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Origem_Manual_com_TipoPapelVinculado_preenchido_e_invalido()
    {
        var request = new CriarAtivoRequest("Poupança", TipoAtivo.RendaFixa, TipoOrigemAtivo.Manual, TipoPapel.Acao, null);

        var resultado = _sut.Validate(request);

        resultado.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Origem_Calculado_com_TipoPapelVinculado_preenchido_e_valido()
    {
        var request = new CriarAtivoRequest("Ações via carteira", TipoAtivo.RendaVariavel, TipoOrigemAtivo.Calculado, TipoPapel.Acao, null);

        var resultado = _sut.Validate(request);

        resultado.IsValid.Should().BeTrue();
    }
}
