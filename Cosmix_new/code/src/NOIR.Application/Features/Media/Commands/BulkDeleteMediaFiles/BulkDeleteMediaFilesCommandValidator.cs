namespace NOIR.Application.Features.Media.Commands.BulkDeleteMediaFiles;

public class BulkDeleteMediaFilesCommandValidator : AbstractValidator<BulkDeleteMediaFilesCommand>
{
    private const int MaxBulkOperationSize = 100;

    public BulkDeleteMediaFilesCommandValidator()
    {
        RuleFor(x => x.Ids)
            .NotEmpty()
            .WithMessage("At least one media file ID is required.");

        RuleFor(x => x.Ids.Count)
            .LessThanOrEqualTo(MaxBulkOperationSize)
            .WithMessage($"Maximum {MaxBulkOperationSize} media files per operation.");

        RuleForEach(x => x.Ids)
            .NotEmpty()
            .WithMessage("Media file ID cannot be empty.");
    }
}
