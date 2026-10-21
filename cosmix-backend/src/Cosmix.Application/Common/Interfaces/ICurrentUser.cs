namespace Cosmix.Application.Common.Interfaces;

public interface ICurrentUser
{
    string? UserId { get; }
    Guid? EmployeeId { get; }

    bool IsAuthenticated { get; }
}
