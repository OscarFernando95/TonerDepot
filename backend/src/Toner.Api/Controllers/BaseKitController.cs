using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toner.Application.Inventory;
using Toner.Application.Inventory.Dtos;
using Toner.Domain.Common;

namespace Toner.Api.Controllers;

// Kit base de consumibles: se define por marca y cada modelo lo hereda y lo puede ajustar. Solo staff.
[ApiController]
[Route("api/asset-brands")]
[Authorize(Roles = RoleNames.StaffRoles)]
public class BaseKitController : ControllerBase
{
    private readonly IBaseKitService _kits;
    private readonly IValidator<SetBrandKitRequest> _brandValidator;
    private readonly IValidator<SetModelKitRequest> _modelValidator;

    public BaseKitController(IBaseKitService kits, IValidator<SetBrandKitRequest> brandValidator, IValidator<SetModelKitRequest> modelValidator)
    {
        _kits = kits;
        _brandValidator = brandValidator;
        _modelValidator = modelValidator;
    }

    [HttpGet("{brandId:guid}/base-items")]
    public async Task<ActionResult<IReadOnlyList<BaseKitItemDto>>> GetBrandKit(Guid brandId, CancellationToken cancellationToken)
    {
        return Ok(await _kits.GetBrandKitAsync(brandId, cancellationToken));
    }

    [HttpPut("{brandId:guid}/base-items")]
    public async Task<ActionResult<IReadOnlyList<BaseKitItemDto>>> SetBrandKit(Guid brandId, [FromBody] SetBrandKitRequest request, CancellationToken cancellationToken)
    {
        await _brandValidator.ValidateAndThrowAsync(request, cancellationToken);

        return Ok(await _kits.SetBrandKitAsync(brandId, request, cancellationToken));
    }

    [HttpGet("{brandId:guid}/models/{modelId:guid}/base-items")]
    public async Task<ActionResult<IReadOnlyList<ModelKitItemDto>>> GetModelKit(Guid brandId, Guid modelId, CancellationToken cancellationToken)
    {
        return Ok(await _kits.GetModelKitAsync(modelId, cancellationToken));
    }

    [HttpPut("{brandId:guid}/models/{modelId:guid}/base-items")]
    public async Task<ActionResult<IReadOnlyList<ModelKitItemDto>>> SetModelKit(Guid brandId, Guid modelId, [FromBody] SetModelKitRequest request, CancellationToken cancellationToken)
    {
        await _modelValidator.ValidateAndThrowAsync(request, cancellationToken);

        return Ok(await _kits.SetModelKitAsync(modelId, request, cancellationToken));
    }
}
