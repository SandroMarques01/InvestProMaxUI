using InvestProMax.Domain.Common;

namespace InvestProMax.Domain.Entidades;

public class Usuario : EntidadeBase
{
    public string Nome { get; set; } = string.Empty;

    /// <summary>Novo em relação ao legado — necessário para eventual recuperação de senha.</summary>
    public string Email { get; set; } = string.Empty;

    public string NomeUsuario { get; set; } = string.Empty;

    /// <summary>Saída do BCrypt (já inclui o salt). Nunca armazenar senha em texto puro.</summary>
    public string SenhaHash { get; set; } = string.Empty;

    public bool Ativo { get; set; } = true;

    /// <summary>Identificador do usuário no Telegram (64-bit), utilizado em integrações como n8n e bots.</summary>
    public long? TelegramId { get; set; }

    public ICollection<Papel> Papeis { get; set; } = new List<Papel>();
    public ICollection<Ativo> Ativos { get; set; } = new List<Ativo>();
    public ICollection<Conta> Contas { get; set; } = new List<Conta>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
