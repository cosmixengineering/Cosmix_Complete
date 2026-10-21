using System.Text.Json.Serialization;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Leaves;

namespace Cosmix.Application.HR.Leaves;

public static class LeaveBenefitPermissions
{
    public const string Review = "attendance.leaves.benefits.review";
    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = [
        new(Review, "Review and grant configured KPI leave benefits as an explicitly authorized Admin", true, true, false)
    ];
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AwardLeaveBenefitRequest(Guid LeaveTypeId, Guid PolicyVersionId, DateOnly KpiYearFrom,
    Guid ExpectedKpiFinalizationId, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RevokeLeaveBenefitRequest(string ExpectedEvidenceFingerprint, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CorrectLeaveBenefitRequest(string ExpectedEvidenceFingerprint, string ExpectedUsageFingerprint,
    IReadOnlyList<LeaveBenefitAllocation> ConsumedUsage, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LeaveBenefitAllocation(Guid LedgerEntryId, int Minutes);
public sealed record LeaveBenefitUsageEntry(Guid Id, Guid EmployeeId, Guid LeaveTypeId, Guid PolicyVersionId,
    DateOnly YearFrom, DateOnly YearTo, LeaveLedgerEntryType Type, int Minutes, Guid? RequestId,
    Guid? ReversesId, DateTimeOffset RecordedAt, int StandardDayMinutes);
public sealed record LeaveBenefitImpact(string UsageFingerprint, IReadOnlyList<LeaveBenefitUsageEntry> Entries)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LeaveBenefitResolutionView? Resolution { get; init; }
}
public sealed record LeaveBenefitCorrectionView(Guid Id, int RemovedUnusedMinutes, int ConsumedMinutes,
    int ProtectedConsumedMinutes, string Status, IReadOnlyList<LeaveBenefitAllocation> ConsumedUsage,
    DateTimeOffset RecordedAtUtc)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LeaveBenefitWaiverView? Waiver { get; init; }
}
public enum LeaveBenefitResolutionDecision { Waive = 1 }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ResolveLeaveBenefitCorrectionRequest(Guid PolicyVersionId, LeaveBenefitResolutionDecision Decision,
    string ExpectedCorrectionFingerprint, string ExpectedUsageFingerprint, string ExpectedResolutionVersion, string Reason);
public sealed record LeaveBenefitWaiverView(Guid Id, Guid PolicyVersionId, int WaivedMinutes,
    Guid ReviewerAccountId, DateTimeOffset RecordedAtUtc, string Reason);
public sealed record LeaveBenefitResolutionView(Guid CorrectionId, string CorrectionFingerprint, string Version,
    int ConsumedMinutes, int ProtectedConsumedMinutes, int WaivedMinutes, string Status, LeaveBenefitWaiverView? Waiver);
public sealed record LeaveBenefitView(Guid Id, Guid EmployeeId, Guid LeaveTypeId, DateOnly SourceFrom,
    DateOnly SourceToExclusive, DateOnly LeaveYearFrom, DateOnly LeaveYearToExclusive, DateOnly AvailableFrom,
    int Minutes, int StandardDayMinutes, bool EncashmentEligible, Guid PolicyVersionId, Guid KpiFinalizationId,
    string Status, string EvidenceFingerprint, DateTimeOffset RecordedAtUtc)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LeaveBenefitCorrectionView? Correction { get; init; }
}
public sealed record LeaveBenefitRow(LeaveLedgerEntry Entry, AuditEvent? Evidence);
public interface ILeaveBenefitRepository
{
    Task<IReadOnlyList<LeaveBenefitRow>> YearAsync(Guid employee, DateOnly from, DateOnly to, CancellationToken ct);
    Task<LeaveBenefitRow?> FindAsync(Guid employee, Guid id, CancellationToken ct);
    Task<bool> SourceExistsAsync(Guid employee, string sourceKey, CancellationToken ct);
    Task<Guid?> RecipientAsync(Guid employee, CancellationToken ct);
    Task<IReadOnlyList<LeaveBenefitUsageEntry>> UsageAsync(Guid employee, DateOnly from, DateOnly to, CancellationToken ct);
    Task<IReadOnlyList<AuditEvent>> ResolutionFactsAsync(Guid employeeId, Guid correctionId, CancellationToken ct);
    Task<IdempotencyReceipt?> ResolutionReceiptAsync(Guid actorAccountId, string key, CancellationToken ct);
    void AddResolution(AuditEvent evidence);
    void Add(LeaveLedgerEntry entry, AuditEvent evidence);
    Task SaveChangesAsync(CancellationToken ct);
}
public interface ILeaveBenefitCreditReader
{
    Task<IReadOnlyList<LeaveBenefitCredit>> ReadAsync(Guid employee, DateOnly yearFrom, DateOnly yearTo,
        DateOnly useFrom, CancellationToken ct);
}
