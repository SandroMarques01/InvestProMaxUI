namespace InvestProMax.Domain.Common;

/// <summary>
/// Base para todas as entidades do domínio. Todas têm Id (Guid) gerado na criação,
/// conforme especificação (seção 5).
/// </summary>
public abstract class EntidadeBase
{
    public Guid Id { get; init; } = Guid.NewGuid();
}
