using FluentValidation;
using InvestProMax.Application.Common;
using InvestProMax.Application.Papeis;
using InvestProMax.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestProMax.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/papeis")]
public class PapeisController : ControllerBase
{
    private readonly IPapelService _service;
    private readonly IValidator<CriarPapelRequest> _criarValidator;
    private readonly IValidator<AtualizarPapelRequest> _atualizarValidator;
    private readonly IValidator<TrocarPapelRequest> _trocarValidator;
    private readonly IValidator<DesdobramentoRequest> _desdobramentoValidator;

    public PapeisController(
        IPapelService service,
        IValidator<CriarPapelRequest> criarValidator,
        IValidator<AtualizarPapelRequest> atualizarValidator,
        IValidator<TrocarPapelRequest> trocarValidator,
        IValidator<DesdobramentoRequest> desdobramentoValidator)
    {
        _service = service;
        _criarValidator = criarValidator;
        _atualizarValidator = atualizarValidator;
        _trocarValidator = trocarValidator;
        _desdobramentoValidator = desdobramentoValidator;
    }

    [HttpGet]
    public async Task<ActionResult<PaginaResultado<PapelResponse>>> Listar(
        [FromQuery] TipoPapel? tipoPapel, [FromQuery] bool? ativo,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var resultado = await _service.ListarAsync(
            new PapelFiltro(tipoPapel, ativo), new ParametrosPaginacao(page, pageSize), ct);
        return Ok(resultado);
    }

    [HttpGet("geral")]
    public async Task<ActionResult<PapeisGeralResponse>> ObterGeral(
        [FromQuery] TipoPapel? tipoPapel, [FromQuery] bool? somenteAtivos = true, [FromQuery] DateOnly? dataFinal = null, CancellationToken ct = default)
    {
        var resultado = await _service.ObterPapeisGeralAsync(tipoPapel, somenteAtivos, dataFinal, ct);
        return Ok(resultado);
    }

    [HttpGet("{id:guid}/detalhe")]
    public async Task<ActionResult<PapelDetalheResponse>> ObterDetalhe(Guid id, CancellationToken ct) =>
        Ok(await _service.ObterDetalheAsync(id, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PapelResponse>> Obter(Guid id, CancellationToken ct) =>
        Ok(await _service.ObterAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<PapelResponse>> Criar(CriarPapelRequest request, CancellationToken ct)
    {
        await _criarValidator.ValidateAndThrowAsync(request, ct);
        var resposta = await _service.CriarAsync(request, ct);
        return CreatedAtAction(nameof(Obter), new { id = resposta.Id }, resposta);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PapelResponse>> Atualizar(Guid id, AtualizarPapelRequest request, CancellationToken ct)
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

    [HttpPost("{id:guid}/trocar")]
    public async Task<ActionResult<PapelResponse>> Trocar(Guid id, TrocarPapelRequest request, CancellationToken ct)
    {
        await _trocarValidator.ValidateAndThrowAsync(request, ct);
        return Ok(await _service.TrocarAsync(id, request, ct));
    }

    [HttpPost("{id:guid}/desdobramento")]
    public async Task<ActionResult<PapelResponse>> Desdobramento(Guid id, DesdobramentoRequest request, CancellationToken ct)
    {
        await _desdobramentoValidator.ValidateAndThrowAsync(request, ct);
        return Ok(await _service.DesdobrarAsync(id, request, ct));
    }
}
