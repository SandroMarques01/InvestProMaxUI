using InvestProMax.Domain.Common;
using InvestProMax.Domain.Enums;

namespace InvestProMax.Domain.Entidades;

public class Dividendo : EntidadeBase
{
    public Guid PapelId { get; set; }
    public Papel? Papel { get; set; }

    public TipoDividendo TipoDividendo { get; set; }
    public decimal ValorRecebido { get; set; }
    public decimal Quantidade { get; set; }
    public DateOnly Data { get; set; }
    public string? Descricao { get; set; }
    public bool Historico { get; set; }

    /// <summary>Mesmo propósito e mesma assunção sinalizada do campo homônimo em Transacao.</summary>
    public string? OrigemImportacaoHash { get; set; }
}
