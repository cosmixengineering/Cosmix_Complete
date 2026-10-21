namespace NOIR.Application.Features.Accounting.Commands.VoidJournalEntry;

/// <summary>
/// Validator for VoidJournalEntryCommand.
/// </summary>
public sealed class VoidJournalEntryCommandValidator : AbstractValidator<VoidJournalEntryCommand>
{
    public VoidJournalEntryCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Journal entry ID is required.");

        RuleFor(x => x.Reason)
            .MaximumLength(500).WithMessage("Void reason must not exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.Reason));
    }
}
