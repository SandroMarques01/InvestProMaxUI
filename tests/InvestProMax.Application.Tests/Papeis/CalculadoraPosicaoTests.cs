using FluentAssertions;
using InvestProMax.Application.Papeis;
using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;
using Xunit;

namespace InvestProMax.Application.Tests.Papeis;

public class CalculadoraPosicaoTests
{
    private static Transacao Compra(decimal quantidade, decimal valorUnitario, DateOnly data) => new()
    {
        TipoTransacao = TipoTransacao.Compra, Quantidade = quantidade, ValorUnitario = valorUnitario, Data = data
    };

    private static Transacao Venda(decimal quantidade, decimal valorUnitario, DateOnly data) => new()
    {
        TipoTransacao = TipoTransacao.Venda, Quantidade = quantidade, ValorUnitario = valorUnitario, Data = data
    };

    [Fact]
    public void Uma_unica_compra_define_o_preco_medio_como_o_proprio_valor_unitario()
    {
        var transacoes = new[] { Compra(100, 10m, new DateOnly(2026, 1, 5)) };

        var posicao = CalculadoraPosicao.Calcular(transacoes);

        posicao.QuantidadeAtual.Should().Be(100);
        posicao.PrecoMedio.Should().Be(10m);
    }

    [Fact]
    public void Duas_compras_em_precos_diferentes_ponderam_o_preco_medio_pela_quantidade()
    {
        var transacoes = new[]
        {
            Compra(100, 10m, new DateOnly(2026, 1, 5)),
            Compra(100, 20m, new DateOnly(2026, 2, 5))
        };

        var posicao = CalculadoraPosicao.Calcular(transacoes);

        // (100*10 + 100*20) / 200 = 15
        posicao.QuantidadeAtual.Should().Be(200);
        posicao.PrecoMedio.Should().Be(15m);
    }

    [Fact]
    public void Venda_parcial_reduz_a_quantidade_mas_nao_muda_o_preco_medio()
    {
        var transacoes = new[]
        {
            Compra(100, 10m, new DateOnly(2026, 1, 5)),
            Venda(40, 50m, new DateOnly(2026, 3, 1)) // preço de venda não entra no cálculo do preço médio
        };

        var posicao = CalculadoraPosicao.Calcular(transacoes);

        posicao.QuantidadeAtual.Should().Be(60);
        posicao.PrecoMedio.Should().Be(10m);
    }

    [Fact]
    public void Venda_total_zera_quantidade_e_mantem_ultimo_preco_medio_como_no_sistema_antigo()
    {
        var transacoes = new[]
        {
            Compra(100, 10m, new DateOnly(2026, 1, 5)),
            Venda(100, 15m, new DateOnly(2026, 3, 1))
        };

        var posicao = CalculadoraPosicao.Calcular(transacoes);

        posicao.QuantidadeAtual.Should().Be(0);
        posicao.PrecoMedio.Should().Be(10m); // MinhasFinancas: posição zerada mantém o último PM
    }

    [Fact]
    public void Compra_apos_zerar_a_posicao_recomeca_o_preco_medio_do_zero()
    {
        var transacoes = new[]
        {
            Compra(100, 10m, new DateOnly(2026, 1, 5)),
            Venda(100, 15m, new DateOnly(2026, 2, 1)),
            Compra(50, 30m, new DateOnly(2026, 3, 1))
        };

        var posicao = CalculadoraPosicao.Calcular(transacoes);

        posicao.QuantidadeAtual.Should().Be(50);
        posicao.PrecoMedio.Should().Be(30m);
    }

    [Fact]
    public void Ordem_das_transacoes_na_lista_nao_importa_o_calculo_usa_a_Data()
    {
        var compra = Compra(100, 10m, new DateOnly(2026, 1, 5));
        var venda = Venda(40, 50m, new DateOnly(2026, 3, 1));

        var posicaoOrdemOriginal = CalculadoraPosicao.Calcular(new[] { compra, venda });
        var posicaoOrdemInvertida = CalculadoraPosicao.Calcular(new[] { venda, compra });

        posicaoOrdemInvertida.Should().Be(posicaoOrdemOriginal);
    }
}
