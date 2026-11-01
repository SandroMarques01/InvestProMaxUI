namespace InvestProMax.Domain.Entidades;

/// <summary>
/// Tabela de junção N:N entre Papel e Segmento (seção 5). Diferente das demais entidades,
/// usa chave composta (PapelId, SegmentoId) em vez de Id próprio, por ser uma associação pura
/// sem atributos adicionais — decisão de implementação não detalhada na especificação.
/// </summary>
public class PapelSegmento
{
    public Guid PapelId { get; set; }
    public Papel? Papel { get; set; }

    public Guid SegmentoId { get; set; }
    public Segmento? Segmento { get; set; }
}
