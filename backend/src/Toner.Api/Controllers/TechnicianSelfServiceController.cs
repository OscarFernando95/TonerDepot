using System.Security.Claims;
using Toner.Application.Common.Paging;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toner.Application.Assets;
using Toner.Application.Assets.Dtos;
using Toner.Application.Common.Exceptions;
using Toner.Application.Maintenance;
using Toner.Application.Maintenance.Dtos;
using Toner.Application.Technicians;
using Toner.Application.Technicians.Dtos;
using Toner.Application.Tickets;
using Toner.Application.Tickets.Dtos;
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
    private readonly IAssetService _assetService;
    private readonly IMaintenanceOrderService _maintenanceOrderService;
    private readonly IMaintenanceScheduleService _maintenanceScheduleService;
    private readonly IServiceTicketService _serviceTicketService;

    public TechnicianSelfServiceController(
        ITechnicianCheckInService checkInService,
        IValidator<CheckInRequest> checkInValidator,
        IAssetService assetService,
        IMaintenanceOrderService maintenanceOrderService,
        IMaintenanceScheduleService maintenanceScheduleService,
        IServiceTicketService serviceTicketService)
    {
        _checkInService = checkInService;
        _checkInValidator = checkInValidator;
        _assetService = assetService;
        _maintenanceOrderService = maintenanceOrderService;
        _maintenanceScheduleService = maintenanceScheduleService;
        _serviceTicketService = serviceTicketService;
    }

    [HttpGet("status")]
    public async Task<ActionResult<TechnicianSelfStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        return Ok(await _checkInService.GetMyStatusAsync(CurrentTechnicianId, cancellationToken));
    }

    // Lista abierta entre los técnicos que cubren la ciudad del activo — no solo las propias.
    [HttpGet("pending-installations")]
    public async Task<ActionResult<PagedResult<PendingInstallationDto>>> GetPendingInstallations([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        return Ok(await _assetService.ListPendingInstallationsAsync(CurrentTechnicianId, page, pageSize, cancellationToken));
    }

    // Solo lectura: órdenes de mantenimiento asignadas a otros técnicos, en ciudades que este técnico
    // cubre. No habilita check-in — el motor de asignación sigue eligiendo un único técnico por orden.
    [HttpGet("coverage-maintenance-orders")]
    public async Task<ActionResult<PagedResult<MaintenanceOrderDto>>> GetCoverageMaintenanceOrders([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        return Ok(await _maintenanceOrderService.ListInCoverageAsync(CurrentTechnicianId, page, pageSize, cancellationToken));
    }

    // Cronogramas de activos en ciudades cubiertas por el técnico — misma info que ve Staff en el
    // módulo de Cronogramas.
    [HttpGet("coverage-schedules")]
    public async Task<ActionResult<PagedResult<MaintenanceScheduleDto>>> GetCoverageSchedules([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        return Ok(await _maintenanceScheduleService.ListInCoverageAsync(CurrentTechnicianId, page, pageSize, cancellationToken));
    }

    // Igual que coverage-maintenance-orders pero para tickets: tickets activos de otros técnicos en
    // ciudades cubiertas. Los que aún no arrancó nadie se pueden tomar vía POST tickets/{id}/claim.
    [HttpGet("coverage-tickets")]
    public async Task<ActionResult<PagedResult<ServiceTicketDto>>> GetCoverageTickets([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        return Ok(await _serviceTicketService.ListInCoverageAsync(CurrentTechnicianId, page, pageSize, cancellationToken));
    }

    // Autoasignación: toma una orden libre o asignada a otro técnico que aún no la inició.
    [HttpPost("maintenance-orders/{id:guid}/claim")]
    public async Task<ActionResult<MaintenanceOrderDto>> ClaimMaintenanceOrder(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _maintenanceOrderService.ClaimAsync(id, CurrentTechnicianId, cancellationToken));
    }

    // Autoasignación: toma un ticket libre o asignado a otro técnico que aún no lo inició.
    [HttpPost("tickets/{id:guid}/claim")]
    public async Task<ActionResult<ServiceTicketDto>> ClaimTicket(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _serviceTicketService.ClaimAsync(id, CurrentTechnicianId, cancellationToken));
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
