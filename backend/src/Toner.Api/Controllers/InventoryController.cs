using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toner.Application.Common.Paging;
using Toner.Application.Inventory;
using Toner.Application.Inventory.Dtos;
using Toner.Domain.Common;

namespace Toner.Api.Controllers;

// Inventario de la empresa: catálogo, bodega principal + inventario por zona, y movimientos. Solo staff.
[ApiController]
[Route("api/inventory")]
[Authorize(Roles = RoleNames.StaffRoles)]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventory;
    private readonly IValidator<CreateInventoryItemRequest> _createItemValidator;
    private readonly IValidator<UpdateInventoryItemRequest> _updateItemValidator;
    private readonly IValidator<UpdateMainLocationRequest> _mainLocationValidator;
    private readonly IValidator<RegisterEntryRequest> _entryValidator;
    private readonly IValidator<TransferRequest> _transferValidator;
    private readonly IValidator<AdjustStockRequest> _adjustValidator;

    public InventoryController(
        IInventoryService inventory,
        IValidator<CreateInventoryItemRequest> createItemValidator,
        IValidator<UpdateInventoryItemRequest> updateItemValidator,
        IValidator<UpdateMainLocationRequest> mainLocationValidator,
        IValidator<RegisterEntryRequest> entryValidator,
        IValidator<TransferRequest> transferValidator,
        IValidator<AdjustStockRequest> adjustValidator)
    {
        _inventory = inventory;
        _createItemValidator = createItemValidator;
        _updateItemValidator = updateItemValidator;
        _mainLocationValidator = mainLocationValidator;
        _entryValidator = entryValidator;
        _transferValidator = transferValidator;
        _adjustValidator = adjustValidator;
    }

    [HttpGet("items")]
    public async Task<ActionResult<PagedResult<InventoryItemDto>>> ListItems(
        [FromQuery] string? search, [FromQuery] string? category, [FromQuery] bool activeOnly, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        return Ok(await _inventory.ListItemsAsync(search, category, activeOnly, page, pageSize, cancellationToken));
    }

    [HttpPost("items")]
    public async Task<ActionResult<InventoryItemDto>> CreateItem([FromBody] CreateInventoryItemRequest request, CancellationToken cancellationToken)
    {
        await _createItemValidator.ValidateAndThrowAsync(request, cancellationToken);

        var item = await _inventory.CreateItemAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ListItems), new { }, item);
    }

    [HttpPut("items/{id:guid}")]
    public async Task<ActionResult<InventoryItemDto>> UpdateItem(Guid id, [FromBody] UpdateInventoryItemRequest request, CancellationToken cancellationToken)
    {
        await _updateItemValidator.ValidateAndThrowAsync(request, cancellationToken);

        return Ok(await _inventory.UpdateItemAsync(id, request, cancellationToken));
    }

    [HttpGet("locations")]
    public async Task<ActionResult<IReadOnlyList<InventoryLocationDto>>> ListLocations(CancellationToken cancellationToken)
    {
        return Ok(await _inventory.ListLocationsAsync(cancellationToken));
    }

    // Define la sede principal de la empresa (donde está la bodega y el inventario principal).
    [HttpPut("locations/main")]
    [Authorize(Roles = RoleNames.Administrador)]
    public async Task<ActionResult<InventoryLocationDto>> UpdateMainLocation([FromBody] UpdateMainLocationRequest request, CancellationToken cancellationToken)
    {
        await _mainLocationValidator.ValidateAndThrowAsync(request, cancellationToken);

        return Ok(await _inventory.UpdateMainLocationAsync(request, cancellationToken));
    }

    [HttpGet("stock")]
    public async Task<ActionResult<PagedResult<StockRowDto>>> ListStock(
        [FromQuery] Guid? locationId, [FromQuery] Guid? itemId, [FromQuery] string? category, [FromQuery] bool onlyLow,
        [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        return Ok(await _inventory.ListStockAsync(locationId, itemId, category, onlyLow, page, pageSize, cancellationToken));
    }

    [HttpGet("movements")]
    public async Task<ActionResult<PagedResult<InventoryMovementDto>>> ListMovements(
        [FromQuery] Guid? locationId, [FromQuery] Guid? itemId, [FromQuery] string? cursor, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        return Ok(await _inventory.ListMovementsAsync(locationId, itemId, cursor, pageSize, cancellationToken));
    }

    [HttpPost("entries")]
    public async Task<ActionResult<InventoryMovementDto>> RegisterEntry([FromBody] RegisterEntryRequest request, CancellationToken cancellationToken)
    {
        await _entryValidator.ValidateAndThrowAsync(request, cancellationToken);

        return Ok(await _inventory.RegisterEntryAsync(request, CurrentUserId, cancellationToken));
    }

    [HttpPost("transfers")]
    public async Task<ActionResult<IReadOnlyList<InventoryMovementDto>>> Transfer([FromBody] TransferRequest request, CancellationToken cancellationToken)
    {
        await _transferValidator.ValidateAndThrowAsync(request, cancellationToken);

        return Ok(await _inventory.TransferAsync(request, CurrentUserId, cancellationToken));
    }

    [HttpPost("adjustments")]
    public async Task<ActionResult<InventoryMovementDto>> Adjust([FromBody] AdjustStockRequest request, CancellationToken cancellationToken)
    {
        await _adjustValidator.ValidateAndThrowAsync(request, cancellationToken);

        return Ok(await _inventory.AdjustAsync(request, CurrentUserId, cancellationToken));
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
