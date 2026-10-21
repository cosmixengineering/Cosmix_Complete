using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.AdvancesLoans;

namespace Cosmix.Application.HR.AdvancesLoans;

public sealed class AdvanceLoanService(
    AttendanceOperation operation,
    IAdvanceLoanRepository repository)
{
    public const string RequestType = "hr.advanceloan";

    public Task<AdvanceLoanRequestResponse> SubmitSelfAsync(
        AdvanceLoanSubmitRequest request,
        string key,
        AuthRequestContext context,
        CancellationToken ct) =>
        operation.WriteAsync(
            AdvanceLoanPermissions.Self,
            "hr.advancesloans.submit.self",
            null,
            key,
            request,
            request.Reason,
            context,
            async (actor, token) =>
            {
                var employeeId = actor.Identity.EmployeeId;
                return await CreateRequestInternalAsync(employeeId, request, actor, token);
            },
            ct,
            self: true);

    public Task<AdvanceLoanRequestResponse> SubmitForEmployeeAsync(
        Guid employeeId,
        AdvanceLoanSubmitRequest request,
        string key,
        AuthRequestContext context,
        CancellationToken ct) =>
        operation.WriteAsync(
            AdvanceLoanPermissions.Request,
            "hr.advancesloans.submit.employee",
            employeeId,
            key,
            request,
            request.Reason,
            context,
            async (actor, token) => await CreateRequestInternalAsync(employeeId, request, actor, token),
            ct,
            self: false);

    private async Task<AttendanceChange<AdvanceLoanRequestResponse>> CreateRequestInternalAsync(
        Guid employeeId,
        AdvanceLoanSubmitRequest request,
        AttendanceActor actor,
        CancellationToken token)
    {
        if (request.RequestedAmount <= 0)
            throw AttendanceOperation.Invalid("invalid_requested_amount");
        if (string.IsNullOrWhiteSpace(request.Currency) || request.Currency.ToUpperInvariant() != "PKR")
            throw AttendanceOperation.Invalid("invalid_currency_pkr_required");
        if (await repository.HasPendingRequestAsync(employeeId, request.Kind, token))
            throw AttendanceOperation.Conflict("pending_request_already_exists");

        var loanRequest = new AdvanceLoanRequest(
            employeeId,
            request.Kind,
            request.RequestedAmount,
            request.Currency.ToUpperInvariant(),
            request.Reason,
            request.RequestedOn,
            actor.Identity.AccountId.ToString(),
            actor.Now);

        var approval = new ApprovalRequest(
            RequestType,
            loanRequest.Id,
            actor.Identity.AccountId.ToString(),
            $"hr:advancesloans:{employeeId:N}:{loanRequest.Id:N}",
            1,
            actor.Now);

        loanRequest.Submit(approval);
        repository.AddRequest(loanRequest, approval);
        await repository.SaveChangesAsync(token);

        var response = new AdvanceLoanRequestResponse(
            loanRequest.Id,
            employeeId,
            loanRequest.Employee?.FullLegalName ?? string.Empty,
            loanRequest.Employee?.EmployeeNumber.ToString() ?? string.Empty,
            loanRequest.Kind,
            loanRequest.RequestedAmount,
            loanRequest.Currency,
            loanRequest.Reason,
            loanRequest.RequestedOn,
            loanRequest.CreatedAt,
            approval.State.ToString(),
            null,
            approval.Id);

        return new AttendanceChange<AdvanceLoanRequestResponse>(loanRequest.Id, null, response);
    }

    public Task<AdvanceLoanRequestResponse> CancelAsync(
        Guid requestId,
        AdvanceLoanCancelRequest request,
        string key,
        AuthRequestContext context,
        CancellationToken ct) =>
        operation.WriteAsync(
            AdvanceLoanPermissions.Self,
            "hr.advancesloans.cancel",
            null,
            key,
            new { requestId, request },
            request.Reason,
            context,
            async (actor, token) =>
            {
                var loanRequest = await repository.FindRequestAsync(requestId, token)
                    ?? throw AttendanceOperation.Missing("request_not_found");

                if (loanRequest.RequestedByUserId != actor.Identity.AccountId.ToString())
                    throw AttendanceOperation.Denied();

                if (loanRequest.ApprovalRequest is null || loanRequest.ApprovalRequest.State != ApprovalState.Pending)
                    throw AttendanceOperation.Conflict("request_not_pending");

                loanRequest.ApprovalRequest.Cancel(actor.Identity.AccountId.ToString(), actor.Now);
                await repository.SaveChangesAsync(token);

                var response = new AdvanceLoanRequestResponse(
                    loanRequest.Id,
                    loanRequest.EmployeeId,
                    loanRequest.Employee?.FullLegalName ?? string.Empty,
                    loanRequest.Employee?.EmployeeNumber.ToString() ?? string.Empty,
                    loanRequest.Kind,
                    loanRequest.RequestedAmount,
                    loanRequest.Currency,
                    loanRequest.Reason,
                    loanRequest.RequestedOn,
                    loanRequest.CreatedAt,
                    loanRequest.ApprovalRequest.State.ToString(),
                    null,
                    loanRequest.ApprovalRequestId);

                return new AttendanceChange<AdvanceLoanRequestResponse>(loanRequest.Id, null, response);
            },
            ct,
            self: true);

    public Task<AdvanceLoanAgreementDetailResponse> ApproveAsync(
        Guid requestId,
        AdvanceLoanApproveRequest request,
        string key,
        AuthRequestContext context,
        CancellationToken ct) =>
        operation.WriteAsync(
            AdvanceLoanPermissions.Approve,
            "hr.advancesloans.approve",
            null,
            key,
            new { requestId, request },
            request.ApprovedTerms,
            context,
            async (actor, token) =>
            {
                var loanRequest = await repository.FindRequestAsync(requestId, token)
                    ?? throw AttendanceOperation.Missing("request_not_found");

                if (loanRequest.ApprovalRequest is null || loanRequest.ApprovalRequest.State != ApprovalState.Pending)
                    throw AttendanceOperation.Conflict("request_not_pending");

                if (loanRequest.EmployeeId == actor.Identity.EmployeeId ||
                    loanRequest.RequestedByUserId == actor.Identity.AccountId.ToString())
                    throw new AuthenticationFailure("advanceloan_self_decision_forbidden", 403);

                if (request.OtherApprovedMonthlyAmount != 0)
                    throw AttendanceOperation.Invalid("interest_free_loan_required");
                if (request.ApprovedPrincipal <= 0 || decimal.Round(request.ApprovedPrincipal, 2) != request.ApprovedPrincipal)
                    throw AttendanceOperation.Invalid("invalid_approved_principal");
                if (request.NumberOfInstallments <= 0)
                    throw AttendanceOperation.Invalid("invalid_installments_count");
                if (request.FirstDueDate < request.EffectiveOn)
                    throw AttendanceOperation.Invalid("invalid_first_due_date");
                if (!string.Equals(request.Currency, loanRequest.Currency, StringComparison.OrdinalIgnoreCase))
                    throw AttendanceOperation.Conflict("currency_mismatch");

                var decision = loanRequest.ApprovalRequest.Decide(
                    ApprovalState.Approved,
                    actor.Identity.AccountId.ToString(),
                    actor.RoleCode,
                    request.ApprovedTerms,
                    actor.Now);

                var agreement = new AdvanceLoanAgreement(
                    loanRequest.Id,
                    request.ApprovedPrincipal,
                    request.Currency.ToUpperInvariant(),
                    request.EffectiveOn,
                    request.ApprovedTerms,
                    loanRequest.ApprovalRequest.Id,
                    actor.Identity.AccountId.ToString(),
                    actor.Now);

                var schedule = new List<RepaymentScheduleEntry>();
                var installments = request.NumberOfInstallments;
                var basePrincipal = Math.Round(request.ApprovedPrincipal / installments, 2);
                var sumSoFar = 0m;

                for (var i = 1; i <= installments; i++)
                {
                    var isLast = (i == installments);
                    var principal = isLast ? (request.ApprovedPrincipal - sumSoFar) : basePrincipal;
                    if (principal <= 0)
                        throw AttendanceOperation.Invalid("invalid_installment_split");
                    sumSoFar += principal;

                    var dueDate = request.FirstDueDate.AddMonths(i - 1);
                    var entry = new RepaymentScheduleEntry(
                        agreement.Id,
                        i,
                        dueDate,
                        principal,
                        0m,
                        request.Currency.ToUpperInvariant());

                    schedule.Add(entry);
                }

                repository.AddAgreement(agreement, decision, schedule);
                await repository.SaveChangesAsync(token);

                var ledger = await repository.ListLedgerEntriesAsync([agreement.Id], token);
                var detail = MapAgreementDetail(agreement, schedule, ledger);
                return new AttendanceChange<AdvanceLoanAgreementDetailResponse>(agreement.Id, null, detail);
            },
            ct,
            self: false);

    public Task<AdvanceLoanRequestResponse> RejectAsync(
        Guid requestId,
        AdvanceLoanRejectRequest request,
        string key,
        AuthRequestContext context,
        CancellationToken ct) =>
        operation.WriteAsync(
            AdvanceLoanPermissions.Review,
            "hr.advancesloans.reject",
            null,
            key,
            new { requestId, request },
            request.Reason,
            context,
            async (actor, token) =>
            {
                var loanRequest = await repository.FindRequestAsync(requestId, token)
                    ?? throw AttendanceOperation.Missing("request_not_found");

                if (loanRequest.ApprovalRequest is null || loanRequest.ApprovalRequest.State != ApprovalState.Pending)
                    throw AttendanceOperation.Conflict("request_not_pending");

                var decision = loanRequest.ApprovalRequest.Decide(
                    ApprovalState.Rejected,
                    actor.Identity.AccountId.ToString(),
                    actor.RoleCode,
                    request.Reason,
                    actor.Now);

                repository.AddDecision(decision);
                await repository.SaveChangesAsync(token);

                var response = new AdvanceLoanRequestResponse(
                    loanRequest.Id,
                    loanRequest.EmployeeId,
                    loanRequest.Employee?.FullLegalName ?? string.Empty,
                    loanRequest.Employee?.EmployeeNumber.ToString() ?? string.Empty,
                    loanRequest.Kind,
                    loanRequest.RequestedAmount,
                    loanRequest.Currency,
                    loanRequest.Reason,
                    loanRequest.RequestedOn,
                    loanRequest.CreatedAt,
                    ApprovalState.Rejected.ToString(),
                    null,
                    loanRequest.ApprovalRequestId);

                return new AttendanceChange<AdvanceLoanRequestResponse>(loanRequest.Id, null, response);
            },
            ct,
            self: false);

    public Task<RepaymentLedgerEntryResponse> RecordRepaymentAsync(
        Guid agreementId,
        RecordRepaymentRequest request,
        string key,
        AuthRequestContext context,
        CancellationToken ct) =>
        operation.WriteAsync(
            AdvanceLoanPermissions.Repay,
            "hr.advancesloans.repayment.record",
            null,
            key,
            new { agreementId, request },
            request.SourceReference,
            context,
            async (actor, token) =>
            {
                var existing = await repository.FindLedgerEntryByIdempotencyKeyAsync(key, token);
                if (existing is not null)
                {
                    if (existing.AdvanceLoanAgreementId != agreementId || existing.RecordedByUserId != actor.Identity.AccountId.ToString() ||
                        existing.RepaymentScheduleEntryId != request.RepaymentScheduleEntryId || existing.Kind != request.Kind ||
                        existing.Amount != request.Amount || existing.Currency != request.Currency.ToUpperInvariant() ||
                        existing.ValueDate != request.ValueDate || existing.SourceReference != request.SourceReference.Trim() ||
                        existing.ReversesEntryId.HasValue)
                        throw AttendanceOperation.Conflict("idempotency_key_reused");
                    // A historical unproved payroll row cannot become new posting evidence.
                    if (existing.Kind == RepaymentEntryKind.PayrollDeduction)
                        throw AttendanceOperation.Conflict("payroll_posting_evidence_required");

                    return new AttendanceChange<RepaymentLedgerEntryResponse>(
                        existing.Id,
                        null,
                        MapLedgerEntry(existing));
                }

                var agreement = await repository.FindAgreementWithDetailsAsync(agreementId, token)
                    ?? throw AttendanceOperation.Missing("agreement_not_found");

                if (request.Kind == RepaymentEntryKind.PayrollDeduction)
                    throw AttendanceOperation.Conflict("payroll_posting_evidence_required");
                if (!Enum.IsDefined(request.Kind) || request.Kind == RepaymentEntryKind.Reversal)
                    throw AttendanceOperation.Invalid("invalid_repayment_kind");
                if (request.RepaymentScheduleEntryId is Guid scheduleId && !agreement.Schedule.Any(x => x.Id == scheduleId))
                    throw AttendanceOperation.Conflict("repayment_schedule_mismatch");
                if (request.Amount <= 0)
                    throw AttendanceOperation.Invalid("repayment_must_be_positive");

                if (!string.Equals(request.Currency, agreement.Currency, StringComparison.OrdinalIgnoreCase))
                    throw AttendanceOperation.Conflict("currency_mismatch");

                var totalRepaid = await repository.GetTotalRepaidAmountAsync(agreementId, token);
                var outstanding = agreement.ApprovedPrincipal - totalRepaid;

                if (request.Amount > outstanding)
                    throw AttendanceOperation.Invalid("repayment_exceeds_outstanding_balance");

                var entry = new RepaymentLedgerEntry(
                    agreementId,
                    request.RepaymentScheduleEntryId,
                    request.Kind,
                    request.Amount,
                    request.Currency.ToUpperInvariant(),
                    request.ValueDate,
                    request.SourceReference,
                    key,
                    null,
                    actor.Identity.AccountId.ToString(),
                    actor.Now);

                repository.AddLedgerEntry(entry);
                await repository.SaveChangesAsync(token);

                return new AttendanceChange<RepaymentLedgerEntryResponse>(
                    entry.Id,
                    null,
                    MapLedgerEntry(entry));
            },
            ct,
            self: false);

    public Task<RepaymentLedgerEntryResponse> ReverseRepaymentAsync(
        Guid agreementId,
        ReverseRepaymentRequest request,
        string key,
        AuthRequestContext context,
        CancellationToken ct) =>
        operation.WriteAsync(
            AdvanceLoanPermissions.Repay,
            "hr.advancesloans.repayment.reverse",
            null,
            key,
            new { agreementId, request },
            request.Reason,
            context,
            async (actor, token) =>
            {
                var existing = await repository.FindLedgerEntryByIdempotencyKeyAsync(key, token);
                if (existing is not null)
                {
                    if (existing.AdvanceLoanAgreementId != agreementId || existing.RecordedByUserId != actor.Identity.AccountId.ToString() ||
                        existing.Kind != RepaymentEntryKind.Reversal || existing.ReversesEntryId != request.ReversesEntryId ||
                        existing.ValueDate != request.ValueDate || existing.SourceReference != request.SourceReference.Trim())
                        throw AttendanceOperation.Conflict("idempotency_key_reused");

                    return new AttendanceChange<RepaymentLedgerEntryResponse>(
                        existing.Id,
                        null,
                        MapLedgerEntry(existing));
                }

                var original = await repository.FindLedgerEntryAsync(request.ReversesEntryId, token)
                    ?? throw AttendanceOperation.Missing("original_entry_not_found");

                if (original.AdvanceLoanAgreementId != agreementId)
                    throw AttendanceOperation.Invalid("original_entry_mismatch");
                if (original.Kind == RepaymentEntryKind.Reversal || original.Amount <= 0)
                    throw AttendanceOperation.Invalid("cannot_reverse_a_reversal");
                if ((await repository.ListLedgerEntriesAsync([agreementId], token)).Any(x => x.ReversesEntryId == original.Id))
                    throw AttendanceOperation.Conflict("repayment_already_reversed");

                var reversal = new RepaymentLedgerEntry(
                    agreementId,
                    original.RepaymentScheduleEntryId,
                    RepaymentEntryKind.Reversal,
                    -original.Amount,
                    original.Currency,
                    request.ValueDate,
                    request.SourceReference,
                    key,
                    original.Id,
                    actor.Identity.AccountId.ToString(),
                    actor.Now);

                repository.AddLedgerEntry(reversal);
                await repository.SaveChangesAsync(token);

                return new AttendanceChange<RepaymentLedgerEntryResponse>(
                    reversal.Id,
                    null,
                    MapLedgerEntry(reversal));
            },
            ct,
            self: false);

    public Task<EmployeeLoanAccountSummaryResponse> GetMySummaryAsync(
        AuthRequestContext context,
        CancellationToken ct) =>
        operation.ReadAsync(
            AdvanceLoanPermissions.Self,
            "hr.advancesloans.summary.self",
            null,
            context,
            async (actor, token) => await LoadAccountSummaryAsync(actor.Identity.EmployeeId, token),
            ct,
            self: true);

    public Task<AdvanceLoanPage<AdvanceLoanRequestResponse>> MyRequestsAsync(int page, int pageSize,
        AuthRequestContext context, CancellationToken ct)
    {
        ValidatePage(page, pageSize);
        return operation.ReadAsync(AdvanceLoanPermissions.Self, "hr.advancesloans.requests.self", null, context,
            async (actor, token) =>
            {
                var skip = (page - 1) * pageSize;
                var (items, total) = await repository.PageRequestsAsync(actor.Identity.EmployeeId, null, skip, pageSize, token);
                var rows = items.Select(r => new AdvanceLoanRequestResponse(r.Id, r.EmployeeId,
                    r.Employee?.FullLegalName ?? string.Empty, r.Employee?.EmployeeNumber.ToString() ?? string.Empty,
                    r.Kind, r.RequestedAmount, r.Currency, r.Reason, r.RequestedOn, r.CreatedAt,
                    r.ApprovalRequest?.State.ToString() ?? "Unavailable", null, r.ApprovalRequestId)).ToArray();
                return new AdvanceLoanPage<AdvanceLoanRequestResponse>(rows, page, pageSize, total, skip + pageSize < total);
            }, ct, self: true);
    }

    public Task<AdvanceLoanPage<AdvanceLoanAgreementDetailResponse>> MyAgreementsAsync(int page, int pageSize,
        AuthRequestContext context, CancellationToken ct)
    {
        ValidatePage(page, pageSize);
        return operation.ReadAsync(AdvanceLoanPermissions.Self, "hr.advancesloans.agreements.self", null, context,
            async (actor, token) =>
            {
                var skip = (page - 1) * pageSize;
                var (items, total) = await repository.PageAgreementsAsync(actor.Identity.EmployeeId, null, skip, pageSize, token);
                var ledger = await repository.ListLedgerEntriesAsync(items.Select(item => item.Id).ToArray(), token);
                var rows = items.Select(item => MapAgreementDetail(item, item.Schedule, ledger)).ToArray();
                return new AdvanceLoanPage<AdvanceLoanAgreementDetailResponse>(rows, page, pageSize, total, skip + pageSize < total);
            }, ct, self: true);
    }

    public Task<EmployeeLoanAccountSummaryResponse> GetEmployeeSummaryAsync(
        Guid employeeId,
        AuthRequestContext context,
        CancellationToken ct) =>
        operation.ReadAsync(
            AdvanceLoanPermissions.Review,
            "hr.advancesloans.summary.employee",
            employeeId,
            context,
            async (_, token) => await LoadAccountSummaryAsync(employeeId, token),
            ct,
            self: false);

    public Task<AdvanceLoanAgreementDetailResponse> GetAgreementDetailAsync(
        Guid agreementId,
        bool self,
        AuthRequestContext context,
        CancellationToken ct) =>
        operation.ReadAsync(
            self ? AdvanceLoanPermissions.Self : AdvanceLoanPermissions.Review,
            "hr.advancesloans.agreement.detail",
            null,
            context,
            async (actor, token) =>
            {
                var agreement = await repository.FindAgreementWithDetailsAsync(agreementId, token)
                    ?? throw AttendanceOperation.Missing("agreement_not_found");

                if (self && agreement.AdvanceLoanRequest?.EmployeeId != actor.Identity.EmployeeId)
                    throw AttendanceOperation.Denied();

                var ledger = await repository.ListLedgerEntriesAsync([agreementId], token);
                return MapAgreementDetail(agreement, agreement.Schedule, ledger);
            },
            ct,
            self: self);

    public Task<AdvanceLoanPage<AdvanceLoanRequestResponse>> PageRequestsAsync(
        Guid? employeeId,
        ApprovalState? state,
        int page,
        int pageSize,
        AuthRequestContext context,
        CancellationToken ct)
    {
        ValidatePage(page, pageSize);
        return operation.ReadAsync(
            AdvanceLoanPermissions.Review,
            "hr.advancesloans.requests.page",
            employeeId,
            context,
            async (_, token) =>
            {
                var p = page;
                var size = pageSize;
                var skip = (p - 1) * size;

                var (items, total) = await repository.PageRequestsAsync(employeeId, state, skip, size, token);
                var dtos = items.Select(r => new AdvanceLoanRequestResponse(
                    r.Id,
                    r.EmployeeId,
                    r.Employee?.FullLegalName ?? string.Empty,
                    r.Employee?.EmployeeNumber.ToString() ?? string.Empty,
                    r.Kind,
                    r.RequestedAmount,
                    r.Currency,
                    r.Reason,
                    r.RequestedOn,
                    r.CreatedAt,
                    r.ApprovalRequest?.State.ToString() ?? ApprovalState.Pending.ToString(),
                    null,
                    r.ApprovalRequestId)).ToArray();

                return new AdvanceLoanPage<AdvanceLoanRequestResponse>(dtos, p, size, total, skip + size < total);
            },
            ct,
            self: false);
    }

    public Task<AdvanceLoanPage<AdvanceLoanAgreementDetailResponse>> PageAgreementsAsync(
        Guid? employeeId,
        bool? activeOnly,
        int page,
        int pageSize,
        AuthRequestContext context,
        CancellationToken ct)
    {
        ValidatePage(page, pageSize);
        return operation.ReadAsync(
            AdvanceLoanPermissions.Review,
            "hr.advancesloans.agreements.page",
            employeeId,
            context,
            async (_, token) =>
            {
                var p = page;
                var size = pageSize;
                var skip = (p - 1) * size;

                var (items, total) = await repository.PageAgreementsAsync(employeeId, activeOnly, skip, size, token);
                var ledger = await repository.ListLedgerEntriesAsync(items.Select(a => a.Id).ToArray(), token);
                var dtos = items.Select(a => MapAgreementDetail(a, a.Schedule, ledger)).ToArray();

                return new AdvanceLoanPage<AdvanceLoanAgreementDetailResponse>(dtos, p, size, total, skip + size < total);
            },
            ct,
            self: false);
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page is < 1 or > 4096 || pageSize is < 1 or > 100)
            throw AttendanceOperation.Invalid("invalid_page");
    }

    private async Task<EmployeeLoanAccountSummaryResponse> LoadAccountSummaryAsync(
        Guid employeeId,
        CancellationToken token)
    {
        var requests = await repository.ListRequestsForEmployeeAsync(employeeId, token);
        var agreements = await repository.ListAgreementsForEmployeeAsync(employeeId, token);
        var ledger = await repository.ListLedgerEntriesAsync(agreements.Select(a => a.Id).ToArray(), token);

        var activeAgreements = new List<AdvanceLoanAgreementDetailResponse>();
        var totalApproved = 0m;
        var totalRepaid = 0m;

        foreach (var ag in agreements)
        {
            var repaid = await repository.GetTotalRepaidAmountAsync(ag.Id, token);
            var balance = ag.ApprovedPrincipal - repaid;
            var isFullyPaid = balance <= 0m;

            totalApproved += ag.ApprovedPrincipal;
            totalRepaid += repaid;

            if (!isFullyPaid)
            {
                activeAgreements.Add(MapAgreementDetail(ag, ag.Schedule, ledger));
            }
        }

        var reqDtos = requests.Take(10).Select(r => new AdvanceLoanRequestResponse(
            r.Id,
            r.EmployeeId,
            r.Employee?.FullLegalName ?? string.Empty,
            r.Employee?.EmployeeNumber.ToString() ?? string.Empty,
            r.Kind,
            r.RequestedAmount,
            r.Currency,
            r.Reason,
            r.RequestedOn,
            r.CreatedAt,
            r.ApprovalRequest?.State.ToString() ?? ApprovalState.Pending.ToString(),
            null,
            r.ApprovalRequestId)).ToArray();

        var outstanding = Math.Max(0m, totalApproved - totalRepaid);

        return new EmployeeLoanAccountSummaryResponse(
            employeeId,
            requests.Count,
            activeAgreements.Count,
            totalApproved,
            totalRepaid,
            outstanding,
            "PKR",
            reqDtos,
            activeAgreements);
    }

    private static AdvanceLoanAgreementDetailResponse MapAgreementDetail(
        AdvanceLoanAgreement a,
        IEnumerable<RepaymentScheduleEntry> schedule,
        IEnumerable<RepaymentLedgerEntry> ledger)
    {
        var rows = ledger
            .Where(x => x.AdvanceLoanAgreementId == a.Id)
            .OrderBy(x => x.RecordedAt)
            .ToArray();
        var ledgerList = rows.Select(MapLedgerEntry).ToArray();
        var payments = rows.Where(x => x.Kind != RepaymentEntryKind.Reversal).Sum(x => x.Amount);
        var reversals = rows.Where(x => x.Kind == RepaymentEntryKind.Reversal).Sum(x => Math.Abs(x.Amount));
        var repaid = Math.Max(0m, payments - reversals);
        var balance = Math.Max(0m, a.ApprovedPrincipal - repaid);

        var scheduleList = schedule.OrderBy(x => x.Sequence).Select(s =>
        {
            var total = s.PrincipalAmount + s.OtherApprovedAmount;
            var paid = rows.Where(x => x.RepaymentScheduleEntryId == s.Id).Sum(x => x.Amount);
            return new RepaymentScheduleEntryResponse(
                s.Id,
                s.Sequence,
                s.DueDate,
                s.PrincipalAmount,
                s.OtherApprovedAmount,
                total,
                s.Currency,
                paid,
                paid >= total);
        }).ToArray();

        return new AdvanceLoanAgreementDetailResponse(
            a.Id,
            a.AdvanceLoanRequestId,
            a.AdvanceLoanRequest?.EmployeeId ?? Guid.Empty,
            a.AdvanceLoanRequest?.Employee?.FullLegalName ?? string.Empty,
            a.AdvanceLoanRequest?.Employee?.EmployeeNumber.ToString() ?? string.Empty,
            a.AdvanceLoanRequest?.Kind ?? AdvanceLoanKind.Loan,
            a.ApprovedPrincipal,
            a.Currency,
            a.EffectiveOn,
            a.ApprovedTermsPrivateReference,
            repaid,
            balance,
            balance <= 0m,
            a.RecordedAt,
            scheduleList,
            ledgerList);
    }

    private static RepaymentLedgerEntryResponse MapLedgerEntry(RepaymentLedgerEntry e) =>
        new(e.Id,
            e.RepaymentScheduleEntryId,
            e.Kind,
            e.Amount,
            e.Currency,
            e.ValueDate,
            e.SourceReference,
            e.ReversesEntryId,
            e.RecordedAt);
}
