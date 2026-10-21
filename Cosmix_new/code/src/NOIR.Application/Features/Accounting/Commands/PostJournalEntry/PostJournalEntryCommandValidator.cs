namespace NOIR.Application.Features.Accounting.Commands.PostJournalEntry;

/// <summary>
/// Validator for PostJournalEntryCommand.
/// </summary>
public sealed class PostJournalEntryCommandValidator : AbstractValidator<PostJournalEntryCommand>
{
    public PostJournalEntryCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Journal entry ID is required.");
    }
}
