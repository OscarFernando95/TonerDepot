using System.Security.Claims;
using Toner.Application.Common.Paging;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toner.Application.Assets;
using Toner.Application.Assets.Dtos;
using Toner.Application.Common;
using Toner.Domain.Common;

namespace Toner.Api.Controllers;

// Módulo restringido a Admin/Coordinador/Técnico — es la vía principal para mantener los cronogramas de
// mantenimiento al día, pero Cliente y Ventas no deben poder leer ni registrar contadores.
[ApiController]
[Route("api/meter-readings")]
[Authorize(Roles = RoleNames.StaffAndTechnicianRoles)]
public class MeterReadingsController : ControllerBase
{
    private readonly IAssetService _assetService;
    private readonly IValidator<CreateMeterReadingRequest> _createValidator;

    public MeterReadingsController(IAssetService assetService, IValidator<CreateMeterReadingRequest> createValidator)
    {
        _assetService = assetService;
        _createValidator = createValidator;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<MeterReadingAssetDto>>> List([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        return Ok(await _assetService.ListForMeterReadingAsync(CurrentUser, page, pageSize, cancellationToken));
    }

    // Pestaña de respaldo (app móvil, solo Técnico) — activos instalados en sus ciudades de cobertura,
    // para cuando el técnico titular de un activo no está disponible (vacaciones, incapacidad,
    // renuncia/despido) y de otro modo quedaría sin nadie que le registre lecturas.
    [HttpGet("by-coverage")]
    public async Task<ActionResult<PagedResult<MeterReadingAssetDto>>> ListByCoverage([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        return Ok(await _assetService.ListForMeterReadingByCoverageAsync(CurrentUser, page, pageSize, cancellationToken));
    }

    [HttpPost("{assetId:guid}")]
    public async Task<ActionResult<MeterReadingDto>> Register(Guid assetId, [FromBody] CreateMeterReadingRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        return Ok(await _assetService.AddMeterReadingAsync(assetId, request, CurrentUser, cancellationToken));
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
