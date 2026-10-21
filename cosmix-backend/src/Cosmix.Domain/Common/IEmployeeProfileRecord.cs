namespace Cosmix.Domain.Common;

/// <summary>A record included in the employee form whose changes share the employee concurrency boundary.</summary>
public interface IEmployeeProfileRecord
{
    Guid EmployeeId { get; }
}
