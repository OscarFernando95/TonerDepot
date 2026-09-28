using FluentValidation;
using Toner.Domain.Enums;
using Toner.Application.Clients.Dtos;

namespace Toner.Application.Clients.Validators;

public class UpdateClientRequestValidator : AbstractValidator<UpdateClientRequest>
{
    public UpdateClientRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TaxId).MaximumLength(50);
        RuleFor(x => x.ContactName).MaximumLength(200);
        RuleFor(x => x.ContactEmail).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrEmpty(x.ContactEmail));
        RuleFor(x => x.ContactPhone).MaximumLength(50);
        RuleFor(x => x.SupportCoverage)
            .Must(v => Enum.TryParse<SupportCoverage>(v, out var parsed) && Enum.IsDefined(parsed))
            .WithMessage($"SupportCoverage debe ser uno de: {string.Join(", ", Enum.GetNames<SupportCoverage>())}.");
    }
}
