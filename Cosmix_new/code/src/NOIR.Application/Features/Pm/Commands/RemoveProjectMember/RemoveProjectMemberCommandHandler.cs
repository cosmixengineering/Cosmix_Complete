namespace NOIR.Application.Features.Pm.Commands.RemoveProjectMember;

public class RemoveProjectMemberCommandHandler
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveProjectMemberCommandHandler(
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Features.Pm.DTOs.ProjectMemberDto>> Handle(
        RemoveProjectMemberCommand command,
        CancellationToken cancellationToken)
    {
        var member = await _dbContext.ProjectMembers
            .Include(m => m.Employee!)
            .TagWith("RemoveProjectMember_Fetch")
            .FirstOrDefaultAsync(m => m.Id == command.MemberId, cancellationToken);

        if (member is null)
        {
            return Result.Failure<Features.Pm.DTOs.ProjectMemberDto>(
                Error.NotFound($"Member with ID '{command.MemberId}' not found.", "NOIR-PM-004"));
        }

        if (member.Role == ProjectMemberRole.Owner)
        {
            return Result.Failure<Features.Pm.DTOs.ProjectMemberDto>(
                Error.Validation("Role", "Cannot remove the project owner.", "NOIR-PM-005"));
        }

        var dto = new Features.Pm.DTOs.ProjectMemberDto(
            member.Id, member.EmployeeId,
            member.Employee != null ? $"{member.Employee.FirstName} {member.Employee.LastName}" : string.Empty,
            member.Employee?.AvatarUrl,
            member.Role, member.JoinedAt);

        _dbContext.ProjectMembers.Remove(member);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(dto);
    }
}
