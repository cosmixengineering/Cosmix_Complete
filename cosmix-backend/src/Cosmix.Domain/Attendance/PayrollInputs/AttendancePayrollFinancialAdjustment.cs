using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.PayrollInputs;

/// <summary>Approved financial input, not posting or payment. Monetary evidence stays private.</summary>
public sealed class AttendancePayrollFinancialAdjustment : IAppendOnlyEntity
{
    private AttendancePayrollFinancialAdjustment() { }
    public AttendancePayrollFinancialAdjustment(Guid employeeId, DateOnly sourceMonth, Guid sourceRevisionId,
        Guid confirmedPostingId, int revision, Guid? previousAdjustmentId, DateOnly targetMonth,
        Guid targetRevisionId, string previewFingerprint, Guid actorUserId, DateTimeOffset createdAtUtc,
        string protectedEvidenceReference, string evidenceFingerprint)
    {
        AttendancePayrollFinancialRules.RequireMonth(sourceMonth);
        AttendancePayrollFinancialRules.RequireMonth(targetMonth);
        if (targetMonth <= sourceMonth || revision < 1 || (revision == 1) != (previousAdjustmentId is null) ||
            previousAdjustmentId == Guid.Empty || !AttendancePayrollAttestationRules.Hash(previewFingerprint) ||
            !AttendancePayrollAttestationRules.Hash(evidenceFingerprint) || createdAtUtc == default || createdAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Invalid financial adjustment provenance.");
        Id = Guid.NewGuid(); EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        SourceMonth = sourceMonth; SourceRevisionId = ModelGuard.Id(sourceRevisionId, nameof(sourceRevisionId));
        ConfirmedPostingId = ModelGuard.Id(confirmedPostingId, nameof(confirmedPostingId));
        Revision = revision; PreviousAdjustmentId = previousAdjustmentId; TargetMonth = targetMonth;
        TargetRevisionId = ModelGuard.Id(targetRevisionId, nameof(targetRevisionId));
        PreviewFingerprint = previewFingerprint; ActorUserId = ModelGuard.Id(actorUserId, nameof(actorUserId));
        CreatedAtUtc = createdAtUtc;
        ProtectedEvidenceReference = ModelGuard.Required(protectedEvidenceReference, 500, nameof(protectedEvidenceReference));
        EvidenceFingerprint = evidenceFingerprint;
    }
    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public DateOnly SourceMonth { get; private set; }
    public Guid SourceRevisionId { get; private set; }
    public Guid ConfirmedPostingId { get; private set; }
    public int Revision { get; private set; }
    public Guid? PreviousAdjustmentId { get; private set; }
    public DateOnly TargetMonth { get; private set; }
    public Guid TargetRevisionId { get; private set; }
    public string PreviewFingerprint { get; private set; } = "";
    public Guid ActorUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public string ProtectedEvidenceReference { get; private set; } = "";
    public string EvidenceFingerprint { get; private set; } = "";
}

/// <summary>One immutable terminal resolution of an approved input; cancellation moves no money.</summary>
public sealed class AttendancePayrollFinancialResolution : IAppendOnlyEntity
{
    private AttendancePayrollFinancialResolution() { }
    public AttendancePayrollFinancialResolution(Guid adjustmentId, string status, string? externalReceiptKey,
        DateTimeOffset occurredAtUtc, Guid actorUserId, DateTimeOffset createdAtUtc,
        string protectedEvidenceReference, string evidenceFingerprint)
    {
        if (status is not ("posted" or "cancelled") ||
            (status == "posted" ? !AttendancePayrollAttestationRules.Hash(externalReceiptKey) : externalReceiptKey is not null) ||
            !AttendancePayrollAttestationRules.Hash(evidenceFingerprint) || occurredAtUtc == default ||
            occurredAtUtc.Offset != TimeSpan.Zero || createdAtUtc.Offset != TimeSpan.Zero || occurredAtUtc > createdAtUtc)
            throw new ArgumentException("Invalid financial adjustment resolution.");
        Id = Guid.NewGuid(); AdjustmentId = ModelGuard.Id(adjustmentId, nameof(adjustmentId));
        Status = status; ExternalReceiptKey = externalReceiptKey; OccurredAtUtc = occurredAtUtc;
        ActorUserId = ModelGuard.Id(actorUserId, nameof(actorUserId)); CreatedAtUtc = createdAtUtc;
        ProtectedEvidenceReference = ModelGuard.Required(protectedEvidenceReference, 500, nameof(protectedEvidenceReference));
        EvidenceFingerprint = evidenceFingerprint;
    }
    public Guid Id { get; private set; }
    public Guid AdjustmentId { get; private set; }
    public string Status { get; private set; } = "";
    public string? ExternalReceiptKey { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public Guid ActorUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public string ProtectedEvidenceReference { get; private set; } = "";
    public string EvidenceFingerprint { get; private set; } = "";
}

public static class AttendancePayrollFinancialRules
{
    public const int MaximumAdjustments = 128;
    public static void RequireMonth(DateOnly month)
    {
        if (month.Day != 1 || month.Year is < 1900 or > 9998) throw new ArgumentException("A supported calendar month is required.");
    }
    // Positive is additional recovery; negative is credit. Only confirmed effects enter this balance.
    public static decimal Difference(decimal actuallyDeducted, decimal correctedDeduction,
        IReadOnlyList<decimal> confirmedAdjustments)
    {
        if (!Money(actuallyDeducted) || actuallyDeducted < 0 || !Money(correctedDeduction) || correctedDeduction < 0 ||
            confirmedAdjustments is null || confirmedAdjustments.Count > MaximumAdjustments ||
            confirmedAdjustments.Any(x => !Money(x) || x == 0)) throw new ArgumentException("Exact confirmed monetary evidence is required.");
        var balance = actuallyDeducted;
        foreach (var amount in confirmedAdjustments)
        {
            balance = checked(balance + amount);
            if (balance < 0) throw new ArgumentException("Confirmed credits exceed actual deductions.");
        }
        return checked(correctedDeduction - balance);
    }
    public static bool Money(decimal value) => decimal.Round(value, 2) == value;
}
