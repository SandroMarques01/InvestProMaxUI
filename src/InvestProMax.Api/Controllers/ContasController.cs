using FluentValidation;
using InvestProMax.Application.Common;
using InvestProMax.Application.Contas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestProMax.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/contas")]
public class ContasController : ControllerBase
{
    private readonly IContaService _service;
    private readonly IValidator<CriarContaRequest> _criarValidator;
    private readonly IValidator<AtualizarContaRequest> _atualizarValidator;

    public ContasController(
        IContaService service, IValidator<CriarContaRequest> criarValidator, IValidator<AtualizarContaRequest> atualizarValidator)
    {
        _service = service;
        _criarValidator = criarValidator;
        _atualizarValidator = atualizarValidator;
    }

    [HttpGet]
    public async Task<ActionResult<PaginaResultado<ContaResponse>>> Listar(
        [FromQuery] bool? statusAtivo, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var resultado = await _service.ListarAsync(new ContaFiltro(statusAtivo), new ParametrosPaginacao(page, pageSize), ct);
        return Ok(resultado);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContaResponse>> Obter(Guid id, CancellationToken ct) =>
        Ok(await _service.ObterAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ContaResponse>> Criar(CriarContaRequest request, CancellationToken ct)
    {
        await _criarValidator.ValidateAndThrowAsync(request, ct);
        var resposta = await _service.CriarAsync(request, ct);
        return CreatedAtAction(nameof(Obter), new { id = resposta.Id }, resposta);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ContaResponse>> Atualizar(Guid id, AtualizarContaRequest request, CancellationToken ct)
    {
        await _atualizarValidator.ValidateAndThrowAsync(request, ct);
        return Ok(await _service.AtualizarAsync(id, request, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remover(Guid id, CancellationToken ct)
    {
        await _service.RemoverAsync(id, ct);
        return NoContent();
    }
}
