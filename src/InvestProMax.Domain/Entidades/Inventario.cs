using InvestProMax.Domain.Common;

namespace InvestProMax.Domain.Entidades;

public class Inventario : EntidadeBase
{
    public Guid AtivoId { get; set; }
    public Ativo? Ativo { get; set; }

    public decimal Valor { get; set; }
    public DateOnly Data { get; set; }
    public string? Descricao { get; set; }
    public bool StatusAtivo { get; set; } = true;

    /// <summary>
    /// A especificação exige um índice único em (AtivoId, Ano, Mes), mas o modelo só define
    /// o campo Data como fonte de verdade — não há colunas Ano/Mes explícitas na seção 5.
    /// Ano e Mes são materializados apenas como *shadow properties* computadas pelo SQL Server
    /// a partir de Data (ver InventarioConfiguration), só para viabilizar o índice único no banco;
    /// o domínio (esta classe) não expõe Ano/Mes — usar sempre Data. Assunção sinalizada aqui por
    /// não estar decidida explicitamente na especificação.
    /// </summary>
}
