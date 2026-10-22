using Cosmix.Domain.Common;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Domain.HR.StatutoryContributions;

public sealed class EmployeeStatutoryRegistration
{
    private EmployeeStatutoryRegistration() { }

    public EmployeeStatutoryRegistration(Guid employeeId, StatutoryScheme scheme, string registrationNumber,
        DateOnly registeredAt, string recordedByUserId, DateTimeOffset recordedAt)
    {
        Id = Guid.NewGuid();
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        Scheme = Enum.IsDefined(scheme) ? scheme : throw new ArgumentOutOfRangeException(nameof(scheme));
        RegistrationNumber = ModelGuard.Required(registrationNumber, 100, nameof(registrationNumber));
        RegisteredAt = registeredAt;
        RecordedByUserId = ModelGuard.Required(recordedByUserId, 200, nameof(recordedByUserId));
        RecordedAt = ModelGuard.Utc(recordedAt);
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Employee Employee { get; private set; } = null!;
    public StatutoryScheme Scheme { get; private set; }
    public string RegistrationNumber { get; private set; } = string.Empty;
    public DateOnly RegisteredAt { get; private set; }
    public string RecordedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }
}
