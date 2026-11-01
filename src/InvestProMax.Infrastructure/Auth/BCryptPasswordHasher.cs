using InvestProMax.Application.Common.Interfaces;

namespace InvestProMax.Infrastructure.Auth;

/// <summary>
/// Hash adaptativo de senha via BCrypt.Net-Next (seção 4/7). O work factor 12 é o padrão
/// da biblioteca em 2025/2026 e equilibra segurança com tempo de resposta do login;
/// ajustável aqui sem qualquer impacto no restante do sistema.
/// </summary>
public class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string senha) => BCrypt.Net.BCrypt.HashPassword(senha, workFactor: WorkFactor);

    public bool Verificar(string senha, string hash) => BCrypt.Net.BCrypt.Verify(senha, hash);
}
