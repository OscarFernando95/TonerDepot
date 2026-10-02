using FluentValidation;
using Toner.Application.Inventory.Dtos;
using Toner.Domain.Enums;

namespace Toner.Application.Inventory.Validators;

public class CreateInventoryItemRequestValidator : AbstractValidator<CreateInventoryItemRequest>
{
    public CreateInventoryItemRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Category).Must(IsCategory).WithMessage($"Category debe ser uno de: {string.Join(", ", Enum.GetNames<InventoryCategory>())}.");
        RuleFor(x => x.Unit).MaximumLength(30);
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0).When(x => x.UnitCost.HasValue);
        RuleFor(x => x.MinimumStock).GreaterThanOrEqualTo(0);
    }

    internal static bool IsCategory(string? value) => Enum.TryParse<InventoryCategory>(value, out var parsed) && Enum.IsDefined(parsed);
}

public class UpdateInventoryItemRequestValidator : AbstractValidator<UpdateInventoryItemRequest>
{
    public UpdateInventoryItemRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Category).Must(CreateInventoryItemRequestValidator.IsCategory)
            .WithMessage($"Category debe ser uno de: {string.Join(", ", Enum.GetNames<InventoryCategory>())}.");
        RuleFor(x => x.Unit).MaximumLength(30);
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0).When(x => x.UnitCost.HasValue);
        RuleFor(x => x.MinimumStock).GreaterThanOrEqualTo(0);
    }
}

public class UpdateMainLocationRequestValidator : AbstractValidator<UpdateMainLocationRequest>
{
    public UpdateMainLocationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Address).MaximumLength(300);
    }
}

public class RegisterEntryRequestValidator : AbstractValidator<RegisterEntryRequest>
{
    public RegisterEntryRequestValidator()
    {
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.Quantity).InclusiveBetween(1, 100000);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public class TransferRequestValidator : AbstractValidator<TransferRequest>
{
    public TransferRequestValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.FromLocationId).NotEmpty();
        RuleFor(x => x.ToLocationId).NotEmpty().NotEqual(x => x.FromLocationId).WithMessage("El origen y el destino deben ser distintos.");
        RuleFor(x => x.Quantity).InclusiveBetween(1, 100000);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public class AdjustStockRequestValidator : AbstractValidator<AdjustStockRequest>
{
    public AdjustStockRequestValidator()
    {
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.Delta).NotEqual(0).WithMessage("El ajuste no puede ser 0.").InclusiveBetween(-100000, 100000);
        RuleFor(x => x.Notes).NotEmpty().WithMessage("El motivo del ajuste es obligatorio.").MaximumLength(500);
    }
}

public class RegisterTonerRequestValidator : AbstractValidator<RegisterTonerRequest>
{
    public RegisterTonerRequestValidator()
    {
        RuleFor(x => x.AssetId).NotEmpty();
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.Quantity).InclusiveBetween(1, 100);
        RuleFor(x => x.CounterValue).GreaterThanOrEqualTo(0).When(x => x.CounterValue.HasValue);
        RuleFor(x => x.Notes).MaximumLength(500);
        // No se registra un tóner "del futuro": un error de fecha ensucia el cálculo de duración.
        RuleFor(x => x.OccurredAt).Must(d => d is null || d.Value.ToUniversalTime() <= DateTime.UtcNow.AddMinutes(5))
            .WithMessage("La fecha del tóner no puede ser futura.");
    }
}

public class SetBrandKitRequestValidator : AbstractValidator<SetBrandKitRequest>
{
    public SetBrandKitRequestValidator()
    {
        RuleFor(x => x.Items).NotNull();
        RuleFor(x => x.Items).Must(items => items.Select(i => i.ItemId).Distinct().Count() == items.Count)
            .WithMessage("Hay ítems repetidos en el kit.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ItemId).NotEmpty();
            item.RuleFor(i => i.GroupName).NotEmpty().MaximumLength(100);
            item.RuleFor(i => i.Quantity).InclusiveBetween(1, 100);
        });
    }
}

public class SetModelKitRequestValidator : AbstractValidator<SetModelKitRequest>
{
    public SetModelKitRequestValidator()
    {
        RuleFor(x => x.Overrides).NotNull();
        RuleFor(x => x.Overrides).Must(o => o.Select(i => i.ItemId).Distinct().Count() == o.Count)
            .WithMessage("Hay ítems repetidos en los ajustes.");
        RuleForEach(x => x.Overrides).ChildRules(item =>
        {
            item.RuleFor(i => i.ItemId).NotEmpty();
            item.RuleFor(i => i.GroupName).MaximumLength(100);
            item.RuleFor(i => i.Quantity).InclusiveBetween(1, 100).When(i => i.Quantity.HasValue);
        });
    }
}
