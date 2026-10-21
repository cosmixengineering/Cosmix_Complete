namespace NOIR.Application.Features.Pm.Commands.ChangeProjectMemberRole;

public class ChangeProjectMemberRoleCommandHandler
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;

    public ChangeProjectMemberRoleCommandHandler(
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Features.Pm.DTOs.ProjectMemberDto>> Handle(
        ChangeProjectMemberRoleCommand command,
        CancellationToken cancellationToken)
    {
        var member = await _dbContext.ProjectMembers
            .Include(m => m.Employee!)
            .TagWith("ChangeProjectMemberRole_Fetch")
            .FirstOrDefaultAsync(m => m.Id == command.MemberId, cancellationToken);

        if (member is null)
        {
            return Result.Failure<Features.Pm.DTOs.ProjectMemberDto>(
                Error.NotFound($"Member with ID '{command.MemberId}' not found.", "NOIR-PM-004"));
        }

        member.ChangeRole(command.Role);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new Features.Pm.DTOs.ProjectMemberDto(
            member.Id, member.EmployeeId,
            member.Employee != null ? $"{member.Employee.FirstName} {member.Employee.LastName}" : string.Empty,
            member.Employee?.AvatarUrl,
            member.Role, member.JoinedAt));
    }
}
