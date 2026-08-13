using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toner.Application.Common;
using Toner.Application.Common.Dtos;
using Toner.Application.Tickets;
using Toner.Application.Tickets.Dtos;
using Toner.Domain.Common;

namespace Toner.Api.Controllers;

// Nota: sin [Authorize(Roles=)] a nivel de clase a propósito — ver TechnicianSelfServiceController
// para el porqué (ASP.NET Core combina roles de clase+método con AND, no que el de método reemplace
// al de clase). Cada acción declara aquí su propio conjunto completo de roles permitidos.
[ApiController]
[Route("api/tickets")]
public class ServiceTicketsController : ControllerBase
{
    private readonly IServiceTicketService _ticketService;
    private readonly IValidator<CreateServiceTicketRequest> _createValidator;
    private readonly IValidator<AssignTicketRequest> _assignValidator;
    private readonly IValidator<SetTicketStatusRequest> _statusValidator;

    public ServiceTicketsController(
        IServiceTicketService ticketService,
        IValidator<CreateServiceTicketRequest> createValidator,
        IValidator<AssignTicketRequest> assignValidator,
        IValidator<SetTicketStatusRequest> statusValidator)
    {
        _ticketService = ticketService;
        _createValidator = createValidator;
        _assignValidator = assignValidator;
        _statusValidator = statusValidator;
    }

    [HttpGet]
    [Authorize(Roles = RoleNames.StaffClientAndTechnicianRoles)]
    public async Task<ActionResult<IReadOnlyList<ServiceTicketDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await _ticketService.ListAsync(CurrentUser, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = RoleNames.StaffClientAndTechnicianRoles)]
    public async Task<ActionResult<ServiceTicketDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _ticketService.GetByIdAsync(CurrentUser, id, cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.StaffAndClientRoles)]
    public async Task<ActionResult<ServiceTicketDto>> Create([FromBody] CreateServiceTicketRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var ticket = await _ticketService.CreateAsync(CurrentUser, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, ticket);
    }

    [HttpPost("{id:guid}/assign")]
    [Authorize(Roles = RoleNames.StaffRoles)]
    public async Task<ActionResult<ServiceTicketDto>> Assign(Guid id, [FromBody] AssignTicketRequest request, CancellationToken cancellationToken)
    {
        await _assignValidator.ValidateAndThrowAsync(request, cancellationToken);

        return Ok(await _ticketService.AssignAsync(id, request, CurrentUser.UserId, cancellationToken));
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = RoleNames.StaffRoles)]
    public async Task<ActionResult<ServiceTicketDto>> SetStatus(Guid id, [FromBody] SetTicketStatusRequest request, CancellationToken cancellationToken)
    {
        await _statusValidator.ValidateAndThrowAsync(request, cancellationToken);

        return Ok(await _ticketService.SetStatusAsync(id, request.Status, cancellationToken));
    }

    [HttpGet("{id:guid}/assignment-history")]
    [Authorize(Roles = RoleNames.StaffRoles)]
    public async Task<ActionResult<IReadOnlyList<AssignmentHistoryDto>>> GetAssignmentHistory(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _ticketService.GetAssignmentHistoryAsync(id, cancellationToken));
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
