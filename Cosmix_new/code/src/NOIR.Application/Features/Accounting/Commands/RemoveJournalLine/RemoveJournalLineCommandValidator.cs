namespace NOIR.Application.Features.Accounting.Commands.RemoveJournalLine;

/// <summary>
/// Validator for RemoveJournalLineCommand.
/// </summary>
public sealed class RemoveJournalLineCommandValidator : AbstractValidator<RemoveJournalLineCommand>
{
    public RemoveJournalLineCommandValidator()
    {
        RuleFor(x => x.JournalEntryId)
            .NotEmpty().WithMessage("Journal entry ID is required.");

        RuleFor(x => x.LineId)
            .NotEmpty().WithMessage("Journal line ID is required.");
    }
}
