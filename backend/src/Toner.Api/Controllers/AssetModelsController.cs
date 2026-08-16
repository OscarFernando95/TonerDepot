using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toner.Application.Assets;
using Toner.Application.Assets.Dtos;
using Toner.Domain.Common;

namespace Toner.Api.Controllers;

[ApiController]
[Route("api/asset-brands/{brandId:guid}/models")]
[Authorize(Roles = RoleNames.StaffRoles)]
public class AssetModelsController : ControllerBase
{
    private readonly IAssetModelService _assetModelService;
    private readonly IValidator<CreateAssetModelRequest> _createValidator;
    private readonly IValidator<UpdateAssetModelRequest> _updateValidator;

    public AssetModelsController(
        IAssetModelService assetModelService,
        IValidator<CreateAssetModelRequest> createValidator,
        IValidator<UpdateAssetModelRequest> updateValidator)
    {
        _assetModelService = assetModelService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AssetModelDto>>> List(Guid brandId, CancellationToken cancellationToken)
    {
        return Ok(await _assetModelService.ListAsync(brandId, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<AssetModelDto>> Create(Guid brandId, [FromBody] CreateAssetModelRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var model = await _assetModelService.CreateAsync(brandId, request, cancellationToken);
        return CreatedAtAction(nameof(List), new { brandId }, model);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AssetModelDto>> Update(Guid brandId, Guid id, [FromBody] UpdateAssetModelRequest request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        return Ok(await _assetModelService.UpdateAsync(brandId, id, request, cancellationToken));
    }
}
