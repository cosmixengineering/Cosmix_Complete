using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.PayrollInputs;

/// <summary>Immutable manually attested event. All financial lines and external evidence remain private.</summary>
public sealed class AttendancePayrollAttestation : IAppendOnlyEntity
{
    private AttendancePayrollAttestation() { }
    public AttendancePayrollAttestation(Guid employeeId, DateOnly periodStart, Guid sourceRevisionId,
        int sourceRevision, string sourceFingerprint, Guid? previousAttestationId, string status,
        string lineFingerprint, string externalReceiptKey, DateTimeOffset occurredAtUtc, DateTimeOffset? postedAtUtc,
        Guid actorUserId, DateTimeOffset createdAtUtc, string protectedEvidenceReference, string evidenceFingerprint)
    {
        var step = AttendancePayrollAttestationRules.Step(status);
        if (periodStart.Day != 1 || periodStart.Year is < 1900 or > 9998 || sourceRevision < 1 ||
            previousAttestationId == Guid.Empty || (step == 1) != (previousAttestationId is null) ||
            new[] { sourceFingerprint, lineFingerprint, externalReceiptKey, evidenceFingerprint }.Any(x => !AttendancePayrollAttestationRules.Hash(x)) ||
            occurredAtUtc == default || occurredAtUtc.Offset != TimeSpan.Zero || createdAtUtc.Offset != TimeSpan.Zero ||
            occurredAtUtc > createdAtUtc ||
            (step == 1 ? postedAtUtc is not null : postedAtUtc is null || postedAtUtc.Value == default ||
                postedAtUtc.Value.Offset != TimeSpan.Zero || postedAtUtc > occurredAtUtc) ||
            step == 2 && postedAtUtc != occurredAtUtc)
            throw new ArgumentException("Invalid bound payroll attestation.");
        Id = Guid.NewGuid(); EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        PeriodStart = periodStart; SourceRevisionId = ModelGuard.Id(sourceRevisionId, nameof(sourceRevisionId));
        SourceRevision = sourceRevision; SourceFingerprint = sourceFingerprint;
        PreviousAttestationId = previousAttestationId; Status = status; Step = step;
        LineFingerprint = lineFingerprint; ExternalReceiptKey = externalReceiptKey;
        OccurredAtUtc = occurredAtUtc; PostedAtUtc = postedAtUtc;
        ActorUserId = ModelGuard.Id(actorUserId, nameof(actorUserId)); CreatedAtUtc = createdAtUtc;
        ProtectedEvidenceReference = ModelGuard.Required(protectedEvidenceReference, 500, nameof(protectedEvidenceReference));
        EvidenceFingerprint = evidenceFingerprint;
    }
    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public Guid SourceRevisionId { get; private set; }
    public int SourceRevision { get; private set; }
    public string SourceFingerprint { get; private set; } = string.Empty;
    public Guid? PreviousAttestationId { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public int Step { get; private set; }
    public string LineFingerprint { get; private set; } = string.Empty;
    public string ExternalReceiptKey { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public DateTimeOffset? PostedAtUtc { get; private set; }
    public Guid ActorUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public string ProtectedEvidenceReference { get; private set; } = string.Empty;
    public string EvidenceFingerprint { get; private set; } = string.Empty;
    public bool ConfirmsPosting => Step is 2 or 3;
}
