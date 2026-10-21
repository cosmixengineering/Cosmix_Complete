using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Leaves;
using Cosmix.Domain.Attendance.MonthLocks;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Leaves;
using Cosmix.Domain.HR.Policies;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed class LeaveRepository(CosmixDbContext db, ILeaveBenefitCreditReader? benefits = null) : ILeaveRepository
{
    public async Task<IReadOnlyList<LeaveType>> TypesAsync(CancellationToken ct) => await db.LeaveTypes.OrderBy(x => x.Code).Take(501).ToArrayAsync(ct);
    public Task<LeaveType?> TypeAsync(Guid id, CancellationToken ct) => db.LeaveTypes.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> TypeCodeExistsAsync(string code, CancellationToken ct) => db.LeaveTypes.AnyAsync(x => x.Code == code, ct);
    public Task<bool> SubmissionExistsAsync(Guid employeeId, string key, CancellationToken ct) => db.LeaveRequests.AnyAsync(x => x.EmployeeId == employeeId && x.SubmissionKey == key, ct);
    public Task<bool> EmployeeExistsAsync(Guid employeeId, CancellationToken ct) => db.Employees.AnyAsync(x => x.Id == employeeId && x.Status == Cosmix.Domain.HR.Employees.EmployeeStatus.Active, ct);
    private IQueryable<LeaveRequest> Query(Guid? employeeId) => db.LeaveRequests.Include(x => x.LeaveType).Include(x => x.ApprovalRequest).Include(x => x.Days)
        .Where(x => !employeeId.HasValue || x.EmployeeId == employeeId);
    public async Task<LeaveRow?> FindAsync(Guid id, Guid? employeeId, CancellationToken ct)
    { var request = await Query(employeeId).SingleOrDefaultAsync(x => x.Id == id, ct); return request is null ? null : (await Rows([request], ct)).Single(); }
    public async Task<PagedResult<LeaveRow>> ListAsync(Guid? employeeId, LeaveListRequest request, CancellationToken ct)
    {
        var query = SearchQuery(employeeId, request);
        var count = await query.CountAsync(ct);
        var requests = await ListQuery(employeeId, request).ToArrayAsync(ct);
        var rows = await Rows(requests, ct);
        return new(rows, count, request.Page, request.PageSize);
    }
    public Task<IReadOnlyList<LeaveRow>> ForDateAsync(Guid employeeId, DateOnly date, CancellationToken ct) => OverlappingAsync(employeeId, date, date, ct);
    public async Task<IReadOnlyList<LeaveRow>> OverlappingAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var requests = await OverlapQuery(employeeId, from, to).ToArrayAsync(ct);
        if (requests.Length > 1000) throw AttendanceOperation.Conflict("leave_history_limit");
        return await Rows(requests, ct);
    }
    private async Task<IReadOnlyList<LeaveRow>> Rows(LeaveRequest[] requests, CancellationToken ct)
    {
        if (requests.Length == 0) return [];
        var requestIds = requests.Select(x => x.Id).ToArray();
        var cancellations = await CancellationQuery(requestIds).ToArrayAsync(ct);
        if (cancellations.Length > 5000 || cancellations.GroupBy(x => x.LeaveRequestId).Any(x => x.Count() > 100)) throw AttendanceOperation.Conflict("leave_cancellation_history_limit");
        var approvalIds = requests.Select(x => x.ApprovalRequestId).Concat(cancellations.Select(x => x.ApprovalRequestId))
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        var decisions = await DecisionQuery(approvalIds).ToArrayAsync(ct);
        if (decisions.GroupBy(x => x.ApprovalRequestId).Any(x => x.Count() > 1)) throw AttendanceOperation.Conflict("leave_decision_history_conflict");
        var successors = await SuccessorQuery(requestIds).ToArrayAsync(ct);
        var decisionGroups = decisions.ToLookup(x => x.ApprovalRequestId);
        var cancellationGroups = cancellations.ToLookup(x => x.LeaveRequestId);
        return requests.Select(r => new LeaveRow(r, r.LeaveType, r.ApprovalRequest,
            r.ApprovalRequestId.HasValue ? decisionGroups[r.ApprovalRequestId.Value].ToArray() : [],
            cancellationGroups[r.Id].Select(c => new LeaveCancellationRow(c, c.ApprovalRequest,
                c.ApprovalRequestId.HasValue ? decisionGroups[c.ApprovalRequestId.Value].ToArray() : [])).ToArray(),
            successors.SingleOrDefault(x => x.PreviousRequestId == r.Id)?.Id)).ToArray();
    }
    public async Task<LeavePolicyEvidence?> PolicyAsync(Guid employeeId, Guid typeId, Guid policyVersionId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var version = await db.Set<PolicyVersion>().Include(x => x.Policy).SingleOrDefaultAsync(x => x.Id == policyVersionId, ct);
        if (version is null || version.Policy.Category != PolicyCategory.Leave || !LeavePolicySettings.SupportsSchema(version.RulesSchemaVersion)) return null;
        var settings = LeavePolicySettings.Parse(version.RulesJson, version.RulesSchemaVersion);
        var assignments = await db.EmployeeAssignments.Where(x => x.EmployeeId == employeeId && x.EffectiveFrom <= to && (x.EffectiveTo == null || x.EffectiveTo > from)).Take(1001).ToArrayAsync(ct);
        if (assignments.Length > 1000) throw AttendanceOperation.Conflict("leave_assignment_history_limit");
        // Every calendar date must have one unambiguous category; a transfer requires separate requests.
        var category = assignments.FirstOrDefault()?.StaffCategoryId ?? Guid.Empty;
        var validAssignment = category != Guid.Empty && assignments.All(x => x.StaffCategoryId == category) &&
            Enumerable.Range(0, to.DayNumber - from.DayNumber + 1).All(i => assignments.Count(x => x.EffectiveFrom <= from.AddDays(i) && (x.EffectiveTo == null || from.AddDays(i) < x.EffectiveTo)) == 1);
        var employee = await db.Employees.SingleOrDefaultAsync(x => x.Id == employeeId, ct);
        var calendar = await db.HolidayCalendars.SingleOrDefaultAsync(x => x.Id == settings.HolidayCalendarId, ct);
        var holidays = await db.Holidays.Where(x => x.HolidayCalendarId == settings.HolidayCalendarId && x.HolidayDate >= from && x.HolidayDate <= to).Select(x => x.HolidayDate).ToArrayAsync(ct);
        var year = LeavePolicySettings.Year(from, settings);
        // A different year boundary cannot hide previously recorded usage. Transition
        // reconciliation has no approved entitlement rule, so mixed windows fail closed.
        if (await ConflictingQuotaYearQuery(employeeId, year.From, year.To).AnyAsync(ct))
            throw AttendanceOperation.Conflict("leave_policy_year_transition_unavailable");
        var ledger = await LedgerQuotaQuery(employeeId, year.From, year.To).ToArrayAsync(ct);
        if (ledger.Length > 10000) throw AttendanceOperation.Conflict("leave_ledger_history_limit");
        var encashments = await db.LeaveEncashmentRequests.Include(x => x.ApprovalRequest)
            .Where(x => x.EmployeeId == employeeId && x.LeaveYearStart < year.To && x.LeaveYearEnd > year.From)
            .Take(1001).ToArrayAsync(ct);
        if (encashments.Length > 1000) throw AttendanceOperation.Conflict("encashment_history_limit");
        var encashmentKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var encashment in encashments)
        {
            var active = encashment.ApprovalRequest?.State is ApprovalState.Pending or ApprovalState.Approved;
            if (active && (encashment.LeaveYearStart != year.From || encashment.LeaveYearEnd != year.To))
                throw AttendanceOperation.Conflict("leave_policy_year_transition_unavailable");
            LeaveEncashmentLedger.Verify(encashment, ledger.Select(x => x.Entry).ToArray(), allowLegacy: !active);
            encashmentKeys.UnionWith([LeaveEncashmentLedger.ReserveKey(encashment.Id), LeaveEncashmentLedger.ReleaseKey(encashment.Id), LeaveEncashmentLedger.ConsumeKey(encashment.Id)]);
        }
        var units = new List<LeaveUsageUnit>();
        var entriesById = ledger.ToDictionary(x => x.Entry.Id);
        var reversals = ledger.Where(x => x.Entry.ReversesEntryId.HasValue).ToLookup(x => x.Entry.ReversesEntryId!.Value);
        var denominators = new Dictionary<Guid, int>();
        foreach (var item in ledger)
        {
            if (!LeavePolicySettings.SupportsSchema(item.RulesSchemaVersion) || item.Authority != PolicyAuthority.DirectHr || item.Category != PolicyCategory.Leave ||
                item.Entry.EntryType == LeaveLedgerEntryType.Utilization && (item.Entry.MinutesDelta >= 0 || item.Entry.ReversesEntryId.HasValue) ||
                item.Entry.EntryType is LeaveLedgerEntryType.Reservation or LeaveLedgerEntryType.Encashment &&
                    (item.Entry.MinutesDelta >= 0 || item.Entry.ReversesEntryId.HasValue || item.Entry.LeaveRequestId is not null || !encashmentKeys.Contains(item.Entry.IdempotencyKey)) ||
                item.Entry.EntryType == LeaveLedgerEntryType.Release && (item.Entry.MinutesDelta <= 0 || item.Entry.ReversesEntryId is null))
                throw AttendanceOperation.Conflict("leave_usage_units_unavailable");
            if (item.Entry.EntryType == LeaveLedgerEntryType.Release)
            {
                var original = entriesById.GetValueOrDefault(item.Entry.ReversesEntryId!.Value)?.Entry;
                if (original is null || original.EntryType is not (LeaveLedgerEntryType.Utilization or LeaveLedgerEntryType.Reservation) || original.PolicyVersionId != item.Entry.PolicyVersionId ||
                    original.EntryType == LeaveLedgerEntryType.Reservation && !encashmentKeys.Contains(item.Entry.IdempotencyKey) ||
                    original.LeaveRequestId != item.Entry.LeaveRequestId || original.LeaveTypeId != item.Entry.LeaveTypeId || original.MinutesDelta != -item.Entry.MinutesDelta ||
                    reversals[original.Id].Count() != 1) throw AttendanceOperation.Conflict("leave_ledger_reversal_conflict");
            }
            if (!denominators.TryGetValue(item.Entry.PolicyVersionId, out var denominator))
            { denominator = LeavePolicySettings.Parse(item.RulesJson, item.RulesSchemaVersion).StandardDayMinutes; denominators.Add(item.Entry.PolicyVersionId, denominator); }
            units.Add(new(-item.Entry.MinutesDelta, denominator, item.Kind));
        }
        var kind = await db.LeaveTypes.Where(x => x.Id == typeId).Select(x => x.Kind).SingleAsync(ct);
        var valid = validAssignment && employee is not null && version.Authority == PolicyAuthority.DirectHr && version.ApprovalRequestId is null &&
            version.EffectiveFrom <= from && (version.EffectiveTo == null || to < version.EffectiveTo) &&
            calendar is not null && calendar.IsActive && calendar.EffectiveFrom <= from && (calendar.EffectiveTo == null || to < calendar.EffectiveTo);
        IReadOnlyList<LeaveBenefitCredit> credits = [];
        if (benefits is not null) credits = await benefits.ReadAsync(employeeId, year.From, year.To, from, ct);
        else if (await db.LeaveLedgerEntries.AnyAsync(x => x.EmployeeId == employeeId && x.LeaveYearStart == year.From &&
            x.LeaveYearEnd == year.To && (x.EntryType == LeaveLedgerEntryType.Entitlement || x.EntryType == LeaveLedgerEntryType.Adjustment), ct))
            throw AttendanceOperation.Conflict("leave_benefit_evidence_unavailable");
        return new(version, category, employee?.JoinedOn, valid, holidays,
            new(units.Sum(x => x.UsedMinutes), units.Where(x => x.Kind == kind).Sum(x => x.UsedMinutes), units) { Credits = credits.Count == 0 ? null : credits })
            { AssignmentIds = assignments.Select(x => x.Id).Order().ToArray() };
    }
    public Task<bool> AnyLockedAsync(DateOnly from, DateOnly to, CancellationToken ct) => db.Set<AttendancePeriodLock>().AnyAsync(x => x.IsLocked && x.PeriodStart <= to && x.PeriodEndExclusive > from, ct);
    public async Task<IReadOnlyList<LeaveLedgerEntry>> LedgerAsync(Guid requestId, CancellationToken ct) => await db.LeaveLedgerEntries.Where(x => x.LeaveRequestId == requestId).OrderBy(x => x.RecordedAt).ToArrayAsync(ct);
    public async Task<bool> SettingsReferencesValidAsync(LeavePolicySettings settings, CancellationToken ct) =>
        await db.StaffCategories.CountAsync(x => settings.EligibleStaffCategoryIds.Contains(x.Id) && x.IsActive, ct) == settings.EligibleStaffCategoryIds.Length &&
        await db.HolidayCalendars.AnyAsync(x => x.Id == settings.HolidayCalendarId && x.IsActive, ct);
    public Task<bool> PolicyCodeExistsAsync(string code, CancellationToken ct) => db.Set<Policy>().AnyAsync(x => x.Code == code, ct);
    public async Task<IReadOnlyList<PolicyVersion>> PolicyVersionsAsync(CancellationToken ct) => await db.Set<PolicyVersion>().Include(x => x.Policy)
        .Where(x => x.Policy.Category == PolicyCategory.Leave &&
            (x.RulesSchemaVersion == LeavePolicySettings.SchemaVersion || x.RulesSchemaVersion == LeavePolicySettings.ConfigurableSchemaVersion || x.RulesSchemaVersion == LeavePolicySettings.EncashmentSchemaVersion || x.RulesSchemaVersion == LeavePolicySettings.BenefitsSchemaVersion))
        .OrderByDescending(x => x.PublishedAt).Take(501).ToArrayAsync(ct);
    public Task<PolicyVersion?> PolicyVersionAsync(Guid id, CancellationToken ct) => db.Set<PolicyVersion>().Include(x => x.Policy).SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<DateOnly>> HolidaysAsync(Guid calendarId, DateOnly from, DateOnly to, CancellationToken ct) =>
        await db.Holidays.Where(x => x.HolidayCalendarId == calendarId && x.HolidayDate >= from && x.HolidayDate <= to).OrderBy(x => x.HolidayDate).Select(x => x.HolidayDate).ToArrayAsync(ct);
    public void Add(LeaveType type) => db.LeaveTypes.Add(type);
    public void Add(LeaveRequest request, IReadOnlyList<LeaveRequestDay> days, ApprovalRequest approval)
    { db.LeaveRequests.Add(request); db.Set<LeaveRequestDay>().AddRange(days); db.Set<ApprovalRequest>().Add(approval); }
    public void Add(LeaveCancellationRequest request, ApprovalRequest approval) { db.LeaveCancellationRequests.Add(request); db.Set<ApprovalRequest>().Add(approval); }
    public void Add(ApprovalDecision decision) => db.Set<ApprovalDecision>().Add(decision);
    public void Add(LeaveLedgerEntry entry) => db.LeaveLedgerEntries.Add(entry);
    public void Add(Policy policy, PolicyVersion version) { db.Set<Policy>().Add(policy); db.Set<PolicyVersion>().Add(version); }
    public void ExpectVersion(ApprovalRequest approval, byte[] version) => db.Entry(approval).Property(x => x.RowVersion).OriginalValue = version;
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    internal IQueryable<LeaveRequest> SearchQuery(Guid? employeeId, LeaveListRequest request)
    {
        var query = Query(employeeId).Where(x => x.StartDate <= request.To && x.EndDate >= request.From);
        return request.PendingOnly ? query.Where(x => x.ApprovalRequest != null && x.ApprovalRequest.State == ApprovalState.Pending ||
            db.LeaveCancellationRequests.Any(c => c.LeaveRequestId == x.Id && c.ApprovalRequest != null && c.ApprovalRequest.State == ApprovalState.Pending)) : query;
    }
    internal IQueryable<LeaveRequest> ListQuery(Guid? employeeId, LeaveListRequest request) => SearchQuery(employeeId, request)
        .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip(checked((request.Page - 1) * request.PageSize)).Take(request.PageSize);
    internal IQueryable<LeaveRequest> OverlapQuery(Guid employeeId, DateOnly from, DateOnly to) => Query(employeeId)
        .Where(x => x.StartDate <= to && x.EndDate >= from).OrderBy(x => x.Id).Take(1001);
    internal IQueryable<LeaveCancellationRequest> CancellationQuery(Guid[] requestIds) => db.LeaveCancellationRequests.Include(x => x.ApprovalRequest)
        .Where(x => requestIds.Contains(x.LeaveRequestId)).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Take(5001);
    internal IQueryable<ApprovalDecision> DecisionQuery(Guid[] approvalIds) => db.Set<ApprovalDecision>().Where(x => approvalIds.Contains(x.ApprovalRequestId))
        .OrderBy(x => x.DecidedAt).ThenBy(x => x.Id).Take(approvalIds.Length * 2 + 1);
    internal IQueryable<LeaveSuccessor> SuccessorQuery(Guid[] requestIds) => db.LeaveRequests.Where(x => x.PreviousRequestId.HasValue && requestIds.Contains(x.PreviousRequestId.Value))
        .Select(x => new LeaveSuccessor(x.PreviousRequestId, x.Id));
    internal IQueryable<LeaveQuotaLedger> LedgerQuotaQuery(Guid employeeId, DateOnly from, DateOnly to) => db.LeaveLedgerEntries
        .Where(x => x.EmployeeId == employeeId && x.LeaveYearStart == from && x.LeaveYearEnd == to &&
            (x.EntryType == LeaveLedgerEntryType.Utilization || x.EntryType == LeaveLedgerEntryType.Release || x.EntryType == LeaveLedgerEntryType.Reservation || x.EntryType == LeaveLedgerEntryType.Encashment))
        .OrderBy(x => x.RecordedAt).ThenBy(x => x.Id).Take(10001)
        .Select(x => new LeaveQuotaLedger(x, x.LeaveType.Kind, x.PolicyVersion.RulesJson, x.PolicyVersion.RulesSchemaVersion, x.PolicyVersion.Authority, x.PolicyVersion.Policy.Category));
    internal sealed record LeaveSuccessor(Guid? PreviousRequestId, Guid Id);
    internal IQueryable<LeaveLedgerEntry> ConflictingQuotaYearQuery(Guid employeeId, DateOnly from, DateOnly to) => db.LeaveLedgerEntries
        .Where(x => x.EmployeeId == employeeId && x.LeaveYearStart < to && x.LeaveYearEnd > from &&
            (x.LeaveYearStart != from || x.LeaveYearEnd != to) &&
            (x.EntryType == LeaveLedgerEntryType.Utilization || x.EntryType == LeaveLedgerEntryType.Release || x.EntryType == LeaveLedgerEntryType.Reservation || x.EntryType == LeaveLedgerEntryType.Encashment || x.EntryType == LeaveLedgerEntryType.Entitlement || x.EntryType == LeaveLedgerEntryType.Adjustment));
    internal sealed record LeaveQuotaLedger(LeaveLedgerEntry Entry, LeaveKind Kind, string RulesJson, string RulesSchemaVersion, PolicyAuthority Authority, PolicyCategory Category);
}
