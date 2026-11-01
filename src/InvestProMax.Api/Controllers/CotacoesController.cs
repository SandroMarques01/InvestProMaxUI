using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Application.Cotacoes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace InvestProMax.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/cotacoes")]
public class CotacoesController : ControllerBase
{
    private readonly IDisparadorAtualizacaoCotacoes _disparador;
    private readonly IUsuarioLogado _usuarioLogado;
    private readonly IMemoryCache _cache;

    public CotacoesController(
        IDisparadorAtualizacaoCotacoes disparador,
        IUsuarioLogado usuarioLogado,
        IMemoryCache cache)
    {
        _disparador = disparador;
        _usuarioLogado = usuarioLogado;
        _cache = cache;
    }

    [HttpPost("atualizar")]
    [ProducesResponseType(typeof(SolicitarAtualizacaoResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Atualizar(CancellationToken ct = default)
    {
        if (!_disparador.EstaConfigurado)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Integração de cotações não configurada.",
                Detail = "O webhook do n8n não está configurado neste ambiente.",
                Instance = HttpContext.Request.Path
            });
        }

        var usuarioId = _usuarioLogado.UsuarioId;
        var chaveCache = $"cotacoes_atualizar_{usuarioId}";

        if (_cache.TryGetValue(chaveCache, out _))
        {
            Response.Headers["Retry-After"] = "60";
            return StatusCode(StatusCodes.Status429TooManyRequests, new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Aguarde um minuto.",
                Detail = "Já houve uma solicitação de atualização há menos de 60 segundos.",
                Instance = HttpContext.Request.Path
            });
        }

        var solicitadoEm = DateTime.UtcNow;

        try
        {
            await _disparador.DispararAsync(solicitadoEm, ct);
            _cache.Set(chaveCache, true, TimeSpan.FromSeconds(60));
        }
        catch (Exception)
        {
            _cache.Remove(chaveCache);
            throw;
        }

        return Accepted(new SolicitarAtualizacaoResponse(solicitadoEm));
    }
}
