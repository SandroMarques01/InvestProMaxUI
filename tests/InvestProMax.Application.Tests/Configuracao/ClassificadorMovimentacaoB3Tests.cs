using FluentAssertions;
using InvestProMax.Application.Configuracao;
using InvestProMax.Domain.Enums;
using Xunit;

namespace InvestProMax.Application.Tests.Configuracao;

public class ClassificadorMovimentacaoB3Tests
{
    [Theory]
    [InlineData("Dividendo", TipoDividendo.Dividendo)]
    [InlineData("Juros Sobre Capital Próprio", TipoDividendo.JSCP)]
    [InlineData("JSCP", TipoDividendo.JSCP)]
    [InlineData("Rendimento", TipoDividendo.Rendimento)]
    [InlineData("Restituição de Capital", TipoDividendo.Outro)]
    public void Tipos_de_provento_sao_classificados_como_Dividendo(string movimentacao, TipoDividendo esperado)
    {
        var resultado = ClassificadorMovimentacaoB3.Classificar(movimentacao);

        resultado.Tipo.Should().Be(TipoRegistroImportado.Dividendo);
        resultado.TipoDividendo.Should().Be(esperado);
    }

    [Fact]
    public void Transferencia_liquidacao_e_classificada_como_transacao_sem_direcao_fixa()
    {
        var resultado = ClassificadorMovimentacaoB3.Classificar("Transferência - Liquidação");

        resultado.Tipo.Should().Be(TipoRegistroImportado.Transacao);
        resultado.TipoTransacao.Should().BeNull(); // direção vem da coluna Entrada/Saída, não do tipo
    }

    [Theory]
    [InlineData("Bonificação em Ativos", TipoTransacao.Compra, true)]
    [InlineData("Desdobro", TipoTransacao.Compra, true)]
    [InlineData("Grupamento", TipoTransacao.Venda, true)]
    [InlineData("Solicitação de Subscrição", TipoTransacao.Compra, false)]
    [InlineData("Leilão de Fração", TipoTransacao.Venda, false)]
    public void Eventos_de_quantidade_sao_classificados_como_transacao_com_direcao_fixa(
        string movimentacao, TipoTransacao direcaoEsperada, bool valorZeradoEsperado)
    {
        var resultado = ClassificadorMovimentacaoB3.Classificar(movimentacao);

        resultado.Tipo.Should().Be(TipoRegistroImportado.Transacao);
        resultado.TipoTransacao.Should().Be(direcaoEsperada);
        resultado.ValorUnitarioZerado.Should().Be(valorZeradoEsperado);
    }

    [Fact]
    public void Tipo_de_movimentacao_desconhecido_e_ignorado_em_vez_de_falhar()
    {
        var resultado = ClassificadorMovimentacaoB3.Classificar("Um Tipo Que Não Existe");

        resultado.Tipo.Should().Be(TipoRegistroImportado.Ignorar);
    }

    [Theory]
    [InlineData("Credito", TipoTransacao.Compra)]
    [InlineData("Debito", TipoTransacao.Venda)]
    public void Direcao_por_entrada_saida(string entradaSaida, TipoTransacao esperado)
    {
        ClassificadorMovimentacaoB3.DirecaoPorEntradaSaida(entradaSaida).Should().Be(esperado);
    }
}
