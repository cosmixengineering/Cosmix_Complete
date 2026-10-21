using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.PayrollInputs;

/// <summary>An immutable replacement set of payroll inputs, never a payment instruction.</summary>
public sealed class AttendancePayrollInputRevision : IAppendOnlyEntity
{
    private AttendancePayrollInputRevision() { }
    public AttendancePayrollInputRevision(Guid employeeId, DateOnly periodStart, int revision,
        Guid? supersedesRevisionId, string calculationVersion, string inputFingerprint,
        string protectedEvidenceReference, Guid actorUserId, DateTimeOffset createdAtUtc)
    {
        if (periodStart.Day != 1 || periodStart.Year is < 1900 or > 9998)
            throw new ArgumentOutOfRangeException(nameof(periodStart));
        if (revision < 1 || (revision == 1) != (supersedesRevisionId is null))
            throw new ArgumentException("A first revision has no predecessor; later revisions require one.");
        var fingerprint = ModelGuard.Required(inputFingerprint, 64, nameof(inputFingerprint));
        if (fingerprint.Length != 64 || fingerprint.Any(c => c is not (>= '0' and <= '9') and not (>= 'A' and <= 'F')))
            throw new ArgumentException("An uppercase SHA-256 fingerprint is required.", nameof(inputFingerprint));
        Id = Guid.NewGuid();
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        PeriodStart = periodStart; Revision = revision;
        SupersedesRevisionId = AttendanceModelGuard.OptionalId(supersedesRevisionId, nameof(supersedesRevisionId));
        CalculationVersion = ModelGuard.Required(calculationVersion, 100, nameof(calculationVersion));
        InputFingerprint = fingerprint;
        ProtectedEvidenceReference = ModelGuard.Required(protectedEvidenceReference, 500, nameof(protectedEvidenceReference));
        ActorUserId = ModelGuard.Id(actorUserId, nameof(actorUserId));
        CreatedAtUtc = ModelGuard.Utc(createdAtUtc);
    }
    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public int Revision { get; private set; }
    public Guid? SupersedesRevisionId { get; private set; }
    public string CalculationVersion { get; private set; } = string.Empty;
    public string InputFingerprint { get; private set; } = string.Empty;
    public string ProtectedEvidenceReference { get; private set; } = string.Empty;
    public Guid ActorUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
