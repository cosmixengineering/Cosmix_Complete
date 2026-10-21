namespace NOIR.Application.Features.Pm.Commands.AddProjectMember;

public class AddProjectMemberCommandHandler
{
    private readonly IRepository<Project, Guid> _projectRepository;
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public AddProjectMemberCommandHandler(
        IRepository<Project, Guid> projectRepository,
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _projectRepository = projectRepository;
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Result<Features.Pm.DTOs.ProjectMemberDto>> Handle(
        AddProjectMemberCommand command,
        CancellationToken cancellationToken)
    {
        // Verify project exists
        var project = await _projectRepository.GetByIdAsync(command.ProjectId, cancellationToken);
        if (project is null)
        {
            return Result.Failure<Features.Pm.DTOs.ProjectMemberDto>(
                Error.NotFound($"Project with ID '{command.ProjectId}' not found.", "NOIR-PM-002"));
        }

        // Check not already a member
        var existing = await _dbContext.ProjectMembers
            .Where(m => m.ProjectId == command.ProjectId && m.EmployeeId == command.EmployeeId)
            .TagWith("AddProjectMember_DuplicateCheck")
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            return Result.Failure<Features.Pm.DTOs.ProjectMemberDto>(
                Error.Conflict("Employee is already a member of this project.", "NOIR-PM-003"));
        }

        var member = ProjectMember.Create(command.ProjectId, command.EmployeeId, command.Role, _currentUser.TenantId);
        _dbContext.ProjectMembers.Add(member);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reload to get Employee navigation property
        var reloaded = await _dbContext.ProjectMembers
            .Include(m => m.Employee!)
            .TagWith("AddProjectMember_Reload")
            .FirstOrDefaultAsync(m => m.Id == member.Id, cancellationToken);

        return Result.Success(new Features.Pm.DTOs.ProjectMemberDto(
            reloaded!.Id, reloaded.EmployeeId,
            reloaded.Employee != null ? $"{reloaded.Employee.FirstName} {reloaded.Employee.LastName}" : string.Empty,
            reloaded.Employee?.AvatarUrl,
            reloaded.Role, reloaded.JoinedAt));
    }
}
