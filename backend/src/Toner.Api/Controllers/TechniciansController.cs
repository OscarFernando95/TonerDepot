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

    public TechniciansController(ITechnicianService technicianService, IValidator<AddTechnicianCoverageRequest> addCoverageValidator)
    {
        _technicianService = technicianService;
        _addCoverageValidator = addCoverageValidator;
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

    [HttpGet("{id:guid}/time-logs")]
    public async Task<ActionResult<PagedResult<TimeLogDto>>> ListTimeLogs(Guid id, [FromQuery] string? cursor, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        return Ok(await _technicianService.ListTimeLogsAsync(id, cursor, pageSize, cancellationToken));
    }
}
