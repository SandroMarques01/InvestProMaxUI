namespace InvestProMax.Application.Auth;

public interface IAuthService
{
    Task<AutenticacaoResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AutenticacaoResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default);
    Task AlterarSenhaAsync(Guid usuarioId, AlterarSenhaRequest request, CancellationToken ct = default);
    Task<UsuarioResponse> CriarUsuarioAsync(CriarUsuarioRequest request, CancellationToken ct = default);
    Task<StatusCadastroResponse> RegistrarAsync(RegistrarUsuarioRequest request, CancellationToken ct = default);
    Task<StatusCadastroResponse> AutenticarComGoogleAsync(GoogleUsuarioInfo googleUsuario, CancellationToken ct = default);
    Task<StatusCadastroResponse> ObterStatusCadastroPorCodigoAsync(string codigo, CancellationToken ct = default);
    Task<ValidarTelegramResponse> ValidarTelegramAsync(ValidarTelegramRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<UsuarioResponse>> ListarUsuariosAsync(CancellationToken ct = default);
    Task<UsuarioResponse> AprovarUsuarioAsync(Guid usuarioId, CancellationToken ct = default);
    Task<UsuarioResponse> RejeitarUsuarioAsync(Guid usuarioId, CancellationToken ct = default);
    Task<DecidirCadastroTelegramResponse> DecidirCadastroViaTelegramAsync(DecidirCadastroTelegramRequest request, CancellationToken ct = default);
}
