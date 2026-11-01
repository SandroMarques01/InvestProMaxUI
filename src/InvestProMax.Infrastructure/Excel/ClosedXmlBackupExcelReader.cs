using System.Globalization;
using ClosedXML.Excel;
using InvestProMax.Application.Configuracao;
using InvestProMax.Domain.Enums;

namespace InvestProMax.Infrastructure.Excel;

public class ClosedXmlBackupExcelReader : IBackupExcelReader
{
    private static readonly CultureInfo CulturaPtBr = CultureInfo.GetCultureInfo("pt-BR");

    public BackupDados Ler(Stream planilha)
    {
        using var workbook = new XLWorkbook(planilha);

        return new BackupDados(
            LerPapeis(RequireSheet(workbook, "Papel")),
            LerTransacoes(RequireSheet(workbook, "Transação")),
            LerDividendos(RequireSheet(workbook, "Dividendo")),
            // Aba opcional: backups gerados antes do suporte a aluguel não a possuem.
            workbook.Worksheets.TryGetWorksheet("Aluguel", out var wsAluguel) ? LerAlugueis(wsAluguel) : []);
    }

    private static IXLWorksheet RequireSheet(XLWorkbook workbook, string nome) =>
        workbook.Worksheets.TryGetWorksheet(nome, out var ws)
            ? ws
            : throw new InvalidDataException($"A planilha de backup não tem a aba obrigatória '{nome}'.");

    private static List<LinhaBackupPapel> LerPapeis(IXLWorksheet ws)
    {
        var colunas = LeitorDePlanilha.MapearCabecalhos(ws.Row(1));
        var colId = LeitorDePlanilha.ColunaObrigatoria(colunas, "Id");
        var colCodigo = LeitorDePlanilha.ColunaObrigatoria(colunas, "Codigo");
        var colNome = LeitorDePlanilha.ColunaObrigatoria(colunas, "Nome");
        var colTipo = LeitorDePlanilha.ColunaObrigatoria(colunas, "TipoPapel");
        var colCotacao = LeitorDePlanilha.ColunaObrigatoria(colunas, "CotacaoAtual");
        var colDescricao = LeitorDePlanilha.ColunaOpcional(colunas, "Descricao");
        var colAtivo = LeitorDePlanilha.ColunaObrigatoria(colunas, "Ativo");
        var colSegmentos = LeitorDePlanilha.ColunaOpcional(colunas, "Segmentos");

        var resultado = new List<LinhaBackupPapel>();
        var ultimaLinha = ws.LastRowUsed()?.RowNumber() ?? 1;

        for (var linha = 2; linha <= ultimaLinha; linha++)
        {
            var row = ws.Row(linha);
            if (row.IsEmpty())
            {
                continue;
            }

            var segmentos = colSegmentos is null ? "" : row.Cell(colSegmentos.Value).GetString();

            resultado.Add(new LinhaBackupPapel(
                Guid.Parse(row.Cell(colId).GetString()),
                row.Cell(colCodigo).GetString().Trim(),
                row.Cell(colNome).GetString().Trim(),
                Enum.Parse<TipoPapel>(row.Cell(colTipo).GetString().Trim(), ignoreCase: true),
                LeitorDePlanilha.LerDecimal(row.Cell(colCotacao), CulturaPtBr),
                colDescricao is null ? null : NuloSeVazio(row.Cell(colDescricao.Value).GetString()),
                row.Cell(colAtivo).GetBoolean(),
                segmentos.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)));
        }

        return resultado;
    }

    private static List<LinhaBackupTransacao> LerTransacoes(IXLWorksheet ws)
    {
        var colunas = LeitorDePlanilha.MapearCabecalhos(ws.Row(1));
        var colId = LeitorDePlanilha.ColunaObrigatoria(colunas, "Id");
        var colPapelCodigo = LeitorDePlanilha.ColunaObrigatoria(colunas, "PapelCodigo");
        var colTipo = LeitorDePlanilha.ColunaObrigatoria(colunas, "TipoTransacao");
        var colQuantidade = LeitorDePlanilha.ColunaObrigatoria(colunas, "Quantidade");
        var colValorUnitario = LeitorDePlanilha.ColunaObrigatoria(colunas, "ValorUnitario");
        var colData = LeitorDePlanilha.ColunaObrigatoria(colunas, "Data");
        var colDescricao = LeitorDePlanilha.ColunaOpcional(colunas, "Descricao");
        var colHistorico = LeitorDePlanilha.ColunaObrigatoria(colunas, "Historico");

        var resultado = new List<LinhaBackupTransacao>();
        var ultimaLinha = ws.LastRowUsed()?.RowNumber() ?? 1;

        for (var linha = 2; linha <= ultimaLinha; linha++)
        {
            var row = ws.Row(linha);
            if (row.IsEmpty())
            {
                continue;
            }

            resultado.Add(new LinhaBackupTransacao(
                Guid.Parse(row.Cell(colId).GetString()),
                row.Cell(colPapelCodigo).GetString().Trim(),
                Enum.Parse<TipoTransacao>(row.Cell(colTipo).GetString().Trim(), ignoreCase: true),
                LeitorDePlanilha.LerDecimal(row.Cell(colQuantidade), CulturaPtBr),
                LeitorDePlanilha.LerDecimal(row.Cell(colValorUnitario), CulturaPtBr),
                LeitorDePlanilha.LerData(row.Cell(colData), CulturaPtBr),
                colDescricao is null ? null : NuloSeVazio(row.Cell(colDescricao.Value).GetString()),
                row.Cell(colHistorico).GetBoolean()));
        }

        return resultado;
    }

    private static List<LinhaBackupAluguel> LerAlugueis(IXLWorksheet ws)
    {
        var colunas = LeitorDePlanilha.MapearCabecalhos(ws.Row(1));
        var colId = LeitorDePlanilha.ColunaObrigatoria(colunas, "Id");
        var colPapelCodigo = LeitorDePlanilha.ColunaObrigatoria(colunas, "PapelCodigo");
        var colTipo = LeitorDePlanilha.ColunaObrigatoria(colunas, "Tipo");
        var colQuantidade = LeitorDePlanilha.ColunaObrigatoria(colunas, "Quantidade");
        var colData = LeitorDePlanilha.ColunaObrigatoria(colunas, "Data");
        var colDescricao = LeitorDePlanilha.ColunaOpcional(colunas, "Descricao");

        var resultado = new List<LinhaBackupAluguel>();
        var ultimaLinha = ws.LastRowUsed()?.RowNumber() ?? 1;

        for (var linha = 2; linha <= ultimaLinha; linha++)
        {
            var row = ws.Row(linha);
            if (row.IsEmpty())
            {
                continue;
            }

            resultado.Add(new LinhaBackupAluguel(
                Guid.Parse(row.Cell(colId).GetString()),
                row.Cell(colPapelCodigo).GetString().Trim(),
                Enum.Parse<TipoMovimentoAluguel>(row.Cell(colTipo).GetString().Trim(), ignoreCase: true),
                LeitorDePlanilha.LerDecimal(row.Cell(colQuantidade), CulturaPtBr),
                LeitorDePlanilha.LerData(row.Cell(colData), CulturaPtBr),
                colDescricao is null ? null : NuloSeVazio(row.Cell(colDescricao.Value).GetString())));
        }

        return resultado;
    }

    private static List<LinhaBackupDividendo> LerDividendos(IXLWorksheet ws)
    {
        var colunas = LeitorDePlanilha.MapearCabecalhos(ws.Row(1));
        var colId = LeitorDePlanilha.ColunaObrigatoria(colunas, "Id");
        var colPapelCodigo = LeitorDePlanilha.ColunaObrigatoria(colunas, "PapelCodigo");
        var colTipo = LeitorDePlanilha.ColunaObrigatoria(colunas, "TipoDividendo");
        var colValorRecebido = LeitorDePlanilha.ColunaObrigatoria(colunas, "ValorRecebido");
        var colQuantidade = LeitorDePlanilha.ColunaObrigatoria(colunas, "Quantidade");
        var colData = LeitorDePlanilha.ColunaObrigatoria(colunas, "Data");
        var colDescricao = LeitorDePlanilha.ColunaOpcional(colunas, "Descricao");
        var colHistorico = LeitorDePlanilha.ColunaObrigatoria(colunas, "Historico");

        var resultado = new List<LinhaBackupDividendo>();
        var ultimaLinha = ws.LastRowUsed()?.RowNumber() ?? 1;

        for (var linha = 2; linha <= ultimaLinha; linha++)
        {
            var row = ws.Row(linha);
            if (row.IsEmpty())
            {
                continue;
            }

            resultado.Add(new LinhaBackupDividendo(
                Guid.Parse(row.Cell(colId).GetString()),
                row.Cell(colPapelCodigo).GetString().Trim(),
                Enum.Parse<TipoDividendo>(row.Cell(colTipo).GetString().Trim(), ignoreCase: true),
                LeitorDePlanilha.LerDecimal(row.Cell(colValorRecebido), CulturaPtBr),
                LeitorDePlanilha.LerDecimal(row.Cell(colQuantidade), CulturaPtBr),
                LeitorDePlanilha.LerData(row.Cell(colData), CulturaPtBr),
                colDescricao is null ? null : NuloSeVazio(row.Cell(colDescricao.Value).GetString()),
                row.Cell(colHistorico).GetBoolean()));
        }

        return resultado;
    }

    private static string? NuloSeVazio(string texto) => texto.Trim() is { Length: > 0 } t ? t : null;
}
