namespace NOIR.Application.Features.Pm.Commands.MoveAllColumnTasks;

public class MoveAllColumnTasksCommandHandler
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IRepository<ProjectTask, Guid> _taskRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public MoveAllColumnTasksCommandHandler(
        IApplicationDbContext dbContext,
        IRepository<ProjectTask, Guid> taskRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IEntityUpdateHubContext entityUpdateHub)
    {
        _dbContext = dbContext;
        _taskRepository = taskRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _entityUpdateHub = entityUpdateHub;
    }

    public async Task<Result<int>> Handle(
        MoveAllColumnTasksCommand command,
        CancellationToken cancellationToken)
    {
        var sourceColumn = await _dbContext.ProjectColumns
            .TagWith("MoveAllColumnTasks_FetchSource")
            .FirstOrDefaultAsync(c => c.Id == command.SourceColumnId && c.ProjectId == command.ProjectId, cancellationToken);

        if (sourceColumn is null)
            return Result.Failure<int>(Error.NotFound($"Source column '{command.SourceColumnId}' not found.", "NOIR-PM-013"));

        var targetColumn = await _dbContext.ProjectColumns
            .TagWith("MoveAllColumnTasks_FetchTarget")
            .FirstOrDefaultAsync(c => c.Id == command.TargetColumnId && c.ProjectId == command.ProjectId, cancellationToken);

        if (targetColumn is null)
            return Result.Failure<int>(Error.NotFound($"Target column '{command.TargetColumnId}' not found.", "NOIR-PM-013"));

        var tasksSpec = new Specifications.TasksByColumnSpec(command.SourceColumnId);
        var tasks = await _taskRepository.ListAsync(tasksSpec, cancellationToken);

        if (tasks.Count == 0)
            return Result.Success(0);

        // Get current max sort order in target column to append tasks at the end
        var maxSortOrder = await _dbContext.ProjectTasks
            .Where(t => t.ColumnId == command.TargetColumnId && !t.IsArchived)
            .TagWith("MoveAllColumnTasks_MaxSortOrder")
            .Select(t => (double?)t.SortOrder)
            .MaxAsync(cancellationToken) ?? 0d;

        foreach (var task in tasks)
        {
            var taskUpdateSpec = new Specifications.TaskByIdForUpdateSpec(task.Id);
            var tracked = await _taskRepository.FirstOrDefaultAsync(taskUpdateSpec, cancellationToken);
            if (tracked is null) continue;

            maxSortOrder += 1d;
            tracked.MoveToColumn(command.TargetColumnId, maxSortOrder);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _entityUpdateHub.PublishEntityUpdatedAsync(
            entityType: "ProjectColumn",
            entityId: command.SourceColumnId,
            operation: EntityOperation.Updated,
            tenantId: _currentUser.TenantId!,
            cancellationToken);

        return Result.Success(tasks.Count);
    }
}
