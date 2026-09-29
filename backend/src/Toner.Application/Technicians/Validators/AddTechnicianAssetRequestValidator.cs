using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Interfaces;
using Toner.Application.Technicians.Dtos;

namespace Toner.Application.Technicians.Validators;

public class AddTechnicianAssetRequestValidator : AbstractValidator<AddTechnicianAssetRequest>
{
    public AddTechnicianAssetRequestValidator(IApplicationDbContext db)
    {
        RuleFor(x => x.AssetId)
            .MustAsync(async (id, ct) => await db.Assets.AnyAsync(a => a.Id == id, ct))
            .WithMessage("El AssetId especificado no existe.");
    }
}
