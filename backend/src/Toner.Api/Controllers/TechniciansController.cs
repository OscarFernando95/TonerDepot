using FluentValidation;
using Toner.Application.Common.Paging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toner.Application.Technicians;
using Toner.Application.Technicians.Dtos;
using Toner.Domain.Common;

namespace Toner.Api.Controllers;

// Directorio de técnicos y su cobertura por ciudad, insumo del motor de asignación (módulo 7).
[ApiController]
[Route("api/technicians")]
[Authorize(Roles = RoleNames.StaffRoles)]
public class TechniciansController : ControllerBase
{
    private readonly ITechnicianService _technicianService;
    private readonly IValidator<AddTechnicianCoverageRequest> _addCoverageValidator;
    private readonly IValidator<AddTechnicianAssetRequest> _addAssetValidator;

    public TechniciansController(
        ITechnicianService technicianService,
        IValidator<AddTechnicianCoverageRequest> addCoverageValidator,
        IValidator<AddTechnicianAssetRequest> addAssetValidator)
    {
        _technicianService = technicianService;
        _addCoverageValidator = addCoverageValidator;
        _addAssetValidator = addAssetValidator;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<TechnicianDto>>> List([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        return Ok(await _technicianService.ListAsync(page, pageSize, cancellationToken));
    }

    [HttpGet("{id:guid}/coverage")]
    public async Task<ActionResult<IReadOnlyList<TechnicianCoverageDto>>> ListCoverage(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _technicianService.ListCoverageAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/coverage")]
    public async Task<ActionResult<TechnicianCoverageDto>> AddCoverage(Guid id, [FromBody] AddTechnicianCoverageRequest request, CancellationToken cancellationToken)
    {
        await _addCoverageValidator.ValidateAndThrowAsync(request, cancellationToken);

        var coverage = await _technicianService.AddCoverageAsync(id, request, cancellationToken);
        return CreatedAtAction(nameof(ListCoverage), new { id }, coverage);
    }

    [HttpDelete("{id:guid}/coverage/{coverageId:guid}")]
    public async Task<IActionResult> RemoveCoverage(Guid id, Guid coverageId, CancellationToken cancellationToken)
    {
        await _technicianService.RemoveCoverageAsync(id, coverageId, cancellationToken);
        return NoContent();
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
