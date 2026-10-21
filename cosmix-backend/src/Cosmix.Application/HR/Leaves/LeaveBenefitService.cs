using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.Leaves;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.HR.Leaves;

public sealed partial class LeaveBenefitService(AttendanceOperation operation, ILeaveRepository leaves,
    ILeaveBenefitRepository repository, LeaveBenefitEvidence evidence, LeaveBenefitKpiReader kpis,
    ILeaveEncashmentRepository ledger, IPrivateFileStore files, IAccountAccessReader access,
    IAccessManagementRepository authorization, TimeProvider clock, INotificationOutbox notifications)
{
    public Task<LeaveBenefitView> AwardAsync(Guid employee, AwardLeaveBenefitRequest request, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        if (employee == Guid.Empty || request is null || request.LeaveTypeId == Guid.Empty || request.PolicyVersionId == Guid.Empty ||
            request.ExpectedKpiFinalizationId == Guid.Empty || request.KpiYearFrom.Year is < 1901 or > 9991)
            throw AttendanceOperation.Invalid("invalid_leave_benefit_request");
        return operation.WriteAsync(LeaveBenefitPermissions.Review, "attendance.leave.benefit.award", employee, key, request,
            request.Reason, context, async (actor, token) =>
            {
                await RequireAdmin(actor, employee, token);
                var proof = await Prepare(employee, request, actor, token);
                if (await repository.SourceExistsAsync(employee, LeaveBenefitEvidence.SourceKey(request.KpiYearFrom), token))
                    throw AttendanceOperation.Conflict("leave_benefit_source_already_awarded");
                var entry = new LeaveLedgerEntry(employee, proof.LeaveTypeId, proof.YearFrom, proof.YearTo,
                    LeaveLedgerEntryType.Entitlement, proof.Minutes, null, null, proof.Policy.VersionId,
                    proof.Reason, LeaveBenefitEvidence.SourceKey(request.KpiYearFrom), actor.Identity.AccountId.ToString(), actor.Now);
                proof = proof with { Id = entry.Id };
                var reference = await Store(proof, employee, entry.Id, token);
                var again = await Prepare(employee, request, actor, token);
                if (LeaveProvenance.Digest(again with { Id = proof.Id }) != LeaveProvenance.Digest(proof))
                    throw AttendanceOperation.Conflict("leave_benefit_source_changed");
                await RequireAdmin(actor, employee, token);
                repository.Add(entry, Audit(LeaveBenefitEvidence.AwardEvent, entry, actor, reference, context));
                await Notify(entry, context, token);
                await RequireAdmin(actor, employee, token);
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<LeaveBenefitView>(entry.Id, null, LeaveBenefitEvidence.View(proof, false));
            }, ct, beforeReplay: (actor, token) => RequireAdmin(actor, employee, token),
            beforeCommit: (actor, token) => RequireAdmin(actor, employee, token));
    }

    public Task<LeaveBenefitView> RevokeAsync(Guid employee, Guid id, RevokeLeaveBenefitRequest request, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        if (employee == Guid.Empty || id == Guid.Empty || request is null || request.ExpectedEvidenceFingerprint is not { Length: 64 })
            throw AttendanceOperation.Invalid("invalid_leave_benefit_revocation");
        return operation.WriteAsync(LeaveBenefitPermissions.Review, "attendance.leave.benefit.revoke", employee, key,
            new { id, request }, request.Reason, context, async (actor, token) =>
            {
                await RequireAdmin(actor, employee, token);
                var row = await repository.FindAsync(employee, id, token) ?? throw AttendanceOperation.Missing("leave_benefit_not_found");
                var proof = await evidence.AwardAsync(row, token);
                if (LeaveBenefitEvidence.Fingerprint(proof) != request.ExpectedEvidenceFingerprint)
                    throw AttendanceOperation.Conflict("leave_benefit_source_changed");
                var usage = await CanRevoke(proof, actor.Now, token);
                var entry = new LeaveLedgerEntry(employee, proof.LeaveTypeId, proof.YearFrom, proof.YearTo,
                    LeaveLedgerEntryType.Adjustment, -proof.Minutes, null, id, proof.Policy.VersionId, request.Reason.Trim(),
                    LeaveBenefitEvidence.RevokeKey(id), actor.Identity.AccountId.ToString(), actor.Now);
                var reversal = new LeaveBenefitRevocationProof(2, entry.Id, id, employee, request.ExpectedEvidenceFingerprint,
                    actor.Identity.AccountId, actor.Identity.EmployeeId, LeaveRoleCodes.Admin, actor.Now, request.Reason.Trim()) { ReleasedUsage = usage };
                var reference = await Store(reversal, employee, entry.Id, token);
                var again = await CanRevoke(proof, clock.GetUtcNow(), token);
                if (LeaveBenefitUsage.Fingerprint(usage) != LeaveBenefitUsage.Fingerprint(again))
                    throw AttendanceOperation.Conflict("leave_benefit_usage_changed");
                await RequireAdmin(actor, employee, token);
                repository.Add(entry, Audit(LeaveBenefitEvidence.RevokeEvent, entry, actor, reference, context));
                await Notify(entry, context, token);
                await RequireAdmin(actor, employee, token);
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<LeaveBenefitView>(entry.Id, LeaveBenefitEvidence.View(proof, false), LeaveBenefitEvidence.View(proof, true));
            }, ct, beforeReplay: (actor, token) => RequireAdmin(actor, employee, token),
            beforeCommit: (actor, token) => RequireAdmin(actor, employee, token));
    }

    public Task<IReadOnlyList<LeaveBenefitView>> ListAsync(Guid? employee, DateOnly from, bool self,
        AuthRequestContext context, CancellationToken ct)
    {
        if (from.Year is < 1901 or > 9997 || !self && (employee is null || employee == Guid.Empty))
            throw AttendanceOperation.Invalid("invalid_leave_benefit_period");
        return operation.ReadAsync<IReadOnlyList<LeaveBenefitView>>(self ? LeavePermissions.Self : LeaveBenefitPermissions.Review,
            "attendance.leave.benefit.list", self ? null : employee, context, async (actor, token) =>
            {
                var subject = self ? actor.Identity.EmployeeId : employee!.Value;
                if (!self) await RequireAdmin(actor, subject, token, allowOwnRead: true);
                var rows = await repository.YearAsync(subject, from, from.AddYears(1), token);
                _ = await evidence.ReadAsync(subject, from, from.AddYears(1), from, token);
                var results = new List<LeaveBenefitView>();
                foreach (var row in rows.Where(x => x.Entry.EntryType == LeaveLedgerEntryType.Entitlement))
                {
                    var proof = await evidence.AwardAsync(row, token);
                    var correction = await evidence.CorrectionAsync(proof, rows, token);
                    if (correction is null)
                        results.Add(LeaveBenefitEvidence.View(proof, await evidence.RevokedAsync(proof, rows, token)));
                    else
                    {
                        var usage = await repository.UsageAsync(subject, proof.YearFrom, proof.YearTo, token);
                        results.Add(LeaveBenefitEvidence.ResolvedView(proof, correction, usage,
                            await evidence.WaiverAsync(proof, correction, usage, token)));
                    }
                }
                if (!self) await RequireAdmin(actor, subject, token, allowOwnRead: true);
                return results;
            }, ct, self: self);
    }

    private async Task<LeaveBenefitAwardProof> Prepare(Guid employee, AwardLeaveBenefitRequest request, AttendanceActor actor, CancellationToken ct)
    {
        var date = LeaveRules.LocalDate(clock.GetUtcNow());
        if (date != LeaveRules.LocalDate(actor.Now)) throw AttendanceOperation.Conflict("leave_benefit_date_changed");
        if (!await leaves.EmployeeExistsAsync(employee, ct)) throw AttendanceOperation.Conflict("leave_employee_inactive");
        if (await leaves.AnyLockedAsync(date, date, ct)) throw AttendanceOperation.Conflict("leave_benefit_period_locked");
        var type = await leaves.TypeAsync(request.LeaveTypeId, ct);
        if (type is null || !type.IsActive || !type.ReducesRegularBalance) throw AttendanceOperation.Conflict("leave_benefit_type_ineligible");
        var source = await leaves.PolicyAsync(employee, type.Id, request.PolicyVersionId, date, date, ct);
        if (source is null || !source.ProvenanceValid || source.Version.Id != request.PolicyVersionId ||
            source.Version.Authority != PolicyAuthority.DirectHr || source.Version.ApprovalRequestId is not null ||
            source.Version.Policy.Category != PolicyCategory.Leave || source.Version.PublishedAt > actor.Now ||
            source.Version.EffectiveFrom > date || source.Version.EffectiveTo <= date)
            throw AttendanceOperation.Conflict("leave_benefit_policy_unavailable");
        var settings = LeavePolicySettings.Parse(source.Version.RulesJson, source.Version.RulesSchemaVersion);
        var rules = settings.Benefits ?? throw AttendanceOperation.Conflict("leave_benefit_policy_not_configured");
        var year = LeavePolicySettings.Year(date, settings);
        if (year.From != LeaveBenefitEvidence.TargetYear(request.KpiYearFrom, settings) || source.JoinedOn is null ||
            !settings.EligibleStaffCategoryIds.Contains(source.StaffCategoryId) || !settings.PaidKinds.Contains(type.Kind) ||
            type.Kind != rules.EligibleKind || !settings.ServiceEligible(source.JoinedOn.Value, date) ||
            !(settings with { MinimumServiceMonths = rules.MinimumServiceMonths }).ServiceEligible(source.JoinedOn.Value, date) ||
            source.AssignmentIds is not { Count: > 0 and <= 1000 } || source.AssignmentIds.Any(x => x == Guid.Empty) ||
            source.AssignmentIds.Distinct().Count() != source.AssignmentIds.Count)
            throw AttendanceOperation.Conflict("leave_benefit_ineligible");
        var sourceTo = request.KpiYearFrom.AddYears(1);
        if (sourceTo > date || date.DayNumber - sourceTo.DayNumber >= rules.AwardWindowDaysAfterSourceEnd)
            throw AttendanceOperation.Conflict("leave_benefit_award_window_closed");
        var score = await kpis.ReadAsync(employee, request.KpiYearFrom, sourceTo, actor.Now, ct);
        if (score.Score.Id != request.ExpectedKpiFinalizationId) throw AttendanceOperation.Conflict("leave_benefit_kpi_version_changed");
        if (score.Score.ExactScore < rules.MinimumKpiPercent) throw AttendanceOperation.Conflict("leave_benefit_kpi_ineligible");
        return new(1, Guid.Empty, employee, type.Id, type.Kind, year.From, year.To, date, rules.AwardMinutes,
            new(source.Version.PolicyId, source.Version.Id, source.Version.RulesSchemaVersion, source.Version.RulesJson,
                source.Version.EffectiveFrom, source.Version.EffectiveTo, source.Version.PublishedAt, source.Version.PublishedByUserId,
                source.StaffCategoryId, source.JoinedOn.Value, source.AssignmentIds), score,
            actor.Identity.AccountId, actor.Identity.EmployeeId, LeaveRoleCodes.Admin, actor.Now, request.Reason.Trim());
    }
    private async Task<IReadOnlyList<LeaveBenefitUsageEntry>> CanRevoke(LeaveBenefitAwardProof proof, DateTimeOffset now, CancellationToken ct)
    {
        var today = LeaveRules.LocalDate(now);
        if (today < proof.AvailableFrom || today >= proof.YearTo || await leaves.AnyLockedAsync(proof.AvailableFrom, today, ct))
            throw AttendanceOperation.Conflict("leave_benefit_period_locked");
        var rows = await repository.YearAsync(proof.EmployeeId, proof.YearFrom, proof.YearTo, ct);
        if (await evidence.RevokedAsync(proof, rows, ct)) throw AttendanceOperation.Conflict("leave_benefit_already_revoked");
        var use = await ledger.LedgerAsync(proof.EmployeeId, proof.YearFrom, proof.YearTo, ct);
        var snapshot = use.Count == 0 ? [] : await repository.UsageAsync(proof.EmployeeId, proof.YearFrom, proof.YearTo, ct);
        if (snapshot.Any(x => x.RecordedAt > now)) throw AttendanceOperation.Conflict("leave_benefit_usage_changed");
        if (LeaveBenefitUsage.Active(proof.EmployeeId, proof.YearFrom, proof.YearTo, snapshot).Count > 0)
            throw AttendanceOperation.Conflict("leave_benefit_consumed_adjustment_review_required");
        return snapshot;
    }
    private async Task RequireAdmin(AttendanceActor actor, Guid employee, CancellationToken ct, bool allowOwnRead = false)
    {
        var now = clock.GetUtcNow();
        if (actor.Identity.EmployeeId == Guid.Empty || !allowOwnRead && actor.Identity.EmployeeId == employee ||
            !await authorization.IsCurrentAdministratorAsync(actor.Identity, now, ct)) throw AttendanceOperation.Denied();
        var grants = await access.GetGrantsAsync(actor.Identity.AccountId, now, ct);
        if (!AccessPermissionEvaluator.HasAllScope(actor.Identity, grants, LeaveBenefitPermissions.Review))
            throw AttendanceOperation.Denied();
        // Permission may be delegated through another role. Fixed Admin membership is
        // independently verified even when that assignment grants no permissions.
        for (var page = 1; ; page++)
        {
            var assignments = await authorization.ListAssignmentsAsync(actor.Identity.AccountId, new(page, 100), ct);
            foreach (var assignment in assignments.Items)
                if (assignment.RoleCode == LeaveRoleCodes.Admin &&
                    await authorization.IsCurrentSystemRoleAssignmentAsync(actor.Identity.AccountId, assignment.Id, now, ct) &&
                    await authorization.GetAssignmentRoleCodeAsync(assignment.Id, ct) == LeaveRoleCodes.Admin)
                {
                    var checkedAt = clock.GetUtcNow();
                    if (assignment.EffectiveFrom <= checkedAt &&
                        (assignment.EffectiveTo is null || checkedAt < assignment.EffectiveTo) &&
                        (assignment.RevokedAt is null || checkedAt < assignment.RevokedAt)) return;
                }
            if (assignments.Items.Count == 0 || (long)page * 100 >= assignments.TotalCount) break;
        }
        throw AttendanceOperation.Denied();
    }
    private async Task<string> Store<T>(T proof, Guid employee, Guid id, CancellationToken ct) =>
        AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(proof), LeaveBenefitEvidence.Purpose(employee, id), ct));
    private async Task Notify(LeaveLedgerEntry entry, AuthRequestContext context, CancellationToken ct)
    {
        var recipient = await repository.RecipientAsync(entry.EmployeeId, ct);
        if (recipient.HasValue) await notifications.StageAsync(new(entry.Id, recipient.Value, NotificationTemplates.Decision, entry.Id), context, ct);
    }
    private static AuditEvent Audit(string name, LeaveLedgerEntry entry, AttendanceActor actor, string reference, AuthRequestContext context) =>
        new(name, actor.Identity.AccountId.ToString(), LeaveRoleCodes.Admin, LeaveBenefitEvidence.TargetType, entry.Id.ToString(), actor.Now,
            context.CorrelationId, employeeScopeId: entry.EmployeeId, protectedChangesReference: reference, ipAddress: context.IpAddress);
}
