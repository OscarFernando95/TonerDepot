using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toner.Application.Clients;
using Toner.Application.Clients.Dtos;
using Toner.Application.Common.Exceptions;
using Toner.Domain.Common;

namespace Toner.Api.Controllers;

// Nota: no hay [Authorize] a nivel de clase a propósito. ASP.NET Core combina los roles de un
// [Authorize(Roles=)] de clase y uno de método con AND (intersección), no que el de método reemplace
// al de clase — así que cada acción declara su propio conjunto completo de roles permitidos.
[ApiController]
[Route("api/clients/{clientId:guid}/locations")]
public class ClientLocationsController : ControllerBase
{
    private readonly IClientLocationService _locationService;
    private readonly IValidator<CreateClientLocationRequest> _createValidator;
    private readonly IValidator<UpdateClientLocationRequest> _updateValidator;

    public ClientLocationsController(
        IClientLocationService locationService,
        IValidator<CreateClientLocationRequest> createValidator,
        IValidator<UpdateClientLocationRequest> updateValidator)
    {
        _locationService = locationService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    // Además del staff, el propio Cliente puede leer las sedes de SU cliente (ej. para reportar un ticket).
    [HttpGet]
    [Authorize(Roles = RoleNames.StaffAndClientRoles)]
    public async Task<ActionResult<IReadOnlyList<ClientLocationDto>>> List(Guid clientId, CancellationToken cancellationToken)
    {
        if (User.IsInRole(RoleNames.Cliente))
        {
            var myClientId = Guid.Parse(User.FindFirstValue("client_id")!);
            if (myClientId != clientId)
            {
                throw new ForbiddenException("No puedes ver sedes de un cliente distinto al tuyo.");
            }
        }

        return Ok(await _locationService.ListByClientAsync(clientId, cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.StaffRoles)]
    public async Task<ActionResult<ClientLocationDto>> Create(Guid clientId, [FromBody] CreateClientLocationRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var location = await _locationService.CreateAsync(clientId, request, cancellationToken);
        return CreatedAtAction(nameof(List), new { clientId }, location);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.StaffRoles)]
    public async Task<ActionResult<ClientLocationDto>> Update(Guid clientId, Guid id, [FromBody] UpdateClientLocationRequest request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        return Ok(await _locationService.UpdateAsync(clientId, id, request, cancellationToken));
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = RoleNames.StaffRoles)]
    public async Task<ActionResult<ClientLocationDto>> SetStatus(Guid clientId, Guid id, [FromBody] SetActiveStatusRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _locationService.SetActiveStatusAsync(clientId, id, request.IsActive, cancellationToken));
    }
}
