using NOIR.Application.Features.Media.Specifications;

namespace NOIR.Application.Features.Media.Commands.RenameMediaFile;

/// <summary>
/// Wolverine handler for renaming a media file.
/// </summary>
public class RenameMediaFileCommandHandler
{
    private readonly IRepository<MediaFile, Guid> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RenameMediaFileCommandHandler(
        IRepository<MediaFile, Guid> repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<bool>> Handle(
        RenameMediaFileCommand command,
        CancellationToken cancellationToken)
    {
        var spec = new MediaFileByIdForUpdateSpec(command.Id);
        var mediaFile = await _repository.FirstOrDefaultAsync(spec, cancellationToken);

        if (mediaFile is null)
        {
            return Result.Failure<bool>(
                Error.NotFound($"Media file with ID '{command.Id}' not found.", "NOIR-MEDIA-003"));
        }

        mediaFile.Rename(command.NewFileName);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(true);
    }
}
