namespace NOIR.Application.Features.Accounting.Commands.CreateJournalEntry;

/// <summary>
/// Validator for CreateJournalEntryCommand.
/// </summary>
public class CreateJournalEntryCommandValidator : AbstractValidator<CreateJournalEntryCommand>
{
    public CreateJournalEntryCommandValidator()
    {
        RuleFor(x => x.EntryDate)
            .NotEmpty().WithMessage("Entry date is required.");

        RuleFor(x => x.Reference)
            .MaximumLength(100).WithMessage("Reference must not exceed 100 characters.")
            .When(x => !string.IsNullOrEmpty(x.Reference));

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("A journal entry must have at least one line.")
            .Must(lines => lines.Count >= 2).WithMessage("A journal entry must have at least two lines.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.AccountId)
                .NotEmpty().WithMessage("Each journal line must reference an account.");

            line.RuleFor(l => l.Debit)
                .GreaterThanOrEqualTo(0).WithMessage("Debit amount cannot be negative.");

            line.RuleFor(l => l.Credit)
                .GreaterThanOrEqualTo(0).WithMessage("Credit amount cannot be negative.");

            line.RuleFor(l => l)
                .Must(l => !(l.Debit > 0 && l.Credit > 0))
                .WithMessage("A journal line cannot have both debit and credit amounts.");

            line.RuleFor(l => l)
                .Must(l => l.Debit > 0 || l.Credit > 0)
                .WithMessage("A journal line must have either a debit or a credit amount.");

            line.RuleFor(l => l.Memo)
                .MaximumLength(500).WithMessage("Line memo must not exceed 500 characters.")
                .When(l => !string.IsNullOrEmpty(l.Memo));
        });
    }
}
