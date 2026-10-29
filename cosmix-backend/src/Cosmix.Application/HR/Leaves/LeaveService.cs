using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Leaves;

namespace Cosmix.Application.HR.Leaves;

public sealed partial class LeaveService(AttendanceOperation operation, ILeaveRepository repository, LeaveEvidence evidence,
    LeaveProvenance provenance, LeaveReviewAuthority authority, IIndividualRosterResolver rosters, IPrivateFileStore files)
{
    public Task<LeaveResponse> SubmitAsync(SubmitLeaveRequest input, string key, AuthRequestContext context, CancellationToken ct)
    {
        // Validate collection shape before copying or hashing caller-owned attachment arrays.
        LeaveRules.Allocate(Guid.NewGuid(), input);
        if (input.Attachments is { Count: > 4 } || input.Attachments?.Any(x => x is null || x.Content is null) == true)
            throw AttendanceOperation.Invalid("invalid_leave_evidence");
        var request = input with { Days = input.Days.ToArray(), Attachments = input.Attachments?.Select(x => x with { Content = x.Content.ToArray() }).ToArray() };
        var payload = new { request.LeaveTypeId, request.Days, request.Reason, request.ContactDuringLeave,
            request.RelieverEmployeeId, request.HandoverNotes, request.PreviousRequestId,
            attachments = request.Attachments?.Select(x => new { x.Kind, x.FileName, x.ContentType, size = x.Content.Length,
                hash = Convert.ToHexString(SHA256.HashData(x.Content)) }) };
        return operation.WriteAsync(LeavePermissions.Self, "attendance.leave.submit", null, key, payload, request.Reason, context,
            async (actor, token) =>
            {
                var employeeId = actor.Identity.EmployeeId;
                if (await repository.SubmissionExistsAsync(employeeId, key, token)) throw AttendanceOperation.Conflict("leave_already_received");
                var type = await repository.TypeAsync(request.LeaveTypeId, token) ?? throw AttendanceOperation.Missing("leave_type_not_found");
                if (!type.IsActive) throw AttendanceOperation.Conflict("leave_type_inactive");
                if (!await repository.EmployeeExistsAsync(employeeId, token)) throw AttendanceOperation.Denied();
                if (request.RelieverEmployeeId.HasValue && (request.RelieverEmployeeId == employeeId ||
                    !await repository.EmployeeExistsAsync(request.RelieverEmployeeId.Value, token))) throw AttendanceOperation.Invalid("invalid_leave_reliever");
                var attachments = request.Attachments ?? [];
                LeaveRules.Evidence(type, attachments);
                var initial = LeaveRules.Allocate(Guid.NewGuid(), request);
                if (type.RequiresAdvanceApproval && initial[0].StartsAtUtc <= actor.Now) throw AttendanceOperation.Invalid("advance_leave_required");
                if (request.PreviousRequestId.HasValue)
                {
                    var previous = await Find(request.PreviousRequestId.Value, employeeId, token);
                    await provenance.VerifyAsync(previous, token);
                    if (previous.Request.RequestedByUserId != actor.Identity.AccountId.ToString() || previous.Approval!.State != ApprovalState.Returned || previous.SupersededByRequestId.HasValue)
                        throw AttendanceOperation.Conflict("leave_not_awaiting_resubmission");
                }
                await Unlocked(initial[0].Date, initial[^1].Date, token);
                await NoOverlap(employeeId, initial, request.PreviousRequestId, token);
                var scheduleIds = new Dictionary<DateOnly, Guid>();
                var officeSources = new List<OfficeLeaveScheduleSource>();
                foreach (var day in initial)
                {
                    var roster = await rosters.ResolveAsync(employeeId, day.Date, token);
                    ValidateSchedule(day, roster);
                    if (OfficeLeaveScheduleSource.Capture(day, roster) is { } source) officeSources.Add(source);
                    scheduleIds.Add(day.Date, roster.ShiftVersionId!.Value);
                }
                // Allocate the entity identity before private evidence; attachment reference is bound to this identity.
                var leave = new LeaveRequest(employeeId, type.Id, initial[0].Date, initial[^1].Date, initial.Sum(x => x.Minutes),
                    request.Reason, request.ContactDuringLeave, request.RelieverEmployeeId, request.HandoverNotes, null,
                    LeavePayTreatment.PendingReview, null, actor.Identity.AccountId.ToString(), actor.Now, key, request.PreviousRequestId);
                var evidenceReference = await evidence.StoreAsync(leave.Id, employeeId, attachments, token);
                leave.AttachEvidence(evidenceReference);
                var days = initial.Select(x => new LeaveRequestDay(leave.Id, x.Date, x.StartsAtUtc, x.EndsAtUtc, scheduleIds[x.Date])).ToArray();
                foreach (var day in days) leave.Days.Add(day);
                var approval = new ApprovalRequest(LeaveRules.RequestType, leave.Id, leave.RequestedByUserId,
                    await provenance.FreezeAsync(leave, token, officeSources), 1, actor.Now);
                leave.Submit(approval); repository.Add(leave, days, approval); await repository.SaveChangesAsync(token);
                return new AttendanceChange<LeaveResponse>(leave.Id, null, LeaveRules.Response(new(leave, type, approval, [], [], null)));
            }, ct, self: true);
    }

    public Task<PagedResult<LeaveResponse>> MineAsync(LeaveListRequest request, AuthRequestContext context, CancellationToken ct) => List(request, true, context, ct);
    public Task<PagedResult<LeaveResponse>> QueueAsync(LeaveListRequest request, AuthRequestContext context, CancellationToken ct) => List(request, false, context, ct);
    private Task<PagedResult<LeaveResponse>> List(LeaveListRequest request, bool self, AuthRequestContext context, CancellationToken ct)
    {
        LeaveRules.List(request);
        return operation.ReadAsync(self ? LeavePermissions.Self : LeavePermissions.Review, self ? "attendance.leave.mine" : "attendance.leave.queue", null,
            context, async (actor, token) => { var page = await repository.ListAsync(self ? actor.Identity.EmployeeId : null, request, token);
                return new PagedResult<LeaveResponse>(page.Items.Select(LeaveRules.Response).ToArray(), page.TotalCount, page.Page, page.PageSize); }, ct,
            self: self, resources: self ? null : authority.ScopeAsync, concurrentRead: !self);
    }
    public Task<LeaveResponse> DetailAsync(Guid id, bool self, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(self ? LeavePermissions.Self : LeavePermissions.Review, "attendance.leave.detail", null, context,
            async (actor, token) => LeaveRules.Response(await Find(id, self ? actor.Identity.EmployeeId : null, token)), ct,
            self: self, resources: self ? null : authority.ScopeAsync);
    public async Task<LeaveEvidenceDownload> EvidenceAsync(Guid id, LeaveEvidenceKind kind, bool self, AuthRequestContext context, CancellationToken ct)
    {
        Func<CancellationToken, Task>? authorizeMedical = null;
        var result = await operation.ReadAsync(self ? LeavePermissions.Self : LeavePermissions.Review, "attendance.leave.evidence", null, context,
            async (actor, token) =>
            {
                var row = await Find(id, self ? actor.Identity.EmployeeId : null, token);
                if (!self && kind is LeaveEvidenceKind.Medical or LeaveEvidenceKind.InjuryMedical)
                    authorizeMedical = cancellation => authority.RequireMedicalReadAsync(actor, row.Request.EmployeeId, cancellation);
                return await evidence.DownloadAsync(row.Request, kind, token, authorizeMedical);
            }, ct,
            self: self, resources: self ? null : authority.ScopeAsync);
        // Audit persistence is also IO; do not disclose bytes after medical authority has ended.
        if (authorizeMedical is not null) await authorizeMedical(ct);
        return result;
    }
    public Task<IReadOnlyList<LeaveTypeResponse>> TypesAsync(bool self, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(self ? LeavePermissions.Self : LeavePermissions.Configure, "attendance.leave.types", null, context,
            async (_, token) => { var types = await repository.TypesAsync(token); if (types.Count > 500) throw AttendanceOperation.Conflict("leave_type_limit");
                return (IReadOnlyList<LeaveTypeResponse>)types.Select(LeaveRules.Type).ToArray(); }, ct, self: self, requireSystemRole: !self);

    public Task<IReadOnlyList<LeavePolicyResponse>> ReviewPoliciesAsync(Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(LeavePermissions.Review, "attendance.leave.review-policies", null, context, async (_, token) =>
        {
            var row = await Find(id, null, token);
            await provenance.VerifyAsync(row, token);
            var versions = await repository.PolicyVersionsAsync(token);
            if (versions.Count > 500) throw AttendanceOperation.Conflict("leave_policy_history_limit");
            var candidates = versions.Where(x => x.Authority == Cosmix.Domain.HR.Policies.PolicyAuthority.DirectHr && x.ApprovalRequestId is null &&
                LeavePolicySettings.SupportsSchema(x.RulesSchemaVersion) && x.EffectiveFrom <= row.Request.StartDate &&
                (x.EffectiveTo is null || row.Request.EndDate < x.EffectiveTo)).ToArray();
            // Bound applicability checks as well as the catalogue query. No automatic winner is selected.
            if (candidates.Length > 50) throw AttendanceOperation.Conflict("leave_policy_selection_limit");
            var result = new List<LeavePolicyResponse>();
            var transitionUnavailable = false;
            foreach (var candidate in candidates)
            {
                try
                {
                    var policy = await repository.PolicyAsync(row.Request.EmployeeId, row.Type.Id, candidate.Id,
                        row.Request.StartDate, row.Request.EndDate, token);
                    if (policy is null || !policy.ProvenanceValid) continue;
                    var settings = LeavePolicySettings.Parse(policy.Version.RulesJson, policy.Version.RulesSchemaVersion);
                    if (!settings.EligibleStaffCategoryIds.Contains(policy.StaffCategoryId)) continue;
                    result.Add(new(candidate.PolicyId, candidate.Id, candidate.Policy.Code, candidate.Policy.Name,
                        candidate.EffectiveFrom, candidate.EffectiveTo, settings, candidate.Authority.ToString()));
                }
                catch (AuthenticationFailure failure) when (failure.StatusCode == 409 && failure.Code == "leave_policy_year_transition_unavailable")
                { transitionUnavailable = true; }
            }
            if (result.Count == 0 && transitionUnavailable) throw AttendanceOperation.Conflict("leave_policy_year_transition_unavailable");
            return (IReadOnlyList<LeavePolicyResponse>)result;
        }, ct, resources: authority.ScopeAsync);

    public Task<LeaveResponse> DecideAsync(Guid id, DecideLeaveRequest request, string key, AuthRequestContext context, CancellationToken ct)
    {
        if (request is null || request.Decision is not (ApprovalState.Approved or ApprovalState.Returned or ApprovalState.Rejected) ||
            !Enum.IsDefined(request.Treatment) || request.PolicyVersionId == Guid.Empty ||
            request.Decision != ApprovalState.Approved && (request.Treatment != LeavePayTreatment.PendingReview || request.PolicyVersionId.HasValue) ||
            (request.Treatment == LeavePayTreatment.PendingReview) == request.PolicyVersionId.HasValue)
            throw AttendanceOperation.Invalid("invalid_leave_decision");
        if (request.Decision == ApprovalState.Approved && request.Treatment == LeavePayTreatment.PendingReview)
            throw AttendanceOperation.Conflict("leave_treatment_requires_explicit_policy");
        return operation.WriteAsync(LeavePermissions.Review, "attendance.leave.decide", null, key, new { id, request }, request.Reason, context,
            async (actor, token) =>
            {
                var role = await authority.RoleAsync(actor, token);
                var row = await Find(id, null, token); await provenance.VerifyAsync(row, token);
                var approval = row.Approval!; NoSelf(row, actor);
                repository.ExpectVersion(approval, LeaveRules.Version(approval, request.ExpectedApprovalVersion));
                if (approval.State != ApprovalState.Pending || row.SupersededByRequestId.HasValue) throw AttendanceOperation.Conflict("leave_not_pending");
                var before = LeaveRules.Response(row);
                LeavePolicyEvidence? policy = null;
                if (request.Decision == ApprovalState.Approved)
                {
                    if (!await repository.EmployeeExistsAsync(row.Request.EmployeeId, token)) throw AttendanceOperation.Conflict("leave_employee_inactive");
                    await Unlocked(row.Request.StartDate, row.Request.EndDate, token);
                    if (row.Type.RequiresAdvanceApproval && row.Request.Days.Min(x => x.StartsAtUtc) <= actor.Now)
                        throw AttendanceOperation.Conflict("advance_leave_approval_elapsed");
                    await NoOverlap(row.Request.EmployeeId, row.Request.Days.ToArray(), id, token);
                    var officeSources = await provenance.OfficeSourcesAsync(row, token);
                    foreach (var day in row.Request.Days)
                    {
                        var roster = await rosters.ResolveAsync(row.Request.EmployeeId, day.Date, token);
                        ValidateSchedule(day, roster);
                        OfficeLeaveScheduleSource.RequireMatching(day, roster, officeSources);
                    }
                    if (row.Request.EvidencePrivateReference is not null) await evidence.ReadAsync(row.Request, token);
                    if (request.PolicyVersionId.HasValue)
                    {
                        policy = await repository.PolicyAsync(row.Request.EmployeeId, row.Type.Id, request.PolicyVersionId.Value, row.Request.StartDate, row.Request.EndDate, token)
                            ?? throw AttendanceOperation.Conflict("leave_policy_unavailable");
                        LeaveTreatmentRules.Validate(row, request.Treatment, policy);
                    }
                }
                var changeReference = AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new LeaveDecisionProof(
                    id, request.Decision, request.Treatment, request.PolicyVersionId, LeaveProvenance.SubmissionDigest(row.Request),
                    policy?.Version.RulesJson, policy?.StaffCategoryId, policy?.JoinedOn, policy?.Holidays, policy?.Usage)), DecisionPurpose(id), token));
                var decision = approval.Decide(request.Decision, actor.Identity.AccountId.ToString(), role, request.Reason, actor.Now, changeReference);
                repository.Add(decision);
                if (request.Decision == ApprovalState.Approved)
                {
                    row.Request.SetApprovedTreatment(request.Treatment, request.PolicyVersionId);
                    if (request.Treatment == LeavePayTreatment.Paid && row.Type.ReducesRegularBalance)
                    {
                        var year = LeavePolicySettings.Year(row.Request.StartDate,
                            LeavePolicySettings.Parse(policy!.Version.RulesJson, policy.Version.RulesSchemaVersion));
                        repository.Add(new LeaveLedgerEntry(row.Request.EmployeeId, row.Type.Id, year.From, year.To,
                            LeaveLedgerEntryType.Utilization, -row.Request.RequestedMinutes, id, null, request.PolicyVersionId!.Value,
                            request.Reason, $"leave-use:{id:N}", actor.Identity.AccountId.ToString(), actor.Now));
                    }
                }
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<LeaveResponse>(id, before, LeaveRules.Response(row with { Decisions = [decision] }));
            }, ct, resources: authority.ScopeAsync);
    }

    public Task<LeaveResponse> RequestCancellationAsync(Guid id, RequestLeaveCancellation request, string key, AuthRequestContext context, CancellationToken ct)
    {
        if (request is null) throw AttendanceOperation.Invalid("invalid_leave_cancellation_request");
        return operation.WriteAsync(LeavePermissions.Self, "attendance.leave.cancellation.request", null, key, new { id, request }, request.Reason, context,
            async (actor, token) =>
            {
                var row = await Find(id, actor.Identity.EmployeeId, token); await provenance.VerifyAsync(row, token);
                if (row.Request.RequestedByUserId != actor.Identity.AccountId.ToString()) throw AttendanceOperation.Denied();
                LeaveRules.Version(row.Approval!, request.ExpectedApprovalVersion);
                if (row.Approval!.State != ApprovalState.Approved || LeaveRules.Cancelled(row) || row.Cancellations.Any(x => x.Approval?.State == ApprovalState.Pending))
                    throw AttendanceOperation.Conflict("leave_cancellation_unavailable");
                if (row.Cancellations.Count >= 100) throw AttendanceOperation.Conflict("leave_cancellation_history_limit");
                await Unlocked(row.Request.StartDate, row.Request.EndDate, token);
                var cancellation = new LeaveCancellationRequest(id, actor.Identity.AccountId.ToString(), request.Reason, actor.Now);
                var reference = AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new CancellationSnapshot(cancellation.Id,
                    id, cancellation.RequestedByUserId, cancellation.Reason, row.Approval.Id)), CancellationPurpose(cancellation.Id), token));
                var approval = new ApprovalRequest(LeaveRules.CancellationType, cancellation.Id, cancellation.RequestedByUserId, reference, 1, actor.Now);
                cancellation.Submit(approval); repository.Add(cancellation, approval); await repository.SaveChangesAsync(token);
                return new AttendanceChange<LeaveResponse>(cancellation.Id, LeaveRules.Response(row), LeaveRules.Response(row with
                    { Cancellations = row.Cancellations.Append(new(cancellation, approval, [])).ToArray() }));
            }, ct, self: true);
    }

    public Task<LeaveResponse> DecideCancellationAsync(Guid id, Guid cancellationId, DecideLeaveCancellation request, string key, AuthRequestContext context, CancellationToken ct)
    {
        if (request is null || request.Decision is not (ApprovalState.Approved or ApprovalState.Returned or ApprovalState.Rejected))
            throw AttendanceOperation.Invalid("invalid_leave_cancellation_decision");
        return operation.WriteAsync(LeavePermissions.Review, "attendance.leave.cancellation.decide", null, key, new { id, cancellationId, request }, request.Reason, context,
            async (actor, token) =>
            {
                var role = await authority.RoleAsync(actor, token); var row = await Find(id, null, token); await provenance.VerifyAsync(row, token); NoSelf(row, actor);
                var cancellation = row.Cancellations.SingleOrDefault(x => x.Request.Id == cancellationId) ?? throw AttendanceOperation.Missing();
                if (!LeaveRules.ApprovalValid(cancellation.Approval, cancellation.Decisions, cancellationId, cancellation.Request.RequestedByUserId, LeaveRules.CancellationType) ||
                    cancellation.Approval!.State != ApprovalState.Pending || row.Approval!.State != ApprovalState.Approved || LeaveRules.Cancelled(row))
                    throw AttendanceOperation.Conflict("leave_cancellation_unavailable");
                var frozen = await operation.ReadPrivateAsync<CancellationSnapshot>(cancellation.Approval.SnapshotPrivateReference, CancellationPurpose(cancellationId), token);
                if (frozen != new CancellationSnapshot(cancellationId, id, cancellation.Request.RequestedByUserId, cancellation.Request.Reason, row.Approval.Id))
                    throw AttendanceOperation.Conflict("leave_cancellation_snapshot_mismatch");
                repository.ExpectVersion(cancellation.Approval, LeaveRules.Version(cancellation.Approval, request.ExpectedApprovalVersion));
                var before = LeaveRules.Response(row);
                if (request.Decision == ApprovalState.Approved)
                {
                    await Unlocked(row.Request.StartDate, row.Request.EndDate, token);
                    var ledger = await repository.LedgerAsync(id, token);
                    var requiresUtilization = row.Request.PayTreatment == LeavePayTreatment.Paid && row.Type.ReducesRegularBalance;
                    if (ledger.Count != (requiresUtilization ? 1 : 0) || ledger.Any(x => x.EntryType != LeaveLedgerEntryType.Utilization ||
                        x.MinutesDelta != -row.Request.RequestedMinutes || x.EmployeeId != row.Request.EmployeeId || x.LeaveTypeId != row.Type.Id || x.PolicyVersionId != row.Request.PolicyVersionId))
                        throw AttendanceOperation.Conflict("leave_ledger_conflict");
                    foreach (var entry in ledger)
                        repository.Add(new LeaveLedgerEntry(entry.EmployeeId, entry.LeaveTypeId, entry.LeaveYearStart, entry.LeaveYearEnd,
                            LeaveLedgerEntryType.Release, -entry.MinutesDelta, id, entry.Id, entry.PolicyVersionId, request.Reason,
                            $"leave-release:{entry.Id:N}", actor.Identity.AccountId.ToString(), actor.Now));
                }
                var decision = cancellation.Approval.Decide(request.Decision, actor.Identity.AccountId.ToString(), role, request.Reason, actor.Now);
                repository.Add(decision); await repository.SaveChangesAsync(token);
                return new AttendanceChange<LeaveResponse>(cancellationId, before, LeaveRules.Response(row with { Cancellations = row.Cancellations
                    .Select(x => x.Request.Id == cancellationId ? x with { Decisions = [decision] } : x).ToArray() }));
            }, ct, resources: authority.ScopeAsync);
    }

    private async Task<LeaveRow> Find(Guid id, Guid? employeeId, CancellationToken ct) =>
        await repository.FindAsync(id, employeeId, ct) ?? throw AttendanceOperation.Missing("leave_not_found");
    private async Task Unlocked(DateOnly from, DateOnly to, CancellationToken ct)
    { if (await repository.AnyLockedAsync(from, to, ct)) throw AttendanceOperation.Conflict("leave_period_locked"); }
    private static void NoSelf(LeaveRow row, AttendanceActor actor)
    { if (row.Request.EmployeeId == actor.Identity.EmployeeId || row.Request.RequestedByUserId == actor.Identity.AccountId.ToString()) throw AttendanceOperation.Denied(); }
    private async Task NoOverlap(Guid employeeId, IReadOnlyList<LeaveRequestDay> days, Guid? except, CancellationToken ct)
    {
        var rows = await repository.OverlappingAsync(employeeId, days.Min(x => x.Date), days.Max(x => x.Date), ct);
        foreach (var row in rows.Where(x => x.Request.Id != except && x.SupersededByRequestId is null))
        {
            if (!LeaveRules.ApprovalValid(row.Approval, row.Decisions, row.Request.Id, row.Request.RequestedByUserId, LeaveRules.RequestType))
                throw AttendanceOperation.Conflict("leave_provenance_unavailable");
            if (row.Approval!.State is not (ApprovalState.Pending or ApprovalState.Approved or ApprovalState.Returned) || LeaveRules.Cancelled(row)) continue;
            if (row.Request.Days.Any(d => days.Any(x => x.StartsAtUtc < d.EndsAtUtc && d.StartsAtUtc < x.EndsAtUtc)))
                throw AttendanceOperation.Conflict("leave_interval_overlap");
        }
    }
    public static void ValidateSchedule(LeaveRequestDay day, EffectiveRosterResponse roster)
    {
        if (roster.Status != "scheduled" || !roster.ShiftVersionId.HasValue || roster.StartsAt is null || roster.EndsAt is null ||
            roster.BusinessDate != day.Date || day.StartsAtUtc < roster.StartsAt || day.EndsAtUtc > roster.EndsAt ||
            day.ShiftVersionId.HasValue && day.ShiftVersionId != roster.ShiftVersionId)
            throw AttendanceOperation.Conflict("leave_schedule_unavailable_or_changed");
    }
    public static string DecisionPurpose(Guid id) => $"hr:leave:{id:N}:decision";
    public static string CancellationPurpose(Guid id) => $"hr:leave:{id:N}:cancellation";
    public sealed record CancellationSnapshot(Guid CancellationId, Guid LeaveRequestId, string Requester, string Reason, Guid LeaveApprovalId);
}
public sealed record LeaveDecisionProof(Guid RequestId, ApprovalState Decision, LeavePayTreatment Treatment, Guid? PolicyVersionId,
    string SubmissionDigest, string? PolicyRulesJson, Guid? StaffCategoryId, DateOnly? JoinedOn, IReadOnlyList<DateOnly>? Holidays, LeaveBalanceUsage? Usage);
