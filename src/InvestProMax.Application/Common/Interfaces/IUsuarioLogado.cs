namespace InvestProMax.Application.Common.Interfaces;

/// <summary>
/// Dá acesso ao UsuarioId do usuário autenticado, extraído do token JWT.
/// Usado por toda regra/consulta que precisa aplicar o isolamento por usuário (regra 10,
/// seção 6) — nunca deve vir de um parâmetro enviado pelo cliente. Implementado na Api
/// (lendo HttpContext.User) para a Application não depender de ASP.NET Core diretamente.
/// </summary>
public interface IUsuarioLogado
{
    Guid UsuarioId { get; }
    bool EstaAutenticado { get; }
}
