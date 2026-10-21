namespace NOIR.Application.Features.Pm.Commands.DeleteColumn;

public sealed class DeleteColumnCommandValidator : AbstractValidator<DeleteColumnCommand>
{
    public DeleteColumnCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("Project ID is required.");

        RuleFor(x => x.ColumnId)
            .NotEmpty().WithMessage("Column ID is required.");

        RuleFor(x => x.MoveToColumnId)
            .NotEmpty().WithMessage("Target column ID is required.");
    }
}
