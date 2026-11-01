using FluentValidation;
using InvestProMax.Application.Common;
using InvestProMax.Application.Inventario;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestProMax.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/inventario")]
public class InventarioController : ControllerBase
{
    private readonly IInventarioService _service;
    private readonly IValidator<LancamentoLoteRequest> _loteValidator;

    public InventarioController(IInventarioService service, IValidator<LancamentoLoteRequest> loteValidator)
    {
        _service = service;
        _loteValidator = loteValidator;
    }

    [HttpGet]
    public async Task<ActionResult<PaginaResultado<InventarioResponse>>> Listar(
        [FromQuery] Guid? ativoId, [FromQuery] int? ano, [FromQuery] int? mes,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var resultado = await _service.ListarAsync(new InventarioFiltro(ativoId, ano, mes), new ParametrosPaginacao(page, pageSize), ct);
        return Ok(resultado);
    }

    [HttpPost("lote")]
    public async Task<ActionResult<LancamentoLoteResponse>> LancarLote(LancamentoLoteRequest request, CancellationToken ct)
    {
        await _loteValidator.ValidateAndThrowAsync(request, ct);
        return Ok(await _service.LancarLoteAsync(request, ct));
    }

    /// <summary>
    /// ASSUNÇÃO SINALIZADA: a especificação (seção 8) descreve a rota como
    /// "sugestao-calculada?mes=" (um único parâmetro). Optei por dois parâmetros explícitos
    /// (ano, mes) em vez de um único texto "yyyy-MM" a parsear, por consistência com os
    /// filtros de GET /inventario acima — mas manteve o mesmo nome de rota.
    /// </summary>
    [HttpGet("sugestao-calculada")]
    public async Task<ActionResult<SugestaoCalculadaResponse>> SugestaoCalculada(
        [FromQuery] int ano, [FromQuery] int mes, CancellationToken ct)
    {
        return Ok(await _service.ObterSugestaoCalculadaAsync(ano, mes, ct));
    }
}
