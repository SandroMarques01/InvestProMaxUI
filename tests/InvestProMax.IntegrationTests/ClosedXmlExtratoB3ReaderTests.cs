using FluentAssertions;
using InvestProMax.Infrastructure.Excel;
using Xunit;

namespace InvestProMax.IntegrationTests;

public class ClosedXmlExtratoB3ReaderTests
{
    [Fact]
    public void Ler_PlanilhaComTracosEHifens_ConverteCorretamente()
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.AddWorksheet("Extrato");
        ws.Cell(1, 1).Value = "Entrada/Saída";
        ws.Cell(1, 2).Value = "Data";
        ws.Cell(1, 3).Value = "Movimentação";
        ws.Cell(1, 4).Value = "Produto";
        ws.Cell(1, 5).Value = "Quantidade";
        ws.Cell(1, 6).Value = "Preço unitário";
        ws.Cell(1, 7).Value = "Valor da Operação";

        ws.Cell(2, 1).Value = "Credito";
        ws.Cell(2, 2).Value = "15/05/2026";
        ws.Cell(2, 3).Value = "Rendimento";
        ws.Cell(2, 4).Value = "HGLG11 - CSHG LOGISTICA FII";
        ws.Cell(2, 5).Value = "-";
        ws.Cell(2, 6).Value = "-";
        ws.Cell(2, 7).Value = "150,75";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var reader = new ClosedXmlExtratoB3Reader();
        var resultado = reader.Ler(ms);

        resultado.Should().HaveCount(1);
        resultado[0].Quantidade.Should().Be(0m);
        resultado[0].PrecoUnitario.Should().BeNull();
        resultado[0].ValorOperacao.Should().Be(150.75m);
        resultado[0].Data.Should().Be(new DateOnly(2026, 5, 15));
    }

    [Fact]
    public void Ler_ArquivoReal_Movimentacao20_SeExistir()
    {
        const string caminho = @"C:\MinhasFinancas\excel\movimentacao20.xlsx";
        if (!File.Exists(caminho))
        {
            return;
        }

        var reader = new ClosedXmlExtratoB3Reader();
        using var stream = File.OpenRead(caminho);
        var linhas = reader.Ler(stream);

        linhas.Should().NotBeNull();
        linhas.Should().NotBeEmpty();
    }
}
