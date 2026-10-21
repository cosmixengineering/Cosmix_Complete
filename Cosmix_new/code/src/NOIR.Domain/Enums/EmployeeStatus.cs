namespace NOIR.Domain.Enums;

/// <summary>
/// Employment status of an employee.
/// </summary>
public enum EmployeeStatus
{
    /// <summary>
    /// Currently employed and active.
    /// </summary>
    Active = 0,

    /// <summary>
    /// Temporarily suspended (disciplinary, investigation).
    /// </summary>
    Suspended = 1,

    /// <summary>
    /// Voluntarily left the organization.
    /// </summary>
    Resigned = 2,

    /// <summary>
    /// Involuntarily terminated.
    /// </summary>
    Terminated = 3
}
