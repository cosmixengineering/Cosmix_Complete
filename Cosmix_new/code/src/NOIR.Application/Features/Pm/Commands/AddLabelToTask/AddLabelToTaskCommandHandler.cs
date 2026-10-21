namespace NOIR.Application.Features.Pm.Commands.AddLabelToTask;

public class AddLabelToTaskCommandHandler
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public AddLabelToTaskCommandHandler(
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Result<Features.Pm.DTOs.TaskLabelBriefDto>> Handle(
        AddLabelToTaskCommand command,
        CancellationToken cancellationToken)
    {
        // Check if already assigned
        var existing = await _dbContext.ProjectTaskLabels
            .Where(tl => tl.TaskId == command.TaskId && tl.LabelId == command.LabelId)
            .TagWith("AddLabelToTask_DuplicateCheck")
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            return Result.Failure<Features.Pm.DTOs.TaskLabelBriefDto>(
                Error.Conflict("Label is already assigned to this task.", "NOIR-PM-011"));
        }

        var label = await _dbContext.TaskLabels
            .TagWith("AddLabelToTask_FetchLabel")
            .FirstOrDefaultAsync(l => l.Id == command.LabelId, cancellationToken);

        if (label is null)
        {
            return Result.Failure<Features.Pm.DTOs.TaskLabelBriefDto>(
                Error.NotFound($"Label with ID '{command.LabelId}' not found.", "NOIR-PM-010"));
        }

        var taskLabel = ProjectTaskLabel.Create(command.TaskId, command.LabelId, _currentUser.TenantId);
        _dbContext.ProjectTaskLabels.Add(taskLabel);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new Features.Pm.DTOs.TaskLabelBriefDto(label.Id, label.Name, label.Color));
    }
}
