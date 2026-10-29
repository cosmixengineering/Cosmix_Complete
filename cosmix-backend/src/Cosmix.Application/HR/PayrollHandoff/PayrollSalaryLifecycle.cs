using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.PayrollSalary;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.PayrollHandoff;

namespace Cosmix.Application.HR.PayrollHandoff;

public sealed partial class PayrollHandoffService
{
    public const string HrReviewRequestType = "payroll-handoff.hr-review";
    public const string ReturnRequestType = "payroll-handoff.return-for-correction";

    public Task<PayrollHandoffBatchDetail> ReturnForCorrectionAsync(Guid id, LockPayrollHandoffRequest request,
        string key, AuthRequestContext context, CancellationToken ct)
    {
        if (id == Guid.Empty || request is null) throw AttendanceOperation.Invalid("invalid_payroll_handoff_request");
        var expected = SchedulingOperation.Version(request.ExpectedVersion);
        return operation.WriteAsync(PayrollHandoffPermissions.HrReview, "attendance.payroll-handoff.return-for-correction",
            null, key, new { id, request }, request.Reason, context, async (actor, token) =>
            {
                var batch = await repository.FindBatchAsync(id, true, token)
                    ?? throw AttendanceOperation.Missing("payroll_handoff_batch_not_found");
                repository.ExpectVersion(batch, expected);
                if (batch.CalculationVersion != "salary-v2" || batch.ReviewState != "hr_approved" || batch.LockedAt.HasValue)
                    throw AttendanceOperation.Conflict("payroll_handoff_return_state_invalid");
                RequireSeparateActor(batch.PreparedByUserId, actor, "payroll_handoff_self_review_forbidden");
                await PayrollFrozenBatchReader.ValidateAsync(batch, repository, files, false, token);
                await PayrollFrozenBatchReader.ValidateHrApprovalAsync(batch, repository, files, token);
                if ((await repository.ProcessingAsync(id, token)).Count != 0)
                    throw AttendanceOperation.Conflict("payroll_handoff_return_state_invalid");
                var before = Detail(batch, []);
                var proof = new { BatchId = id, Fingerprint = batch.FrozenFingerprint, OriginalHrApprovalId = batch.HrApprovalRequestId,
                    Actor = actor.Identity.AccountId, At = actor.Now, Reason = request.Reason.Trim() };
                var reference = AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(proof),
                    $"hr:payroll-handoff:{id:N}:return", token));
                var approval = new ApprovalRequest(ReturnRequestType, id, batch.PreparedByUserId, reference, 1, actor.Now);
                var decision = approval.Decide(ApprovalState.Returned, actor.Identity.AccountId.ToString(), actor.RoleCode,
                    request.Reason.Trim(), actor.Now);
                batch.ReturnForCorrection(approval.Id, actor.Identity.AccountId.ToString(), actor.Now);
                repository.Add(approval, decision);
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<PayrollHandoffBatchDetail>(id, before, Detail(batch, []));
            }, ct);
    }

    public Task<PayrollHandoffBatchDetail> HrReviewAsync(Guid id, PayrollSalaryReviewRequest request, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        if (id == Guid.Empty || request is null || request.Decision is not (ApprovalState.Approved or ApprovalState.Rejected))
            throw AttendanceOperation.Invalid("invalid_payroll_handoff_review");
        var expected = SchedulingOperation.Version(request.ExpectedVersion);
        return operation.WriteAsync(PayrollHandoffPermissions.HrReview, "attendance.payroll-handoff.hr-review", null,
            key, new { id, request }, request.Reason, context, async (actor, token) =>
            {
                var batch = await repository.FindBatchAsync(id, true, token)
                    ?? throw AttendanceOperation.Missing("payroll_handoff_batch_not_found");
                repository.ExpectVersion(batch, expected);
                if (batch.CalculationVersion != "salary-v2" || batch.ReviewState != "prepared" || batch.LockedAt.HasValue)
                    throw AttendanceOperation.Conflict("payroll_handoff_review_state_invalid");
                RequireSeparateActor(batch.PreparedByUserId, actor, "payroll_handoff_self_review_forbidden");
                RequireNoSelfApproval(batch, actor);
                await PayrollFrozenBatchReader.ValidateAsync(batch, repository, files, false, token);
                if (request.Decision == ApprovalState.Approved)
                {
                    await ValidateFrozenLoanBalancesAsync(batch, token);
                    await ValidateFrozenFinancialAdjustmentsAsync(batch, actor, token);
                }
                var before = Detail(batch, []);
                var proof = new { BatchId = batch.Id, Fingerprint = batch.FrozenFingerprint, Decision = request.Decision,
                    Actor = actor.Identity.AccountId, At = actor.Now, Reason = request.Reason.Trim() };
                var reference = AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(proof),
                    $"hr:payroll-handoff:{batch.Id:N}:hr-review", token));
                var approval = new ApprovalRequest(HrReviewRequestType, batch.Id, batch.PreparedByUserId, reference, 1, actor.Now);
                var decision = approval.Decide(request.Decision, actor.Identity.AccountId.ToString(), actor.RoleCode,
                    request.Reason.Trim(), actor.Now);
                batch.ReviewSalary(approval.Id, actor.Identity.AccountId.ToString(), actor.Now, request.Decision == ApprovalState.Approved);
                repository.Add(approval, decision);
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<PayrollHandoffBatchDetail>(id, before, Detail(batch, []));
            }, ct);
    }

    public Task<PayrollHandoffBatchDetail> CancelAsync(Guid id, LockPayrollHandoffRequest request, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        if (id == Guid.Empty || request is null) throw AttendanceOperation.Invalid("invalid_payroll_handoff_request");
        var expected = SchedulingOperation.Version(request.ExpectedVersion);
        return operation.WriteAsync(PayrollHandoffPermissions.Prepare, "attendance.payroll-handoff.cancel", null,
            key, new { id, request }, request.Reason, context, async (actor, token) =>
            {
                var batch = await repository.FindBatchAsync(id, true, token)
                    ?? throw AttendanceOperation.Missing("payroll_handoff_batch_not_found");
                repository.ExpectVersion(batch, expected);
                if (batch.CalculationVersion != "salary-v2" || batch.ReviewState != "prepared" || batch.LockedAt.HasValue)
                    throw AttendanceOperation.Conflict("payroll_handoff_cancel_state_invalid");
                if (batch.PreparedByUserId != actor.Identity.AccountId.ToString()) throw AttendanceOperation.Denied();
                await PayrollFrozenBatchReader.ValidateAsync(batch, repository, files, false, token);
                var before = Detail(batch, []);
                batch.CancelSalary(actor.Identity.AccountId.ToString());
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<PayrollHandoffBatchDetail>(id, before, Detail(batch, []));
            }, ct);
    }

    private static void RequireNoSelfApproval(PayrollHandoffBatch batch, AttendanceActor actor)
    {
        if (batch.Lines.Any(x => x.EmployeeId == actor.Identity.EmployeeId))
            throw AttendanceOperation.Denied();
    }
}
