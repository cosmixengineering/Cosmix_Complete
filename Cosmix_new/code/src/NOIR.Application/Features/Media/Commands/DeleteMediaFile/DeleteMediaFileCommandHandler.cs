using NOIR.Application.Features.Media.Specifications;

namespace NOIR.Application.Features.Media.Commands.DeleteMediaFile;

/// <summary>
/// Wolverine handler for soft deleting a media file.
/// </summary>
public class DeleteMediaFileCommandHandler
{
    private readonly IRepository<MediaFile, Guid> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteMediaFileCommandHandler(
        IRepository<MediaFile, Guid> repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<bool>> Handle(
        DeleteMediaFileCommand command,
        CancellationToken cancellationToken)
    {
        var spec = new MediaFileByIdForUpdateSpec(command.Id);
        var mediaFile = await _repository.FirstOrDefaultAsync(spec, cancellationToken);

        if (mediaFile is null)
        {
            return Result.Failure<bool>(
                Error.NotFound($"Media file with ID '{command.Id}' not found.", "NOIR-MEDIA-002"));
        }

        mediaFile.MarkAsDeleted();
        _repository.Remove(mediaFile);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(true);
    }
}
