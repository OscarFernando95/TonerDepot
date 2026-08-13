using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Interfaces;
using Toner.Application.Maintenance.Dtos;
using Toner.Domain.Enums;

namespace Toner.Application.Maintenance.Validators;

public class CreateMaintenanceScheduleRequestValidator : AbstractValidator<CreateMaintenanceScheduleRequest>
{
    public CreateMaintenanceScheduleRequestValidator(IApplicationDbContext db)
    {
        RuleFor(x => x.AssetId)
            .MustAsync(async (id, ct) => await db.Assets.AnyAsync(a => a.Id == id, ct))
            .WithMessage("El AssetId especificado no existe.");

        RuleFor(x => x.ContractId)
            .MustAsync(async (id, ct) => await db.Contracts.AnyAsync(c => c.Id == id, ct))
            .WithMessage("El ContractId especificado no existe.")
            .When(x => x.ContractId.HasValue);

        RuleFor(x => x.FrequencyType)
            .NotEmpty()
            .Must(t => Enum.TryParse<MaintenanceFrequencyType>(t, out _))
            .WithMessage($"FrequencyType debe ser uno de: {string.Join(", ", Enum.GetNames<MaintenanceFrequencyType>())}.");

        RuleFor(x => x.PrintThreshold)
            .NotNull().GreaterThan(0)
            .WithMessage("PrintThreshold es obligatorio y debe ser mayor a 0 cuando FrequencyType es PorContador.")
            .When(x => x.FrequencyType == nameof(MaintenanceFrequencyType.PorContador));

        RuleFor(x => x.TimeIntervalDays)
            .NotNull().GreaterThan(0)
            .WithMessage("TimeIntervalDays es obligatorio y debe ser mayor a 0 cuando FrequencyType es PorTiempo.")
            .When(x => x.FrequencyType == nameof(MaintenanceFrequencyType.PorTiempo));
    }
}
