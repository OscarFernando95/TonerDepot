using FluentValidation;
using Toner.Application.Common;
using Toner.Domain.Enums;
using Toner.Application.Clients.Dtos;
using Toner.Application.Common.Interfaces;

namespace Toner.Application.Clients.Validators;

public class CreateClientRequestValidator : AbstractValidator<CreateClientRequest>
{
    public CreateClientRequestValidator(IApplicationDbContext db)
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TaxId).MaximumLength(50);
        RuleFor(x => x.ContactName).MaximumLength(200);
        RuleFor(x => x.ContactEmail).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrEmpty(x.ContactEmail));
        RuleFor(x => x.ContactPhone).Matches(PhoneRules.Pattern).WithMessage(PhoneRules.Message).When(x => !string.IsNullOrEmpty(x.ContactPhone));
        RuleFor(x => x.SupportCoverage)
            .Must(v => Enum.TryParse<SupportCoverage>(v, out var parsed) && Enum.IsDefined(parsed))
            .WithMessage($"SupportCoverage debe ser uno de: {string.Join(", ", Enum.GetNames<SupportCoverage>())}.");

        RuleFor(x => x.Locations)
            .NotEmpty()
            .WithMessage("Debe indicar al menos una sede para crear el cliente.");

        RuleForEach(x => x.Locations).SetValidator(new CreateClientLocationRequestValidator(db));
    }
}
