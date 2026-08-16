using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toner.Application.Assets;
using Toner.Application.Assets.Dtos;
using Toner.Application.Common;
using Toner.Domain.Common;

namespace Toner.Api.Controllers;

// Nota: sin [Authorize(Roles=)] a nivel de clase a propósito (ver ServiceTicketsController) —
// cada acción declara su propio conjunto completo de roles.
[ApiController]
[Route("api/assets")]
public class AssetsController : ControllerBase
{
    private readonly IAssetService _assetService;
    private readonly IValidator<CreateAssetRequest> _createValidator;
    private readonly IValidator<UpdateAssetRequest> _updateValidator;
    private readonly IValidator<ChangeAssetStatusRequest> _changeStatusValidator;
    private readonly IValidator<CreateMeterReadingRequest> _meterReadingValidator;

    public AssetsController(
        IAssetService assetService,
        IValidator<CreateAssetRequest> createValidator,
        IValidator<UpdateAssetRequest> updateValidator,
        IValidator<ChangeAssetStatusRequest> changeStatusValidator,
        IValidator<CreateMeterReadingRequest> meterReadingValidator)
    {
        _assetService = assetService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _changeStatusValidator = changeStatusValidator;
        _meterReadingValidator = meterReadingValidator;
    }

    [HttpGet]
    [Authorize(Roles = RoleNames.StaffAndClientRoles)]
    public async Task<ActionResult<IReadOnlyList<AssetDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await _assetService.ListAsync(CurrentUser, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = RoleNames.StaffAndClientRoles)]
    public async Task<ActionResult<AssetDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _assetService.GetByIdAsync(CurrentUser, id, cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.StaffRoles)]
    public async Task<ActionResult<AssetDto>> Create([FromBody] CreateAssetRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var asset = await _assetService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = asset.Id }, asset);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.StaffRoles)]
    public async Task<ActionResult<AssetDto>> Update(Guid id, [FromBody] UpdateAssetRequest request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        return Ok(await _assetService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:guid}/status")]
    [Authorize(Roles = RoleNames.StaffRoles)]
    public async Task<ActionResult<AssetDto>> ChangeStatus(Guid id, [FromBody] ChangeAssetStatusRequest request, CancellationToken cancellationToken)
    {
        await _changeStatusValidator.ValidateAndThrowAsync(request, cancellationToken);

        return Ok(await _assetService.ChangeStatusAsync(id, request, CurrentUser.UserId, cancellationToken));
    }

    [HttpGet("{id:guid}/status-history")]
    [Authorize(Roles = RoleNames.StaffRoles)]
    public async Task<ActionResult<IReadOnlyList<AssetStatusLogDto>>> GetStatusHistory(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _assetService.GetStatusHistoryAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/meter-readings")]
    [Authorize(Roles = RoleNames.StaffRoles)]
    public async Task<ActionResult<MeterReadingDto>> AddMeterReading(Guid id, [FromBody] CreateMeterReadingRequest request, CancellationToken cancellationToken)
    {
        await _meterReadingValidator.ValidateAndThrowAsync(request, cancellationToken);

        var reading = await _assetService.AddMeterReadingAsync(id, request, CurrentUser, cancellationToken);
        return CreatedAtAction(nameof(GetMeterReadings), new { id }, reading);
    }

    [HttpGet("{id:guid}/meter-readings")]
    [Authorize(Roles = RoleNames.StaffRoles)]
    public async Task<ActionResult<IReadOnlyList<MeterReadingDto>>> GetMeterReadings(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _assetService.GetMeterReadingsAsync(id, cancellationToken));
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
