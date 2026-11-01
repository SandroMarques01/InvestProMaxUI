using System.Security.Cryptography;
using System.Text;

namespace InvestProMax.Application.Configuracao;

internal static class HashDeLinhaImportada
{
    /// <summary>
    /// Hash SHA-256 (hex, 64 caracteres) dos valores brutos de uma linha do extrato B3, usado
    /// como chave de deduplicação (regra 5) — ver observação em Transacao.OrigemImportacaoHash
    /// sobre por que não dá para usar Quantidade/ValorUnitario da Transacao já criada.
    /// </summary>
    public static string CalcularParaLinhaB3(LinhaExtratoB3 linha)
    {
        var texto = string.Join('|',
            linha.EntradaSaida.Trim(),
            linha.Data.ToString("O"),
            linha.Movimentacao.Trim(),
            linha.Produto.Trim(),
            linha.Quantidade.ToString("G29"),
            linha.PrecoUnitario?.ToString("G29") ?? "",
            linha.ValorOperacao.ToString("G29"));

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(texto));
        return Convert.ToHexString(bytes);
    }
}
