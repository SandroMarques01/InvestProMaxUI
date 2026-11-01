using System.Globalization;
using ClosedXML.Excel;

namespace InvestProMax.Infrastructure.Excel;

internal static class LeitorDePlanilha
{
    public static Dictionary<string, int> MapearCabecalhos(IXLRow linhaCabecalho)
    {
        var mapa = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var ultimaColuna = linhaCabecalho.LastCellUsed()?.Address.ColumnNumber ?? 0;

        for (var col = 1; col <= ultimaColuna; col++)
        {
            var texto = linhaCabecalho.Cell(col).GetString().Trim();
            if (texto.Length > 0)
            {
                mapa[texto] = col;
            }
        }

        return mapa;
    }

    public static int ColunaObrigatoria(Dictionary<string, int> colunas, string nome) =>
        colunas.TryGetValue(nome, out var indice)
            ? indice
            : throw new InvalidDataException(
                $"A planilha não tem a coluna obrigatória '{nome}'. Cabeçalhos encontrados: {string.Join(", ", colunas.Keys)}.");

    public static int? ColunaOpcional(Dictionary<string, int> colunas, string nome) =>
        colunas.TryGetValue(nome, out var indice) ? indice : null;

    public static DateOnly LerData(IXLCell celula, CultureInfo cultura)
    {
        if (celula.DataType == XLDataType.DateTime)
        {
            return DateOnly.FromDateTime(celula.GetDateTime());
        }

        var texto = celula.GetString().Trim();
        return DateOnly.TryParse(texto, cultura, DateTimeStyles.None, out var data)
            ? data
            : DateOnly.Parse(texto, CultureInfo.InvariantCulture);
    }

    public static decimal LerDecimal(IXLCell celula, CultureInfo cultura) =>
        LerDecimalOpcional(celula, cultura)
            ?? throw new InvalidDataException($"Valor numérico esperado na célula {celula.Address}, encontrado vazio.");

    public static decimal? LerDecimalOpcional(IXLCell celula, CultureInfo cultura)
    {
        if (celula.IsEmpty())
        {
            return null;
        }

        if (celula.DataType == XLDataType.Number)
        {
            return (decimal)celula.GetDouble();
        }

        var texto = celula.GetString().Trim().Replace("\u00A0", " ").Trim();
        if (texto.Length == 0 || texto == "-" || texto == "—" || texto == "–" || texto == "--" || texto.Equals("N/A", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        texto = texto.Replace("R$", "", StringComparison.OrdinalIgnoreCase).Trim();

        if (decimal.TryParse(texto, NumberStyles.Any, cultura, out var valor))
        {
            return valor;
        }

        if (decimal.TryParse(texto, NumberStyles.Any, CultureInfo.InvariantCulture, out var valorInv))
        {
            return valorInv;
        }

        throw new InvalidDataException($"Valor numérico inválido na célula {celula.Address}: '{texto}'.");
    }
}
