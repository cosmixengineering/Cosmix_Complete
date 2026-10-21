namespace NOIR.Application.Features.Pm.Commands.DuplicateColumn;

public class DuplicateColumnCommandValidator : AbstractValidator<DuplicateColumnCommand>
{
    public DuplicateColumnCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.ColumnId).NotEmpty();
    }
}
