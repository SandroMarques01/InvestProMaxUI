using FluentAssertions;
using InvestProMax.Application.Common.Exceptions;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Application.Contas;
using InvestProMax.Domain.Entidades;
using Moq;
using Xunit;

namespace InvestProMax.Application.Tests.Contas;

public class ContaServiceTests
{
    private readonly Mock<IContaRepository> _repository = new();
    private readonly Mock<IUsuarioLogado> _usuarioLogado = new();
    private readonly Guid _usuarioId = Guid.NewGuid();
    private readonly ContaService _sut;

    public ContaServiceTests()
    {
        _usuarioLogado.Setup(u => u.UsuarioId).Returns(_usuarioId);
        _sut = new ContaService(_repository.Object, _usuarioLogado.Object);
    }

    [Fact]
    public async Task Remover_conta_com_faturas_lanca_RegraDeNegocio()
    {
        var conta = new Conta { UsuarioId = _usuarioId, Nome = "Aluguel" };
        conta.Faturas.Add(new Fatura { ContaId = conta.Id, Competencia = new DateOnly(2026, 6, 1), DataVencimento = new DateOnly(2026, 6, 5) });
        _repository.Setup(r => r.ObterPorIdAsync(conta.Id, _usuarioId, default)).ReturnsAsync(conta);

        var acao = () => _sut.RemoverAsync(conta.Id);

        await acao.Should().ThrowAsync<RegraDeNegocioException>();
        _repository.Verify(r => r.RemoverAsync(It.IsAny<Conta>(), default), Times.Never);
    }

    [Fact]
    public async Task Remover_conta_sem_faturas_remove_normalmente()
    {
        var conta = new Conta { UsuarioId = _usuarioId, Nome = "Aluguel" };
        _repository.Setup(r => r.ObterPorIdAsync(conta.Id, _usuarioId, default)).ReturnsAsync(conta);

        await _sut.RemoverAsync(conta.Id);

        _repository.Verify(r => r.RemoverAsync(conta, default), Times.Once);
    }
}
