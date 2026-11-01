using InvestProMax.Domain.Common;
using InvestProMax.Domain.Enums;

namespace InvestProMax.Domain.Entidades;

/// <summary>
/// Aluguel de ações (BTC). A saída para aluguel e a devolução NÃO são venda/compra: o papel
/// continua na carteira (Transacao não é criada), e a quantidade emprestada é só informativa —
/// Quantidade em aluguel = Σ Saida − Σ Devolucao até uma data.
/// </summary>
public class MovimentoAluguel : EntidadeBase
{
    public Guid PapelId { get; set; }
    public Papel? Papel { get; set; }

    public TipoMovimentoAluguel Tipo { get; set; }
    public decimal Quantidade { get; set; }
    public DateOnly Data { get; set; }
    public string? Descricao { get; set; }

    /// <summary>Hash da linha do extrato B3 de origem (mesma finalidade de Transacao.OrigemImportacaoHash).</summary>
    public string? OrigemImportacaoHash { get; set; }
}
