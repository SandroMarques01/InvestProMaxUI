using FluentValidation;
using InvestProMax.Application.Auth;
using InvestProMax.Application.Common.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestProMax.Api.Controllers;

[ApiController]
[Authorize(Policy = "IntegracaoCotacoes")]
[Route("api/integracao/telegram")]
public class IntegracaoTelegramController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IValidator<ValidarTelegramRequest> _validarValidator;

    public IntegracaoTelegramController(
        IAuthService authService,
        IValidator<ValidarTelegramRequest> validarValidator)
    {
        _authService = authService;
        _validarValidator = validarValidator;
    }

    [HttpPost("validar-cadastro")]
    [ProducesResponseType(typeof(ValidarTelegramResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ValidarTelegramResponse>> ValidarCadastro(
        [FromBody] ValidarTelegramRequest request,
        CancellationToken ct = default)
    {
        await _validarValidator.ValidateAndThrowAsync(request, ct);
        var resposta = await _authService.ValidarTelegramAsync(request, ct);
        return Ok(resposta);
    }

    [HttpPost("decidir-cadastro")]
    [ProducesResponseType(typeof(DecidirCadastroTelegramResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DecidirCadastroTelegramResponse>> DecidirCadastro(
        [FromBody] DecidirCadastroTelegramRequest request,
        CancellationToken ct = default)
    {
        if (request.UsuarioId == Guid.Empty || request.AdminTelegramId <= 0)
        {
            throw new RegraDeNegocioException("UsuarioId e AdminTelegramId são obrigatórios.");
        }

        var resposta = await _authService.DecidirCadastroViaTelegramAsync(request, ct);
        return Ok(resposta);
    }
}
