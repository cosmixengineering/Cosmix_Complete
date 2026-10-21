namespace NOIR.Application.Features.Pm.Commands.BulkArchiveTasks;

public class BulkArchiveTasksCommandHandler
{
    private readonly IRepository<ProjectTask, Guid> _taskRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public BulkArchiveTasksCommandHandler(
        IRepository<ProjectTask, Guid> taskRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IEntityUpdateHubContext entityUpdateHub)
    {
        _taskRepository = taskRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _entityUpdateHub = entityUpdateHub;
    }

    public async Task<Result<int>> Handle(
        BulkArchiveTasksCommand command,
        CancellationToken cancellationToken)
    {
        var archived = 0;
        foreach (var taskId in command.TaskIds)
        {
            var spec = new Specifications.TaskByIdForUpdateSpec(taskId);
            var task = await _taskRepository.FirstOrDefaultAsync(spec, cancellationToken);
            if (task is null || task.IsArchived) continue;

            task.Archive();
            archived++;
        }

        if (archived > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _entityUpdateHub.PublishEntityUpdatedAsync(
                entityType: "ProjectTask",
                entityId: command.TaskIds[0],
                operation: EntityOperation.Updated,
                tenantId: _currentUser.TenantId!,
                cancellationToken);
        }

        return Result.Success(archived);
    }
}
