using System.Globalization;
using ClosedXML.Excel;
using InvestProMax.Application.Configuracao;

namespace InvestProMax.Infrastructure.Excel;

/// <summary>
/// Lê o extrato de movimentação da B3 (regra 5). Espera os cabeçalhos do relatório real
/// exportado pela Área do Investidor: Entrada/Saída, Data, Movimentação, Produto, Quantidade,
/// Preço unitário, Valor da Operação (a coluna Instituição, se presente, é ignorada). A busca
/// de coluna é por nome de cabeçalho, não por posição, então a ordem das colunas não importa.
/// </summary>
public class ClosedXmlExtratoB3Reader : IExtratoB3Reader
{
    private static readonly CultureInfo CulturaPtBr = CultureInfo.GetCultureInfo("pt-BR");

    public IReadOnlyList<LinhaExtratoB3> Ler(Stream planilha)
    {
        using var workbook = new XLWorkbook(planilha);
        var ws = workbook.Worksheet(1);
        var colunas = LeitorDePlanilha.MapearCabecalhos(ws.Row(1));

        var colEntradaSaida = LeitorDePlanilha.ColunaObrigatoria(colunas, "Entrada/Saída");
        var colData = LeitorDePlanilha.ColunaObrigatoria(colunas, "Data");
        var colMovimentacao = LeitorDePlanilha.ColunaObrigatoria(colunas, "Movimentação");
        var colProduto = LeitorDePlanilha.ColunaObrigatoria(colunas, "Produto");
        var colQuantidade = LeitorDePlanilha.ColunaObrigatoria(colunas, "Quantidade");
        var colPrecoUnitario = LeitorDePlanilha.ColunaOpcional(colunas, "Preço unitário");
        var colValorOperacao = LeitorDePlanilha.ColunaObrigatoria(colunas, "Valor da Operação");

        var linhas = new List<LinhaExtratoB3>();
        var ultimaLinha = ws.LastRowUsed()?.RowNumber() ?? 1;

        for (var linha = 2; linha <= ultimaLinha; linha++)
        {
            var row = ws.Row(linha);
            if (row.IsEmpty())
            {
                continue;
            }

            var quantidade = LeitorDePlanilha.LerDecimalOpcional(row.Cell(colQuantidade), CulturaPtBr) ?? 0m;
            var precoUnitario = colPrecoUnitario is null ? null : LeitorDePlanilha.LerDecimalOpcional(row.Cell(colPrecoUnitario.Value), CulturaPtBr);
            var valorOperacao = LeitorDePlanilha.LerDecimalOpcional(row.Cell(colValorOperacao), CulturaPtBr) ?? 0m;

            linhas.Add(new LinhaExtratoB3(
                EntradaSaida: row.Cell(colEntradaSaida).GetString().Trim(),
                Data: LeitorDePlanilha.LerData(row.Cell(colData), CulturaPtBr),
                Movimentacao: row.Cell(colMovimentacao).GetString().Trim(),
                Produto: row.Cell(colProduto).GetString().Trim(),
                Quantidade: quantidade,
                PrecoUnitario: precoUnitario,
                ValorOperacao: valorOperacao));
        }

        return linhas;
    }
}
