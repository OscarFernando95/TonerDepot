using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Interfaces;
using Toner.Application.Contracts.Dtos;

namespace Toner.Application.Contracts.Validators;

public class AddContractAssetRequestValidator : AbstractValidator<AddContractAssetRequest>
{
    public AddContractAssetRequestValidator(IApplicationDbContext db)
    {
        RuleFor(x => x.AssetId)
            .MustAsync(async (id, ct) => await db.Assets.AnyAsync(a => a.Id == id, ct))
            .WithMessage("El AssetId especificado no existe.");

        RuleFor(x => x.ClientLocationId)
            .MustAsync(async (id, ct) => await db.ClientLocations.AnyAsync(l => l.Id == id, ct))
            .WithMessage("El ClientLocationId especificado no existe.");
    }
}
