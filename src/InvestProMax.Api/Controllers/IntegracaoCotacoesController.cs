using FluentValidation;
using InvestProMax.Application.Cotacoes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestProMax.Api.Controllers;

[ApiController]
[Authorize(Policy = "IntegracaoCotacoes")]
[Route("api/integracao/cotacoes")]
public class IntegracaoCotacoesController : ControllerBase
{
    private readonly IAtualizacaoCotacoesService _service;
    private readonly IValidator<ReceberCotacoesRequest> _validator;

    public IntegracaoCotacoesController(
        IAtualizacaoCotacoesService service,
        IValidator<ReceberCotacoesRequest> validator)
    {
        _service = service;
        _validator = validator;
    }

    [HttpGet("tickers")]
    [ProducesResponseType(typeof(TickersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TickersResponse>> ObterTickers(
        [FromQuery] int desatualizadasHaMinutos = 0,
        CancellationToken ct = default)
    {
        var resultado = await _service.ListarTickersAsync(desatualizadasHaMinutos, ct);
        return Ok(resultado);
    }

    [HttpPost("lote")]
    [ProducesResponseType(typeof(ReceberCotacoesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ReceberCotacoesResponse>> ReceberLote(
        [FromBody] ReceberCotacoesRequest request,
        CancellationToken ct = default)
    {
        await _validator.ValidateAndThrowAsync(request, ct);
        var resultado = await _service.ReceberLoteAsync(request, ct);
        return Ok(resultado);
    }
}
