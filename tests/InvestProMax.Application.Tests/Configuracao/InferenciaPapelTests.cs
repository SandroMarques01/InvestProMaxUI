using FluentAssertions;
using InvestProMax.Application.Configuracao;
using Xunit;

namespace InvestProMax.Application.Tests.Configuracao;

public class InferenciaPapelTests
{
    [Theory]
    [InlineData("GAME12 - GAMEFI", "GAME11")]
    [InlineData("XPCA13 - XP CREDITO", "XPCA11")]
    [InlineData("PETR4 - PETROBRAS", "PETR4")]
    [InlineData("A1LB34 - ALIBABA", "A1LB34")]   // "13/12" no meio do código não muda
    public void Codigos_com_sufixo_12_ou_13_viram_11_como_no_sistema_antigo(string produto, string esperado)
    {
        InferenciaPapel.SepararProduto(produto).Codigo.Should().Be(esperado);
    }
}
