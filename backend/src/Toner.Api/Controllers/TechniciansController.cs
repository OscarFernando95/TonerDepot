using FluentValidation;
using Toner.Application.Common.Paging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toner.Application.Technicians;
using Toner.Application.Technicians.Dtos;
using Toner.Domain.Common;

namespace Toner.Api.Controllers;

// Directorio de técnicos y sus zonas (de ahí se deriva su cobertura por municipio), insumo del motor de asignación.
[ApiController]
[Route("api/technicians")]
[Authorize(Roles = RoleNames.StaffRoles)]
public class TechniciansController : ControllerBase
{
    private readonly ITechnicianService _technicianService;
    private readonly IValidator<SetTechnicianZonesRequest> _setZonesValidator;
    private readonly IValidator<AddTechnicianAssetRequest> _addAssetValidator;

    public TechniciansController(
        ITechnicianService technicianService,
        IValidator<SetTechnicianZonesRequest> setZonesValidator,
        IValidator<AddTechnicianAssetRequest> addAssetValidator)
    {
        _technicianService = technicianService;
        _setZonesValidator = setZonesValidator;
        _addAssetValidator = addAssetValidator;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<TechnicianDto>>> List([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        return Ok(await _technicianService.ListAsync(page, pageSize, cancellationToken));
    }

    [HttpGet("{id:guid}/zones")]
    public async Task<ActionResult<IReadOnlyList<TechnicianZoneDto>>> ListZones(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _technicianService.ListZonesAsync(id, cancellationToken));
    }

    // Reemplaza el conjunto de zonas del técnico (normalmente una).
    [HttpPut("{id:guid}/zones")]
    public async Task<ActionResult<IReadOnlyList<TechnicianZoneDto>>> SetZones(Guid id, [FromBody] SetTechnicianZonesRequest request, CancellationToken cancellationToken)
    {
        await _setZonesValidator.ValidateAndThrowAsync(request, cancellationToken);

        return Ok(await _technicianService.SetZonesAsync(id, request, cancellationToken));
    }

    [HttpGet("{id:guid}/assets")]
    public async Task<ActionResult<IReadOnlyList<TechnicianAssetDto>>> ListLinkedAssets(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _technicianService.ListLinkedAssetsAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/assets")]
    public async Task<ActionResult<TechnicianAssetDto>> LinkAsset(Guid id, [FromBody] AddTechnicianAssetRequest request, CancellationToken cancellationToken)
    {
        await _addAssetValidator.ValidateAndThrowAsync(request, cancellationToken);

        var link = await _technicianService.LinkAssetAsync(id, request, cancellationToken);
        return CreatedAtAction(nameof(ListLinkedAssets), new { id }, link);
    }

    [HttpDelete("{id:guid}/assets/{technicianAssetId:guid}")]
    public async Task<IActionResult> UnlinkAsset(Guid id, Guid technicianAssetId, CancellationToken cancellationToken)
    {
        await _technicianService.UnlinkAssetAsync(id, technicianAssetId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/time-logs")]
    public async Task<ActionResult<PagedResult<TimeLogDto>>> ListTimeLogs(Guid id, [FromQuery] string? cursor, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        return Ok(await _technicianService.ListTimeLogsAsync(id, cursor, pageSize, cancellationToken));
    }
}
