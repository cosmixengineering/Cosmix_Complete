using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Organization;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.HR.FoundationPolicies;

public sealed class HrFoundationPolicyService(HrFoundationPolicyOperation operation,
    IHrFoundationPolicyRepository repository, HrFoundationPolicyReader reader,
    HrFoundationPolicyAuthorityOptions authority, MobileAuthenticationOptions authentication)
{
    public Task<HrFoundationPolicyCurrent> CurrentAsync(HrFoundationPolicyKind kind, DateOnly? selectedDate,
        AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(kind, "hr.foundation-policy.current.read", context, async (actor, token) =>
        {
            var date = selectedDate ?? Today(actor.Now);
            if (date == default || date == DateOnly.MaxValue) throw Invalid("invalid_hr_foundation_policy_date");
            var policy = await repository.PolicyAsync(kind, token);
            var versions = policy is null ? [] : await repository.VersionsAsync(policy.Id, kind, token);
            var result = HrFoundationPolicyReader.Resolve(kind, date, await reader.VerifySourceAsync(kind, new(policy, versions), token));
            var row = result.Identity is null ? null : versions.Single(x => x.Version.Id == result.Identity.VersionId).Version;
            return new HrFoundationPolicyCurrent(policy is null ? null : Definition(policy, kind), date, result.State,
                row is null ? null : new(result.Identity!, result.Rules, result.State, null, row.ApprovalRequestId,
                    row.PublishedByUserId, row.PublishedAt), result.UnavailableReason);
        }, ct);

    public Task<PagedResult<HrFoundationPolicyVersionView>> HistoryAsync(HrFoundationPolicyKind kind,
        int page, int pageSize, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(kind, "hr.foundation-policy.history.read", context, async (_, token) =>
        {
            Page(page, pageSize);
            var policy = await repository.PolicyAsync(kind, token);
            if (policy is null) return new PagedResult<HrFoundationPolicyVersionView>([], 0, page, pageSize);
            var versions = (await reader.VerifySourceAsync(kind, new(policy, await repository.VersionsAsync(policy.Id, kind, token)), token)).Versions;
            var items = versions.OrderByDescending(x => x.Version.VersionNumber).Skip((page - 1) * pageSize).Take(pageSize)
                .Select(candidate =>
                {
                    var row = candidate.Version;
                    var decoded = HrFoundationPolicyRulesCodec.Decode(row.RulesJson);
                    var resolution = HrFoundationPolicyReader.Resolve(kind, row.EffectiveFrom, new(policy, versions));
                    var valid = resolution.State == "effective" && resolution.Identity?.VersionId == row.Id;
                    return new HrFoundationPolicyVersionView(new(policy.Id, row.Id, row.VersionNumber,
                        decoded?.ApprovedVersion ?? "", row.EffectiveFrom, resolution.Identity?.VersionId == row.Id
                            ? resolution.Identity.EffectiveTo : row.EffectiveTo), valid ? decoded!.Rules : null,
                        valid ? "published" : "unavailable", valid ? null : resolution.UnavailableReason ?? "invalid_policy_chain",
                        row.ApprovalRequestId, row.PublishedByUserId, row.PublishedAt);
                }).ToArray();
            return new PagedResult<HrFoundationPolicyVersionView>(items, versions.Count, page, pageSize);
        }, ct);

    public Task<PagedResult<HrFoundationPolicyRequestView>> RequestsAsync(HrFoundationPolicyKind kind, int page,
        int pageSize, ApprovalState? state, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(kind, "hr.foundation-policy.requests.read", context, async (_, token) =>
        {
            Page(page, pageSize);
            if (state.HasValue && !Enum.IsDefined(state.Value)) throw Invalid("invalid_hr_foundation_policy_state");
            var policy = await repository.PolicyAsync(kind, token);
            return policy is null ? new([], 0, page, pageSize) :
                await repository.RequestsAsync(policy.Id, kind, page, pageSize, state, token);
        }, ct);

    public Task<HrFoundationPolicyRequestDetail> DetailAsync(HrFoundationPolicyKind kind, Guid requestId,
        AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(kind, "hr.foundation-policy.request.read", context, async (_, token) =>
        {
            var policy = await PolicyAsync(kind, token);
            var request = await RequestAsync(policy, kind, requestId, token);
            var snapshot = await ProposalAsync(request, kind, token);
            var decisions = await repository.DecisionsAsync(request.Id, token);
            return new HrFoundationPolicyRequestDetail(RequestView(request, kind), snapshot.Command,
                snapshot.ApprovedAuthorityVersion, decisions.Select(x => new HrFoundationPolicyDecisionView(x.Id,
                    x.Decision, x.ActorUserId, x.ActorRoleCode, x.Reason, x.DecidedAt)).ToArray());
        }, ct);

    public Task<HrFoundationPolicyWriteResult> RequestAsync(HrFoundationPolicyKind kind, RequestHrFoundationPolicy input,
        string key, AuthRequestContext context, CancellationToken ct)
    {
        if (input is null) throw Invalid("invalid_hr_foundation_policy_request");
        return operation.WriteAsync(kind, "hr.foundation-policy.publication.requested", key, new { input, authority }, input.Reason, context,
            async (actor, token) =>
            {
                var today = Today(actor.Now);
                var authorityVersion = ApprovedAuthority(today);
                ValidateInput(kind, input, today, authorityVersion);
                await ValidateDocumentCategoriesAsync(input.Rules, token);
                var policy = await repository.PolicyAsync(kind, token);
                if (policy is null)
                {
                    if (input.ExpectedPolicyVersion is not null || input.ExpectedLatestVersion != 0)
                        throw Conflict("hr_foundation_policy_version_conflict");
                    policy = new Policy(HrFoundationPolicyRulesCodec.Code(kind), "HR foundation " + kind, PolicyCategory.EmployeeException);
                    repository.Add(policy);
                }
                else ExpectPolicy(policy, kind, input.ExpectedPolicyVersion);
                var versions = (await reader.VerifySourceAsync(kind, new(policy, await repository.VersionsAsync(policy.Id, kind, token)), token)).Versions;
                ValidateSuccessor(policy, kind, input, versions);
                if (await repository.PendingAsync(policy.Id, kind, token)) throw Conflict("hr_foundation_policy_request_pending");
                var snapshot = new HrFoundationPolicyProposal(1, kind, policy.Id, input, authorityVersion,
                    authority.EffectiveFrom!.Value, authority.EffectiveTo, actor.Identity.AccountId.ToString(), actor.Now);
                var reference = await operation.ProtectAsync(snapshot, HrFoundationPolicyOperation.Purpose(kind, "proposal"), token);
                var request = new ApprovalRequest(HrFoundationPolicyRulesCodec.RequestType(kind), policy.Id,
                    actor.Identity.AccountId.ToString(), reference, 1, actor.Now);
                repository.Add(request);
                return new(policy.Id, request.Id, "pending");
            }, async (actor, token) =>
            {
                ApprovedAuthority(Today(actor.Now));
                var policy = await repository.PolicyAsync(kind, token);
                if (policy is not null) Definition(policy, kind);
            }, ct);
    }

    public Task<HrFoundationPolicyWriteResult> DecideAsync(HrFoundationPolicyKind kind, Guid requestId,
        DecideHrFoundationPolicy input, string key, AuthRequestContext context, CancellationToken ct)
    {
        if (input is null) throw Invalid("invalid_hr_foundation_policy_decision");
        return operation.WriteAsync(kind, "hr.foundation-policy.publication.decided", key, new { requestId, input, authority }, input.Reason, context,
            async (actor, token) =>
            {
                if (input is null || input.Decision is not (ApprovalState.Approved or ApprovalState.Returned or ApprovalState.Rejected))
                    throw Invalid("invalid_hr_foundation_policy_decision");
                var today = Today(actor.Now);
                var authorityVersion = ApprovedAuthority(today);
                var policy = await PolicyAsync(kind, token);
                ExpectPolicy(policy, kind, input.ExpectedPolicyVersion);
                var request = await RequestAsync(policy, kind, requestId, token);
                if (request.State != ApprovalState.Pending) throw Conflict("hr_foundation_policy_request_closed");
                if (request.RequesterUserId == actor.Identity.AccountId.ToString()) throw HrFoundationPolicyOperation.Denied();
                repository.ExpectVersion(request, OrganizationOperation.Version(input.ExpectedRequestVersion));
                if (Convert.ToBase64String(request.RowVersion) != input.ExpectedRequestVersion) throw Conflict("hr_foundation_policy_request_version_conflict");
                var snapshot = await ProposalAsync(request, kind, token);
                if (snapshot.ApprovedAuthorityVersion != authorityVersion || snapshot.AuthorityFrom != authority.EffectiveFrom ||
                    snapshot.AuthorityTo != authority.EffectiveTo) throw Conflict("hr_foundation_policy_authority_changed");
                var versions = (await reader.VerifySourceAsync(kind, new(policy, await repository.VersionsAsync(policy.Id, kind, token)), token)).Versions;
                if (input.ExpectedLatestVersion != snapshot.Command.ExpectedLatestVersion || input.ExpectedLatestVersion != versions.Count ||
                    policy.Revision != input.ExpectedLatestVersion) throw Conflict("hr_foundation_policy_version_conflict");
                if (input.Decision == ApprovalState.Approved)
                {
                    ValidateInput(kind, snapshot.Command, today, authorityVersion);
                    await ValidateDocumentCategoriesAsync(snapshot.Command.Rules, token);
                    ValidateSuccessor(policy, kind, snapshot.Command, versions);
                }
                var evidence = await operation.ProtectAsync(new HrFoundationPolicyDecisionEvidence(1, kind, policy.Id,
                    requestId, snapshot, input, actor.Identity.AccountId.ToString(), actor.RoleCode, actor.Now),
                    HrFoundationPolicyOperation.Purpose(kind, "decision"), token);
                var decision = request.Decide(input.Decision, actor.Identity.AccountId.ToString(), actor.RoleCode, input.Reason,
                    actor.Now, evidence);
                repository.Add(decision);
                PolicyVersion? published = null;
                if (input.Decision == ApprovalState.Approved)
                {
                    published = new(policy.Id, checked(versions.Count + 1), snapshot.Command.EffectiveFrom,
                        snapshot.Command.EffectiveTo, HrFoundationPolicyRulesCodec.Schema,
                        HrFoundationPolicyRulesCodec.Encode(new(1, kind, snapshot.Command.ApprovedVersion, snapshot.Command.Rules, authorityVersion)),
                        request.SnapshotPrivateReference, request.Id, actor.Identity.AccountId.ToString(), actor.Now);
                    policy.AdvanceRevision();
                    repository.Add(published);
                }
                return new(policy.Id, request.Id, input.Decision.ToString().ToLowerInvariant(), published?.Id, decision.Id);
            }, async (actor, token) =>
            {
                ApprovedAuthority(Today(actor.Now));
                var policy = await PolicyAsync(kind, token);
                var request = await RequestAsync(policy, kind, requestId, token);
                if (request.RequesterUserId == actor.Identity.AccountId.ToString()) throw HrFoundationPolicyOperation.Denied();
            }, ct, review: true);
    }

    public Task<HrFoundationPolicyWriteResult> CancelAsync(HrFoundationPolicyKind kind, Guid requestId,
        CancelHrFoundationPolicy input, string key, AuthRequestContext context, CancellationToken ct)
    {
        if (input is null) throw Invalid("invalid_hr_foundation_policy_cancel");
        return operation.WriteAsync(kind, "hr.foundation-policy.publication.cancelled", key, new { requestId, input }, input.Reason,
            context, async (actor, token) =>
            {
                var policy = await PolicyAsync(kind, token);
                ExpectPolicy(policy, kind, input.ExpectedPolicyVersion);
                var request = await RequestAsync(policy, kind, requestId, token);
                repository.ExpectVersion(request, OrganizationOperation.Version(input.ExpectedRequestVersion));
                if (Convert.ToBase64String(request.RowVersion) != input.ExpectedRequestVersion)
                    throw Conflict("hr_foundation_policy_request_version_conflict");
                if (request.State != ApprovalState.Pending) throw Conflict("hr_foundation_policy_request_closed");
                request.Cancel(actor.Identity.AccountId.ToString(), actor.Now);
                return new(policy.Id, request.Id, "cancelled");
            }, async (actor, token) =>
            {
                var policy = await PolicyAsync(kind, token);
                var request = await RequestAsync(policy, kind, requestId, token);
                if (request.RequesterUserId != actor.Identity.AccountId.ToString()) throw HrFoundationPolicyOperation.Denied();
            }, ct);
    }

    private void ExpectPolicy(Policy policy, HrFoundationPolicyKind kind, string? expected)
    {
        Definition(policy, kind);
        var bytes = OrganizationOperation.Version(expected);
        if (Convert.ToBase64String(policy.RowVersion) != expected) throw Conflict("hr_foundation_policy_version_conflict");
        repository.ExpectVersion(policy, bytes);
    }
    private async Task ValidateDocumentCategoriesAsync(HrFoundationPolicyRules rules, CancellationToken ct)
    {
        if (rules.Fields is not { } fields) return;
        foreach (var row in fields.RequiredDocuments)
            if (row.StaffCategoryCode is { } code && !await repository.StaffCategoryCodeExistsAsync(code, ct))
                throw Invalid("hr_foundation_policy_document_category_not_found");
    }
    private static void ValidateInput(HrFoundationPolicyKind kind, RequestHrFoundationPolicy input, DateOnly today, string authorityVersion)
    {
        if (input is null || !HrFoundationPolicyRulesCodec.Text(input.ApprovedVersion, 100) ||
            !HrFoundationPolicyRulesCodec.Text(input.Reason, 2000) || input.ExpectedLatestVersion is < 0 or >= 1024 ||
            !HrFoundationPolicyRulesCodec.Dates(input.EffectiveFrom, input.EffectiveTo) ||
            HrFoundationPolicyRulesCodec.Validate(kind, input.Rules).Count != 0) throw Invalid("invalid_hr_foundation_policy_request");
        if (input.EffectiveFrom < today) throw Conflict("hr_foundation_policy_backdating_not_allowed");
        HrFoundationPolicyRulesCodec.Encode(new(1, kind, input.ApprovedVersion, input.Rules, authorityVersion));
    }
    private static void ValidateSuccessor(Policy policy, HrFoundationPolicyKind kind, RequestHrFoundationPolicy input,
        IReadOnlyList<HrFoundationPolicyCandidate> versions)
    {
        if (policy.Revision != versions.Count || input.ExpectedLatestVersion != versions.Count) throw Conflict("hr_foundation_policy_version_conflict");
        if (versions.Count == 0) return;
        var latest = versions.OrderBy(x => x.Version.VersionNumber).Last().Version;
        var current = HrFoundationPolicyReader.Resolve(kind, latest.EffectiveFrom, new(policy, versions));
        if (current.State != "effective") throw Conflict("hr_foundation_policy_history_invalid");
        if (versions.Any(x => HrFoundationPolicyRulesCodec.Decode(x.Version.RulesJson)?.ApprovedVersion == input.ApprovedVersion))
            throw Conflict("hr_foundation_policy_approved_version_reused");
        // An open predecessor is cut over by its successor. Explicit finite windows may touch or leave a gap, never overlap.
        if (input.EffectiveFrom <= latest.EffectiveFrom || latest.EffectiveTo > input.EffectiveFrom)
            throw Conflict("hr_foundation_policy_effective_overlap");
    }
    private async Task<Policy> PolicyAsync(HrFoundationPolicyKind kind, CancellationToken ct)
    {
        var policy = await repository.PolicyAsync(kind, ct) ?? throw new AuthenticationFailure("hr_foundation_policy_not_found", 404);
        Definition(policy, kind); return policy;
    }
    private async Task<ApprovalRequest> RequestAsync(Policy policy, HrFoundationPolicyKind kind, Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty) throw Invalid("invalid_hr_foundation_policy_request_id");
        var request = await repository.RequestAsync(policy.Id, kind, id, ct) ?? throw new AuthenticationFailure("hr_foundation_policy_request_not_found", 404);
        if (request.SubjectId != policy.Id || request.RequestType != HrFoundationPolicyRulesCodec.RequestType(kind) || request.Stage != 1 || request.RouteRuleId.HasValue)
            throw Conflict("hr_foundation_policy_request_invalid");
        return request;
    }
    private async Task<HrFoundationPolicyProposal> ProposalAsync(ApprovalRequest request, HrFoundationPolicyKind kind, CancellationToken ct)
    {
        var snapshot = await operation.ReadProtectedAsync<HrFoundationPolicyProposal>(request.SnapshotPrivateReference,
            HrFoundationPolicyOperation.Purpose(kind, "proposal"), ct);
        if (snapshot.SchemaVersion != 1 || snapshot.Kind != kind || snapshot.PolicyId != request.SubjectId ||
            snapshot.RequesterId != request.RequesterUserId || snapshot.RequestedAt != request.RequestedAt ||
            !HrFoundationPolicyRulesCodec.Text(snapshot.ApprovedAuthorityVersion, 100) ||
            !HrFoundationPolicyRulesCodec.Dates(snapshot.AuthorityFrom, snapshot.AuthorityTo) ||
            snapshot.Command is null || HrFoundationPolicyRulesCodec.Validate(kind, snapshot.Command.Rules).Count != 0)
            throw Conflict("hr_foundation_policy_proposal_invalid");
        return snapshot;
    }
    private string ApprovedAuthority(DateOnly date)
    {
        if (!HrFoundationPolicyRulesCodec.Text(authority.ApprovedVersion, 100) || !authority.EffectiveFrom.HasValue ||
            !HrFoundationPolicyRulesCodec.Dates(authority.EffectiveFrom.Value, authority.EffectiveTo) ||
            authority.ApprovedReviewMode != "FixedAdminReview" || date < authority.EffectiveFrom || authority.EffectiveTo <= date)
            throw new AuthenticationFailure("hr_foundation_policy_authority_not_configured", 503);
        return authority.ApprovedVersion!;
    }
    private DateOnly Today(DateTimeOffset now)
    {
        try { return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(authentication.BusinessTimeZoneId)).DateTime); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        { throw new AuthenticationFailure("hr_foundation_policy_business_time_zone_not_configured", 503); }
    }
    private static void Page(int page, int size)
    { if (page is < 1 or > 1000000 || size is < 1 or > 100) throw Invalid("invalid_hr_foundation_policy_page"); }
    private static HrFoundationPolicyDefinition Definition(Policy policy, HrFoundationPolicyKind kind)
    {
        if (policy.Category != PolicyCategory.EmployeeException || policy.Code != HrFoundationPolicyRulesCodec.Code(kind) || !policy.IsActive)
            throw Conflict("hr_foundation_policy_definition_invalid");
        return new(policy.Id, kind, policy.Code, Convert.ToBase64String(policy.RowVersion), policy.Revision, policy.IsActive);
    }
    private static HrFoundationPolicyRequestView RequestView(ApprovalRequest request, HrFoundationPolicyKind kind) =>
        new(request.Id, request.SubjectId, kind, request.State, request.RequesterUserId, request.RequestedAt, request.ClosedAt, Convert.ToBase64String(request.RowVersion));
    private static AuthenticationFailure Invalid(string code) => HrFoundationPolicyOperation.Invalid(code);
    private static AuthenticationFailure Conflict(string code) => HrFoundationPolicyOperation.Conflict(code);
}
