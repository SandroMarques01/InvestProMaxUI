using InvestProMax.Domain.Entidades;

namespace InvestProMax.Application.Common.Interfaces;

public interface IRefreshTokenRepository
{
    Task AdicionarAsync(RefreshToken token, CancellationToken ct = default);
    Task<RefreshToken?> ObterPorHashAsync(string tokenHash, CancellationToken ct = default);
    Task AtualizarAsync(RefreshToken token, CancellationToken ct = default);

    /// <summary>
    /// Detecção de reuso: revoga todos os refresh tokens ainda ativos (não revogados e não
    /// expirados) do usuário. Usado quando um token já revogado é reapresentado — sinal de
    /// possível comprometimento — para forçar novo login em todos os dispositivos.
    /// </summary>
    Task RevogarTodosDoUsuarioAsync(Guid usuarioId, CancellationToken ct = default);
}
