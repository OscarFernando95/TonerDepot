using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toner.Application.Dashboard;
using Toner.Application.Dashboard.Dtos;
using Toner.Domain.Common;

namespace Toner.Api.Controllers;

// Módulo 11: indicadores operativos para Administrador/Coordinador. Un solo endpoint compuesto
// evita 5 llamadas separadas desde el dashboard del front.
[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = RoleNames.StaffRoles)]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary([FromQuery] int days, CancellationToken cancellationToken)
    {
        var periodDays = days <= 0 ? 30 : days;
        return Ok(await _dashboardService.GetSummaryAsync(periodDays, cancellationToken));
    }
}
