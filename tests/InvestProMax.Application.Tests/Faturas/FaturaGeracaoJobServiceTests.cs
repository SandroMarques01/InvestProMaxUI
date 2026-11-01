using FluentAssertions;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Application.Contas;
using InvestProMax.Application.Faturas;
using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;
using Moq;
using Xunit;

namespace InvestProMax.Application.Tests.Faturas;

public class FaturaGeracaoJobServiceTests
{
    private readonly Mock<IUsuarioRepository> _usuarioRepository = new();
    private readonly Mock<IContaRepository> _contaRepository = new();
    private readonly Mock<IFaturaRepository> _faturaRepository = new();
    private readonly FaturaGeracaoJobService _sut;

    public FaturaGeracaoJobServiceTests()
    {
        _sut = new FaturaGeracaoJobService(_usuarioRepository.Object, _contaRepository.Object, _faturaRepository.Object);
    }

    [Fact]
    public async Task Executa_geracao_e_reclassificacao_para_cada_usuario_ativo()
    {
        var usuario1 = Guid.NewGuid();
        var usuario2 = Guid.NewGuid();
        _usuarioRepository.Setup(r => r.ListarIdsAtivosAsync(default)).ReturnsAsync(new List<Guid> { usuario1, usuario2 });

        var contaUsuario1 = new Conta { UsuarioId = usuario1, Nome = "Luz", ValorEstimado = 200m, DiaVencimento = 15, StatusAtivo = true };
        _contaRepository.Setup(r => r.ListarAtivasDoUsuarioAsync(usuario1, default)).ReturnsAsync(new List<Conta> { contaUsuario1 });
        _contaRepository.Setup(r => r.ListarAtivasDoUsuarioAsync(usuario2, default)).ReturnsAsync(new List<Conta>());

        _faturaRepository.Setup(r => r.ObterPorContaECompetenciaAsync(contaUsuario1.Id, It.IsAny<DateOnly>(), default)).ReturnsAsync((Fatura?)null);

        var faturaVencidaUsuario2 = new Fatura { Status = StatusFatura.Pendente, DataVencimento = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5) };
        _faturaRepository.Setup(r => r.ListarPendentesVencidasAsync(usuario1, It.IsAny<DateOnly>(), default)).ReturnsAsync(new List<Fatura>());
        _faturaRepository.Setup(r => r.ListarPendentesVencidasAsync(usuario2, It.IsAny<DateOnly>(), default))
            .ReturnsAsync(new List<Fatura> { faturaVencidaUsuario2 });

        var resultado = await _sut.ExecutarParaTodosUsuariosAsync();

        resultado.UsuariosProcessados.Should().Be(2);
        resultado.FaturasCriadas.Should().Be(1);
        resultado.FaturasReclassificadasAtrasadas.Should().Be(1);
        faturaVencidaUsuario2.Status.Should().Be(StatusFatura.Atrasada);
    }

    [Fact]
    public async Task Sem_usuarios_ativos_nao_faz_nada()
    {
        _usuarioRepository.Setup(r => r.ListarIdsAtivosAsync(default)).ReturnsAsync(new List<Guid>());

        var resultado = await _sut.ExecutarParaTodosUsuariosAsync();

        resultado.UsuariosProcessados.Should().Be(0);
        resultado.FaturasCriadas.Should().Be(0);
        resultado.FaturasReclassificadasAtrasadas.Should().Be(0);
        _contaRepository.Verify(r => r.ListarAtivasDoUsuarioAsync(It.IsAny<Guid>(), default), Times.Never);
    }
}
