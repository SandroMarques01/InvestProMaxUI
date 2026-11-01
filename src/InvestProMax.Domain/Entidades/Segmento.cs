using InvestProMax.Domain.Common;

namespace InvestProMax.Domain.Entidades;

public class Segmento : EntidadeBase
{
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public bool Ativo { get; set; } = true;

    public ICollection<PapelSegmento> PapelSegmentos { get; set; } = new List<PapelSegmento>();
}
