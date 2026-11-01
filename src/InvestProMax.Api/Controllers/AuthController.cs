using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentValidation;
using InvestProMax.Application.Auth;
using InvestProMax.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestProMax.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUsuarioLogado _usuarioLogado;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<RefreshRequest> _refreshValidator;
    private readonly IValidator<AlterarSenhaRequest> _alterarSenhaValidator;
    private readonly IValidator<CriarUsuarioRequest> _criarUsuarioValidator;

    public AuthController(
        IAuthService authService,
        IUsuarioLogado usuarioLogado,
        IUsuarioRepository usuarioRepository,
        IValidator<LoginRequest> loginValidator,
        IValidator<RefreshRequest> refreshValidator,
        IValidator<AlterarSenhaRequest> alterarSenhaValidator,
        IValidator<CriarUsuarioRequest> criarUsuarioValidator)
    {
        _authService = authService;
        _usuarioLogado = usuarioLogado;
        _usuarioRepository = usuarioRepository;
        _loginValidator = loginValidator;
        _refreshValidator = refreshValidator;
        _alterarSenhaValidator = alterarSenhaValidator;
        _criarUsuarioValidator = criarUsuarioValidator;
    }

    /// <summary>Autentica e retorna access + refresh token. Único endpoint sem JWT (seção 8).</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AutenticacaoResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AutenticacaoResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        await _loginValidator.ValidateAndThrowAsync(request, ct);
        var resposta = await _authService.LoginAsync(request, ct);
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

    /// <summary>Cria um novo usuário no sistema. Permitido apenas para o usuário admin.</summary>
    [HttpPost("usuarios")]
    [Authorize]
    [ProducesResponseType(typeof(UsuarioResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UsuarioResponse>> CriarUsuario(CriarUsuarioRequest request, CancellationToken ct)
    {
        var nomeUsuarioClaim = User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value
            ?? User.FindFirst(ClaimTypes.Name)?.Value;

        if (!string.Equals(nomeUsuarioClaim, "admin", StringComparison.OrdinalIgnoreCase))
        {
            var usuario = await _usuarioRepository.ObterPorIdAsync(_usuarioLogado.UsuarioId, ct);
            if (!string.Equals(usuario?.NomeUsuario, "admin", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }
        }

        await _criarUsuarioValidator.ValidateAndThrowAsync(request, ct);
        var resposta = await _authService.CriarUsuarioAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, resposta);
    }
}
