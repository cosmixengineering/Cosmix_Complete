using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.Rosters;

public sealed class TemporaryShiftChangeEmployee : IAppendOnlyEntity
{
    private TemporaryShiftChangeEmployee() { }
    public TemporaryShiftChangeEmployee(Guid temporaryShiftChangeId, Guid employeeId)
    {
        Id = Guid.NewGuid(); TemporaryShiftChangeId = ModelGuard.Id(temporaryShiftChangeId, nameof(temporaryShiftChangeId)); EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
    }
    public Guid Id { get; private set; }
    public Guid TemporaryShiftChangeId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public TemporaryShiftChange Change { get; private set; } = null!;
}
