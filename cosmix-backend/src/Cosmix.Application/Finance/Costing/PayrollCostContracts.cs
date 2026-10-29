using Cosmix.Domain.HR.PayrollAccounting;

namespace Cosmix.Application.Finance.Costing;

public sealed record PayrollCostRequest(string ExpectedSourceFingerprint, Guid? ExpectedPreviousPlanId,
    IReadOnlyList<PayrollCostShare> Shares, string Reason);
public sealed record PayrollCostDecision(string ExpectedVersion, bool Approve, string Reason);
public sealed record PayrollCostTarget(Guid Id, string Code, string Name, Guid? ProjectId = null);
public sealed record PayrollCostTargets(IReadOnlyList<PayrollCostTarget> Projects, IReadOnlyList<PayrollCostTarget> Sites);
public sealed record PayrollCostPlanResponse(Guid Id, Guid BatchId, int Revision, string State, string Version,
    string CreatedBy, Guid CreatedByEmployeeId, string? DecidedBy, DateTimeOffset CreatedAt, DateTimeOffset? DecidedAt,
    IReadOnlyList<PayrollAllocatedCost> Costs, string Reason);
public sealed record PayrollCostWorkspace(Guid BatchId, string SourceFingerprint, IReadOnlyList<PayrollCostBasis> Employees,
    PayrollCostTargets Targets, IReadOnlyList<PayrollCostPlanResponse> Plans, Guid? EffectivePlanId);
public sealed record PayrollCostProof(Guid Id, Guid BatchId, string Fingerprint, int Revision, Guid? SupersedesId,
    string Maker, Guid MakerEmployeeId, DateTimeOffset At, string Reason, IReadOnlyList<PayrollAllocatedCost> Costs);
public sealed record PayrollCostReportRow(Guid? ProjectId, Guid? SiteId, string Currency, decimal Amount, int Employees)
{ public string ProjectLabel { get; init; } = ""; public string SiteLabel { get; init; } = ""; }
public sealed record PayrollCostMonthReport(int Year, int Month, int ApprovedBatchCount,
    IReadOnlyList<Guid> UnallocatedBatchIds, IReadOnlyList<PayrollCostReportRow> Rows);
public interface IPayrollCostRepository
{
    Task<IReadOnlyList<Guid>> MonthBatchIdsAsync(DateOnly start, DateOnly end, CancellationToken ct);
    Task<IReadOnlyList<PayrollCostPlan>> PlansAsync(Guid batchId, CancellationToken ct);
    Task<PayrollCostPlan?> PlanAsync(Guid id, CancellationToken ct);
    Task<PayrollCostTargets> TargetsAsync(CancellationToken ct, bool includeInactive = false);
    void Add(PayrollCostPlan plan);
    void ExpectVersion(PayrollCostPlan plan, byte[] version);
    Task SaveChangesAsync(CancellationToken ct);
}
