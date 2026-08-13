using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Interfaces;
using Toner.Application.Maintenance.Dtos;

namespace Toner.Application.Maintenance.Validators;

public class AssignMaintenanceOrderRequestValidator : AbstractValidator<AssignMaintenanceOrderRequest>
{
    public AssignMaintenanceOrderRequestValidator(IApplicationDbContext db)
    {
        RuleFor(x => x.TechnicianId)
            .MustAsync(async (id, ct) => await db.Technicians.AnyAsync(t => t.Id == id, ct))
            .WithMessage("El TechnicianId especificado no existe.");

        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
