namespace NOIR.Application.Features.Accounting.Commands.DeleteAccount;

/// <summary>
/// Validator for DeleteAccountCommand.
/// </summary>
public sealed class DeleteAccountCommandValidator : AbstractValidator<DeleteAccountCommand>
{
    public DeleteAccountCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Account ID is required.");
    }
}
