using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toner.Application.Common.Exceptions;
using Toner.Application.Technicians;
using Toner.Application.Technicians.Dtos;
using Toner.Domain.Common;

namespace Toner.Api.Controllers;

// Autoservicio del propio técnico: check-in/check-out. Deliberadamente en un controller aparte de
// TechniciansController (que es solo-Staff) para no mezclar roles en [Authorize(Roles=)] de clase y
// método — ASP.NET Core combina ambos con AND, no que el de método reemplace al de clase.
[ApiController]
[Route("api/technicians/me")]
[Authorize(Roles = RoleNames.Tecnico)]
public class TechnicianSelfServiceController : ControllerBase
{
    private readonly ITechnicianCheckInService _checkInService;
    private readonly IValidator<CheckInRequest> _checkInValidator;

    public TechnicianSelfServiceController(ITechnicianCheckInService checkInService, IValidator<CheckInRequest> checkInValidator)
    {
        _checkInService = checkInService;
        _checkInValidator = checkInValidator;
    }

    [HttpGet("status")]
    public async Task<ActionResult<TechnicianSelfStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        return Ok(await _checkInService.GetMyStatusAsync(CurrentTechnicianId, cancellationToken));
    }

    [HttpPost("check-in")]
    public async Task<ActionResult<TechnicianSelfStatusDto>> CheckIn([FromBody] CheckInRequest request, CancellationToken cancellationToken)
    {
        await _checkInValidator.ValidateAndThrowAsync(request, cancellationToken);

        return Ok(await _checkInService.CheckInAsync(CurrentTechnicianId, request, cancellationToken));
    }

    [HttpPost("check-out")]
    public async Task<ActionResult<TechnicianSelfStatusDto>> CheckOut([FromBody] CheckOutRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _checkInService.CheckOutAsync(CurrentTechnicianId, request, cancellationToken));
    }

    private Guid CurrentTechnicianId =>
        Guid.TryParse(User.FindFirstValue("technician_id"), out var id)
            ? id
            : throw new ForbiddenException("Tu usuario no tiene un perfil de técnico asociado.");
}
