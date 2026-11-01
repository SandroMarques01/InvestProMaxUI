using FluentValidation;
using InvestProMax.Application.Ativos;
using InvestProMax.Application.Common;
using InvestProMax.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestProMax.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/ativos")]
public class AtivosController : ControllerBase
{
    private readonly IAtivoService _service;
    private readonly IValidator<CriarAtivoRequest> _criarValidator;
    private readonly IValidator<AtualizarAtivoRequest> _atualizarValidator;

    public AtivosController(
        IAtivoService service, IValidator<CriarAtivoRequest> criarValidator, IValidator<AtualizarAtivoRequest> atualizarValidator)
    {
        _service = service;
        _criarValidator = criarValidator;
        _atualizarValidator = atualizarValidator;
    }

    [HttpGet]
    public async Task<ActionResult<PaginaResultado<AtivoResponse>>> Listar(
        [FromQuery] TipoAtivo? tipoAtivo, [FromQuery] bool? statusAtivo,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var resultado = await _service.ListarAsync(new AtivoFiltro(tipoAtivo, statusAtivo), new ParametrosPaginacao(page, pageSize), ct);
        return Ok(resultado);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AtivoResponse>> Obter(Guid id, CancellationToken ct) =>
        Ok(await _service.ObterAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<AtivoResponse>> Criar(CriarAtivoRequest request, CancellationToken ct)
    {
        await _criarValidator.ValidateAndThrowAsync(request, ct);
        var resposta = await _service.CriarAsync(request, ct);
        return CreatedAtAction(nameof(Obter), new { id = resposta.Id }, resposta);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AtivoResponse>> Atualizar(Guid id, AtualizarAtivoRequest request, CancellationToken ct)
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
