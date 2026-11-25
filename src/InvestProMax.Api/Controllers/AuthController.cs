using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using InvestProMax.Application.Auth;
using InvestProMax.Application.Common.Exceptions;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace InvestProMax.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUsuarioLogado _usuarioLogado;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IGoogleAuthProvider _googleAuthProvider;
    private readonly INotificadorTelegramService _notificadorTelegram;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly JwtOptions _jwtOptions;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<RefreshRequest> _refreshValidator;
    private readonly IValidator<AlterarSenhaRequest> _alterarSenhaValidator;
    private readonly IValidator<CriarUsuarioRequest> _criarUsuarioValidator;
    private readonly IValidator<RegistrarUsuarioRequest> _registrarUsuarioValidator;
    private readonly IValidator<TrocarCodigoGoogleRequest> _trocarCodigoGoogleValidator;

    public AuthController(
        IAuthService authService,
        IUsuarioLogado usuarioLogado,
        IUsuarioRepository usuarioRepository,
        IGoogleAuthProvider googleAuthProvider,
        INotificadorTelegramService notificadorTelegram,
        IMemoryCache cache,
        IConfiguration configuration,
        IOptions<JwtOptions> jwtOptions,
        IValidator<LoginRequest> loginValidator,
        IValidator<RefreshRequest> refreshValidator,
        IValidator<AlterarSenhaRequest> alterarSenhaValidator,
        IValidator<CriarUsuarioRequest> criarUsuarioValidator,
        IValidator<RegistrarUsuarioRequest> registrarUsuarioValidator,
        IValidator<TrocarCodigoGoogleRequest> trocarCodigoGoogleValidator)
    {
        _authService = authService;
        _usuarioLogado = usuarioLogado;
        _usuarioRepository = usuarioRepository;
        _googleAuthProvider = googleAuthProvider;
        _notificadorTelegram = notificadorTelegram;
        _cache = cache;
        _configuration = configuration;
        _jwtOptions = jwtOptions.Value;
        _loginValidator = loginValidator;
        _refreshValidator = refreshValidator;
        _alterarSenhaValidator = alterarSenhaValidator;
        _criarUsuarioValidator = criarUsuarioValidator;
        _registrarUsuarioValidator = registrarUsuarioValidator;
        _trocarCodigoGoogleValidator = trocarCodigoGoogleValidator;
    }

    [HttpGet("config-publica")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthConfigPublicaResponse), StatusCodes.Status200OK)]
    public ActionResult<AuthConfigPublicaResponse> ConfigPublica() =>
        Ok(new AuthConfigPublicaResponse(_googleAuthProvider.EstaConfigurado, _notificadorTelegram.BotUsername));

    /// <summary>Autentica e retorna access + refresh token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AutenticacaoResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AutenticacaoResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        await _loginValidator.ValidateAndThrowAsync(request, ct);
        var resposta = await _authService.LoginAsync(request, ct);
        return Ok(resposta);
    }

    [HttpPost("registrar")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(StatusCadastroResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<StatusCadastroResponse>> Registrar(RegistrarUsuarioRequest request, CancellationToken ct)
    {
        await _registrarUsuarioValidator.ValidateAndThrowAsync(request, ct);
        var resposta = await _authService.RegistrarAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, resposta);
    }

    [HttpGet("cadastro/status")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(StatusCadastroResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<StatusCadastroResponse>> ObterStatusCadastro([FromQuery] string codigo, CancellationToken ct)
    {
        var resposta = await _authService.ObterStatusCadastroPorCodigoAsync(codigo, ct);
        return Ok(resposta);
    }

    [HttpGet("google/iniciar")]
    [AllowAnonymous]
    public IActionResult IniciarGoogle([FromQuery] string? retorno = null)
    {
        var retornoBase = NormalizarRetornoPermitido(retorno);

        if (!_googleAuthProvider.EstaConfigurado)
        {
            var erro = Uri.EscapeDataString("Login com Google ainda não foi configurado no servidor.");
            return Redirect($"{retornoBase}/login?erroGoogle={erro}");
        }

        var redirectUri = $"{Request.Scheme}://{Request.Host}/api/auth/google/callback";
        var state = AssinarStateGoogle(retornoBase);
        var urlGoogle = _googleAuthProvider.GerarUrlAutorizacao(redirectUri, state);
        return Redirect(urlGoogle);
    }

    [HttpGet("google/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> CallbackGoogle(
        [FromQuery] string? code = null,
        [FromQuery] string? state = null,
        [FromQuery] string? error = null,
        CancellationToken ct = default)
    {
        if (!TryValidarStateGoogle(state, out var retornoBase))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Parâmetro state do login Google inválido ou expirado.",
                Instance = HttpContext.Request.Path
            });
        }

        if (!string.IsNullOrWhiteSpace(error) || string.IsNullOrWhiteSpace(code))
        {
            var msgErro = Uri.EscapeDataString("Autenticação com Google cancelada ou não autorizada.");
            return Redirect($"{retornoBase}/login?erroGoogle={msgErro}");
        }

        try
        {
            var redirectUri = $"{Request.Scheme}://{Request.Host}/api/auth/google/callback";
            var googleUsuario = await _googleAuthProvider.TrocarCodigoPorUsuarioAsync(code, redirectUri, ct);
            var resultado = await _authService.AutenticarComGoogleAsync(googleUsuario, ct);

            var googleCodigo = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
            _cache.Set(ObterChaveCacheGoogle(googleCodigo), resultado, TimeSpan.FromMinutes(2));

            return Redirect($"{retornoBase}/login?googleCodigo={Uri.EscapeDataString(googleCodigo)}");
        }
        catch (RegraDeNegocioException ex)
        {
            var msgErro = Uri.EscapeDataString(ex.Message);
            return Redirect($"{retornoBase}/login?erroGoogle={msgErro}");
        }
    }

    [HttpPost("google/trocar")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(StatusCadastroResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<StatusCadastroResponse>> TrocarCodigoGoogle(
        TrocarCodigoGoogleRequest request,
        CancellationToken ct = default)
    {
        await _trocarCodigoGoogleValidator.ValidateAndThrowAsync(request, ct);
        var chave = ObterChaveCacheGoogle(request.Codigo.Trim());
        if (!_cache.TryGetValue(chave, out StatusCadastroResponse? resposta) || resposta is null)
        {
            throw new NaoAutorizadoException("Código de autenticação Google inválido ou expirado.");
        }

        _cache.Remove(chave);
        return Ok(resposta);
    }

    /// <summary>Renova o access token a partir de um refresh token válido (com rotação).</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AutenticacaoResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AutenticacaoResponse>> Refresh(RefreshRequest request, CancellationToken ct)
    {
        await _refreshValidator.ValidateAndThrowAsync(request, ct);
        var resposta = await _authService.RefreshAsync(request, ct);
        return Ok(resposta);
    }

    /// <summary>Troca a senha do usuário autenticado.</summary>
    [HttpPost("alterar-senha")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AlterarSenha(AlterarSenhaRequest request, CancellationToken ct)
    {
        await _alterarSenhaValidator.ValidateAndThrowAsync(request, ct);
        await _authService.AlterarSenhaAsync(_usuarioLogado.UsuarioId, request, ct);
        return NoContent();
    }

    [HttpGet("usuarios")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<UsuarioResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<UsuarioResponse>>> ListarUsuarios(CancellationToken ct)
    {
        if (!await EhAdminAsync(ct))
        {
            return Forbid();
        }

        return Ok(await _authService.ListarUsuariosAsync(ct));
    }

    [HttpPost("usuarios/{id:guid}/aprovar")]
    [Authorize]
    [ProducesResponseType(typeof(UsuarioResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UsuarioResponse>> AprovarUsuario(Guid id, CancellationToken ct)
    {
        if (!await EhAdminAsync(ct))
        {
            return Forbid();
        }

        return Ok(await _authService.AprovarUsuarioAsync(id, ct));
    }

    [HttpPost("usuarios/{id:guid}/rejeitar")]
    [Authorize]
    [ProducesResponseType(typeof(UsuarioResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UsuarioResponse>> RejeitarUsuario(Guid id, CancellationToken ct)
    {
        if (!await EhAdminAsync(ct))
        {
            return Forbid();
        }

        return Ok(await _authService.RejeitarUsuarioAsync(id, ct));
    }

    /// <summary>Cria um novo usuário no sistema. Permitido apenas para o usuário admin.</summary>
    [HttpPost("usuarios")]
    [Authorize]
    [ProducesResponseType(typeof(UsuarioResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UsuarioResponse>> CriarUsuario(CriarUsuarioRequest request, CancellationToken ct)
    {
        if (!await EhAdminAsync(ct))
        {
            return Forbid();
        }

        await _criarUsuarioValidator.ValidateAndThrowAsync(request, ct);
        var resposta = await _authService.CriarUsuarioAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, resposta);
    }

    private async Task<bool> EhAdminAsync(CancellationToken ct)
    {
        var nomeUsuarioClaim = User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value
            ?? User.FindFirst(ClaimTypes.Name)?.Value;

        if (string.Equals(nomeUsuarioClaim, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var usuario = await _usuarioRepository.ObterPorIdAsync(_usuarioLogado.UsuarioId, ct);
        return string.Equals(usuario?.NomeUsuario, "admin", StringComparison.OrdinalIgnoreCase);
    }

    private string NormalizarRetornoPermitido(string? retorno)
    {
        var origensConfiguradas = _configuration.GetSection("Cors:OrigensPermitidas").Get<string[]>() ?? [];
        var origensPermitidas = origensConfiguradas
            .Union(["https://investpromaxui.pages.dev"], StringComparer.OrdinalIgnoreCase)
            .Select(o => o.TrimEnd('/'))
            .ToArray();

        if (!string.IsNullOrWhiteSpace(retorno) && Uri.TryCreate(retorno.Trim(), UriKind.Absolute, out var uri))
        {
            var baseUrl = $"{uri.Scheme}://{uri.Authority}".TrimEnd('/');
            if (uri.IsLoopback || origensPermitidas.Contains(baseUrl, StringComparer.OrdinalIgnoreCase))
            {
                return baseUrl;
            }
        }

        return origensPermitidas.FirstOrDefault() ?? "https://investpromaxui.pages.dev";
    }

    private string AssinarStateGoogle(string retornoBase)
    {
        var payloadObj = new GoogleStatePayload(
            retornoBase,
            Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant(),
            DateTime.UtcNow.AddMinutes(10).Ticks);

        var payloadJson = JsonSerializer.Serialize(payloadObj);
        var payloadEncoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
        var assinatura = CalcularHmac(payloadEncoded);
        return $"{payloadEncoded}.{assinatura}";
    }

    private bool TryValidarStateGoogle(string? state, out string retornoBase)
    {
        retornoBase = NormalizarRetornoPermitido(null);
        if (string.IsNullOrWhiteSpace(state))
        {
            return false;
        }

        var partes = state.Split('.');
        if (partes.Length != 2)
        {
            return false;
        }

        var assinaturaEsperada = CalcularHmac(partes[0]);
        var bytesRecebidos = Encoding.UTF8.GetBytes(partes[1]);
        var bytesEsperados = Encoding.UTF8.GetBytes(assinaturaEsperada);
        if (bytesRecebidos.Length != bytesEsperados.Length
            || !CryptographicOperations.FixedTimeEquals(bytesRecebidos, bytesEsperados))
        {
            return false;
        }

        try
        {
            var json = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(partes[0]));
            var payload = JsonSerializer.Deserialize<GoogleStatePayload>(json);
            if (payload is null || payload.ExpiraEmTicks < DateTime.UtcNow.Ticks)
            {
                return false;
            }

            retornoBase = NormalizarRetornoPermitido(payload.RetornoBase);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string CalcularHmac(string dados)
    {
        var chave = Encoding.UTF8.GetBytes(_jwtOptions.ChaveSecreta);
        var mac = HMACSHA256.HashData(chave, Encoding.UTF8.GetBytes(dados));
        return WebEncoders.Base64UrlEncode(mac);
    }

    private static string ObterChaveCacheGoogle(string codigo) => $"google_oauth_code_{codigo}";

    private sealed record GoogleStatePayload(string RetornoBase, string Nonce, long ExpiraEmTicks);
}
