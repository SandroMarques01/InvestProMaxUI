using FluentValidation;
using InvestProMax.Application.Common;
using InvestProMax.Application.Faturas;
using InvestProMax.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestProMax.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/faturas")]
public class FaturasController : ControllerBase
{
    private readonly IFaturaService _service;
    private readonly IValidator<PagarFaturaRequest> _pagarValidator;

    public FaturasController(IFaturaService service, IValidator<PagarFaturaRequest> pagarValidator)
    {
        _service = service;
        _pagarValidator = pagarValidator;
    }

    [HttpGet]
    public async Task<ActionResult<PaginaResultado<FaturaResponse>>> Listar(
        [FromQuery] StatusFatura? status, [FromQuery] DateOnly? competencia,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var resultado = await _service.ListarAsync(new FaturaFiltro(status, competencia), new ParametrosPaginacao(page, pageSize), ct);
        return Ok(resultado);
    }

    /// <summary>Dispara manualmente a regra 7, além do job automático diário (seção 9).</summary>
    [HttpPost("gerar")]
    public async Task<ActionResult<GerarFaturasResponse>> Gerar(CancellationToken ct) =>
        Ok(await _service.GerarFaturasAsync(ct));

    [HttpPost("{id:guid}/pagar")]
    public async Task<ActionResult<FaturaResponse>> Pagar(Guid id, PagarFaturaRequest request, CancellationToken ct)
    {
        await _pagarValidator.ValidateAndThrowAsync(request, ct);
        return Ok(await _service.PagarAsync(id, request, ct));
    }
}
