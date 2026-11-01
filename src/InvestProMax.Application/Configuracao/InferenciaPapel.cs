using InvestProMax.Domain.Enums;

namespace InvestProMax.Application.Configuracao;

/// <summary>
/// ASSUNÇÃO SINALIZADA: usado só quando a importação B3 encontra um código de papel que ainda
/// não existe para o usuário e precisa criar um Papel novo — a especificação não define como
/// extrair Codigo/Nome/TipoPapel da coluna "Produto" do extrato, nem havia planilha de exemplo.
/// </summary>
public static class InferenciaPapel
{
    /// <summary>
    /// A coluna "Produto" do extrato B3 normalmente vem como "PETR4 - PETROLEO BRASILEIRO
    /// S.A. PETROBRAS". Se não houver " - ", o texto inteiro vira Nome e o próprio texto
    /// (sem espaços) vira Codigo, no melhor esforço.
    /// </summary>
    public static (string Codigo, string Nome) SepararProduto(string produto)
    {
        var partes = produto.Split(" - ", 2, StringSplitOptions.TrimEntries);
        return partes.Length == 2
            ? (NormalizarCodigo(partes[0]), partes[1])
            : (NormalizarCodigo(produto.Trim().Replace(" ", "")), produto.Trim());
    }

    /// <summary>
    /// Regra herdada do MinhasFinancas: códigos de direito/recibo terminados em 12 ou 13 (ex.:
    /// GAME12, XPCA13) pertencem ao papel de sufixo 11. O sistema antigo trocava em qualquer
    /// posição do código; aqui só o sufixo, para não corromper códigos como "A1LB34".
    /// </summary>
    public static string NormalizarCodigo(string codigo)
    {
        var c = codigo.Trim();
        return c.Length > 2 && (c.EndsWith("12", StringComparison.Ordinal) || c.EndsWith("13", StringComparison.Ordinal))
            ? c[..^2] + "11"
            : c;
    }

    /// <summary>
    /// Heurística IMPRECISA por natureza: sufixo "11" é usado tanto por FIIs quanto por ETFs e
    /// units, e não há como diferenciar só pelo código. Sufixo "34"/"35" é o padrão comum de
    /// BDR. Qualquer coisa fora disso cai em Ação, o caso mais comum. O usuário deve revisar
    /// (e pode corrigir via PUT /papeis/{id}, que agora aceita TipoPapel — ver PapelDtos.cs).
    /// </summary>
    public static TipoPapel InferirTipo(string codigo)
    {
        var codigoNormalizado = codigo.Trim().ToUpperInvariant();

        if (codigoNormalizado.EndsWith("34", StringComparison.Ordinal) || codigoNormalizado.EndsWith("35", StringComparison.Ordinal))
        {
            return TipoPapel.BDR;
        }

        if (codigoNormalizado.EndsWith("11", StringComparison.Ordinal))
        {
            return TipoPapel.FII;
        }

        return TipoPapel.Acao;
    }
}
