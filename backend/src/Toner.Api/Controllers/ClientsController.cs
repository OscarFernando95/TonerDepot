using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toner.Application.Clients;
using Toner.Application.Clients.Dtos;
using Toner.Domain.Common;

namespace Toner.Api.Controllers;

[ApiController]
[Route("api/clients")]
[Authorize(Roles = RoleNames.StaffRoles)]
public class ClientsController : ControllerBase
{
    private readonly IClientService _clientService;
    private readonly IValidator<CreateClientRequest> _createValidator;
    private readonly IValidator<UpdateClientRequest> _updateValidator;

    public ClientsController(
        IClientService clientService,
        IValidator<CreateClientRequest> createValidator,
        IValidator<UpdateClientRequest> updateValidator)
    {
        _clientService = clientService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClientDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await _clientService.ListAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClientDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _clientService.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<ClientDto>> Create([FromBody] CreateClientRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var client = await _clientService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = client.Id }, client);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ClientDto>> Update(Guid id, [FromBody] UpdateClientRequest request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        return Ok(await _clientService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<ClientDto>> SetStatus(Guid id, [FromBody] SetActiveStatusRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _clientService.SetActiveStatusAsync(id, request.IsActive, cancellationToken));
    }
}
