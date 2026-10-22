using FluentValidation;
using Toner.Application.Maintenance.Dtos;

namespace Toner.Application.Maintenance.Validators;

public class AddOrderCommentRequestValidator : AbstractValidator<AddOrderCommentRequest>
{
    public const int MaxLength = 1000;

    public AddOrderCommentRequestValidator()
    {
        RuleFor(x => x.Body).NotEmpty().MaximumLength(MaxLength);
    }
}
