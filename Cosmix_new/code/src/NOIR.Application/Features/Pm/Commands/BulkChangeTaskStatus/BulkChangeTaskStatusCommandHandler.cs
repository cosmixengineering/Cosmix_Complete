namespace NOIR.Application.Features.Pm.Commands.BulkChangeTaskStatus;

public class BulkChangeTaskStatusCommandHandler
{
    private readonly IRepository<ProjectTask, Guid> _taskRepository;
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public BulkChangeTaskStatusCommandHandler(
        IRepository<ProjectTask, Guid> taskRepository,
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IEntityUpdateHubContext entityUpdateHub)
    {
        _taskRepository = taskRepository;
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _entityUpdateHub = entityUpdateHub;
    }

    public async Task<Result<int>> Handle(
        BulkChangeTaskStatusCommand command,
        CancellationToken cancellationToken)
    {
        var updated = 0;
        List<ProjectColumn>? columns = null;

        // Map status to expected column names
        var statusColumnNameMap = new Dictionary<ProjectTaskStatus, string>
        {
            { ProjectTaskStatus.Todo, "todo" },
            { ProjectTaskStatus.InProgress, "in progress" },
            { ProjectTaskStatus.InReview, "in review" },
            { ProjectTaskStatus.Done, "done" },
        };

        foreach (var taskId in command.TaskIds)
        {
            var spec = new Specifications.TaskByIdForUpdateSpec(taskId);
            var task = await _taskRepository.FirstOrDefaultAsync(spec, cancellationToken);
            if (task is null) continue;

            // Load columns once from the first task's project
            if (columns is null)
            {
                columns = await _dbContext.ProjectColumns
                    .Where(c => c.ProjectId == task.ProjectId)
                    .OrderBy(c => c.SortOrder)
                    .TagWith("BulkChangeTaskStatus_FetchColumns")
                    .ToListAsync(cancellationToken);
            }

            // Skip if already at target status
            if (task.Status == command.Status) continue;

            // Change status (ChangeStatus handles Done case internally)
            if (command.Status == ProjectTaskStatus.Done)
            {
                task.Complete();
            }
            else
            {
                task.ChangeStatus(command.Status);
            }

            // Move to matching column if found
            if (statusColumnNameMap.TryGetValue(command.Status, out var targetColumnName))
            {
                var targetColumn = columns.FirstOrDefault(c =>
                    string.Equals(c.Name, targetColumnName, StringComparison.OrdinalIgnoreCase));
                if (targetColumn is not null && task.ColumnId != targetColumn.Id)
                {
                    task.MoveToColumn(targetColumn.Id, targetColumn.Tasks?.Count ?? 0);
                }
            }

            updated++;
        }

        if (updated > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _entityUpdateHub.PublishEntityUpdatedAsync(
                entityType: "ProjectTask",
                entityId: command.TaskIds[0],
                operation: EntityOperation.Updated,
                tenantId: _currentUser.TenantId!,
                cancellationToken);
        }

        return Result.Success(updated);
    }
}
