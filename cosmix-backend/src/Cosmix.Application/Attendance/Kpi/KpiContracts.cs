using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Attendance.Kpi;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.Policies;
using Cosmix.Application.Attendance.Waiting;

namespace Cosmix.Application.Attendance.Kpi;

public static class KpiPermissions
{
    public const string Read = "attendance.kpi.read", Prepare = "attendance.kpi.prepare", Decide = "attendance.kpi.decide";
    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = [
        new(Read, "Read verified and finalized KPI evidence in historical employee scope", true, true, false),
        new(Prepare, "Submit verified KPI calculation for independent review, never salary or leave activation", true, true, false),
        new(Decide, "Independently review KPI evidence and finalize exact complete annual score", true, true, false)
    ];
}
public sealed record KpiSource(Guid EmployeeId, DateOnly From, DateOnly ToExclusive,
    Guid? PolicyVersionId, KpiResult Calculation, string Fingerprint,
    IReadOnlyList<AttendanceResourceScope> Scopes, IReadOnlyList<Guid> PolicyAssignmentIds)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? PolicyFingerprint { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public KpiSourceManifest? SourceManifest { get; init; }
}
public sealed record KpiSavedDay(DateOnly BusinessDate, Guid SummaryId, int Revision, string CalculationVersion,
    string InputFingerprint, Guid? AttendancePolicyVersionId, Guid? ShiftVersionId,
    Guid KpiPolicyVersionId, IReadOnlyList<Guid> KpiAssignmentIds, string KpiPolicyFingerprint);
public sealed record KpiSourceManifest(string CalculationVersion, KpiInput Input,
    IReadOnlyList<KpiSavedDay> SavedDays, string? SupplementalFingerprint)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WaitingPerformanceEffects? WaitingEvidence { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ReviewedKpiMissingEvidenceSource? MissingEvidence { get; init; }
}
public interface IKpiSourceReader {
    Task<IReadOnlyList<AttendanceResourceScope>> ScopesAsync(Guid employeeId, DateOnly asOf, CancellationToken ct);
    Task<KpiSource> ReadAsync(Guid employeeId, DateOnly asOf, CancellationToken ct);
}
public sealed record KpiSupplementalDay(DateOnly Date, long? CommittedTicks, long? ShortageTicks,
    int? ReviewedMissingEvidenceOccurrences, decimal? ReviewedWaitingPoints);
public sealed record KpiSupplementalSource(string State, IReadOnlyList<KpiSupplementalDay> Days,
    string Fingerprint, IReadOnlyList<string> Issues)
{
    public IReadOnlyList<AttendanceResourceScope> Scopes { get; init; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WaitingPerformanceEffects? WaitingEvidence { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ReviewedKpiMissingEvidenceSource? MissingEvidence { get; init; }
}
/// <summary>Verified existing records only, inside the caller's transaction; never client asserted outcomes.</summary>
public interface IKpiSupplementalSourceReader {
    Task<KpiSupplementalSource> ReadAsync(Guid employeeId, DateOnly from, DateOnly toExclusive,
        AdvancedAttendanceRules rules, CancellationToken ct);
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PrepareKpiRequest(DateOnly AsOf, string ExpectedSourceFingerprint,
    Guid? ExpectedPreviousFinalizationId, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DecideKpiRequest(string ExpectedVersion, ApprovalState Decision, string Reason);
public sealed record KpiRequestView(Guid Id, Guid EmployeeId, DateOnly From, DateOnly ToExclusive,
    string State, string Version, string SourceFingerprint, KpiResult Calculation, Guid? DecisionId,
    Guid? FinalizationId, int? FinalRevision);
public sealed record KpiRequestSnapshot(int SchemaVersion, Guid EmployeeId, Guid RequesterAccountId,
    DateOnly AsOf, KpiSource Source, Guid? ExpectedPreviousFinalizationId);
public sealed record FinalizedKpiScore(int SchemaVersion, Guid Id, Guid EmployeeId, DateOnly From, DateOnly ToExclusive,
    int Revision, Guid? SupersedesId, Guid RequestId, Guid DecisionId, Guid PolicyVersionId,
    decimal ExactScore, decimal DisplayScore, string CalculationFingerprint, string SourceFingerprint,
    Guid ActorAccountId, DateTimeOffset FinalizedAtUtc, IReadOnlyList<AttendanceResourceScope> OriginalScopes);
public sealed record KpiRequestRow(ApprovalRequest Request, IReadOnlyList<ApprovalDecision> Decisions);
public interface IKpiRepository {
    Task<KpiRequestRow?> FindAsync(Guid employeeId, Guid requestId, CancellationToken ct);
    Task<IReadOnlyList<AuditEvent>> FinalizationsAsync(Guid employeeId, DateOnly from, CancellationToken ct);
    Task<IReadOnlyList<ApprovalRequest>> PendingAsync(Guid employeeId, CancellationToken ct);
    void Add(ApprovalRequest request);
    void Add(ApprovalDecision decision);
    void Add(AuditEvent audit);
    void ExpectVersion(ApprovalRequest request, byte[] version);
    Task SaveChangesAsync(CancellationToken ct);
}
public interface IFinalizedKpiReader {
    /// <summary>Retained exact annual score, not an eligibility decision or a rounded display percentage.</summary>
    Task<FinalizedKpiScore?> ReadAsync(Guid employeeId, DateOnly yearFrom, CancellationToken ct);
}
