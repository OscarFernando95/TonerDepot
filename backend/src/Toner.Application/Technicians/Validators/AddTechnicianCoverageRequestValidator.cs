using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Interfaces;
using Toner.Application.Technicians.Dtos;

namespace Toner.Application.Technicians.Validators;

public class AddTechnicianCoverageRequestValidator : AbstractValidator<AddTechnicianCoverageRequest>
{
    public AddTechnicianCoverageRequestValidator(IApplicationDbContext db)
    {
        RuleFor(x => x.CityId)
            .MustAsync(async (id, ct) => await db.Cities.AnyAsync(c => c.Id == id, ct))
            .WithMessage("El CityId especificado no existe.");
    }
}
