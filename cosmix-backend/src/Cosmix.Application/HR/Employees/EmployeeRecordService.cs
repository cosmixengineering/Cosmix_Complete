using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Authentication.Providers;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Organization;
using Cosmix.Application.HR.FoundationPolicies;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.EmployeeRecords;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Application.HR.Employees;

public sealed class EmployeeRecordService(EmployeeOperation operation, IEmployeeHiringRepository hiring,
    IEmployeeLifecycleRepository lifecycle, IEmployeeRecordRepository repository, IAccessManagementRepository access,
    IPrivateFileStore files, IPhoneNumberNormalizer phones, MobileAuthenticationOptions authenticationOptions,
    EmployeeFieldPolicyOptions fieldPolicy, EmployeeRecordPolicyOptions recordPolicy, IHrFoundationPolicyReader? publishedPolicies = null)
{
    public static string RequestType(EmployeeRecordKind kind) { EmployeeRecordPermissions.Read(kind); return "EmployeeRecord." + kind; }
    private static string[] ReadPermissions(EmployeeRecordKind kind) => [EmployeePermissions.Read, EmployeeRecordPermissions.Read(kind)];
    private static string[] RequestPermissions(EmployeeRecordKind kind) => [EmployeePermissions.Read, EmployeeRecordPermissions.Read(kind), EmployeeRecordPermissions.Request(kind)];

    public Task<EmployeeWriteResult> RequestAsync(Guid employeeId, EmployeeRecordKind kind, RequestEmployeeRecord input,
        string key, AuthRequestContext context, CancellationToken ct) =>
        operation.WriteAsync(RequestPermissions(kind), "hr.employee.record." + kind + ".requested", employeeId, key, input, context,
            async (actor, token) =>
            {
                EmployeeRecordValidation.Shape(kind, input);
                var employee = await EmployeeAsync(employeeId, token);
                hiring.ExpectVersion(employee, OrganizationOperation.Version(input.ExpectedEmployeeVersion));
                var date = Today(actor.Now);
                var policy = EmployeeRecordValidation.Policy(await RecordsAsync(date, token), date);
                input = await NormalizeAsync(kind, input, date, token);
                await ValidateAsync(employee, kind, input, actor, token);
                if (await repository.HasPendingAsync(employeeId, RequestType(kind), token)) throw EmployeeOperation.Conflict("employee_record_request_pending");
                var snapshot = new EmployeeRecordSnapshot(1, employeeId, kind, input, policy,
                    kind == EmployeeRecordKind.Sim && input.Action != EmployeeRecordAction.Return ? (await FieldsAsync(date, token)).ApprovedVersion : null);
                var request = new ApprovalRequest(RequestType(kind), employeeId, actor.Identity.AccountId.ToString(),
                    await ProtectAsync(snapshot, employeeId, "records/proposal", token), 1, actor.Now);
                repository.Add(request);
                await hiring.SaveChangesAsync(token);
                return EmployeeHiringService.Result(employee) with { RecordRequestId = request.Id };
            }, ct, authorizeResource: async (actor, token) =>
            { EmployeeRecordValidation.Policy(await RecordsAsync(Today(actor.Now), token), Today(actor.Now)); await EmployeeAsync(employeeId, token); });

    public Task<EmployeeWriteResult> DecideAsync(Guid employeeId, EmployeeRecordKind kind, Guid requestId,
        DecideEmployeeRecord input, string key, AuthRequestContext context, CancellationToken ct) =>
        operation.WriteAsync([EmployeePermissions.Read, EmployeeRecordPermissions.Read(kind), EmployeeRecordPermissions.Review(kind)],
            "hr.employee.record." + kind + ".decided", employeeId, key, new { requestId, input }, context,
            async (actor, token) =>
            {
                EmployeeRecordValidation.Decision(input);
                var employee = await EmployeeAsync(employeeId, token);
                hiring.ExpectVersion(employee, OrganizationOperation.Version(input.ExpectedEmployeeVersion));
                var request = await FindRequestAsync(employeeId, kind, requestId, token);
                repository.ExpectVersion(request, OrganizationOperation.Version(input.ExpectedRequestVersion));
                if (request.State != ApprovalState.Pending) throw EmployeeOperation.Conflict("employee_record_request_closed");
                if (request.RequesterUserId == actor.Identity.AccountId.ToString()) throw EmployeeOperation.Denied();
                var snapshot = await SnapshotAsync(request, kind, token);
                if (snapshot.ApprovedPolicyVersion != EmployeeRecordValidation.Policy(await RecordsAsync(Today(actor.Now), token), Today(actor.Now)))
                    throw EmployeeOperation.Conflict("employee_record_policy_changed");
                if (snapshot.Kind == EmployeeRecordKind.Sim && snapshot.Command.Action != EmployeeRecordAction.Return &&
                    snapshot.ApprovedSimCreditPolicyVersion != (await FieldsAsync(Today(actor.Now), token)).ApprovedVersion)
                    throw EmployeeOperation.Conflict("employee_sim_credit_policy_changed");
                Guid? recordId = null;
                if (input.Decision == ApprovalState.Approved) await ValidateAsync(employee, kind, snapshot.Command, actor, token);
                var decision = request.Decide(input.Decision, actor.Identity.AccountId.ToString(), actor.RoleCode, input.Reason.Trim(), actor.Now,
                    await ProtectAsync(new { snapshot, input.Decision, input.Reason }, employeeId, "records/decision", token));
                repository.Add(decision);
                if (input.Decision == ApprovalState.Approved) recordId = await ApplyAsync(employee, kind, snapshot.Command, requestId, actor, token);
                await hiring.SaveChangesAsync(token);
                return EmployeeHiringService.Result(employee) with { RecordRequestId = request.Id, EmployeeRecordId = recordId, DecisionId = decision.Id };
            }, ct, adminReview: true, fixedAdminPermission: EmployeeRecordPermissions.Review(kind),
            authorizeResource: async (actor, token) =>
            { EmployeeRecordValidation.Policy(await RecordsAsync(Today(actor.Now), token), Today(actor.Now)); await EmployeeAsync(employeeId, token); });

    public Task<EmployeeWriteResult> CancelAsync(Guid employeeId, EmployeeRecordKind kind, Guid requestId,
        CancelEmployeeRecord input, string key, AuthRequestContext context, CancellationToken ct) =>
        operation.WriteAsync(RequestPermissions(kind), "hr.employee.record." + kind + ".cancelled", employeeId, key,
            new { requestId, input }, context, async (actor, token) =>
            {
                if (input is null) throw EmployeeOperation.Invalid("invalid_employee_record_cancel");
                var employee = await EmployeeAsync(employeeId, token);
                var request = await FindRequestAsync(employeeId, kind, requestId, token);
                repository.ExpectVersion(request, OrganizationOperation.Version(input.ExpectedRequestVersion));
                if (request.State != ApprovalState.Pending) throw EmployeeOperation.Conflict("employee_record_request_closed");
                if (request.RequesterUserId != actor.Identity.AccountId.ToString()) throw EmployeeOperation.Denied();
                request.Cancel(actor.Identity.AccountId.ToString(), actor.Now);
                await hiring.SaveChangesAsync(token);
                return EmployeeHiringService.Result(employee) with { RecordRequestId = requestId };
            }, ct, authorizeResource: async (_, token) => { await EmployeeAsync(employeeId, token); });

    public Task<PagedResult<EmployeeRecordRequestSummary>> RequestsAsync(Guid? employeeId, EmployeeRecordKind kind,
        EmployeeRecordQuery query, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(ReadPermissions(kind), "hr.employee.record." + kind + ".requests.read", employeeId, context,
            async (_, token) =>
            {
                EmployeeRecordValidation.Query(query, states: true);
                if (employeeId.HasValue) await EmployeeAsync(employeeId.Value, token);
                return await repository.RequestsAsync(employeeId, RequestType(kind), query, token);
            }, ct);
    public Task<EmployeeRecordRequestDetail> DetailAsync(Guid employeeId, EmployeeRecordKind kind, Guid requestId,
        AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(ReadPermissions(kind), "hr.employee.record." + kind + ".request.read", employeeId, context,
            async (_, token) =>
            {
                await EmployeeAsync(employeeId, token);
                var request = await FindRequestAsync(employeeId, kind, requestId, token);
                var snapshot = await SnapshotAsync(request, kind, token);
                return new EmployeeRecordRequestDetail(new(request.Id, employeeId, request.State, request.RequesterUserId,
                    request.RequestedAt, request.ClosedAt, Convert.ToBase64String(request.RowVersion)), kind, snapshot.Command,
                    snapshot.ApprovedPolicyVersion, (await repository.DecisionsAsync(requestId, token)).Select(d =>
                        new EmployeeChangeDecision(d.Id, d.Decision, d.ActorUserId, d.ActorRoleCode, d.Reason, d.DecidedAt)).ToArray());
            }, ct, auditTarget: _ => requestId.ToString());

    public Task<PagedResult<EmployeeAssetRecord>> AssetsAsync(Guid employeeId, bool history, DateTimeOffset? asOf,
        EmployeeRecordQuery query, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(ReadPermissions(EmployeeRecordKind.Asset), "hr.employee.assets." + (history ? "history" : "current"), employeeId, context,
            async (actor, token) =>
            {
                EmployeeRecordValidation.Query(query); await EmployeeAsync(employeeId, token);
                var at = asOf ?? actor.Now;
                if (at == default || at > actor.Now || at.Offset != TimeSpan.Zero) throw EmployeeOperation.Invalid("invalid_employee_record_asof");
                var page = await repository.AssetsAsync(employeeId, history, at, query, token);
                return new PagedResult<EmployeeAssetRecord>(page.Items.Select(a => new EmployeeAssetRecord(a.Id, a.EmployeeId,
                    a.AssetReference, a.IssuedAt, history || a.ReturnedAt <= at ? a.ReturnedAt : null, a.IssueCondition,
                    history || a.ReturnedAt <= at ? a.ReturnCondition : null, history || a.ReturnedAt <= at ? a.ReturnedToUserId : null,
                    a.RecordedByUserId, Convert.ToBase64String(a.RowVersion))).ToArray(), page.TotalCount, page.Page, page.PageSize);
            }, ct);
    public Task<IReadOnlyList<EmployeeAssetHistoryRecord>> AssetHistoryAsync(Guid employeeId, Guid custodyId,
        AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync<IReadOnlyList<EmployeeAssetHistoryRecord>>(ReadPermissions(EmployeeRecordKind.Asset), "hr.employee.asset.events.read", employeeId,
            context, async (_, token) =>
            {
                await EmployeeAsync(employeeId, token);
                var custody = await repository.AssetAsync(custodyId, token);
                if (custody?.EmployeeId != employeeId) throw EmployeeOperation.Missing();
                return (await repository.AssetHistoryAsync(employeeId, custodyId, token)).Select(h =>
                    new EmployeeAssetHistoryRecord(h.Id, h.EmployeeAssetCustodyId, h.Revision, h.Action, h.EffectiveAt,
                        h.Condition, h.EvidenceDocumentId, h.ReceiverAccountId, h.TransferredFromCustodyId, h.ApprovalRequestId,
                        h.ActorUserId, h.RecordedAt, h.Reason)).ToArray();
            }, ct, auditTarget: _ => custodyId.ToString());
    public Task<PagedResult<EmployeeSimRecord>> SimsAsync(Guid employeeId, bool history, DateOnly? asOf,
        EmployeeRecordQuery query, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(ReadPermissions(EmployeeRecordKind.Sim), "hr.employee.sims." + (history ? "history" : "current"), employeeId, context,
            async (actor, token) =>
            {
                EmployeeRecordValidation.Query(query); await EmployeeAsync(employeeId, token);
                var date = AsOf(asOf, actor.Now); var page = await repository.SimsAsync(employeeId, history, date, query, token);
                return new PagedResult<EmployeeSimRecord>(page.Items.Select(s => new EmployeeSimRecord(s.Id, s.EmployeeId, s.Revision,
                    s.SupersedesEmployeeSimAssignmentId, s.TransferredFromAssignmentId, s.SimReference, s.PhoneNumber, s.AllowedCreditTierCode,
                    s.Status, s.IssuedOn, s.EffectiveOn, s.ReturnedOn, s.IssueDocumentId, s.ReturnDocumentId, s.ReturnedToAccountId,
                    s.RecordedByUserId, s.RecordedAt, s.ApprovalRequestId, s.Reason)).ToArray(), page.TotalCount, page.Page, page.PageSize);
            }, ct);
    public Task<PagedResult<EmployeeCertificationRecord>> CertificationsAsync(Guid employeeId, bool history, DateOnly? asOf,
        EmployeeRecordQuery query, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(ReadPermissions(EmployeeRecordKind.Certification), "hr.employee.certifications." + (history ? "history" : "current"), employeeId, context,
            async (actor, token) =>
            {
                EmployeeRecordValidation.Query(query); await EmployeeAsync(employeeId, token);
                var date = AsOf(asOf, actor.Now); var page = await repository.CertificationsAsync(employeeId, history, date, query, token);
                return new PagedResult<EmployeeCertificationRecord>(page.Items.Select(c => new EmployeeCertificationRecord(c.Id, c.EmployeeId,
                    c.Revision, c.SupersedesEmployeeCertificationId, c.CertificateType, c.CertificateNumber, c.Issuer, c.IssuedOn,
                    c.ExpiresOn, c.EffectiveOn, c.Status, c.ExpiresOn < date, c.EmployeeDocumentId, c.ActionDocumentId, c.RecordedByUserId,
                    c.RecordedAt, c.ApprovalRequestId, c.Reason)).ToArray(), page.TotalCount, page.Page, page.PageSize);
            }, ct);

    private async Task ValidateAsync(Employee employee, EmployeeRecordKind kind, RequestEmployeeRecord input, EmployeeActor actor, CancellationToken ct)
    {
        EmployeeRecordValidation.Shape(kind, input);
        var date = Today(actor.Now);
        if (Today(input.EffectiveAt) != date || input.EffectiveAt > actor.Now || !employee.JoinedOn.HasValue || date < employee.JoinedOn)
            throw EmployeeOperation.Invalid("employee_record_requires_current_business_date");
        // Return/revocation remains possible after offboarding; new issues/renewals/transfers require active unscheduled employment.
        if (input.Action is not (EmployeeRecordAction.Return or EmployeeRecordAction.Revoke or EmployeeRecordAction.Suspend)) await ActiveAsync(employee, actor.Now, ct);
        if (kind == EmployeeRecordKind.Asset)
        {
            var a = input.Asset!;
            await EvidenceAsync(employee.Id, a.EvidenceDocumentId, date, certificatesOnly: false, ct);
            if (input.Action == EmployeeRecordAction.Issue)
            {
                var old = await repository.LatestAssetAsync(a.AssetReference!.Trim().ToUpperInvariant(), ct);
                if (old is not null && (!old.ReturnedAt.HasValue || old.ReturnedAt > input.EffectiveAt)) throw EmployeeOperation.Conflict("asset_already_in_custody");
            }
            else
            {
                var old = await repository.AssetAsync(input.ExpectedCurrentRecordId!.Value, ct);
                if (old?.EmployeeId != employee.Id) throw EmployeeOperation.Missing();
                if (old.ReturnedAt.HasValue || old.IssuedAt > input.EffectiveAt) throw EmployeeOperation.Conflict("asset_already_returned_or_date_conflict");
                await ReceiverAsync(a.ReceiverAccountId!.Value, actor.Now, ct);
                if (input.Action == EmployeeRecordAction.Transfer)
                    await TargetAsync(employee.Id, a.TargetEmployeeId!.Value, a.ExpectedTargetVersion!, a.TargetEvidenceDocumentId!.Value, date, actor.Now, ct);
            }
        }
        else if (kind == EmployeeRecordKind.Sim)
        {
            var s = input.Sim!;
            await EvidenceAsync(employee.Id, s.EvidenceDocumentId, date, certificatesOnly: false, ct);
            if (input.Action == EmployeeRecordAction.Issue)
            {
                EmployeeRecordValidation.CreditTier(await FieldsAsync(date, ct), s.CreditTierCode, date);
                var tips = await repository.LatestSimsAsync(s.SimReference!.Trim().ToUpperInvariant(), s.PhoneNumber!, date, ct);
                if (tips.Any(t => t.Status != CompanySimStatus.Returned || t.ReturnedOn > date)) throw EmployeeOperation.Conflict("sim_or_phone_already_in_custody");
            }
            else
            {
                var old = await repository.SimAsync(input.ExpectedCurrentRecordId!.Value, ct);
                if (old?.EmployeeId != employee.Id) throw EmployeeOperation.Missing();
                if (old.Status == CompanySimStatus.Returned || old.EffectiveOn > date || await repository.SimSuccessorAsync(old.Id, ct)) throw EmployeeOperation.Conflict("sim_already_returned_or_revised");
                var tips = await repository.LatestSimsAsync(old.SimReference, old.PhoneNumber, date, ct);
                if (tips.Any(t => t.Id != old.Id)) throw EmployeeOperation.Conflict("sim_custody_history_requires_review");
                await ReceiverAsync(s.ReceiverAccountId!.Value, actor.Now, ct);
                if (input.Action == EmployeeRecordAction.Transfer)
                {
                    if (old.Status != CompanySimStatus.Active) throw EmployeeOperation.Conflict("suspended_sim_transfer_requires_separate_policy");
                    EmployeeRecordValidation.CreditTier(await FieldsAsync(date, ct), s.CreditTierCode, date);
                    await TargetAsync(employee.Id, s.TargetEmployeeId!.Value, s.ExpectedTargetVersion!, s.TargetEvidenceDocumentId!.Value, date, actor.Now, ct);
                }
            }
        }
        else
        {
            var c = input.Certification!;
            var metadata = input.Action is EmployeeRecordAction.Issue or EmployeeRecordAction.Renew or EmployeeRecordAction.Replace;
            var evidence = await EvidenceAsync(employee.Id, c.EvidenceDocumentId, date, certificatesOnly: metadata, ct);
            if (metadata && (c.IssuedOn > date || c.ExpiresOn < date || evidence.IssuedOn != c.IssuedOn || evidence.ExpiresOn != c.ExpiresOn))
                throw EmployeeOperation.Invalid("certification_metadata_must_match_current_evidence");
            if (input.Action == EmployeeRecordAction.Issue)
            {
                if (await repository.ActiveCertificationAsync(employee.Id, c.Type!.Trim(), c.Number!.Trim(), c.Issuer!.Trim(), ct)) throw EmployeeOperation.Conflict("certification_already_recorded");
            }
            else
            {
                var old = await repository.CertificationAsync(input.ExpectedCurrentRecordId!.Value, ct);
                if (old?.EmployeeId != employee.Id) throw EmployeeOperation.Missing();
                if (old.Status == EmployeeCertificationStatus.Revoked || old.EffectiveOn > date || await repository.CertificationSuccessorAsync(old.Id, ct))
                    throw EmployeeOperation.Conflict("certification_already_revoked_or_revised");
                if (input.Action == EmployeeRecordAction.Suspend && old.Status != EmployeeCertificationStatus.Active) throw EmployeeOperation.Conflict("certification_already_suspended");
                if (metadata && (old.CertificateType != c.Type!.Trim() || old.Issuer != c.Issuer!.Trim() || old.EmployeeDocumentId == c.EvidenceDocumentId))
                    throw EmployeeOperation.Invalid("certification_successor_requires_same_type_issuer_new_evidence");
                if (metadata && old.CertificateNumber != c.Number!.Trim() &&
                    await repository.ActiveCertificationAsync(employee.Id, c.Type!.Trim(), c.Number.Trim(), c.Issuer!.Trim(), ct))
                    throw EmployeeOperation.Conflict("certification_already_recorded");
                if (input.Action == EmployeeRecordAction.Renew && (old.Status != EmployeeCertificationStatus.Active || !old.ExpiresOn.HasValue ||
                    !c.ExpiresOn.HasValue || c.ExpiresOn <= old.ExpiresOn || c.IssuedOn < old.IssuedOn))
                    throw EmployeeOperation.Invalid("certification_renewal_requires_later_validity");
                if (input.Action == EmployeeRecordAction.Replace && (old.Status != EmployeeCertificationStatus.Active || old.CertificateNumber != c.Number!.Trim() || old.IssuedOn != c.IssuedOn || old.ExpiresOn != c.ExpiresOn))
                    throw EmployeeOperation.Invalid("certification_replacement_preserves_validity");
            }
        }
    }

    private async Task<Guid> ApplyAsync(Employee employee, EmployeeRecordKind kind, RequestEmployeeRecord input, Guid requestId, EmployeeActor actor, CancellationToken ct)
    {
        var actorId = actor.Identity.AccountId.ToString(); var date = Today(actor.Now);
        if (kind == EmployeeRecordKind.Asset)
        {
            var a = input.Asset!;
            EmployeeAssetCustody? prior = null;
            if (input.Action != EmployeeRecordAction.Issue)
            {
                prior = (await repository.AssetAsync(input.ExpectedCurrentRecordId!.Value, ct))!;
                prior.RecordReturn(input.EffectiveAt, a.Condition.Trim(), a.ReceiverAccountId!.Value.ToString(), null);
                repository.Add(new EmployeeAssetHistory(prior, 2, EmployeeAssetHistoryAction.Returned, a.EvidenceDocumentId,
                    requestId, input.Reason.Trim(), actorId, actor.Now, a.ReceiverAccountId));
                // Free the existing SQL unique active-asset index before inserting the transfer receiver, inside the same outer transaction.
                await hiring.SaveChangesAsync(ct);
                if (input.Action == EmployeeRecordAction.Return) return prior.Id;
            }
            var row = new EmployeeAssetCustody(a.TargetEmployeeId ?? employee.Id, prior?.AssetReference ?? a.AssetReference!.Trim().ToUpperInvariant(),
                input.EffectiveAt, prior is null ? a.Condition.Trim() : a.TargetCondition!.Trim(), null, actorId);
            repository.Add(row);
            repository.Add(new EmployeeAssetHistory(row, 1, EmployeeAssetHistoryAction.Issued, a.TargetEvidenceDocumentId ?? a.EvidenceDocumentId,
                requestId, input.Reason.Trim(), actorId, actor.Now, transferredFromCustodyId: prior?.Id));
            return row.Id;
        }
        if (kind == EmployeeRecordKind.Sim)
        {
            var s = input.Sim!;
            EmployeeSimAssignment? prior = null;
            EmployeeSimAssignment? returned = null;
            if (input.Action != EmployeeRecordAction.Issue)
            {
                prior = (await repository.SimAsync(input.ExpectedCurrentRecordId!.Value, ct))!;
                returned = new EmployeeSimAssignment(employee.Id, prior.PhoneNumber, prior.SimReference, prior.AllowedCreditTierCode,
                    CompanySimStatus.Returned, prior.IssuedOn, date, actorId, actor.Now, checked(prior.Revision + 1), prior.Id,
                    date, prior.IssueDocumentId, s.EvidenceDocumentId, s.ReceiverAccountId, requestId, input.Reason.Trim());
                repository.Add(returned);
                if (input.Action == EmployeeRecordAction.Return) return returned.Id;
            }
            var row = new EmployeeSimAssignment(s.TargetEmployeeId ?? employee.Id, prior?.PhoneNumber ?? s.PhoneNumber!,
                prior?.SimReference ?? s.SimReference!.Trim().ToUpperInvariant(), EmployeeRecordValidation.CreditTier(await FieldsAsync(date, ct), s.CreditTierCode, date),
                CompanySimStatus.Active, date, null, actorId, actor.Now, effectiveOn: date,
                issueDocumentId: s.TargetEvidenceDocumentId ?? s.EvidenceDocumentId, approvalRequestId: requestId,
                reason: input.Reason.Trim(), transferredFromAssignmentId: returned?.Id);
            repository.Add(row); return row.Id;
        }
        var c = input.Certification!;
        var old = input.ExpectedCurrentRecordId.HasValue ? await repository.CertificationAsync(input.ExpectedCurrentRecordId.Value, ct) : null;
        var metadata = input.Action is EmployeeRecordAction.Issue or EmployeeRecordAction.Renew or EmployeeRecordAction.Replace;
        var status = input.Action switch { EmployeeRecordAction.Suspend => EmployeeCertificationStatus.Suspended,
            EmployeeRecordAction.Revoke => EmployeeCertificationStatus.Revoked, _ => EmployeeCertificationStatus.Active };
        var certification = new EmployeeCertification(employee.Id, metadata ? c.Type!.Trim() : old!.CertificateType,
            metadata ? c.Number!.Trim() : old!.CertificateNumber, metadata ? c.Issuer!.Trim() : old!.Issuer,
            metadata ? c.IssuedOn!.Value : old!.IssuedOn, metadata ? c.ExpiresOn : old!.ExpiresOn,
            metadata ? c.EvidenceDocumentId : old!.EmployeeDocumentId, actorId, actor.Now,
            old is null ? 1 : checked(old.Revision + 1), old?.Id, date, status, requestId, input.Reason.Trim(), c.EvidenceDocumentId);
        repository.Add(certification); return certification.Id;
    }
    private async Task<Employee> EmployeeAsync(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty) throw EmployeeOperation.Invalid("invalid_employee_id");
        var employee = await hiring.FindAsync(id, ct) ?? throw EmployeeOperation.Missing();
        if (!await lifecycle.HasApprovedEmploymentAsync(employee, ct)) throw EmployeeOperation.Conflict("employee_not_approved");
        return employee;
    }
    private async Task ActiveAsync(Employee employee, DateTimeOffset now, CancellationToken ct)
    {
        var changes = await lifecycle.CurrentChangesAsync(employee.Id, now, ct);
        if (employee.Status != EmployeeStatus.Active || employee.IsArchived || !employee.JoinedOn.HasValue || Today(now) < employee.JoinedOn ||
            changes.Count > 0 && changes[0].NewStatus != EmployeeStatus.Active ||
            changes.Count > 1 && changes[0].EffectiveAt == changes[1].EffectiveAt || await lifecycle.HasFutureChangeAsync(employee.Id, now, ct))
            throw EmployeeOperation.Conflict("employee_record_requires_active_unscheduled_employment");
    }
    private async Task ReceiverAsync(Guid id, DateTimeOffset now, CancellationToken ct)
    {
        var account = await access.FindAccountAsync(id, ct);
        if (account?.Status != AccountStatus.Active) throw EmployeeOperation.Invalid("asset_sim_receiver_must_be_active_account");
        await ActiveAsync(await EmployeeAsync(account.EmployeeId, ct), now, ct);
    }
    private async Task TargetAsync(Guid sourceId, Guid id, string version, Guid evidenceId, DateOnly date, DateTimeOffset now, CancellationToken ct)
    {
        if (sourceId == id) throw EmployeeOperation.Invalid("employee_record_transfer_requires_different_employee");
        var target = await EmployeeAsync(id, ct); hiring.ExpectVersion(target, OrganizationOperation.Version(version));
        await ActiveAsync(target, now, ct); await EvidenceAsync(id, evidenceId, date, certificatesOnly: false, ct);
    }
    private async Task<EmployeeDocument> EvidenceAsync(Guid employeeId, Guid id, DateOnly date, bool certificatesOnly, CancellationToken ct)
    {
        var document = await hiring.FindDocumentAsync(employeeId, id, ct);
        if (document is null || document.Privacy != EmployeeDocumentPrivacy.Standard || !document.IsAvailableOn(date) ||
            (certificatesOnly ? document.DocumentType is not (EmployeeDocumentType.Certificate or EmployeeDocumentType.License) :
                document.DocumentType is not (EmployeeDocumentType.Other or EmployeeDocumentType.Certificate or EmployeeDocumentType.License)))
            throw EmployeeOperation.Conflict("employee_record_evidence_unavailable");
        byte[]? content = null;
        try
        {
            var reference = JsonSerializer.Deserialize<PrivateFileReference>(document.StorageKey) ?? throw EmployeeOperation.Conflict("employee_record_evidence_unavailable");
            content = await files.ReadAsync(reference, EmployeeHiringService.Purpose(employeeId, "document"), ct);
            if (content.LongLength != document.SizeBytes || Convert.ToHexString(SHA256.HashData(content)) != document.Sha256)
                throw EmployeeOperation.Conflict("employee_record_evidence_unavailable");
        }
        catch (JsonException) { throw EmployeeOperation.Conflict("employee_record_evidence_unavailable"); }
        finally { if (content is not null) CryptographicOperations.ZeroMemory(content); }
        return document;
    }
    private async Task<RequestEmployeeRecord> NormalizeAsync(EmployeeRecordKind kind, RequestEmployeeRecord input, DateOnly date, CancellationToken ct)
    {
        if (kind != EmployeeRecordKind.Sim || input.Sim is not { } sim || input.Action == EmployeeRecordAction.Return) return input;
        string? phone = sim.PhoneNumber;
        if (input.Action == EmployeeRecordAction.Issue)
        {
            try { phone = phones.Normalize(sim.PhoneNumber!); }
            catch (ArgumentException) { throw EmployeeOperation.Invalid("invalid_employee_sim_phone"); }
        }
        return input with { Sim = sim with { PhoneNumber = phone, CreditTierCode = EmployeeRecordValidation.CreditTier(await FieldsAsync(date, ct), sim.CreditTierCode, date) } };
    }
    private Task<EmployeeFieldPolicyOptions> FieldsAsync(DateOnly date, CancellationToken ct) =>
        EmployeePublishedPolicyOptions.FieldsAsync(fieldPolicy, publishedPolicies, date, ct);
    private Task<EmployeeRecordPolicyOptions> RecordsAsync(DateOnly date, CancellationToken ct) =>
        EmployeePublishedPolicyOptions.RecordsAsync(recordPolicy, publishedPolicies, date, ct);
    private async Task<string> ProtectAsync<T>(T content, Guid employeeId, string purpose, CancellationToken ct)
    {
        var encoded = JsonSerializer.Serialize(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(content), EmployeeHiringService.Purpose(employeeId, purpose), ct));
        if (encoded.Length > 500) throw EmployeeOperation.Conflict("employee_record_evidence_reference_invalid");
        return encoded;
    }
    private async Task<EmployeeRecordSnapshot> SnapshotAsync(ApprovalRequest request, EmployeeRecordKind kind, CancellationToken ct)
    {
        try
        {
            var reference = JsonSerializer.Deserialize<PrivateFileReference>(request.SnapshotPrivateReference) ?? throw EmployeeOperation.Conflict("employee_record_snapshot_unavailable");
            var snapshot = JsonSerializer.Deserialize<EmployeeRecordSnapshot>(await files.ReadAsync(reference,
                EmployeeHiringService.Purpose(request.SubjectId, "records/proposal"), ct)) ?? throw EmployeeOperation.Conflict("employee_record_snapshot_unavailable");
            if (snapshot.SchemaVersion != 1 || snapshot.EmployeeId != request.SubjectId || snapshot.Kind != kind || snapshot.Command is null)
                throw EmployeeOperation.Conflict("employee_record_snapshot_invalid");
            EmployeeRecordValidation.Shape(kind, snapshot.Command); return snapshot;
        }
        catch (JsonException) { throw EmployeeOperation.Conflict("employee_record_snapshot_unavailable"); }
    }
    private async Task<ApprovalRequest> FindRequestAsync(Guid employeeId, EmployeeRecordKind kind, Guid id, CancellationToken ct) =>
        await repository.RequestAsync(employeeId, RequestType(kind), id, ct) ?? throw EmployeeOperation.Missing();
    private DateOnly AsOf(DateOnly? date, DateTimeOffset now)
    {
        var today = Today(now); if (date == DateOnly.MinValue || date > today) throw EmployeeOperation.Invalid("invalid_employee_record_asof");
        return date ?? today;
    }
    private DateOnly Today(DateTimeOffset now)
    {
        try { return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(authenticationOptions.BusinessTimeZoneId)).DateTime); }
        catch (Exception ex) when (ex is ArgumentException or TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new AuthenticationFailure("business_timezone_not_configured", 503); }
    }
}
