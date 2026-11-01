using FluentValidation;
using InvestProMax.Application.Common;
using InvestProMax.Application.Segmentos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestProMax.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/segmentos")]
public class SegmentosController : ControllerBase
{
    private readonly ISegmentoService _service;
    private readonly IValidator<CriarSegmentoRequest> _criarValidator;
    private readonly IValidator<AtualizarSegmentoRequest> _atualizarValidator;

    public SegmentosController(
        ISegmentoService service,
        IValidator<CriarSegmentoRequest> criarValidator,
        IValidator<AtualizarSegmentoRequest> atualizarValidator)
    {
        _service = service;
        _criarValidator = criarValidator;
        _atualizarValidator = atualizarValidator;
    }

    [HttpGet]
    public async Task<ActionResult<PaginaResultado<SegmentoResponse>>> Listar(
        [FromQuery] bool? ativo, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var resultado = await _service.ListarAsync(ativo, new ParametrosPaginacao(page, pageSize), ct);
        return Ok(resultado);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SegmentoResponse>> Obter(Guid id, CancellationToken ct) =>
        Ok(await _service.ObterAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<SegmentoResponse>> Criar(CriarSegmentoRequest request, CancellationToken ct)
    {
        await _criarValidator.ValidateAndThrowAsync(request, ct);
        var resposta = await _service.CriarAsync(request, ct);
        return CreatedAtAction(nameof(Obter), new { id = resposta.Id }, resposta);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SegmentoResponse>> Atualizar(Guid id, AtualizarSegmentoRequest request, CancellationToken ct)
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
