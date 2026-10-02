using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toner.Application.Zones;
using Toner.Application.Zones.Dtos;
using Toner.Domain.Common;

namespace Toner.Api.Controllers;

// Zonas de cobertura: agrupan municipios y se asignan a los técnicos. Solo staff.
[ApiController]
[Route("api/zones")]
[Authorize(Roles = RoleNames.StaffRoles)]
public class ZonesController : ControllerBase
{
    private readonly IZoneService _zoneService;
    private readonly IValidator<CreateZoneRequest> _createValidator;
    private readonly IValidator<UpdateZoneRequest> _updateValidator;
    private readonly IValidator<SetZoneCitiesRequest> _setCitiesValidator;

    public ZonesController(
        IZoneService zoneService,
        IValidator<CreateZoneRequest> createValidator,
        IValidator<UpdateZoneRequest> updateValidator,
        IValidator<SetZoneCitiesRequest> setCitiesValidator)
    {
        _zoneService = zoneService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _setCitiesValidator = setCitiesValidator;
    }

    // Catálogo de desplegable (pocas filas): no se pagina.
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ZoneDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await _zoneService.ListAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<ZoneDto>> Create([FromBody] CreateZoneRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var zone = await _zoneService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(List), new { }, zone);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ZoneDto>> Rename(Guid id, [FromBody] UpdateZoneRequest request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        return Ok(await _zoneService.RenameAsync(id, request, cancellationToken));
    }

    [HttpPut("{id:guid}/cities")]
    public async Task<ActionResult<ZoneDto>> SetCities(Guid id, [FromBody] SetZoneCitiesRequest request, CancellationToken cancellationToken)
    {
        await _setCitiesValidator.ValidateAndThrowAsync(request, cancellationToken);

        return Ok(await _zoneService.SetCitiesAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _zoneService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
