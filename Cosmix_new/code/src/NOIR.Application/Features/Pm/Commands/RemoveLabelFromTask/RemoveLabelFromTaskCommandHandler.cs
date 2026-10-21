namespace NOIR.Application.Features.Pm.Commands.RemoveLabelFromTask;

public class RemoveLabelFromTaskCommandHandler
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveLabelFromTaskCommandHandler(
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Features.Pm.DTOs.TaskLabelBriefDto>> Handle(
        RemoveLabelFromTaskCommand command,
        CancellationToken cancellationToken)
    {
        var junction = await _dbContext.ProjectTaskLabels
            .Where(tl => tl.TaskId == command.TaskId && tl.LabelId == command.LabelId)
            .TagWith("RemoveLabelFromTask_Fetch")
            .FirstOrDefaultAsync(cancellationToken);

        if (junction is null)
        {
            return Result.Failure<Features.Pm.DTOs.TaskLabelBriefDto>(
                Error.NotFound("Label is not assigned to this task.", "NOIR-PM-012"));
        }

        var label = await _dbContext.TaskLabels
            .TagWith("RemoveLabelFromTask_FetchLabel")
            .FirstOrDefaultAsync(l => l.Id == command.LabelId, cancellationToken);

        var dto = label is not null
            ? new Features.Pm.DTOs.TaskLabelBriefDto(label.Id, label.Name, label.Color)
            : new Features.Pm.DTOs.TaskLabelBriefDto(command.LabelId, string.Empty, string.Empty);

        _dbContext.ProjectTaskLabels.Remove(junction);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(dto);
    }
}
