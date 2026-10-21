namespace NOIR.Application.Features.Media.Commands.RenameMediaFile;

public class RenameMediaFileCommandValidator : AbstractValidator<RenameMediaFileCommand>
{
    public RenameMediaFileCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Media file ID is required.");

        RuleFor(x => x.NewFileName)
            .NotEmpty()
            .WithMessage("New file name is required.")
            .MaximumLength(500)
            .WithMessage("File name must not exceed 500 characters.");
    }
}
