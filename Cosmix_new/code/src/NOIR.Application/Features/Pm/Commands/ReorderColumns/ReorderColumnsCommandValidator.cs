namespace NOIR.Application.Features.Pm.Commands.ReorderColumns;

public sealed class ReorderColumnsCommandValidator : AbstractValidator<ReorderColumnsCommand>
{
    public ReorderColumnsCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("Project ID is required.");

        RuleFor(x => x.ColumnIds)
            .NotEmpty().WithMessage("Column IDs are required.");
    }
}
