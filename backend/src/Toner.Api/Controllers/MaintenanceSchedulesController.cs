using FluentValidation;
using Microsoft.AspNetCore.Authorization;
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
    private readonly IValidator<CreateMaintenanceScheduleRequest> _createValidator;
    private readonly IValidator<UpdateMaintenanceScheduleRequest> _updateValidator;

    public MaintenanceSchedulesController(
        IMaintenanceScheduleService scheduleService,
        IMaintenanceOrderService orderService,
        MaintenanceScheduleEvaluationJob evaluationJob,
        IValidator<CreateMaintenanceScheduleRequest> createValidator,
        IValidator<UpdateMaintenanceScheduleRequest> updateValidator)
    {
        _scheduleService = scheduleService;
        _orderService = orderService;
        _evaluationJob = evaluationJob;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MaintenanceScheduleDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await _scheduleService.ListAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MaintenanceScheduleDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _scheduleService.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<MaintenanceScheduleDto>> Create([FromBody] CreateMaintenanceScheduleRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var schedule = await _scheduleService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = schedule.Id }, schedule);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MaintenanceScheduleDto>> Update(Guid id, [FromBody] UpdateMaintenanceScheduleRequest request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        return Ok(await _scheduleService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<MaintenanceScheduleDto>> SetStatus(Guid id, [FromBody] SetMaintenanceScheduleActiveRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _scheduleService.SetActiveStatusAsync(id, request.IsActive, cancellationToken));
    }

    [HttpGet("{id:guid}/orders")]
    public async Task<ActionResult<IReadOnlyList<MaintenanceOrderDto>>> GetOrders(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _orderService.ListByScheduleAsync(id, cancellationToken));
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
