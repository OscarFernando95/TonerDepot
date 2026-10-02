using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toner.Application.Common;
using Toner.Application.Common.Paging;
using Toner.Application.Inventory;
using Toner.Application.Inventory.Dtos;
using Toner.Domain.Common;

namespace Toner.Api.Controllers;

// Inventario en el campo: el kit base y los repuestos que el técnico ve al cerrar una visita, y el registro de tóner por
// máquina. Staff y técnicos; el servicio limita al técnico a las máquinas que tiene vinculadas. (El resto de
// /api/inventory — catálogo, traspasos, ajustes — es solo staff, ver InventoryController.)
[ApiController]
[Route("api/inventory")]
public class VisitInventoryController : ControllerBase
{
    private readonly IInventoryConsumptionService _consumption;
    private readonly IValidator<RegisterTonerRequest> _tonerValidator;

    public VisitInventoryController(IInventoryConsumptionService consumption, IValidator<RegisterTonerRequest> tonerValidator)
    {
        _consumption = consumption;
        _tonerValidator = tonerValidator;
    }

    // Kit base (efectivo) del modelo de la máquina con el saldo de su zona. Sin assetId (ticket de equipo no catalogado)
    // devuelve el kit vacío y la bodega principal como origen.
    [HttpGet("kit")]
    [Authorize(Roles = RoleNames.StaffAndTechnicianRoles)]
    public async Task<ActionResult<VisitKitDto>> GetKit([FromQuery] Guid? assetId, CancellationToken cancellationToken)
    {
        return Ok(await _consumption.GetKitForAssetAsync(assetId, cancellationToken));
    }

    [HttpGet("parts")]
    [Authorize(Roles = RoleNames.StaffAndTechnicianRoles)]
    public async Task<ActionResult<PagedResult<PartOptionDto>>> SearchParts(
        [FromQuery] Guid? assetId, [FromQuery] string? search, [FromQuery] string? category, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        return Ok(await _consumption.SearchPartsAsync(assetId, search, page, pageSize, cancellationToken, category));
    }

    [HttpPost("toner")]
    [Authorize(Roles = RoleNames.StaffAndTechnicianRoles)]
    public async Task<ActionResult<TonerEntryDto>> RegisterToner([FromBody] RegisterTonerRequest request, CancellationToken cancellationToken)
    {
        await _tonerValidator.ValidateAndThrowAsync(request, cancellationToken);

        return Ok(await _consumption.RegisterTonerAsync(request, CurrentUser, cancellationToken));
    }

    [HttpGet("toner/assets/{assetId:guid}")]
    [Authorize(Roles = RoleNames.StaffAndTechnicianRoles)]
    public async Task<ActionResult<PagedResult<TonerEntryDto>>> ListToner(
        Guid assetId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? cursor, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        return Ok(await _consumption.ListTonerAsync(assetId, from, to, cursor, pageSize, CurrentUser, cancellationToken));
    }

    private RequestingUser CurrentUser
    {
        get
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = User.FindFirstValue(ClaimTypes.Role)!;
            var technicianIdClaim = User.FindFirstValue("technician_id");
            var technicianId = technicianIdClaim is not null ? Guid.Parse(technicianIdClaim) : (Guid?)null;
            return new RequestingUser(userId, role, null, technicianId);
        }
    }
}
