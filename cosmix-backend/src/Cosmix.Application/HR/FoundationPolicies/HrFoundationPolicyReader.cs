using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.HR.FoundationPolicies;

/// <summary>Resolves the dated successor chain. Expiry, a gap or invalid persisted data never revives an older version.</summary>
public sealed class HrFoundationPolicyReader(IHrFoundationPolicyRepository repository, IPrivateFileStore files) : IHrFoundationPolicyReader
{
    public async Task<HrFoundationPolicyResolution<HrFoundationFieldRules>> GetFieldsAsync(DateOnly date, CancellationToken ct) =>
        Project(await ReadAsync(HrFoundationPolicyKind.Fields, date, ct), x => x.Fields);
    public async Task<HrFoundationPolicyResolution<HrFoundationLifecycleRules>> GetLifecycleAsync(DateOnly date, CancellationToken ct) =>
        Project(await ReadAsync(HrFoundationPolicyKind.Lifecycle, date, ct), x => x.Lifecycle);
    public async Task<HrFoundationPolicyResolution<HrFoundationRecordRules>> GetRecordsAsync(DateOnly date, CancellationToken ct) =>
        Project(await ReadAsync(HrFoundationPolicyKind.Records, date, ct), x => x.Records);
    private static HrFoundationPolicyResolution<T> Project<T>(HrFoundationPolicyResolution<HrFoundationPolicyRules> result,
        Func<HrFoundationPolicyRules, T?> select) where T : class =>
        new(result.State, result.Rules is null ? null : select(result.Rules), result.Identity, result.UnavailableReason);
    public async Task<HrFoundationPolicyResolution<HrFoundationPolicyRules>> ReadAsync(HrFoundationPolicyKind kind,
        DateOnly date, CancellationToken ct)
    {
        var policy = await repository.PolicyAsync(kind, ct);
        var source = new HrFoundationPolicySource(policy, policy is null ? [] : await repository.VersionsAsync(policy.Id, kind, ct));
        return Resolve(kind, date, await VerifySourceAsync(kind, source, ct));
    }
    public async Task<HrFoundationPolicySource> VerifySourceAsync(HrFoundationPolicyKind kind, HrFoundationPolicySource source, CancellationToken ct)
    {
        var verified = new List<HrFoundationPolicyCandidate>();
        foreach (var candidate in source.Versions)
            verified.Add(candidate with { AuthorityValid = candidate.AuthorityValid && await EvidenceValidAsync(kind, candidate, ct) });
        return source with { Versions = verified };
    }
    private async Task<bool> EvidenceValidAsync(HrFoundationPolicyKind kind, HrFoundationPolicyCandidate candidate, CancellationToken ct)
    {
        var row = candidate.Version; var request = candidate.Request; var decision = candidate.Decision;
        if (request is null || decision is null || request.Id != row.ApprovalRequestId || request.SubjectId != row.PolicyId ||
            request.RequestType != HrFoundationPolicyRulesCodec.RequestType(kind) || request.Stage != 1 || request.RouteRuleId.HasValue ||
            request.State != ApprovalState.Approved || request.RequesterUserId == row.PublishedByUserId ||
            request.SnapshotPrivateReference != row.PolicyDocumentPrivateReference || request.ClosedAt != row.PublishedAt ||
            decision.ApprovalRequestId != request.Id || decision.Decision != ApprovalState.Approved ||
            decision.ActorUserId != row.PublishedByUserId || decision.DecidedAt != row.PublishedAt ||
            decision.ActorRoleCode is not ("ADMIN" or "SUPERADMIN") || decision.ProtectedChangeReference is null) return false;
        try
        {
            var proposal = await ReadAsync<HrFoundationPolicyProposal>(request.SnapshotPrivateReference,
                HrFoundationPolicyOperation.Purpose(kind, "proposal"), ct);
            var evidence = await ReadAsync<HrFoundationPolicyDecisionEvidence>(decision.ProtectedChangeReference,
                HrFoundationPolicyOperation.Purpose(kind, "decision"), ct);
            var decoded = HrFoundationPolicyRulesCodec.Decode(row.RulesJson);
            if (proposal is null || evidence is null || decoded is null || proposal.Command is null ||
                proposal.SchemaVersion != 1 || proposal.Kind != kind || proposal.PolicyId != row.PolicyId ||
                proposal.RequesterId != request.RequesterUserId || proposal.RequestedAt != request.RequestedAt ||
                !HrFoundationPolicyRulesCodec.Dates(proposal.AuthorityFrom, proposal.AuthorityTo) ||
                proposal.Command.ApprovedVersion != decoded.ApprovedVersion || proposal.Command.EffectiveFrom != row.EffectiveFrom ||
                proposal.Command.EffectiveTo != row.EffectiveTo || proposal.Command.ExpectedLatestVersion != row.VersionNumber - 1 ||
                proposal.ApprovedAuthorityVersion != decoded.ApprovedAuthorityVersion ||
                evidence.SchemaVersion != 1 || evidence.Kind != kind || evidence.PolicyId != row.PolicyId || evidence.RequestId != request.Id ||
                evidence.ActorId != decision.ActorUserId || evidence.RoleCode != decision.ActorRoleCode || evidence.Now != decision.DecidedAt ||
                evidence.Input is null || evidence.Input.Decision != ApprovalState.Approved || evidence.Input.Reason != decision.Reason ||
                evidence.Input.ExpectedLatestVersion != proposal.Command.ExpectedLatestVersion ||
                JsonSerializer.Serialize(evidence.Snapshot, HrFoundationPolicyRulesCodec.JsonOptions) != JsonSerializer.Serialize(proposal, HrFoundationPolicyRulesCodec.JsonOptions))
                return false;
            return HrFoundationPolicyRulesCodec.Encode(new(1, kind, proposal.Command.ApprovedVersion, proposal.Command.Rules,
                proposal.ApprovedAuthorityVersion)) == HrFoundationPolicyRulesCodec.Encode(decoded);
        }
        catch (Exception ex) when (ex is JsonException or AuthenticationFailure or IOException or FormatException or KeyNotFoundException)
        { return false; }
    }
    private async Task<T?> ReadAsync<T>(string value, string purpose, CancellationToken ct)
    {
        var reference = JsonSerializer.Deserialize<PrivateFileReference>(value);
        if (reference is null || reference.SizeBytes is < 1 or > 65536) return default;
        var bytes = await files.ReadAsync(reference, purpose, ct);
        if (bytes.Length != reference.SizeBytes || Convert.ToHexString(SHA256.HashData(bytes)) != reference.Sha256) return default;
        return JsonSerializer.Deserialize<T>(bytes, HrFoundationPolicyRulesCodec.JsonOptions);
    }
    public static HrFoundationPolicyResolution<HrFoundationPolicyRules> Resolve(HrFoundationPolicyKind kind,
        DateOnly date, HrFoundationPolicySource source)
    {
        HrFoundationPolicyResolution<HrFoundationPolicyRules> Unavailable(string reason) => new("unavailable", null, null, reason);
        if (!Enum.IsDefined(kind) || date == default || date == DateOnly.MaxValue) return Unavailable("invalid_selected_date_or_kind");
        if (source.Policy is null) return source.Versions.Count == 0 ? new("none", null, null, null) : Unavailable("invalid_policy_source");
        var policy = source.Policy;
        if (policy.Code != HrFoundationPolicyRulesCodec.Code(kind)) return Unavailable("invalid_policy_definition");
        var versions = source.Versions.OrderBy(x => x.Version.VersionNumber).ToArray();
        // A first pending proposal must not disable the configuration baseline. A publication, even future/invalid,
        // changes this to fail closed; an unapproved proposal alone has no such authority.
        if (versions.Length == 0) return new("none", null, null, null);
        if (policy.Category != PolicyCategory.EmployeeException || !policy.IsActive) return Unavailable("invalid_policy_definition");
        if (versions.Length > 1024 || policy.Revision != versions.Length || versions.Select(x => x.Version.Id).Distinct().Count() != versions.Length)
            return Unavailable("invalid_policy_chain");
        var rules = new List<HrFoundationPublishedRules>();
        for (var i = 0; i < versions.Length; i++)
        {
            var row = versions[i].Version;
            var decoded = HrFoundationPolicyRulesCodec.Decode(row.RulesJson);
            if (row.PolicyId != policy.Id || row.VersionNumber != i + 1 || row.RulesSchemaVersion != HrFoundationPolicyRulesCodec.Schema ||
                row.Authority != PolicyAuthority.ApprovedWorkflow || !row.ApprovalRequestId.HasValue || !versions[i].AuthorityValid ||
                !HrFoundationPolicyRulesCodec.Dates(row.EffectiveFrom, row.EffectiveTo) || decoded is null || decoded.Kind != kind ||
                i > 0 && (row.EffectiveFrom <= versions[i - 1].Version.EffectiveFrom ||
                    versions[i - 1].Version.EffectiveTo > row.EffectiveFrom)) return Unavailable("invalid_policy_chain_or_authority");
            rules.Add(decoded);
        }
        var selected = Array.FindLastIndex(versions, x => x.Version.EffectiveFrom <= date);
        if (selected < 0) return Unavailable("policy_not_yet_effective");
        var version = versions[selected].Version;
        // Select the latest started successor first, then check its end. Do not search for any still-open older row.
        if (version.EffectiveTo <= date) return Unavailable("policy_expired_or_gap");
        var end = version.EffectiveTo;
        if (selected + 1 < versions.Length && (!end.HasValue || versions[selected + 1].Version.EffectiveFrom < end))
            end = versions[selected + 1].Version.EffectiveFrom;
        return new("effective", rules[selected].Rules,
            new(policy.Id, version.Id, version.VersionNumber, rules[selected].ApprovedVersion, version.EffectiveFrom, end), null);
    }
}
