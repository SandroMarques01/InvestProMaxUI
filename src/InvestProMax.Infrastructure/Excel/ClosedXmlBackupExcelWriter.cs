using ClosedXML.Excel;
using InvestProMax.Application.Configuracao;

namespace InvestProMax.Infrastructure.Excel;

public class ClosedXmlBackupExcelWriter : IBackupExcelWriter
{
    public byte[] Gerar(BackupDados dados)
    {
        using var workbook = new XLWorkbook();

        var wsPapel = workbook.Worksheets.Add("Papel");
        string[] cabecalhoPapel = ["Id", "Codigo", "Nome", "TipoPapel", "CotacaoAtual", "Descricao", "Ativo", "Segmentos"];
        EscreverCabecalho(wsPapel, cabecalhoPapel);
        var linha = 2;
        foreach (var p in dados.Papeis)
        {
            wsPapel.Cell(linha, 1).Value = p.Id.ToString();
            wsPapel.Cell(linha, 2).Value = p.Codigo;
            wsPapel.Cell(linha, 3).Value = p.Nome;
            wsPapel.Cell(linha, 4).Value = p.TipoPapel.ToString();
            wsPapel.Cell(linha, 5).Value = p.CotacaoAtual;
            wsPapel.Cell(linha, 6).Value = p.Descricao ?? "";
            wsPapel.Cell(linha, 7).Value = p.Ativo;
            wsPapel.Cell(linha, 8).Value = string.Join(", ", p.SegmentoNomes);
            linha++;
        }

        var wsTransacao = workbook.Worksheets.Add("Transação");
        string[] cabecalhoTransacao = ["Id", "PapelCodigo", "TipoTransacao", "Quantidade", "ValorUnitario", "Data", "Descricao", "Historico"];
        EscreverCabecalho(wsTransacao, cabecalhoTransacao);
        linha = 2;
        foreach (var t in dados.Transacoes)
        {
            wsTransacao.Cell(linha, 1).Value = t.Id.ToString();
            wsTransacao.Cell(linha, 2).Value = t.PapelCodigo;
            wsTransacao.Cell(linha, 3).Value = t.TipoTransacao.ToString();
            wsTransacao.Cell(linha, 4).Value = t.Quantidade;
            wsTransacao.Cell(linha, 5).Value = t.ValorUnitario;
            wsTransacao.Cell(linha, 6).Value = t.Data.ToDateTime(TimeOnly.MinValue);
            wsTransacao.Cell(linha, 7).Value = t.Descricao ?? "";
            wsTransacao.Cell(linha, 8).Value = t.Historico;
            linha++;
        }

        var wsDividendo = workbook.Worksheets.Add("Dividendo");
        string[] cabecalhoDividendo = ["Id", "PapelCodigo", "TipoDividendo", "ValorRecebido", "Quantidade", "Data", "Descricao", "Historico"];
        EscreverCabecalho(wsDividendo, cabecalhoDividendo);
        linha = 2;
        foreach (var d in dados.Dividendos)
        {
            wsDividendo.Cell(linha, 1).Value = d.Id.ToString();
            wsDividendo.Cell(linha, 2).Value = d.PapelCodigo;
            wsDividendo.Cell(linha, 3).Value = d.TipoDividendo.ToString();
            wsDividendo.Cell(linha, 4).Value = d.ValorRecebido;
            wsDividendo.Cell(linha, 5).Value = d.Quantidade;
            wsDividendo.Cell(linha, 6).Value = d.Data.ToDateTime(TimeOnly.MinValue);
            wsDividendo.Cell(linha, 7).Value = d.Descricao ?? "";
            wsDividendo.Cell(linha, 8).Value = d.Historico;
            linha++;
        }

        var wsAluguel = workbook.Worksheets.Add("Aluguel");
        string[] cabecalhoAluguel = ["Id", "PapelCodigo", "Tipo", "Quantidade", "Data", "Descricao"];
        EscreverCabecalho(wsAluguel, cabecalhoAluguel);
        linha = 2;
        foreach (var a in dados.Alugueis ?? [])
        {
            wsAluguel.Cell(linha, 1).Value = a.Id.ToString();
            wsAluguel.Cell(linha, 2).Value = a.PapelCodigo;
            wsAluguel.Cell(linha, 3).Value = a.Tipo.ToString();
            wsAluguel.Cell(linha, 4).Value = a.Quantidade;
            wsAluguel.Cell(linha, 5).Value = a.Data.ToDateTime(TimeOnly.MinValue);
            wsAluguel.Cell(linha, 6).Value = a.Descricao ?? "";
            linha++;
        }

        using var memoria = new MemoryStream();
        workbook.SaveAs(memoria);
        return memoria.ToArray();
    }

    private static void EscreverCabecalho(IXLWorksheet ws, string[] colunas)
    {
        for (var i = 0; i < colunas.Length; i++)
        {
            ws.Cell(1, i + 1).Value = colunas[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
        }
    }
}
