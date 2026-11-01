namespace InvestProMax.Application.Auth;

public interface IAuthService
{
    Task<AutenticacaoResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AutenticacaoResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default);
    Task AlterarSenhaAsync(Guid usuarioId, AlterarSenhaRequest request, CancellationToken ct = default);
    Task<UsuarioResponse> CriarUsuarioAsync(CriarUsuarioRequest request, CancellationToken ct = default);
}
