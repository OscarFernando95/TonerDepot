using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Toner.Application.Assets.Dtos;
using Toner.Application.Common.Interfaces;
using Toner.Domain.Enums;

namespace Toner.Application.Assets.Validators;

public class ChangeAssetStatusRequestValidator : AbstractValidator<ChangeAssetStatusRequest>
{
    public ChangeAssetStatusRequestValidator(IApplicationDbContext db)
    {
        RuleFor(x => x.NewStatus)
            .NotEmpty()
            .Must(s => Enum.TryParse<AssetLifecycleStatus>(s, out _))
            .WithMessage($"NewStatus debe ser uno de: {string.Join(", ", Enum.GetNames<AssetLifecycleStatus>())}.");

        RuleFor(x => x.ClientLocationId)
            .MustAsync(async (id, ct) => await db.ClientLocations.AnyAsync(l => l.Id == id, ct))
            .WithMessage("El ClientLocationId especificado no existe.")
            .When(x => x.ClientLocationId.HasValue);

        RuleFor(x => x.ClientLocationId)
            .NotNull()
            .WithMessage("Debe indicar ClientLocationId para mover el activo a Pendiente de instalar.")
            .When(x => x.NewStatus == nameof(AssetLifecycleStatus.PendienteInstalacion));

        RuleFor(x => x.Area)
            .NotEmpty()
            .MaximumLength(150)
            .WithMessage("El área es obligatoria al instalar el activo.")
            .When(x => x.NewStatus == nameof(AssetLifecycleStatus.Instalado));

        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
