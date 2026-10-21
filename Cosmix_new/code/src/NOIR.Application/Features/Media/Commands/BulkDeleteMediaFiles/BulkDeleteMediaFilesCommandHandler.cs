using NOIR.Application.Features.Media.Dtos;
using NOIR.Application.Features.Media.Specifications;

namespace NOIR.Application.Features.Media.Commands.BulkDeleteMediaFiles;

/// <summary>
/// Wolverine handler for bulk soft-deleting media files.
/// </summary>
public class BulkDeleteMediaFilesCommandHandler
{
    private readonly IRepository<MediaFile, Guid> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public BulkDeleteMediaFilesCommandHandler(
        IRepository<MediaFile, Guid> repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<BulkMediaOperationResultDto>> Handle(
        BulkDeleteMediaFilesCommand command,
        CancellationToken cancellationToken)
    {
        var successCount = 0;
        var errors = new List<BulkMediaOperationErrorDto>();

        var spec = new MediaFilesByIdsForUpdateSpec(command.Ids);
        var mediaFiles = await _repository.ListAsync(spec, cancellationToken);

        foreach (var id in command.Ids)
        {
            var mediaFile = mediaFiles.FirstOrDefault(m => m.Id == id);

            if (mediaFile is null)
            {
                errors.Add(new BulkMediaOperationErrorDto(id, null, "Media file not found"));
                continue;
            }

            try
            {
                mediaFile.MarkAsDeleted();
                _repository.Remove(mediaFile);
                successCount++;
            }
            catch (Exception ex)
            {
                errors.Add(new BulkMediaOperationErrorDto(id, mediaFile.OriginalFileName, ex.Message));
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new BulkMediaOperationResultDto(
            successCount,
            errors.Count,
            errors));
    }
}
