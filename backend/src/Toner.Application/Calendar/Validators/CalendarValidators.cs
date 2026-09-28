using System.Globalization;
using FluentValidation;
using Toner.Application.Calendar.Dtos;

namespace Toner.Application.Calendar.Validators;

public class SetTechnicianScheduleRequestValidator : AbstractValidator<SetTechnicianScheduleRequest>
{
    public SetTechnicianScheduleRequestValidator()
    {
        RuleFor(x => x.Intervals)
            .NotEmpty().WithMessage("El horario debe tener al menos un tramo.")
            .Must(i => i.Count <= 35).WithMessage("Demasiados tramos.");

        RuleForEach(x => x.Intervals).ChildRules(interval =>
        {
            interval.RuleFor(i => i.Day).InclusiveBetween(0, 6).WithMessage("El día debe estar entre 0 (domingo) y 6 (sábado).");
            interval.RuleFor(i => i.Start).Must(BeATime).WithMessage("La hora de inicio debe tener el formato HH:mm.");
            interval.RuleFor(i => i.End).Must(BeATime).WithMessage("La hora de fin debe tener el formato HH:mm.");
        });

        RuleFor(x => x.Intervals)
            .Must(NotHaveInvertedOrOverlappingIntervals)
            .WithMessage("Cada tramo debe terminar después de empezar y no puede solaparse con otro del mismo día.");
    }

    public static bool BeATime(string? value) =>
        TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static bool NotHaveInvertedOrOverlappingIntervals(List<WorkIntervalDto> intervals)
    {
        if (intervals.Any(i => !BeATime(i.Start) || !BeATime(i.End)))
        {
            return true; // ya lo reporta la regla del formato
        }

        var parsed = intervals
            .Select(i => (i.Day, Start: TimeOnly.ParseExact(i.Start, "HH:mm", CultureInfo.InvariantCulture), End: TimeOnly.ParseExact(i.End, "HH:mm", CultureInfo.InvariantCulture)))
            .ToList();

        if (parsed.Any(p => p.End <= p.Start))
        {
            return false;
        }

        foreach (var day in parsed.GroupBy(p => p.Day))
        {
            var ordered = day.OrderBy(p => p.Start).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].Start < ordered[i - 1].End)
                {
                    return false;
                }
            }
        }

        return true;
    }
}

public class CreateTimeOffRequestValidator : AbstractValidator<CreateTimeOffRequest>
{
    public CreateTimeOffRequestValidator()
    {
        RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt).WithMessage("La fecha de fin debe ser posterior a la de inicio.");
        RuleFor(x => x)
            .Must(x => (x.EndsAt - x.StartsAt).TotalDays <= 366)
            .WithMessage("El período fuera de la oficina no puede superar un año.");
        RuleFor(x => x.Reason).MaximumLength(300);
    }
}

public class SetHolidayOverrideRequestValidator : AbstractValidator<SetHolidayOverrideRequest>
{
    public SetHolidayOverrideRequestValidator()
    {
        RuleFor(x => x.Date)
            .Must(d => DateOnly.TryParseExact(d, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            .WithMessage("La fecha debe tener el formato yyyy-MM-dd.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
    }
}
