using FluentAssertions;
using InvestProMax.Application.Papeis;
using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;
using Xunit;

namespace InvestProMax.Application.Tests.Papeis;

public class CalculadoraAluguelTests
{
    private static MovimentoAluguel Mov(TipoMovimentoAluguel tipo, decimal qtd, int dia) =>
        new() { Tipo = tipo, Quantidade = qtd, Data = new DateOnly(2026, 9, dia) };

    [Fact]
    public void Saida_sem_devolucao_fica_em_aluguel()
    {
        CalculadoraAluguel.QuantidadeEmAluguel([Mov(TipoMovimentoAluguel.Saida, 100, 1)]).Should().Be(100);
    }

    [Fact]
    public void Devolucao_zera_o_aluguel()
    {
        CalculadoraAluguel.QuantidadeEmAluguel(
            [Mov(TipoMovimentoAluguel.Saida, 100, 1), Mov(TipoMovimentoAluguel.Devolucao, 100, 10)]).Should().Be(0);
    }

    [Fact]
    public void Data_final_ignora_movimentos_posteriores()
    {
        var movimentos = new[] { Mov(TipoMovimentoAluguel.Saida, 100, 1), Mov(TipoMovimentoAluguel.Devolucao, 100, 10) };
        CalculadoraAluguel.QuantidadeEmAluguel(movimentos, new DateOnly(2026, 9, 5)).Should().Be(100);
    }

    [Fact]
    public void Em_aluguel_nunca_excede_a_quantidade_em_carteira()
    {
        var papel = new Papel();
        papel.MovimentosAluguel.Add(Mov(TipoMovimentoAluguel.Saida, 100, 1));
        CalculadoraAluguel.QuantidadeEmAluguel(papel, quantidadeEmCarteira: 60).Should().Be(60);
    }
}
