using InvestProMax.Domain.Common;

namespace InvestProMax.Domain.Entidades;

public class Pagamento : EntidadeBase
{
    /// <summary>No legado era ContaId; mudou para vincular ao mês específico (à Fatura).</summary>
    public Guid FaturaId { get; set; }
    public Fatura? Fatura { get; set; }

    public decimal ValorPago { get; set; }
    public DateOnly Data { get; set; }
    public string? FormaPagamento { get; set; }
    public string? Descricao { get; set; }
}
