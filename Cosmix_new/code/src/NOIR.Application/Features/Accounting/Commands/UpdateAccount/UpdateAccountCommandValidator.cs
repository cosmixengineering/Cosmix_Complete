namespace NOIR.Application.Features.Accounting.Commands.UpdateAccount;

/// <summary>
/// Validator for UpdateAccountCommand.
/// </summary>
public class UpdateAccountCommandValidator : AbstractValidator<UpdateAccountCommand>
{
    public UpdateAccountCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Account ID is required.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Account code is required.")
            .MaximumLength(20).WithMessage("Account code must not exceed 20 characters.")
            .Matches("^[A-Za-z0-9.-]+$").WithMessage("Account code can only contain letters, numbers, dots, and hyphens.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Account name is required.")
            .MaximumLength(200).WithMessage("Account name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.")
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}
