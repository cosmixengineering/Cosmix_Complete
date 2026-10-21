namespace NOIR.Domain.Events.Hr;

/// <summary>
/// Raised when a new employee is created.
/// </summary>
public record EmployeeCreatedEvent(Guid EmployeeId) : DomainEvent;

/// <summary>
/// Raised when an employee's information is updated.
/// </summary>
public record EmployeeUpdatedEvent(Guid EmployeeId) : DomainEvent;

/// <summary>
/// Raised when an employee is deactivated (resigned or terminated).
/// </summary>
public record EmployeeDeactivatedEvent(Guid EmployeeId, EmployeeStatus NewStatus) : DomainEvent;

/// <summary>
/// Raised when an employee's department assignment changes.
/// </summary>
public record EmployeeDepartmentChangedEvent(Guid EmployeeId, Guid OldDepartmentId, Guid NewDepartmentId) : DomainEvent;

/// <summary>
/// Raised when a new department is created.
/// </summary>
public record DepartmentCreatedEvent(Guid DepartmentId) : DomainEvent;

/// <summary>
/// Raised when a department is updated.
/// </summary>
public record DepartmentUpdatedEvent(Guid DepartmentId) : DomainEvent;
