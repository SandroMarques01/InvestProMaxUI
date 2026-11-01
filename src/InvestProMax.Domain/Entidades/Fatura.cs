using InvestProMax.Domain.Common;
using InvestProMax.Domain.Enums;

namespace InvestProMax.Domain.Entidades;

/// <summary>Entidade nova, não existia no sistema legado (seção 5).</summary>
public class Fatura : EntidadeBase
{
    public Guid ContaId { get; set; }
    public Conta? Conta { get; set; }

    /// <summary>Dia 1 do mês de referência.</summary>
    public DateOnly Competencia { get; set; }

    public DateOnly DataVencimento { get; set; }
    public decimal ValorPrevisto { get; set; }
    public StatusFatura Status { get; set; } = StatusFatura.Pendente;

    public Pagamento? Pagamento { get; set; }
}
