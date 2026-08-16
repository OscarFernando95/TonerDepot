using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Toner.Application.Assets.Dtos;
using Toner.Application.Common.Interfaces;
using Toner.Domain.Enums;

namespace Toner.Application.Assets.Validators;

public class UpdateAssetRequestValidator : AbstractValidator<UpdateAssetRequest>
{
    public UpdateAssetRequestValidator(IApplicationDbContext db)
    {
        RuleFor(x => x.SerialNumber).NotEmpty().MaximumLength(100);

        RuleFor(x => x.Type)
            .NotEmpty()
            .Must(t => Enum.TryParse<AssetType>(t, out _))
            .WithMessage($"Type debe ser uno de: {string.Join(", ", Enum.GetNames<AssetType>())}.");

        RuleFor(x => x.AssetModelId)
            .MustAsync(async (id, ct) => await db.AssetModels.AnyAsync(m => m.Id == id, ct))
            .WithMessage("El AssetModelId especificado no existe.");
    }
}
