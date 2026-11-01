using FluentAssertions;
using InvestProMax.Application.Contas;
using InvestProMax.Application.Faturas;
using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;
using Moq;
using Xunit;

namespace InvestProMax.Application.Tests.Faturas;

public class GeradorDeFaturasTests
{
    // ---------- Regra 7: cálculo de vencimento — caso explicitamente exigido pela seção 11 ----------

    [Theory]
    [InlineData(2026, 2, 31, 28)]  // fevereiro 2026 (não bissexto) tem 28 dias
    [InlineData(2024, 2, 31, 29)]  // 2024 é bissexto — 29 dias
    [InlineData(2026, 4, 31, 30)]  // abril tem 30 dias
    [InlineData(2026, 1, 31, 31)]  // janeiro tem 31 — não precisa ajustar
    [InlineData(2026, 6, 15, 15)]  // dia comum, dentro do mês — sem ajuste
    public void CalcularDataVencimento_ajusta_para_o_ultimo_dia_do_mes_quando_necessario(
        int ano, int mes, int diaVencimento, int diaEsperado)
    {
        var competencia = new DateOnly(ano, mes, 1);

        var resultado = GeradorDeFaturas.CalcularDataVencimento(competencia, diaVencimento);

        resultado.Should().Be(new DateOnly(ano, mes, diaEsperado));
    }

    // ---------- Regra 7: geração idempotente ----------

    [Fact]
    public async Task GerarParaUsuario_cria_fatura_pendente_para_conta_ativa_sem_fatura_na_competencia()
    {
        var usuarioId = Guid.NewGuid();
        var competencia = new DateOnly(2026, 6, 1);
        var conta = new Conta { UsuarioId = usuarioId, Nome = "Internet", ValorEstimado = 120m, DiaVencimento = 10, StatusAtivo = true };

        var contaRepository = new Mock<IContaRepository>();
        var faturaRepository = new Mock<IFaturaRepository>();
        contaRepository.Setup(r => r.ListarAtivasDoUsuarioAsync(usuarioId, default)).ReturnsAsync(new List<Conta> { conta });
        faturaRepository.Setup(r => r.ObterPorContaECompetenciaAsync(conta.Id, competencia, default)).ReturnsAsync((Fatura?)null);

        var criadas = await GeradorDeFaturas.GerarParaUsuarioAsync(
            usuarioId, competencia, contaRepository.Object, faturaRepository.Object, default);

        criadas.Should().Be(1);
        faturaRepository.Verify(r => r.AdicionarAsync(
            It.Is<Fatura>(f => f.ContaId == conta.Id && f.Status == StatusFatura.Pendente
                && f.ValorPrevisto == 120m && f.DataVencimento == new DateOnly(2026, 6, 10)), default), Times.Once);
    }

    [Fact]
    public async Task GerarParaUsuario_nao_duplica_fatura_ja_existente_na_competencia()
    {
        var usuarioId = Guid.NewGuid();
        var competencia = new DateOnly(2026, 6, 1);
        var conta = new Conta { UsuarioId = usuarioId, Nome = "Internet", ValorEstimado = 120m, DiaVencimento = 10, StatusAtivo = true };
        var faturaExistente = new Fatura { ContaId = conta.Id, Competencia = competencia };

        var contaRepository = new Mock<IContaRepository>();
        var faturaRepository = new Mock<IFaturaRepository>();
        contaRepository.Setup(r => r.ListarAtivasDoUsuarioAsync(usuarioId, default)).ReturnsAsync(new List<Conta> { conta });
        faturaRepository.Setup(r => r.ObterPorContaECompetenciaAsync(conta.Id, competencia, default)).ReturnsAsync(faturaExistente);

        var criadas = await GeradorDeFaturas.GerarParaUsuarioAsync(
            usuarioId, competencia, contaRepository.Object, faturaRepository.Object, default);

        criadas.Should().Be(0);
        faturaRepository.Verify(r => r.AdicionarAsync(It.IsAny<Fatura>(), default), Times.Never);
    }

    // ---------- Regra 8 ----------

    [Fact]
    public async Task AtualizarAtrasadas_reclassifica_faturas_pendentes_vencidas()
    {
        var usuarioId = Guid.NewGuid();
        var hoje = new DateOnly(2026, 6, 15);
        var faturaVencida = new Fatura { Status = StatusFatura.Pendente, DataVencimento = new DateOnly(2026, 6, 10) };

        var faturaRepository = new Mock<IFaturaRepository>();
        faturaRepository.Setup(r => r.ListarPendentesVencidasAsync(usuarioId, hoje, default))
            .ReturnsAsync(new List<Fatura> { faturaVencida });

        var quantidade = await GeradorDeFaturas.AtualizarAtrasadasParaUsuarioAsync(usuarioId, hoje, faturaRepository.Object, default);

        quantidade.Should().Be(1);
        faturaVencida.Status.Should().Be(StatusFatura.Atrasada);
        faturaRepository.Verify(r => r.AtualizarEmLoteAsync(It.Is<IEnumerable<Fatura>>(l => l.Contains(faturaVencida)), default), Times.Once);
    }

    [Theory]
    [InlineData(StatusFatura.Pendente, "2026-06-10", "2026-06-15", StatusFatura.Atrasada)] // vencida e ainda pendente no banco
    [InlineData(StatusFatura.Pendente, "2026-06-20", "2026-06-15", StatusFatura.Pendente)] // ainda não venceu
    [InlineData(StatusFatura.Paga, "2026-06-10", "2026-06-15", StatusFatura.Paga)]         // já paga, nunca vira atrasada
    [InlineData(StatusFatura.Cancelada, "2026-06-10", "2026-06-15", StatusFatura.Cancelada)]
    public void EfetivarStatus_projeta_atrasada_apenas_para_pendente_vencida(
        StatusFatura statusPersistido, string dataVencimento, string hoje, StatusFatura statusEsperado)
    {
        var fatura = new Fatura { Status = statusPersistido, DataVencimento = DateOnly.Parse(dataVencimento) };

        var resultado = GeradorDeFaturas.EfetivarStatus(fatura, DateOnly.Parse(hoje));

        resultado.Should().Be(statusEsperado);
    }
}
