using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toner.Application.Assets;
using Toner.Application.Assets.Dtos;
using Toner.Domain.Common;

namespace Toner.Api.Controllers;

[ApiController]
[Route("api/asset-brands")]
[Authorize(Roles = RoleNames.StaffRoles)]
public class AssetBrandsController : ControllerBase
{
    private readonly IAssetBrandService _assetBrandService;
    private readonly IValidator<CreateAssetBrandRequest> _createValidator;

    public AssetBrandsController(IAssetBrandService assetBrandService, IValidator<CreateAssetBrandRequest> createValidator)
    {
        _assetBrandService = assetBrandService;
        _createValidator = createValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AssetBrandDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await _assetBrandService.ListAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<AssetBrandDto>> Create([FromBody] CreateAssetBrandRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var brand = await _assetBrandService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(List), new { }, brand);
    }
}
