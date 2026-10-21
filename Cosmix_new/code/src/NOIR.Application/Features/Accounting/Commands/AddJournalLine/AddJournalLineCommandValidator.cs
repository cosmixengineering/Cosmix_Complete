namespace NOIR.Application.Features.Accounting.Commands.AddJournalLine;

/// <summary>
/// Validator for AddJournalLineCommand.
/// </summary>
public class AddJournalLineCommandValidator : AbstractValidator<AddJournalLineCommand>
{
    public AddJournalLineCommandValidator()
    {
        RuleFor(x => x.JournalEntryId)
            .NotEmpty().WithMessage("Journal entry ID is required.");

        RuleFor(x => x.AccountId)
            .NotEmpty().WithMessage("Account ID is required.");

        RuleFor(x => x.Debit)
            .GreaterThanOrEqualTo(0).WithMessage("Debit amount cannot be negative.");

        RuleFor(x => x.Credit)
            .GreaterThanOrEqualTo(0).WithMessage("Credit amount cannot be negative.");

        RuleFor(x => x)
            .Must(x => !(x.Debit > 0 && x.Credit > 0))
            .WithMessage("A journal line cannot have both debit and credit amounts.");

        RuleFor(x => x)
            .Must(x => x.Debit > 0 || x.Credit > 0)
            .WithMessage("A journal line must have either a debit or a credit amount.");

        RuleFor(x => x.Memo)
            .MaximumLength(500).WithMessage("Memo must not exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.Memo));
    }
}
