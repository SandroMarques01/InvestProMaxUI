using InvestProMax.Domain.Common;

namespace InvestProMax.Domain.Entidades;

/// <summary>
/// ASSUNÇÃO SINALIZADA: a especificação (seção 7) exige refresh token de vida longa,
/// mas o modelo de domínio da seção 5 não inclui uma entidade para ele. Sem persistência,
/// não é possível revogar um refresh token comprometido nem impedir reuso após rotação —
/// por isso esta entidade foi adicionada. Armazena apenas o hash do token (nunca o valor
/// em texto puro), seguindo o mesmo princípio aplicado à senha do usuário.
/// </summary>
public class RefreshToken : EntidadeBase
{
    public Guid UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public string TokenHash { get; set; } = string.Empty;
    public DateTime CriadoEmUtc { get; set; }
    public DateTime ExpiraEmUtc { get; set; }
    public DateTime? RevogadoEmUtc { get; set; }

    /// <summary>Preenchido quando este token é substituído por outro na rotação (refresh).</summary>
    public Guid? SubstituidoPorId { get; set; }

    public bool EstaAtivo => RevogadoEmUtc is null && DateTime.UtcNow < (ExpiraEmUtc.Kind switch
    {
        DateTimeKind.Utc => ExpiraEmUtc,
        DateTimeKind.Local => ExpiraEmUtc.ToUniversalTime(),
        _ => DateTime.SpecifyKind(ExpiraEmUtc, DateTimeKind.Utc)
    });
}
