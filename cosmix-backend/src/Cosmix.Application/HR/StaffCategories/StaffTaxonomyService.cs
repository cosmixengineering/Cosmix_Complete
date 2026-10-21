using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Organization;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.HR.StaffCategories;

public sealed class StaffTaxonomyService(StaffTaxonomyOperation operation, IStaffTaxonomyRepository repository,
    StaffTaxonomyResolver resolver, StaffTaxonomyAuthority authority, IPrivateFileStore files)
{
    public Task<StaffTaxonomyPolicyView?> PolicyAsync(AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(StaffTaxonomyPermissions.Read, "hr.staff_taxonomy.policy_read", context,
            async (_, token) => (await repository.FindPolicyAsync(token)) is { } policy ? Map(policy) : null, ct);

    public Task<PagedResult<StaffTaxonomyRequestView>> RequestsAsync(int page, int pageSize,
        AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(StaffTaxonomyPermissions.Read, "hr.staff_taxonomy.requests_read", context,
            (_, token) => { Page(page, pageSize); return repository.RequestsAsync(page, pageSize, token); }, ct);

    public Task<StaffTaxonomyRequestDetail> DetailAsync(Guid id, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(StaffTaxonomyPermissions.Read, "hr.staff_taxonomy.request_read", context,
            async (_, token) =>
            {
                var request = await Request(id, token);
                var evidence = await resolver.ReadRequestEvidenceAsync(request, token);
                return new StaffTaxonomyRequestDetail(Map(request), evidence.EffectiveFrom, evidence.EffectiveTo,
                    StaffTaxonomyRulesCodec.Parse(evidence.CanonicalRulesJson), evidence.Reason, evidence.WrittenEvidence, evidence.Authority.ApprovedVersion);
            }, ct);

    public Task<PagedResult<StaffTaxonomyVersionView>> HistoryAsync(int page, int pageSize,
        AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(StaffTaxonomyPermissions.Read, "hr.staff_taxonomy.history_read", context,
            async (_, token) =>
            {
                Page(page, pageSize);
                var rows = (await resolver.VerifiedTimelineAsync(token)).OrderByDescending(s => s.Version.PublishedAt).ThenBy(s => s.Version.Id).ToArray();
                return new PagedResult<StaffTaxonomyVersionView>(rows.Skip((page - 1) * pageSize).Take(pageSize).Select(s =>
                    new StaffTaxonomyVersionView(s.Version.Id, s.Policy.Id, s.Version.VersionNumber, s.Version.EffectiveFrom,
                        s.Version.EffectiveTo, s.Version.RulesJson, StaffTaxonomyRulesCodec.RulesHash(s.Version.RulesJson),
                        s.Version.PublishedByUserId, s.Version.PublishedAt, s.EvidenceValid ? "verified" : "invalid_evidence")).ToArray(), rows.Length, page, pageSize);
            }, ct);

    public Task<StaffTaxonomyResolution> EffectiveAsync(string categoryCode, DateOnly on,
        AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(StaffTaxonomyPermissions.Read, "hr.staff_taxonomy.effective_read", context,
            (_, token) => resolver.ResolveAsync(categoryCode, on, token), ct);

    public Task<StaffTaxonomyWriteResult> RequestAsync(RequestStaffTaxonomy input, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        Validate(input);
        var canonical = StaffTaxonomyRulesCodec.Canonicalize(input.Rules);
        var rules = StaffTaxonomyRulesCodec.Parse(canonical);
        StaffTaxonomyRequestEvidence? submitted = null;
        return operation.WriteAsync(StaffTaxonomyPermissions.Request, "hr.staff_taxonomy.requested", key, input, context,
            (actor, _) => { authority.Require(StaffTaxonomyAuthority.BusinessDate(actor.Now), input.EffectiveFrom); return Task.CompletedTask; },
            async (actor, token) =>
            {
                var today = StaffTaxonomyAuthority.BusinessDate(actor.Now);
                var stamp = authority.Require(today, input.EffectiveFrom);
                Future(input.EffectiveFrom, today);
                if (await repository.HasLockedPeriodAsync(input.EffectiveFrom, input.EffectiveTo, token)) throw Conflict("staff_taxonomy_period_locked");
                var policy = await repository.FindPolicyAsync(token);
                if (policy is null)
                {
                    if (input.ExpectedRevision != 0 || input.ExpectedPolicyVersion is not null) throw Conflict("staff_taxonomy_policy_version_conflict");
                    policy = new Policy(StaffTaxonomyRulesCodec.PolicyCode, "Staff taxonomy", PolicyCategory.EmployeeException);
                    repository.Add(policy);
                }
                else Current(policy, input.ExpectedPolicyVersion, input.ExpectedRevision);
                if (await repository.HasPendingRequestAsync(policy.Id, token)) throw Conflict("staff_taxonomy_request_pending");
                var timeline = await resolver.VerifiedTimelineAsync(token);
                ValidateTimeline(policy, timeline);
                if (timeline.Any(s => s.Version.EffectiveFrom >= input.EffectiveFrom)) throw Conflict("staff_taxonomy_requires_later_effective_date");
                await repository.ValidatePolicyReferencesAsync(rules, input.EffectiveFrom, input.EffectiveTo, token);
                policy.AdvanceRevision();
                var proposal = new StaffTaxonomyRequestEvidence(1, policy.Id, policy.Revision, timeline.Count,
                    TimelineFingerprint(timeline), input.EffectiveFrom, input.EffectiveTo, canonical, input.Reason.Trim(),
                    input.WrittenEvidence.Trim(), actor.Identity.AccountId.ToString(), actor.Now, stamp);
                submitted = proposal;
                var reference = await Protect(proposal, StaffTaxonomyResolver.RequestPurpose(policy.Id), token);
                var request = new ApprovalRequest(StaffTaxonomyRulesCodec.RequestType, policy.Id,
                    actor.Identity.AccountId.ToString(), reference, 1, actor.Now);
                repository.Add(request); await repository.SaveChangesAsync(token);
                return new(Map(policy), Map(request));
            }, ct, beforeCommit: async (_, result, token) =>
            {
                await RevalidateCandidateAsync(result, token);
                if (await repository.HasLockedPeriodAsync(input.EffectiveFrom, input.EffectiveTo, token)) throw Conflict("staff_taxonomy_period_locked");
                await repository.ValidatePolicyReferencesAsync(rules, input.EffectiveFrom, input.EffectiveTo, token);
            }, commitTimeGuard: actor =>
            {
                var today = StaffTaxonomyAuthority.BusinessDate(actor.Now);
                if (submitted is null || authority.Require(today, input.EffectiveFrom) != submitted.Authority)
                    throw Conflict("staff_taxonomy_authority_changed_request_again");
                Future(input.EffectiveFrom, today);
            });
    }

    public Task<StaffTaxonomyWriteResult> DecideAsync(Guid id, DecideStaffTaxonomy input, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        if (id == Guid.Empty || input is null || input.Decision is not (ApprovalState.Approved or ApprovalState.Returned or ApprovalState.Rejected))
            throw new AuthenticationFailure("invalid_staff_taxonomy_decision", 400);
        OrganizationOperation.Version(input.ExpectedPolicyVersion); OrganizationOperation.Version(input.ExpectedRequestVersion);
        StaffTaxonomyRulesCodec.Text(input.Reason, 500); StaffTaxonomyRulesCodec.Text(input.WrittenEvidence, 8000);
        StaffTaxonomyRequestEvidence? deciding = null;
        return operation.WriteAsync(StaffTaxonomyPermissions.Review, "hr.staff_taxonomy.decided", key, new { id, input }, context,
            async (actor, token) =>
            {
                var request = await Request(id, token);
                if (request.RequesterUserId == actor.Identity.AccountId.ToString()) throw new AuthenticationFailure("access_denied", 403);
                var proposal = await resolver.ReadRequestEvidenceAsync(request, token);
                deciding = proposal;
                if (authority.Require(StaffTaxonomyAuthority.BusinessDate(actor.Now), proposal.EffectiveFrom) != proposal.Authority)
                    throw Conflict("staff_taxonomy_authority_changed_request_again");
            }, async (actor, token) =>
            {
                var request = await Request(id, token);
                repository.ExpectVersion(request, OrganizationOperation.Version(input.ExpectedRequestVersion));
                if (request.State != ApprovalState.Pending) throw Conflict("staff_taxonomy_request_closed");
                var proposal = await resolver.ReadRequestEvidenceAsync(request, token);
                var policy = await repository.FindPolicyAsync(token) ?? throw Missing();
                Current(policy, input.ExpectedPolicyVersion, proposal.PolicyRevision);
                if (request.SubjectId != policy.Id) throw Conflict("staff_taxonomy_request_subject_conflict");
                var timeline = await resolver.VerifiedTimelineAsync(token);
                ValidateTimeline(policy, timeline);
                if (timeline.Count != proposal.PublishedRevision || TimelineFingerprint(timeline) != proposal.TimelineFingerprint)
                    throw Conflict("staff_taxonomy_publication_changed_request_again");
                if (input.Decision == ApprovalState.Approved)
                {
                    Future(proposal.EffectiveFrom, StaffTaxonomyAuthority.BusinessDate(actor.Now));
                    if (await repository.HasLockedPeriodAsync(proposal.EffectiveFrom, proposal.EffectiveTo, token)) throw Conflict("staff_taxonomy_period_locked");
                    await repository.ValidatePolicyReferencesAsync(StaffTaxonomyRulesCodec.Parse(proposal.CanonicalRulesJson), proposal.EffectiveFrom, proposal.EffectiveTo, token);
                }
                var proof = new StaffTaxonomyDecisionEvidence(1, request.Id, policy.Id,
                    StaffTaxonomyResolver.ParseReference(request.SnapshotPrivateReference).Sha256, input.Decision,
                    actor.Identity.AccountId.ToString(), actor.RoleCode, actor.RoleAssignmentId, actor.Now,
                    input.Reason.Trim(), input.WrittenEvidence.Trim(), proposal.Authority);
                var reference = await Protect(proof, StaffTaxonomyResolver.DecisionPurpose(policy.Id), token);
                var decision = request.Decide(input.Decision, actor.Identity.AccountId.ToString(), actor.RoleCode,
                    input.Reason.Trim(), actor.Now, reference);
                repository.Add(decision);
                PolicyVersion? version = null;
                if (input.Decision == ApprovalState.Approved)
                {
                    version = new PolicyVersion(policy.Id, checked(proposal.PublishedRevision + 1), proposal.EffectiveFrom,
                        proposal.EffectiveTo, StaffTaxonomyRulesCodec.Schema, proposal.CanonicalRulesJson,
                        request.SnapshotPrivateReference, request.Id, actor.Identity.AccountId.ToString(), actor.Now,
                        PolicyAuthority.ApprovedWorkflow);
                    repository.Add(version);
                }
                policy.AdvanceRevision(); await repository.SaveChangesAsync(token);
                return new(Map(policy), Map(request), version?.Id);
            }, ct, fixedAdministrator: true, beforeCommit: async (_, result, token) =>
            {
                await RevalidateCandidateAsync(result, token);
                if (deciding is null) throw Conflict("staff_taxonomy_candidate_changed");
                if (input.Decision == ApprovalState.Approved)
                {
                    if (await repository.HasLockedPeriodAsync(deciding.EffectiveFrom, deciding.EffectiveTo, token)) throw Conflict("staff_taxonomy_period_locked");
                    await repository.ValidatePolicyReferencesAsync(StaffTaxonomyRulesCodec.Parse(deciding.CanonicalRulesJson), deciding.EffectiveFrom, deciding.EffectiveTo, token);
                    var rows = await repository.TimelineAsync(token);
                    if (!rows.Any(s => s.Version.Id == result.PublishedVersionId && s.Version.ApprovalRequestId == id &&
                        s.Version.RulesJson == deciding.CanonicalRulesJson && s.Version.PolicyId == result.Policy.Id))
                        throw Conflict("staff_taxonomy_candidate_changed");
                }
            }, commitTimeGuard: actor =>
            {
                if (deciding is null || authority.Require(StaffTaxonomyAuthority.BusinessDate(actor.Now), deciding.EffectiveFrom) != deciding.Authority)
                    throw Conflict("staff_taxonomy_authority_changed_request_again");
                if (input.Decision == ApprovalState.Approved) Future(deciding.EffectiveFrom, StaffTaxonomyAuthority.BusinessDate(actor.Now));
            });
    }

    public Task<StaffTaxonomyWriteResult> CancelAsync(Guid id, CancelStaffTaxonomy input, string key,
        AuthRequestContext context, CancellationToken ct)
    {
        if (id == Guid.Empty || input is null) throw new AuthenticationFailure("invalid_staff_taxonomy_cancel", 400);
        OrganizationOperation.Version(input.ExpectedPolicyVersion); OrganizationOperation.Version(input.ExpectedRequestVersion);
        StaffTaxonomyRulesCodec.Text(input.Reason, 500); StaffTaxonomyRulesCodec.Text(input.WrittenEvidence, 8000);
        return operation.WriteAsync(StaffTaxonomyPermissions.Request, "hr.staff_taxonomy.cancelled", key, new { id, input }, context,
            async (actor, token) =>
            {
                var request = await Request(id, token);
                if (request.RequesterUserId != actor.Identity.AccountId.ToString()) throw new AuthenticationFailure("access_denied", 403);
                await resolver.ReadRequestEvidenceAsync(request, token);
            }, async (actor, token) =>
            {
                var request = await Request(id, token);
                repository.ExpectVersion(request, OrganizationOperation.Version(input.ExpectedRequestVersion));
                if (request.State != ApprovalState.Pending) throw Conflict("staff_taxonomy_request_closed");
                var policy = await repository.FindPolicyAsync(token) ?? throw Missing();
                if (request.SubjectId != policy.Id || policy.Category != PolicyCategory.EmployeeException ||
                    policy.Code != StaffTaxonomyRulesCodec.PolicyCode) throw Conflict("staff_taxonomy_request_subject_conflict");
                repository.ExpectVersion(policy, OrganizationOperation.Version(input.ExpectedPolicyVersion));
                // Cancellation publishes nothing. It remains available to the requester after authority expiry/replacement.
                // Required cancellation reason/proof is retained by the protected operation audit.
                request.Cancel(actor.Identity.AccountId.ToString(), actor.Now); policy.AdvanceRevision();
                await repository.SaveChangesAsync(token);
                return new(Map(policy), Map(request));
            }, ct, beforeCommit: (_, result, token) => RevalidateCandidateAsync(result, token, allowInactive: true));
    }

    private async Task RevalidateCandidateAsync(StaffTaxonomyWriteResult result, CancellationToken ct, bool allowInactive = false)
    {
        var policy = await repository.FindPolicyAsync(ct) ?? throw Missing();
        var request = await Request(result.Request.Id, ct);
        if ((!allowInactive && !policy.IsActive) || policy.Id != result.Policy.Id || policy.Revision != result.Policy.Revision ||
            Convert.ToBase64String(policy.RowVersion) != result.Policy.Version || request.SubjectId != policy.Id ||
            Convert.ToBase64String(request.RowVersion) != result.Request.Version || request.State.ToString() != result.Request.State ||
            request.RequesterUserId != result.Request.RequesterId || request.RequestedAt != result.Request.RequestedAt || request.ClosedAt != result.Request.ClosedAt)
            throw Conflict("staff_taxonomy_candidate_changed");
    }

    public static string TimelineFingerprint(IReadOnlyList<StaffTaxonomyVersionSource> rows) =>
        StaffTaxonomyRulesCodec.Hash(rows.OrderBy(s => s.Version.VersionNumber).ThenBy(s => s.Version.Id).Select(s =>
            new { s.Policy.Id, s.Policy.Code, VersionId = s.Version.Id, s.Version.VersionNumber,
                s.Version.EffectiveFrom, s.Version.EffectiveTo, s.Version.RulesSchemaVersion, s.Version.RulesJson,
                s.Version.ApprovalRequestId, s.Version.PublishedByUserId, s.Version.PublishedAt }).ToArray());

    private async Task<ApprovalRequest> Request(Guid id, CancellationToken ct) => id == Guid.Empty ? throw Missing() :
        await repository.FindRequestAsync(id, ct) ?? throw Missing();

    private void Current(Policy policy, string? expectedVersion, int expectedRevision)
    {
        var bytes = OrganizationOperation.Version(expectedVersion);
        if (!policy.IsActive || policy.Category != PolicyCategory.EmployeeException || policy.Code != StaffTaxonomyRulesCodec.PolicyCode ||
            policy.Revision != expectedRevision || !policy.RowVersion.SequenceEqual(bytes)) throw Conflict("staff_taxonomy_policy_version_conflict");
        repository.ExpectVersion(policy, bytes);
    }

    private static void ValidateTimeline(Policy policy, IReadOnlyList<StaffTaxonomyVersionSource> rows)
    {
        if (rows.Any(s => s.Policy.Id != policy.Id || !s.EvidenceValid || s.Version.RulesSchemaVersion != StaffTaxonomyRulesCodec.Schema) ||
            !rows.OrderBy(s => s.Version.VersionNumber).Select(s => s.Version.VersionNumber).SequenceEqual(Enumerable.Range(1, rows.Count)) ||
            rows.Select(s => s.Version.EffectiveFrom).Distinct().Count() != rows.Count ||
            !rows.OrderBy(s => s.Version.VersionNumber).Select(s => s.Version.EffectiveFrom).SequenceEqual(rows.Select(s => s.Version.EffectiveFrom).Order()))
            throw Conflict("staff_taxonomy_history_conflict");
    }

    private async Task<string> Protect<T>(T value, string purpose, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        try
        {
            var reference = await files.WriteAsync(bytes, purpose, ct);
            var json = JsonSerializer.Serialize(reference);
            StaffTaxonomyResolver.ParseReference(json);
            if (reference.SizeBytes != bytes.Length || reference.Sha256 != Convert.ToHexString(SHA256.HashData(bytes)))
                throw Conflict("staff_taxonomy_private_write_invalid");
            return json;
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    private static void Validate(RequestStaffTaxonomy input)
    {
        if (input is null || input.ExpectedRevision < 0) throw new AuthenticationFailure("invalid_staff_taxonomy_request", 400);
        if (input.ExpectedPolicyVersion is not null) OrganizationOperation.Version(input.ExpectedPolicyVersion);
        StaffTaxonomyRulesCodec.ValidateDates(input.EffectiveFrom, input.EffectiveTo);
        StaffTaxonomyRulesCodec.Text(input.Reason, 500); StaffTaxonomyRulesCodec.Text(input.WrittenEvidence, 8000);
    }
    private static void Future(DateOnly from, DateOnly today)
    { if (from <= today) throw Conflict("staff_taxonomy_future_date_required"); }
    private static void Page(int page, int pageSize)
    { if (page is < 1 or > 10000 || pageSize is < 1 or > 100) throw new AuthenticationFailure("invalid_staff_taxonomy_page", 400); }
    private static StaffTaxonomyPolicyView Map(Policy policy) => new(policy.Id, policy.Code, policy.Revision, Convert.ToBase64String(policy.RowVersion));
    private static StaffTaxonomyRequestView Map(ApprovalRequest request) => new(request.Id, request.SubjectId,
        request.State.ToString(), Convert.ToBase64String(request.RowVersion), request.RequesterUserId, request.RequestedAt, request.ClosedAt);
    private static AuthenticationFailure Conflict(string code) => new(code, 409);
    private static AuthenticationFailure Missing() => new("staff_taxonomy_resource_not_found", 404);
}
