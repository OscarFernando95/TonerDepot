using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Interfaces;
using Toner.Application.Technicians.Dtos;

namespace Toner.Application.Technicians.Validators;

public class SetTechnicianZonesRequestValidator : AbstractValidator<SetTechnicianZonesRequest>
{
    public SetTechnicianZonesRequestValidator(IApplicationDbContext db)
    {
        RuleFor(x => x.ZoneIds)
            .NotNull()
            .Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("La lista de zonas tiene repetidos.")
            .MustAsync(async (ids, ct) =>
            {
                var distinct = ids.Distinct().ToList();
                return await db.Zones.CountAsync(z => distinct.Contains(z.Id), ct) == distinct.Count;
            })
            .WithMessage("Alguna de las zonas especificadas no existe.");
    }
}
