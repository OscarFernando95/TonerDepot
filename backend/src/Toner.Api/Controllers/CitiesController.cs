using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toner.Application.Cities;
using Toner.Application.Cities.Dtos;
using Toner.Domain.Common;

namespace Toner.Api.Controllers;

// Solo lectura: las ciudades son un catálogo fijo, sembrado desde el dataset de municipios de Colombia
// (ver DataSeeder). Ya no hay endpoint para crear ciudades a mano.
[ApiController]
[Route("api/cities")]
[Authorize(Roles = RoleNames.StaffRoles)]
public class CitiesController : ControllerBase
{
    private readonly ICityService _cityService;

    public CitiesController(ICityService cityService)
    {
        _cityService = cityService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CityDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await _cityService.ListAsync(cancellationToken));
    }
}
