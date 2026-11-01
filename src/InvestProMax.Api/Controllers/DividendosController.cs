using FluentValidation;
using InvestProMax.Application.Common;
using InvestProMax.Application.Dividendos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestProMax.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dividendos")]
public class DividendosController : ControllerBase
{
    private readonly IDividendoService _service;
    private readonly IValidator<CriarDividendoRequest> _criarValidator;
    private readonly IValidator<AtualizarDividendoRequest> _atualizarValidator;

    public DividendosController(
        IDividendoService service,
        IValidator<CriarDividendoRequest> criarValidator,
        IValidator<AtualizarDividendoRequest> atualizarValidator)
    {
        _service = service;
        _criarValidator = criarValidator;
        _atualizarValidator = atualizarValidator;
    }

    [HttpGet]
    public async Task<ActionResult<PaginaResultado<DividendoResponse>>> Listar(
        [FromQuery] Guid? papelId,
        [FromQuery] Domain.Enums.TipoPapel? tipoPapel,
        [FromQuery] DateOnly? dataInicio,
        [FromQuery] DateOnly? dataFim,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var resultado = await _service.ListarAsync(new DividendoFiltro(papelId, tipoPapel, dataInicio, dataFim), new ParametrosPaginacao(page, pageSize), ct);
        return Ok(resultado);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DividendoResponse>> Obter(Guid id, CancellationToken ct) =>
        Ok(await _service.ObterAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<DividendoResponse>> Criar(CriarDividendoRequest request, CancellationToken ct)
    {
        await _criarValidator.ValidateAndThrowAsync(request, ct);
        var resposta = await _service.CriarAsync(request, ct);
        return CreatedAtAction(nameof(Obter), new { id = resposta.Id }, resposta);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DividendoResponse>> Atualizar(Guid id, AtualizarDividendoRequest request, CancellationToken ct)
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
