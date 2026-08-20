using System.Security.Claims;
using Toner.Application.Common.Paging;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toner.Application.Contracts;
using Toner.Application.Contracts.Dtos;
using Toner.Domain.Common;

namespace Toner.Api.Controllers;

[ApiController]
[Route("api/contracts/{contractId:guid}/assets")]
[Authorize(Roles = RoleNames.StaffRoles)]
public class ContractAssetsController : ControllerBase
{
    private readonly IContractAssetService _contractAssetService;
    private readonly IValidator<AddContractAssetRequest> _addValidator;

    public ContractAssetsController(IContractAssetService contractAssetService, IValidator<AddContractAssetRequest> addValidator)
    {
        _contractAssetService = contractAssetService;
        _addValidator = addValidator;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ContractAssetDto>>> List(Guid contractId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        return Ok(await _contractAssetService.ListByContractAsync(contractId, page, pageSize, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<ContractAssetDto>> Add(Guid contractId, [FromBody] AddContractAssetRequest request, CancellationToken cancellationToken)
    {
        await _addValidator.ValidateAndThrowAsync(request, cancellationToken);

        var contractAsset = await _contractAssetService.AddAsync(contractId, request, CurrentUserId, cancellationToken);
        return CreatedAtAction(nameof(List), new { contractId }, contractAsset);
    }

    [HttpPatch("{id:guid}/end")]
    public async Task<ActionResult<ContractAssetDto>> End(Guid contractId, Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _contractAssetService.EndAsync(contractId, id, cancellationToken));
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
