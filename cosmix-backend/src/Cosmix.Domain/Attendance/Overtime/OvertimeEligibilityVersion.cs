using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.Overtime;

public enum OvertimeEligibilityTarget { GlobalSite = 1, Employee = 2 }

/// <summary>Direct effective-dated OT setting. Employee true restores inheritance, never overrides a global denial.</summary>
public sealed class OvertimeEligibilityVersion : IAppendOnlyEntity
{
    private OvertimeEligibilityVersion() { }
    public OvertimeEligibilityVersion(Guid id, OvertimeEligibilityTarget target, Guid? employeeId, int revision,
        Guid? supersedesVersionId, DateOnly effectiveFrom, bool isEnabled, string actor, DateTimeOffset recordedAt,
        string reason, string protectedEvidenceReference)
    {
        if (!Enum.IsDefined(target) || (target == OvertimeEligibilityTarget.Employee) != employeeId.HasValue)
            throw new ArgumentException("An employee target requires exactly one employee; global site eligibility has none.");
        if ((revision == 1) != !supersedesVersionId.HasValue || effectiveFrom == default || effectiveFrom == DateOnly.MaxValue)
            throw new ArgumentException("Valid dated revision/predecessor is required.");
        Id = ModelGuard.Id(id, nameof(id)); Target = target; EmployeeId = AttendanceModelGuard.OptionalId(employeeId, nameof(employeeId));
        Revision = AttendanceModelGuard.Revision(revision); SupersedesVersionId = AttendanceModelGuard.OptionalId(supersedesVersionId, nameof(supersedesVersionId));
        EffectiveFrom = effectiveFrom; IsEnabled = isEnabled; RecordedBy = ModelGuard.Required(actor, 200, nameof(actor));
        RecordedAtUtc = ModelGuard.Utc(recordedAt); Reason = ModelGuard.Required(reason, 1000, nameof(reason));
        ProtectedEvidenceReference = ModelGuard.Required(protectedEvidenceReference, 500, nameof(protectedEvidenceReference));
    }
    public Guid Id { get; private set; }
    public OvertimeEligibilityTarget Target { get; private set; }
    public Guid? EmployeeId { get; private set; }
    public int Revision { get; private set; }
    public Guid? SupersedesVersionId { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public bool IsEnabled { get; private set; }
    public string RecordedBy { get; private set; } = "";
    public DateTimeOffset RecordedAtUtc { get; private set; }
    public string Reason { get; private set; } = "";
    public string ProtectedEvidenceReference { get; private set; } = "";
}
