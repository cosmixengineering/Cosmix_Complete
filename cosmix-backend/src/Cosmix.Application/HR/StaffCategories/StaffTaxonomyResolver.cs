using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.HR.StaffCategories;

/// <summary>Call inside the caller's authorization/consistency transaction. Mapping is never a pay or access grant.</summary>
public sealed class StaffTaxonomyResolver(IStaffTaxonomyRepository repository, IPrivateFileStore files) : IStaffTaxonomyResolver
{
    public async Task<StaffTaxonomyResolution> ResolveAsync(string categoryCode, DateOnly on, CancellationToken ct)
    {
        var exists = await repository.NamespaceExistsAsync(ct);
        return Resolve(categoryCode, on, await VerifiedTimelineAsync(ct), exists);
    }

    public async Task<StaffTaxonomyResolution> ResolveForRangeAsync(string categoryCode, DateOnly from,
        DateOnly toExclusive, CancellationToken ct)
    {
        StaffTaxonomyRulesCodec.ValidateDates(from, toExclusive);
        var exists = await repository.NamespaceExistsAsync(ct);
        var sources = await VerifiedTimelineAsync(ct);
        var first = Resolve(categoryCode, from, sources, exists);
        if (!first.Available) return first;
        var boundaries = sources.SelectMany(s => new[] { (DateOnly?)s.Version.EffectiveFrom, s.Version.EffectiveTo })
            .Where(d => d.HasValue && from < d && d < toExclusive).Select(d => d!.Value).Distinct();
        foreach (var date in boundaries)
        {
            var next = Resolve(categoryCode, date, sources, exists);
            if (!next.Available || next.Fingerprint != first.Fingerprint)
                return new("mapping_changes_during_interval", null, null,
                    StaffTaxonomyRulesCodec.Hash(new { categoryCode, from, toExclusive, first.Fingerprint, Next = next.Fingerprint }));
        }
        return first;
    }

    public static StaffTaxonomyResolution Resolve(string categoryCode, DateOnly on,
        IReadOnlyList<StaffTaxonomyVersionSource> sources, bool namespaceExists = false)
    {
        StaffTaxonomyRulesCodec.ValidateDate(on);
        if (string.IsNullOrWhiteSpace(categoryCode) || categoryCode.Length > 30 || categoryCode != categoryCode.Trim())
            throw new AuthenticationFailure("invalid_staff_category_code", 400);
        var code = categoryCode.ToUpperInvariant();
        var effective = sources.Where(s => s.Policy.IsActive && s.Version.EffectiveFrom <= on)
            .GroupBy(s => s.Policy.Id).SelectMany(g =>
            {
                var from = g.Max(s => s.Version.EffectiveFrom);
                return g.Where(s => s.Version.EffectiveFrom == from && (s.Version.EffectiveTo is null || on < s.Version.EffectiveTo));
            }).ToArray();
        StaffTaxonomyResolution Failure(string state) => new(state, null, null,
            StaffTaxonomyRulesCodec.Hash(new { state, code, Sources = effective.Select(s => s.Version.Id).Order() }));
        if (effective.Length == 0)
        {
            if (sources.Count == 0 && !namespaceExists) return Baseline(code);
            // A first future publication does not rewrite the already-confirmed historical Office/Site rules.
            // This narrow exception never applies after a version starts, or to invalid/inactive/unknown history.
            var futureHistory = sources.OrderBy(s => s.Version.VersionNumber).ToArray();
            if (futureHistory.Length > 0 && on < futureHistory[0].Version.EffectiveFrom &&
                futureHistory.Select(s => s.Policy.Id).Distinct().Count() == 1 &&
                futureHistory.All(s => s.EvidenceValid && s.Policy.IsActive && s.Policy.Code == StaffTaxonomyRulesCodec.PolicyCode &&
                    s.Policy.Category == PolicyCategory.EmployeeException && s.Version.RulesSchemaVersion == StaffTaxonomyRulesCodec.Schema) &&
                futureHistory.Select(s => s.Version.VersionNumber).SequenceEqual(Enumerable.Range(1, futureHistory.Length)) &&
                futureHistory.Select(s => s.Version.EffectiveFrom).Distinct().Count() == futureHistory.Length &&
                futureHistory.Select(s => s.Version.EffectiveFrom).SequenceEqual(futureHistory.Select(s => s.Version.EffectiveFrom).Order()))
            {
                try
                {
                    foreach (var row in futureHistory) StaffTaxonomyRulesCodec.Parse(row.Version.RulesJson);
                    return Baseline(code); // Exact original fingerprint/metadata, no fabricated version or dates.
                }
                catch (AuthenticationFailure) { return Failure("invalid_taxonomy_rules"); }
            }
            return Failure("taxonomy_not_effective");
        }
        if (effective.Length != 1) return Failure("ambiguous_taxonomy");
        var source = effective[0];
        if (!source.EvidenceValid || source.Policy.Category != PolicyCategory.EmployeeException ||
            source.Policy.Code != StaffTaxonomyRulesCodec.PolicyCode || source.Version.PolicyId != source.Policy.Id ||
            source.Version.RulesSchemaVersion != StaffTaxonomyRulesCodec.Schema)
            return Failure("invalid_taxonomy_evidence");
        var history = sources.Where(s => s.Policy.Id == source.Policy.Id && s.Version.VersionNumber <= source.Version.VersionNumber)
            .OrderBy(s => s.Version.VersionNumber).ToArray();
        if (history.Length != source.Version.VersionNumber ||
            !history.Select(s => s.Version.VersionNumber).SequenceEqual(Enumerable.Range(1, history.Length)) ||
            history.Any(s => !s.EvidenceValid || s.Version.RulesSchemaVersion != StaffTaxonomyRulesCodec.Schema ||
                s.Policy.Code != StaffTaxonomyRulesCodec.PolicyCode || s.Policy.Category != PolicyCategory.EmployeeException) ||
            history.Select(s => s.Version.EffectiveFrom).Distinct().Count() != history.Length ||
            !history.Select(s => s.Version.EffectiveFrom).SequenceEqual(history.Select(s => s.Version.EffectiveFrom).Order()))
            return Failure("taxonomy_history_conflict");
        StaffTaxonomyRules rules;
        try { rules = StaffTaxonomyRulesCodec.Parse(source.Version.RulesJson); }
        catch (AuthenticationFailure) { return Failure("invalid_taxonomy_rules"); }
        var mapping = rules.Categories.SingleOrDefault(c => c.Code == code);
        if (mapping is null) return Failure("category_not_configured");
        return new("effective", mapping, source.Version.Id,
            StaffTaxonomyRulesCodec.Hash(new { source.Policy.Id, VersionId = source.Version.Id,
                source.Version.VersionNumber, source.Version.EffectiveFrom, source.Version.EffectiveTo,
                source.Version.RulesSchemaVersion, Canonical = StaffTaxonomyRulesCodec.Canonicalize(rules), mapping }),
            EffectiveFrom: source.Version.EffectiveFrom, EffectiveTo: source.Version.EffectiveTo);
    }

    public static StaffCategoryMapping RequireMapping(StaffTaxonomyResolution result) =>
        result.Available && result.Mapping is not null ? result.Mapping :
        throw new AuthenticationFailure("staff_taxonomy_" + result.State, 503);

    public static IReadOnlyList<string> MissingLocationFields(StaffTaxonomyResolution result, Guid? branchId,
        Guid? siteId, StaffTaxonomyValidationPhase phase)
    {
        ValidateLocation(result, branchId, siteId, StaffTaxonomyValidationPhase.Draft);
        if (!Enum.IsDefined(phase)) throw new AuthenticationFailure("invalid_staff_taxonomy_validation_phase", 400);
        if (phase == StaffTaxonomyValidationPhase.Draft) return [];
        var mapping = RequireMapping(result);
        if (mapping.WorkLocation == StaffWorkLocation.Office && branchId is null) return ["branchId"];
        if (mapping.WorkLocation == StaffWorkLocation.Site && siteId is null &&
            (phase == StaffTaxonomyValidationPhase.ScheduledDuty || mapping.RequireSiteBeforeActivation)) return ["siteId"];
        return [];
    }

    public static void ValidateLocation(StaffTaxonomyResolution result, Guid? branchId, Guid? siteId,
        StaffTaxonomyValidationPhase phase)
    {
        if (!Enum.IsDefined(phase) || branchId == Guid.Empty || siteId == Guid.Empty)
            throw new AuthenticationFailure("invalid_staff_taxonomy_location", 400);
        var mapping = RequireMapping(result);
        if (mapping.WorkLocation == StaffWorkLocation.Office && siteId.HasValue ||
            mapping.WorkLocation == StaffWorkLocation.Site && branchId.HasValue)
            throw new AuthenticationFailure("staff_taxonomy_location_conflict", 400);
        if (phase != StaffTaxonomyValidationPhase.Draft &&
            (mapping.WorkLocation == StaffWorkLocation.Office && branchId is null ||
             mapping.WorkLocation == StaffWorkLocation.Site && siteId is null &&
             (phase == StaffTaxonomyValidationPhase.ScheduledDuty || mapping.RequireSiteBeforeActivation)))
            throw new AuthenticationFailure("staff_taxonomy_location_required", 400);
    }

    public async Task<IReadOnlyList<StaffTaxonomyVersionSource>> VerifiedTimelineAsync(CancellationToken ct)
    {
        var sources = await repository.TimelineAsync(ct);
        var verified = new List<StaffTaxonomyVersionSource>();
        foreach (var source in sources)
        {
            var valid = false;
            if (source.Request is { State: ApprovalState.Approved } request && source.Decisions.Count == 1 &&
                source.Version.Authority == PolicyAuthority.ApprovedWorkflow && source.Version.ApprovalRequestId == request.Id)
            {
                try
                {
                    var proposal = await ReadRequestEvidenceAsync(request, ct);
                    var decision = source.Decisions[0];
                    var proof = await ReadPrivateAsync<StaffTaxonomyDecisionEvidence>(decision.ProtectedChangeReference,
                        DecisionPurpose(request.SubjectId), ct);
                    var requestRef = ParseReference(request.SnapshotPrivateReference);
                    valid = decision.Decision == ApprovalState.Approved && request.ClosedAt == decision.DecidedAt &&
                        request.SubjectId == source.Policy.Id && source.Version.PolicyId == source.Policy.Id &&
                        source.Version.PublishedAt == decision.DecidedAt && source.Version.PublishedByUserId == decision.ActorUserId &&
                        source.Version.RulesSchemaVersion == StaffTaxonomyRulesCodec.Schema &&
                        source.Version.RulesJson == proposal.CanonicalRulesJson && source.Version.EffectiveFrom == proposal.EffectiveFrom &&
                        source.Version.EffectiveTo == proposal.EffectiveTo && source.Version.VersionNumber == proposal.PublishedRevision + 1 &&
                        proposal.TimelineFingerprint == StaffTaxonomyService.TimelineFingerprint(sources.Where(s =>
                            s.Policy.Id == source.Policy.Id && s.Version.VersionNumber < source.Version.VersionNumber).ToArray()) &&
                        source.Version.PolicyDocumentPrivateReference == request.SnapshotPrivateReference &&
                        proof.SchemaVersion == 1 && proof.RequestId == request.Id && proof.PolicyId == source.Policy.Id &&
                        proof.RequestEvidenceSha256 == requestRef.Sha256 && proof.Decision == decision.Decision &&
                        proof.ActorId == decision.ActorUserId && proof.ActorId != request.RequesterUserId &&
                        proof.ActorRoleCode == decision.ActorRoleCode && proof.ActorRoleCode is "ADMIN" or "SUPERADMIN" &&
                        proof.RoleAssignmentId != Guid.Empty && proof.DecidedAt == decision.DecidedAt &&
                        proof.Reason == decision.Reason && proof.Authority == proposal.Authority &&
                        StaffTaxonomyAuthority.Valid(proof.Authority, StaffTaxonomyAuthority.BusinessDate(proof.DecidedAt), proposal.EffectiveFrom);
                    StaffTaxonomyRulesCodec.Text(proof.WrittenEvidence, 8000);
                    StaffTaxonomyRulesCodec.Text(proof.Reason, 500);
                }
                catch (AuthenticationFailure e) when (e.Code is "staff_taxonomy_evidence_invalid" or "invalid_staff_taxonomy_rules" or "staff_taxonomy_confirmed_baseline_changed")
                { valid = false; }
            }
            verified.Add(source with { EvidenceValid = valid });
        }
        // A successor's proof is not a complete publication chain if a predecessor's proof is unavailable.
        // Retain every invalid row; never discard it and accidentally expose legacy fallback.
        var complete = new Dictionary<Guid, bool>();
        foreach (var group in verified.GroupBy(s => s.Policy.Id))
        {
            var ordered = group.OrderBy(s => s.Version.VersionNumber).ToArray();
            var chainValid = true;
            DateOnly? previousStart = null;
            for (var index = 0; index < ordered.Length; index++)
            {
                var row = ordered[index];
                chainValid = chainValid && row.EvidenceValid && row.Version.VersionNumber == index + 1 &&
                    row.Policy.Code == StaffTaxonomyRulesCodec.PolicyCode && row.Policy.Category == PolicyCategory.EmployeeException &&
                    (!previousStart.HasValue || row.Version.EffectiveFrom > previousStart);
                complete[row.Version.Id] = chainValid;
                previousStart = row.Version.EffectiveFrom;
            }
        }
        return verified.Select(s => s with { EvidenceValid = complete[s.Version.Id] }).ToArray();
    }

    public async Task<StaffTaxonomyRequestEvidence> ReadRequestEvidenceAsync(ApprovalRequest request, CancellationToken ct)
    {
        if (request.RequestType != StaffTaxonomyRulesCodec.RequestType || request.Stage != 1 || request.RouteRuleId is not null)
            throw EvidenceInvalid();
        var proof = await ReadPrivateAsync<StaffTaxonomyRequestEvidence>(request.SnapshotPrivateReference, RequestPurpose(request.SubjectId), ct);
        if (proof.SchemaVersion != 1 || proof.PolicyId != request.SubjectId || proof.RequesterId != request.RequesterUserId ||
            proof.RequestedAt != request.RequestedAt || proof.PolicyRevision < 1 || proof.PublishedRevision < 0 ||
            string.IsNullOrWhiteSpace(proof.TimelineFingerprint) || proof.TimelineFingerprint.Length != 64 ||
            proof.TimelineFingerprint.Any(c => !Uri.IsHexDigit(c)) ||
            proof.CanonicalRulesJson != StaffTaxonomyRulesCodec.Canonicalize(StaffTaxonomyRulesCodec.Parse(proof.CanonicalRulesJson)) ||
            !StaffTaxonomyAuthority.Valid(proof.Authority, StaffTaxonomyAuthority.BusinessDate(proof.RequestedAt), proof.EffectiveFrom)) throw EvidenceInvalid();
        StaffTaxonomyRulesCodec.ValidateDates(proof.EffectiveFrom, proof.EffectiveTo);
        StaffTaxonomyRulesCodec.Text(proof.Reason, 500); StaffTaxonomyRulesCodec.Text(proof.WrittenEvidence, 8000);
        return proof;
    }

    public static string RequestPurpose(Guid policyId) => $"hr/staff-taxonomy/{policyId:N}/request";
    public static string DecisionPurpose(Guid policyId) => $"hr/staff-taxonomy/{policyId:N}/decision";

    public static PrivateFileReference ParseReference(string? json)
    {
        try
        {
            var value = json is null ? null : JsonSerializer.Deserialize<PrivateFileReference>(json);
            if (json is null || json.Length > 500 || value is null || string.IsNullOrWhiteSpace(value.StorageKey) ||
                string.IsNullOrWhiteSpace(value.ObjectVersion) || string.IsNullOrWhiteSpace(value.KeyReference) ||
                value.SizeBytes is < 1 or > 262144 || string.IsNullOrWhiteSpace(value.Sha256) ||
                value.Sha256.Length != 64 || value.Sha256.Any(c => !Uri.IsHexDigit(c))) throw EvidenceInvalid();
            return value;
        }
        catch (JsonException) { throw EvidenceInvalid(); }
    }

    private async Task<T> ReadPrivateAsync<T>(string? json, string purpose, CancellationToken ct)
    {
        var reference = ParseReference(json);
        var bytes = await files.ReadAsync(reference, purpose, ct);
        try
        {
            if (bytes.Length != reference.SizeBytes || Convert.ToHexString(SHA256.HashData(bytes)) != reference.Sha256) throw EvidenceInvalid();
            return JsonSerializer.Deserialize<T>(bytes) ?? throw EvidenceInvalid();
        }
        catch (JsonException) { throw EvidenceInvalid(); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static StaffTaxonomyResolution Baseline(string code)
    {
        if (code is not ("OFFICE" or "SITE")) return new("category_not_configured", null, null,
            StaffTaxonomyRulesCodec.Hash(new { State = "category_not_configured", code }));
        var office = code == "OFFICE";
        var mapping = new StaffCategoryMapping(code, office ? "Office" : "Site", office ? StaffWorkLocation.Office : StaffWorkLocation.Site,
            office ? StaffScheduleBasis.Office : StaffScheduleBasis.Site, false, !office,
            Enum.GetValues<StaffTaxonomyPolicyUse>().Select(use => new StaffTaxonomyPolicyReference(use, StaffTaxonomyPolicyApplicability.Unconfigured, null)).ToArray());
        return new("legacy_baseline", mapping, null, StaffTaxonomyRulesCodec.Hash(new { Baseline = "confirmed-office-site-v1", mapping }), true);
    }
    private static AuthenticationFailure EvidenceInvalid() => new("staff_taxonomy_evidence_invalid", 409);
}
