using InvestProMax.Domain.Common;
using InvestProMax.Domain.Enums;

namespace InvestProMax.Domain.Entidades;

public class Ativo : EntidadeBase
{
    public Guid UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public string Nome { get; set; } = string.Empty;
    public TipoAtivo TipoAtivo { get; set; }

    /// <summary>Novo em relação ao legado — ver regra de negócio 6.</summary>
    public TipoOrigemAtivo TipoOrigem { get; set; } = TipoOrigemAtivo.Manual;

    /// <summary>Só preenchido quando TipoOrigem = Calculado.</summary>
    public TipoPapel? TipoPapelVinculado { get; set; }

    public string? Descricao { get; set; }
    public bool StatusAtivo { get; set; } = true;

    public ICollection<Inventario> HistoricoInventario { get; set; } = new List<Inventario>();
}
