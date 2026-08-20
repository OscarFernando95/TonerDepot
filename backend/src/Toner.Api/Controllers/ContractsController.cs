using System.Security.Claims;
using Toner.Application.Common.Paging;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toner.Application.Common;
using Toner.Application.Contracts;
using Toner.Application.Contracts.Dtos;
using Toner.Domain.Common;

namespace Toner.Api.Controllers;

// Nota: sin [Authorize(Roles=)] a nivel de clase a propósito (ver ServiceTicketsController) —
// cada acción declara su propio conjunto completo de roles.
[ApiController]
[Route("api/contracts")]
public class ContractsController : ControllerBase
{
    private readonly IContractService _contractService;
    private readonly IValidator<CreateContractRequest> _createValidator;
    private readonly IValidator<UpdateContractRequest> _updateValidator;
    private readonly IValidator<SetContractStatusRequest> _statusValidator;

    public ContractsController(
        IContractService contractService,
        IValidator<CreateContractRequest> createValidator,
        IValidator<UpdateContractRequest> updateValidator,
        IValidator<SetContractStatusRequest> statusValidator)
    {
        _contractService = contractService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _statusValidator = statusValidator;
    }

    [HttpGet]
    [Authorize(Roles = RoleNames.StaffAndClientRoles)]
    public async Task<ActionResult<PagedResult<ContractDto>>> List([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        return Ok(await _contractService.ListAsync(CurrentUser, page, pageSize, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = RoleNames.StaffAndClientRoles)]
    public async Task<ActionResult<ContractDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _contractService.GetByIdAsync(CurrentUser, id, cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.StaffRoles)]
    public async Task<ActionResult<ContractDto>> Create([FromBody] CreateContractRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var contract = await _contractService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = contract.Id }, contract);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.StaffRoles)]
    public async Task<ActionResult<ContractDto>> Update(Guid id, [FromBody] UpdateContractRequest request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        return Ok(await _contractService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = RoleNames.StaffRoles)]
    public async Task<ActionResult<ContractDto>> SetStatus(Guid id, [FromBody] SetContractStatusRequest request, CancellationToken cancellationToken)
    {
        await _statusValidator.ValidateAndThrowAsync(request, cancellationToken);

        return Ok(await _contractService.SetStatusAsync(id, request.Status, cancellationToken));
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
