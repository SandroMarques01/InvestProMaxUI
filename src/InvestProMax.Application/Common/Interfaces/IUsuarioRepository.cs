using InvestProMax.Domain.Entidades;

namespace InvestProMax.Application.Common.Interfaces;

public interface IUsuarioRepository
{
    Task<Usuario?> ObterPorNomeUsuarioAsync(string nomeUsuario, CancellationToken ct = default);
    Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken ct = default);
    Task AtualizarAsync(Usuario usuario, CancellationToken ct = default);

    Task AdicionarAsync(Usuario usuario, CancellationToken ct = default);
    Task<bool> ExisteNomeUsuarioAsync(string nomeUsuario, CancellationToken ct = default);
    Task<bool> ExisteEmailAsync(string email, CancellationToken ct = default);

    /// <summary>Usado pelo job diário de faturas (seção 9), que roda para todos os usuários,
    /// não tem HttpContext/JWT e por isso não pode usar IUsuarioLogado.</summary>
    Task<IReadOnlyList<Guid>> ListarIdsAtivosAsync(CancellationToken ct = default);
}
