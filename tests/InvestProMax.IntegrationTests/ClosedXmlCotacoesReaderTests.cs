using FluentAssertions;
using InvestProMax.Infrastructure.Excel;
using Xunit;

namespace InvestProMax.IntegrationTests;

public class ClosedXmlCotacoesReaderTests
{
    [Fact]
    public void Ler_ArquivoReal_AcaoXlsx()
    {
        const string caminho = @"C:\MinhasFinancas\DocInvestProMax\acao.xlsx";
        if (!File.Exists(caminho))
        {
            return;
        }

        using var fileStream = new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var memoryStream = new MemoryStream();
        fileStream.CopyTo(memoryStream);
        memoryStream.Position = 0;

        var reader = new ClosedXmlCotacoesReader();
        var resultado = reader.Ler(memoryStream);

        resultado.Should().NotBeNull();
        resultado.Should().HaveCount(51);

        var vale = resultado.FirstOrDefault(r => r.Codigo == "VALE3");
        vale.Should().NotBeNull();
        vale!.Cotacao.Should().Be(73.37m);

        var bbas = resultado.FirstOrDefault(r => r.Codigo == "BBAS3");
        bbas.Should().NotBeNull();
        bbas!.Cotacao.Should().Be(23.2m);

        var snel = resultado.FirstOrDefault(r => r.Codigo == "SNEL11");
        snel.Should().NotBeNull();
        snel!.Cotacao.Should().Be(7.96m);
    }

    [Fact]
    public void Ler_PlanilhaComCabecalhoPadrao_RetornaLinhasCorretas()
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.AddWorksheet("Cotação");
        ws.Cell(1, 1).Value = "Codigo";
        ws.Cell(1, 2).Value = "Cotacao";

        ws.Cell(2, 1).Value = "PETR4";
        ws.Cell(2, 2).Value = "38,50";

        ws.Cell(3, 1).Value = "ITUB4";
        ws.Cell(3, 2).Value = "35,20";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var reader = new ClosedXmlCotacoesReader();
        var resultado = reader.Ler(ms);

        resultado.Should().HaveCount(2);
        resultado[0].Codigo.Should().Be("PETR4");
        resultado[0].Cotacao.Should().Be(38.50m);
        resultado[1].Codigo.Should().Be("ITUB4");
        resultado[1].Cotacao.Should().Be(35.20m);
    }

    [Fact]
    public void Ler_PlanilhaSemCabecalho_RetornaLinhasCorretasDesdeLinha1()
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.AddWorksheet("Planilha1");

        ws.Cell(1, 1).Value = "VALE3";
        ws.Cell(1, 2).Value = "73,37";

        ws.Cell(2, 1).Value = "BBAS3";
        ws.Cell(2, 2).Value = "23,20";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var reader = new ClosedXmlCotacoesReader();
        var resultado = reader.Ler(ms);

        resultado.Should().HaveCount(2);
        resultado[0].Codigo.Should().Be("VALE3");
        resultado[0].Cotacao.Should().Be(73.37m);
        resultado[1].Codigo.Should().Be("BBAS3");
        resultado[1].Cotacao.Should().Be(23.20m);
    }
}
