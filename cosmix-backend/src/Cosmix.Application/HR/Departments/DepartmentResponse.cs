namespace Cosmix.Application.HR.Departments;

public sealed record DepartmentResponse(Guid Id, string Code, string Name, bool IsActive);
