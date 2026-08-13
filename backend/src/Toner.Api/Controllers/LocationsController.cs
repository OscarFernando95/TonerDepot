using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toner.Application.Clients;
using Toner.Application.Clients.Dtos;
using Toner.Domain.Common;

namespace Toner.Api.Controllers;

// Listado plano de sedes (todas, de todos los clientes) para selectores como "instalar activo en sede".
// El CRUD de sedes por cliente vive en ClientLocationsController (api/clients/{clientId}/locations).
[ApiController]
[Route("api/locations")]
[Authorize(Roles = RoleNames.StaffRoles)]
public class LocationsController : ControllerBase
{
    private readonly IClientLocationService _locationService;

    public LocationsController(IClientLocationService locationService)
    {
        _locationService = locationService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClientLocationDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await _locationService.ListAllAsync(cancellationToken));
    }
}
