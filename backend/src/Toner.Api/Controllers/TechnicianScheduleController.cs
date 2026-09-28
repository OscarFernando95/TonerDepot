using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toner.Application.Calendar;
using Toner.Application.Calendar.Dtos;
using Toner.Domain.Common;

namespace Toner.Api.Controllers;

// Horario laboral y "fuera de la oficina" de cada técnico. Insumo de la asignación y del cálculo de SLA.
[ApiController]
[Route("api/technicians/{technicianId:guid}")]
[Authorize(Roles = RoleNames.StaffRoles)]
public class TechnicianScheduleController : ControllerBase
{
    private readonly ITechnicianScheduleService _service;
    private readonly IValidator<SetTechnicianScheduleRequest> _scheduleValidator;
    private readonly IValidator<CreateTimeOffRequest> _timeOffValidator;

    public TechnicianScheduleController(
        ITechnicianScheduleService service,
        IValidator<SetTechnicianScheduleRequest> scheduleValidator,
        IValidator<CreateTimeOffRequest> timeOffValidator)
    {
        _service = service;
        _scheduleValidator = scheduleValidator;
        _timeOffValidator = timeOffValidator;
    }

    [HttpGet("schedule")]
    public async Task<ActionResult<TechnicianScheduleDto>> GetSchedule(Guid technicianId, CancellationToken cancellationToken) =>
        Ok(await _service.GetScheduleAsync(technicianId, cancellationToken));

    [HttpPut("schedule")]
    public async Task<ActionResult<TechnicianScheduleDto>> SetSchedule(Guid technicianId, [FromBody] SetTechnicianScheduleRequest request, CancellationToken cancellationToken)
    {
        await _scheduleValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await _service.SetScheduleAsync(technicianId, request, cancellationToken));
    }

    [HttpDelete("schedule")]
    public async Task<ActionResult<TechnicianScheduleDto>> ResetSchedule(Guid technicianId, CancellationToken cancellationToken) =>
        Ok(await _service.ResetScheduleAsync(technicianId, cancellationToken));

    [HttpGet("time-off")]
    public async Task<ActionResult<IReadOnlyList<TimeOffDto>>> ListTimeOff(Guid technicianId, CancellationToken cancellationToken) =>
        Ok(await _service.ListTimeOffAsync(technicianId, cancellationToken));

    [HttpPost("time-off")]
    public async Task<ActionResult<TimeOffDto>> AddTimeOff(Guid technicianId, [FromBody] CreateTimeOffRequest request, CancellationToken cancellationToken)
    {
        await _timeOffValidator.ValidateAndThrowAsync(request, cancellationToken);
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await _service.AddTimeOffAsync(technicianId, request, userId, cancellationToken));
    }

    [HttpPost("time-off/{timeOffId:guid}/cancel")]
    public async Task<ActionResult<TimeOffDto>> CancelTimeOff(Guid technicianId, Guid timeOffId, CancellationToken cancellationToken) =>
        Ok(await _service.CancelTimeOffAsync(technicianId, timeOffId, cancellationToken));
}
