using InvestProMax.Domain.Common;
using InvestProMax.Domain.Enums;

namespace InvestProMax.Domain.Entidades;

public class Conta : EntidadeBase
{
    public Guid UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public string Nome { get; set; } = string.Empty;
    public decimal ValorEstimado { get; set; }

    /// <summary>1–31. Se o mês não tiver esse dia, a fatura cai no último dia do mês.</summary>
    public int DiaVencimento { get; set; }

    /// <summary>Novo em relação ao legado.</summary>
    public Recorrencia Recorrencia { get; set; } = Recorrencia.Mensal;

    /// <summary>Só usado quando Recorrencia = Parcelada.</summary>
    public int? NumeroParcelas { get; set; }

    /// <summary>Novo, opcional, para relatório de gastos.</summary>
    public string? Categoria { get; set; }

    public string? Descricao { get; set; }
    public bool StatusAtivo { get; set; } = true;

    public ICollection<Fatura> Faturas { get; set; } = new List<Fatura>();
}
