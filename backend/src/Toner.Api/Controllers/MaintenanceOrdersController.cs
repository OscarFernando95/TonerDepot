using System.Security.Claims;
using Toner.Application.Common.Paging;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toner.Application.Common;
using Toner.Application.Common.Dtos;
using Toner.Application.Maintenance;
using Toner.Application.Maintenance.Dtos;
using Toner.Domain.Common;

namespace Toner.Api.Controllers;

// Nota: sin [Authorize(Roles=)] a nivel de clase a propósito (ver ServiceTicketsController /
// TechnicianSelfServiceController) — cada acción declara su propio conjunto completo de roles.
[ApiController]
[Route("api/maintenance-orders")]
public class MaintenanceOrdersController : ControllerBase
{
    private readonly IMaintenanceOrderService _orderService;
    private readonly IValidator<AssignMaintenanceOrderRequest> _assignValidator;
    private readonly IValidator<CompleteMaintenanceOrderRequest> _completeValidator;

    public MaintenanceOrdersController(
        IMaintenanceOrderService orderService,
        IValidator<AssignMaintenanceOrderRequest> assignValidator,
        IValidator<CompleteMaintenanceOrderRequest> completeValidator)
    {
        _orderService = orderService;
        _assignValidator = assignValidator;
        _completeValidator = completeValidator;
    }

    [HttpGet]
    [Authorize(Roles = RoleNames.StaffAndTechnicianRoles)]
    public async Task<ActionResult<PagedResult<MaintenanceOrderDto>>> List([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        return Ok(await _orderService.ListAsync(CurrentUser, page, pageSize, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = RoleNames.StaffAndTechnicianRoles)]
    public async Task<ActionResult<MaintenanceOrderDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _orderService.GetByIdAsync(CurrentUser, id, cancellationToken));
    }

    [HttpPost("{id:guid}/assign")]
    [Authorize(Roles = RoleNames.StaffRoles)]
    public async Task<ActionResult<MaintenanceOrderDto>> Assign(Guid id, [FromBody] AssignMaintenanceOrderRequest request, CancellationToken cancellationToken)
    {
        await _assignValidator.ValidateAndThrowAsync(request, cancellationToken);

        return Ok(await _orderService.AssignAsync(id, request, CurrentUser.UserId, cancellationToken));
    }

    [HttpPost("{id:guid}/complete")]
    [Authorize(Roles = RoleNames.StaffRoles)]
    public async Task<ActionResult<MaintenanceOrderDto>> Complete(Guid id, [FromBody] CompleteMaintenanceOrderRequest request, CancellationToken cancellationToken)
    {
        await _completeValidator.ValidateAndThrowAsync(request, cancellationToken);

        return Ok(await _orderService.CompleteAsync(id, request, CurrentUser.UserId, cancellationToken));
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = RoleNames.StaffRoles)]
    public async Task<ActionResult<MaintenanceOrderDto>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _orderService.CancelAsync(id, cancellationToken));
    }

    [HttpGet("{id:guid}/assignment-history")]
    [Authorize(Roles = RoleNames.StaffRoles)]
    public async Task<ActionResult<PagedResult<AssignmentHistoryDto>>> GetAssignmentHistory(Guid id, [FromQuery] string? cursor, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        return Ok(await _orderService.GetAssignmentHistoryAsync(id, cursor, pageSize, cancellationToken));
    }

    private RequestingUser CurrentUser
    {
        get
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = User.FindFirstValue(ClaimTypes.Role)!;
            var clientIdClaim = User.FindFirstValue("client_id");
            var clientId = clientIdClaim is not null ? Guid.Parse(clientIdClaim) : (Guid?)null;
            var technicianIdClaim = User.FindFirstValue("technician_id");
            var technicianId = technicianIdClaim is not null ? Guid.Parse(technicianIdClaim) : (Guid?)null;
            return new RequestingUser(userId, role, clientId, technicianId);
        }
    }
}
