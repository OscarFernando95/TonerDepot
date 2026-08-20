using Microsoft.AspNetCore.Authorization;
using Toner.Application.Common.Paging;
using Microsoft.AspNetCore.Mvc;
using Toner.Application.Maintenance;
using Toner.Application.Maintenance.Dtos;
using Toner.Domain.Common;
using Toner.Infrastructure.Jobs;

namespace Toner.Api.Controllers;

[ApiController]
[Route("api/maintenance-schedules")]
[Authorize(Roles = RoleNames.StaffRoles)]
public class MaintenanceSchedulesController : ControllerBase
{
    private readonly IMaintenanceScheduleService _scheduleService;
    private readonly IMaintenanceOrderService _orderService;
    private readonly MaintenanceScheduleEvaluationJob _evaluationJob;

    public MaintenanceSchedulesController(
        IMaintenanceScheduleService scheduleService,
        IMaintenanceOrderService orderService,
        MaintenanceScheduleEvaluationJob evaluationJob)
    {
        _scheduleService = scheduleService;
        _orderService = orderService;
        _evaluationJob = evaluationJob;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<MaintenanceScheduleDto>>> List([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        return Ok(await _scheduleService.ListAsync(page, pageSize, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MaintenanceScheduleDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _scheduleService.GetByIdAsync(id, cancellationToken));
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<MaintenanceScheduleDto>> SetStatus(Guid id, [FromBody] SetMaintenanceScheduleActiveRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _scheduleService.SetActiveStatusAsync(id, request.IsActive, cancellationToken));
    }

    [HttpGet("{id:guid}/orders")]
    public async Task<ActionResult<PagedResult<MaintenanceOrderDto>>> GetOrders(Guid id, [FromQuery] string? cursor, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        return Ok(await _orderService.ListByScheduleAsync(id, cursor, pageSize, cancellationToken));
    }

    // Crea el cronograma de activos ya Instalados con contrato activo que quedaron sin uno (instalados
    // antes de este rediseño, o creados fuera del flujo normal de check-out de instalación del técnico).
    [HttpPost("backfill")]
    public async Task<ActionResult<object>> Backfill(CancellationToken cancellationToken)
    {
        var created = await _scheduleService.BackfillMissingAsync(cancellationToken);
        return Ok(new { created });
    }

    // Dispara la misma evaluación que corre automáticamente por Hangfire, para pruebas/demo sin esperar al cron.
    [HttpPost("evaluate-now")]
    [Authorize(Roles = RoleNames.Administrador)]
    public async Task<ActionResult<object>> EvaluateNow(CancellationToken cancellationToken)
    {
        var ordersCreated = await _evaluationJob.RunAsync(cancellationToken);
        return Ok(new { ordersCreated });
    }
}
