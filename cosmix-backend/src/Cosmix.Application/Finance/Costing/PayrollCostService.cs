using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Security;
using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Domain.HR.PayrollAccounting;

namespace Cosmix.Application.Finance.Costing;

public sealed class PayrollCostService(SensitiveComplianceOperation reads, PayrollFinanceOperation writes,
    IPayrollCostRepository repository, PayrollCostSourceReader sources, PayrollFinanceSetupService finance,
    IPrivateFileStore files, TimeProvider clock)
{
    public const string Read = "finance.payroll-costs.read", Manage = "finance.payroll-costs.manage", Approve = "finance.payroll-costs.approve";

    public Task<PayrollCostMonthReport> MonthReportAsync(int year, int month, AuthRequestContext context, CancellationToken ct)
    {
        if (year is < 1 or > 9998 || month is < 1 or > 12) throw AttendanceOperation.Invalid();
        var start = new DateOnly(year, month, 1);
        return reads.ExecuteAsync<PayrollCostMonthReport>(Read, "finance.payroll-costs.month-report", "PayrollMonth", $"{year}-{month:00}",
            context, async (_, token) =>
            {
                var batches = await repository.MonthBatchIdsAsync(start, start.AddMonths(1), token);
                if (batches.Count > 500 || batches.Distinct().Count() != batches.Count)
                    throw AttendanceOperation.Conflict("payroll_cost_report_limit");
                var unallocated = new List<Guid>(); var costs = new List<PayrollAllocatedCost>();
                foreach (var batch in batches)
                {
                    var approved = (await PlansAsync(batch, token)).Where(x => x.State == "Approved").MaxBy(x => x.Revision);
                    if (approved is null) { unallocated.Add(batch); continue; }
                    var source = await sources.ReadAsync(batch, token);
                    if (source.PeriodStart < start || source.PeriodStart >= start.AddMonths(1))
                        throw AttendanceOperation.Conflict("payroll_cost_evidence_invalid");
                    costs.AddRange((await MapAsync(approved, source, token)).Costs);
                }
                var targets = await repository.TargetsAsync(token, includeInactive: true);
                if (targets.Projects.Count > 5000 || targets.Sites.Count > 5000) throw AttendanceOperation.Conflict("payroll_cost_target_limit");
                var projects = targets.Projects.ToDictionary(x => x.Id, x => $"{x.Code} · {x.Name}");
                var sites = targets.Sites.ToDictionary(x => x.Id, x => $"{x.Code} · {x.Name}");
                var rows = costs.GroupBy(x => new { x.ProjectId, x.SiteId, x.Currency })
                    .Select(x => new PayrollCostReportRow(x.Key.ProjectId, x.Key.SiteId, x.Key.Currency, x.Sum(c => c.Amount), x.Select(c => c.EmployeeId).Distinct().Count())
                    { ProjectLabel = x.Key.ProjectId.HasValue ? projects.GetValueOrDefault(x.Key.ProjectId.Value, "Archived project") : x.Key.SiteId.HasValue ? "Independent site" : "General",
                        SiteLabel = x.Key.SiteId.HasValue ? sites.GetValueOrDefault(x.Key.SiteId.Value, "Archived site") : "—" })
                    .OrderBy(x => x.Currency, StringComparer.Ordinal).ThenBy(x => x.ProjectId).ThenBy(x => x.SiteId).ToArray();
                return new(year, month, batches.Count - unallocated.Count, unallocated, rows);
            }, ct);
    }

    public Task<PayrollCostWorkspace> WorkspaceAsync(Guid batchId, AuthRequestContext context, CancellationToken ct) =>
        reads.ExecuteAsync<PayrollCostWorkspace>(Read, "finance.payroll-costs.read", "PayrollBatch", batchId.ToString(), context, async (_, token) =>
        {
            var source = await sources.ReadAsync(batchId, token);
            var plans = await PlansAsync(batchId, token);
            var responses = new List<PayrollCostPlanResponse>();
            foreach (var plan in plans) responses.Add(await MapAsync(plan, source, token));
            var targets = await TargetsAsync(token);
            return new(batchId, source.Fingerprint, source.Employees, targets, responses,
                plans.Where(x => x.State == "Approved").MaxBy(x => x.Revision)?.Id);
        }, ct);

    public Task<PayrollCostPlanResponse> CreateAsync(Guid batchId, PayrollCostRequest input, string key, AuthRequestContext context, CancellationToken ct)
    {
        if (batchId == Guid.Empty || input is null || input.Shares is null) throw AttendanceOperation.Invalid();
        return writes.WriteAsync(Manage, "finance.payroll-costs.create", batchId.ToString(), key, input, input.Reason, context,
            async (actor, token) =>
            {
                var source = await sources.ReadAsync(batchId, token);
                if (source.Fingerprint != input.ExpectedSourceFingerprint) throw AttendanceOperation.Conflict("payroll_cost_source_changed");
                await finance.RequirePeriodAsync(source.PeriodStart, source.Currency, token);
                var plans = await PlansAsync(batchId, token);
                if (plans.Any(x => x.State == "Draft") || plans.Where(x => x.State == "Approved").MaxBy(x => x.Revision)?.Id != input.ExpectedPreviousPlanId)
                    throw AttendanceOperation.Conflict("payroll_cost_plan_changed");
                var costs = PayrollCostAllocation.Allocate(source.Employees, input.Shares);
                ValidateTargets(costs, await TargetsAsync(token));
                var id = Guid.NewGuid(); var now = clock.GetUtcNow();
                var revision = plans.Select(x => x.Revision).DefaultIfEmpty().Max() + 1;
                var proof = new PayrollCostProof(id, batchId, source.Fingerprint, revision, input.ExpectedPreviousPlanId,
                    actor.AccountId.ToString(), actor.EmployeeId, now, input.Reason.Trim(), costs);
                var reference = await WriteProofAsync(proof, Purpose(id), token);
                var plan = new PayrollCostPlan(id, batchId, source.Fingerprint, revision, input.ExpectedPreviousPlanId,
                    reference, actor.AccountId.ToString(), actor.EmployeeId, now);
                repository.Add(plan); await repository.SaveChangesAsync(token);
                return await MapAsync(plan, source, token);
            }, ct);
    }

    public Task<PayrollCostPlanResponse> DecideAsync(Guid id, PayrollCostDecision input, string key, AuthRequestContext context, CancellationToken ct)
    {
        if (id == Guid.Empty || input is null) throw AttendanceOperation.Invalid();
        var version = SchedulingOperation.Version(input.ExpectedVersion);
        return writes.WriteAsync(Approve, "finance.payroll-costs.decide", id.ToString(), key, input, input.Reason, context,
            async (actor, token) =>
            {
                var plan = await repository.PlanAsync(id, token) ?? throw AttendanceOperation.Missing("payroll_cost_plan_not_found");
                repository.ExpectVersion(plan, version);
                if (plan.State != "Draft") throw AttendanceOperation.Conflict("payroll_cost_plan_already_reviewed");
                if (plan.CreatedBy == actor.AccountId.ToString() || plan.CreatedByEmployeeId == actor.EmployeeId) throw AttendanceOperation.Denied();
                var source = await sources.ReadAsync(plan.BatchId, token);
                var before = await MapAsync(plan, source, token);
                if (input.Approve)
                {
                    await finance.RequirePeriodAsync(source.PeriodStart, source.Currency, token);
                    var plans = await PlansAsync(plan.BatchId, token);
                    if (plans.Where(x => x.State == "Approved").MaxBy(x => x.Revision)?.Id != plan.SupersedesId)
                        throw AttendanceOperation.Conflict("payroll_cost_plan_changed");
                    ValidateTargets(before.Costs, await TargetsAsync(token));
                }
                var now = clock.GetUtcNow();
                var decision = new CostDecisionProof(id, input.Approve, actor.AccountId.ToString(), actor.EmployeeId, now, input.Reason.Trim(), plan.ProofReference);
                var reference = await WriteProofAsync(decision, Purpose(id) + ":decision", token);
                PayrollFinanceErrors.Run(() => plan.Decide(input.Approve, actor.AccountId.ToString(), actor.EmployeeId, now, reference));
                await repository.SaveChangesAsync(token);
                return await MapAsync(plan, source, token);
            }, ct);
    }

    private async Task<PayrollCostPlanResponse> MapAsync(PayrollCostPlan plan, PayrollCostSource source, CancellationToken ct)
    {
        var proof = await ReadProofAsync<PayrollCostProof>(plan.ProofReference, Purpose(plan.Id), ct);
        if (proof.Id != plan.Id || proof.BatchId != plan.BatchId || proof.Fingerprint != plan.SourceFingerprint ||
            source.BatchId != plan.BatchId || source.Fingerprint != plan.SourceFingerprint || proof.Maker != plan.CreatedBy ||
            proof.MakerEmployeeId != plan.CreatedByEmployeeId || proof.Revision != plan.Revision || proof.SupersedesId != plan.SupersedesId || proof.At != plan.CreatedAt)
            throw AttendanceOperation.Conflict("payroll_cost_evidence_invalid");
        var recalculated = PayrollCostAllocation.Allocate(source.Employees,
            proof.Costs.Select(x => new PayrollCostShare(x.EmployeeId, x.ProjectId, x.SiteId, x.BasisPoints)).ToArray());
        if (!recalculated.SequenceEqual(proof.Costs)) throw AttendanceOperation.Conflict("payroll_cost_evidence_invalid");
        if (plan.State != "Draft")
        {
            var decision = await ReadProofAsync<CostDecisionProof>(plan.DecisionProofReference!, Purpose(plan.Id) + ":decision", ct);
            if (decision.Id != plan.Id || decision.Approve != (plan.State == "Approved") || decision.Actor != plan.DecidedBy ||
                decision.EmployeeId != plan.DecidedByEmployeeId || decision.At != plan.DecidedAt || decision.PlanProof != plan.ProofReference)
                throw AttendanceOperation.Conflict("payroll_cost_evidence_invalid");
        }
        return new(plan.Id, plan.BatchId, plan.Revision, plan.State, Convert.ToBase64String(plan.RowVersion), plan.CreatedBy,
            plan.CreatedByEmployeeId, plan.DecidedBy, plan.CreatedAt, plan.DecidedAt, proof.Costs, proof.Reason);
    }

    private async Task<IReadOnlyList<PayrollCostPlan>> PlansAsync(Guid batchId, CancellationToken ct)
    {
        var plans = await repository.PlansAsync(batchId, ct);
        if (plans.Count > 100) throw AttendanceOperation.Conflict("payroll_cost_history_limit");
        return plans;
    }
    private async Task<PayrollCostTargets> TargetsAsync(CancellationToken ct)
    {
        var targets = await repository.TargetsAsync(ct);
        if (targets.Projects.Count > 5000 || targets.Sites.Count > 5000) throw AttendanceOperation.Conflict("payroll_cost_target_limit");
        return targets;
    }
    private static void ValidateTargets(IReadOnlyList<PayrollAllocatedCost> costs, PayrollCostTargets targets)
    {
        var projects = targets.Projects.Select(x => x.Id).ToHashSet(); var sites = targets.Sites.ToDictionary(x => x.Id);
        if (costs.Any(x => x.ProjectId.HasValue && !projects.Contains(x.ProjectId.Value) ||
            x.SiteId.HasValue && (!sites.TryGetValue(x.SiteId.Value, out var site) || site.ProjectId != x.ProjectId)))
            throw AttendanceOperation.Invalid("payroll_cost_target_invalid");
    }
    private async Task<string> WriteProofAsync<T>(T proof, string purpose, CancellationToken ct) =>
        AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(proof), purpose, ct));
    private async Task<T> ReadProofAsync<T>(string reference, string purpose, CancellationToken ct)
    {
        try
        {
            var file = JsonSerializer.Deserialize<PrivateFileReference>(reference) ?? throw AttendanceOperation.Conflict("payroll_cost_evidence_invalid");
            return JsonSerializer.Deserialize<T>(await files.ReadAsync(file, purpose, ct)) ?? throw AttendanceOperation.Conflict("payroll_cost_evidence_invalid");
        }
        catch (JsonException) { throw AttendanceOperation.Conflict("payroll_cost_evidence_invalid"); }
    }
    private static string Purpose(Guid id) => $"finance:payroll-cost:{id:N}";
    private sealed record CostDecisionProof(Guid Id, bool Approve, string Actor, Guid EmployeeId, DateTimeOffset At, string Reason, string PlanProof);
}
