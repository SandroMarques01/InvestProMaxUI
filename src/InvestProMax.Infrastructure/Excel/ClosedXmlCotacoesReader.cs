using System.Globalization;
using ClosedXML.Excel;
using InvestProMax.Application.Configuracao;

namespace InvestProMax.Infrastructure.Excel;

public class ClosedXmlCotacoesReader : ICotacoesReader
{
    private static readonly CultureInfo CulturaPtBr = CultureInfo.GetCultureInfo("pt-BR");

    public IReadOnlyList<LinhaCotacao> Ler(Stream planilha)
    {
        using var workbook = new XLWorkbook(planilha);
        var ws = workbook.Worksheets.FirstOrDefault(w =>
            w.Name.Equals("Cotação", StringComparison.OrdinalIgnoreCase) ||
            w.Name.Equals("Cotacao", StringComparison.OrdinalIgnoreCase)) ?? workbook.Worksheet(1);

        var ultimaLinha = ws.LastRowUsed()?.RowNumber() ?? 0;
        if (ultimaLinha == 0)
        {
            return Array.Empty<LinhaCotacao>();
        }

        var colCodigo = 1;
        var colCotacao = 2;
        var linhaInicial = 1;

        // Verifica se a linha 1 é um cabeçalho
        var linha1 = ws.Row(1);
        if (!linha1.IsEmpty())
        {
            var celula1 = linha1.Cell(1).GetString().Trim();
            var celula2 = linha1.Cell(2).GetString().Trim();

            if (EhCabecalhoCodigo(celula1) || EhCabecalhoCotacao(celula2))
            {
                linhaInicial = 2;
                var colunas = LeitorDePlanilha.MapearCabecalhos(linha1);
                colCodigo = EncontrarColuna(colunas, "Codigo", "Código", "Ticker", "Papel", "Ativo", "Ação", "Acao") ?? 1;
                colCotacao = EncontrarColuna(colunas, "Cotacao", "Cotação", "Preco", "Preço", "Valor", "Cotação Atual", "Cotacao Atual") ?? 2;
            }
        }

        var linhas = new List<LinhaCotacao>();

        for (var linha = linhaInicial; linha <= ultimaLinha; linha++)
        {
            var row = ws.Row(linha);
            if (row.IsEmpty())
            {
                continue;
            }

            var codigo = row.Cell(colCodigo).GetString().Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(codigo) || codigo.StartsWith("#"))
            {
                continue;
            }

            var cotacao = LeitorDePlanilha.LerDecimalOpcional(row.Cell(colCotacao), CulturaPtBr);
            if (cotacao.HasValue)
            {
                linhas.Add(new LinhaCotacao(codigo, cotacao.Value));
            }
        }

        return linhas;
    }

    private static bool EhCabecalhoCodigo(string texto) =>
        texto.Equals("Codigo", StringComparison.OrdinalIgnoreCase) ||
        texto.Equals("Código", StringComparison.OrdinalIgnoreCase) ||
        texto.Equals("Ticker", StringComparison.OrdinalIgnoreCase) ||
        texto.Equals("Papel", StringComparison.OrdinalIgnoreCase) ||
        texto.Equals("Ativo", StringComparison.OrdinalIgnoreCase) ||
        texto.Equals("Ação", StringComparison.OrdinalIgnoreCase) ||
        texto.Equals("Acao", StringComparison.OrdinalIgnoreCase);

    private static bool EhCabecalhoCotacao(string texto) =>
        texto.Equals("Cotacao", StringComparison.OrdinalIgnoreCase) ||
        texto.Equals("Cotação", StringComparison.OrdinalIgnoreCase) ||
        texto.Equals("Preco", StringComparison.OrdinalIgnoreCase) ||
        texto.Equals("Preço", StringComparison.OrdinalIgnoreCase) ||
        texto.Equals("Valor", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("Cotação", StringComparison.OrdinalIgnoreCase) ||
        texto.Contains("Cotacao", StringComparison.OrdinalIgnoreCase);

    private static int? EncontrarColuna(Dictionary<string, int> colunas, params string[] nomesPossiveis)
    {
        foreach (var nome in nomesPossiveis)
        {
            if (colunas.TryGetValue(nome, out var col))
            {
                return col;
            }
        }

        foreach (var (chave, valor) in colunas)
        {
            foreach (var nome in nomesPossiveis)
            {
                if (chave.Contains(nome, StringComparison.OrdinalIgnoreCase))
                {
                    return valor;
                }
            }
        }

        return null;
    }
}
