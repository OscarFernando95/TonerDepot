using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toner.Application.Common.Paging;
using Toner.Application.Inventory.Analytics;
using Toner.Domain.Common;

namespace Toner.Api.Controllers;

// BI de consumo de tóner: solo el Administrador.
[ApiController]
[Route("api/analytics/toner")]
[Authorize(Roles = RoleNames.Administrador)]
public class TonerAnalyticsController : ControllerBase
{
    private readonly ITonerAnalyticsService _analytics;

    public TonerAnalyticsController(ITonerAnalyticsService analytics) => _analytics = analytics;

    [HttpGet("summary")]
    public async Task<ActionResult<TonerSummaryDto>> Summary([FromQuery] TonerFilter filter, CancellationToken cancellationToken)
    {
        return Ok(await _analytics.GetSummaryAsync(filter, cancellationToken));
    }

    [HttpGet("machines")]
    public async Task<ActionResult<PagedResult<TonerMachineRowDto>>> Machines(
        [FromQuery] TonerFilter filter, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        return Ok(await _analytics.ListMachinesAsync(filter, page, pageSize, cancellationToken));
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] TonerFilter filter, CancellationToken cancellationToken)
    {
        var csv = await _analytics.ExportCsvAsync(filter, cancellationToken);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", $"toner-por-maquina-{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}
