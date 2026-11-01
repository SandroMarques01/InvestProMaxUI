namespace InvestProMax.Application.Common.Interfaces;

/// <summary>
/// Abstração sobre o hash adaptativo de senha (BCrypt, seção 7). A implementação concreta
/// vive na Infrastructure — a Application não conhece a biblioteca usada.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string senha);
    bool Verificar(string senha, string hash);
}
