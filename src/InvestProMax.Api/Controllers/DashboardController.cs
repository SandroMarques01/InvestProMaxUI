using InvestProMax.Application.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestProMax.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _service;
    private readonly IDashboardLayoutService _layoutService;

    public DashboardController(IDashboardService service, IDashboardLayoutService layoutService)
    {
        _service = service;
        _layoutService = layoutService;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> Obter(CancellationToken ct) =>
        Ok(await _service.ObterAsync(ct));

    [HttpGet("layout")]
    public async Task<ActionResult<DashboardLayoutDto>> ObterLayout(CancellationToken ct) =>
        Ok(await _layoutService.ObterAsync(ct));

    [HttpPut("layout")]
    public async Task<ActionResult<DashboardLayoutDto>> SalvarLayout(DashboardLayoutDto layout, CancellationToken ct) =>
        Ok(await _layoutService.SalvarAsync(layout, ct));
}
