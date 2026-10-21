namespace NOIR.Application.Features.Pm.Commands.DuplicateColumn;

public class DuplicateColumnCommandHandler
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public DuplicateColumnCommandHandler(
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IEntityUpdateHubContext entityUpdateHub)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _entityUpdateHub = entityUpdateHub;
    }

    public async Task<Result<Features.Pm.DTOs.ProjectColumnDto>> Handle(
        DuplicateColumnCommand command,
        CancellationToken cancellationToken)
    {
        var source = await _dbContext.ProjectColumns
            .TagWith("DuplicateColumn_FetchSource")
            .FirstOrDefaultAsync(c => c.Id == command.ColumnId && c.ProjectId == command.ProjectId, cancellationToken);

        if (source is null)
            return Result.Failure<Features.Pm.DTOs.ProjectColumnDto>(
                Error.NotFound($"Column '{command.ColumnId}' not found.", "NOIR-PM-013"));

        var maxSortOrder = await _dbContext.ProjectColumns
            .Where(c => c.ProjectId == command.ProjectId)
            .TagWith("DuplicateColumn_MaxSortOrder")
            .MaxAsync(c => c.SortOrder, cancellationToken);

        var copy = ProjectColumn.Create(
            command.ProjectId,
            $"{source.Name} (Copy)",
            maxSortOrder + 1,
            _currentUser.TenantId,
            source.Color,
            source.WipLimit);

        _dbContext.ProjectColumns.Add(copy);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _entityUpdateHub.PublishEntityUpdatedAsync(
            entityType: "ProjectColumn",
            entityId: copy.Id,
            operation: EntityOperation.Created,
            tenantId: _currentUser.TenantId!,
            cancellationToken);

        return Result.Success(new Features.Pm.DTOs.ProjectColumnDto(
            copy.Id, copy.Name, copy.SortOrder, copy.Color, copy.WipLimit, copy.StatusMapping));
    }
}
