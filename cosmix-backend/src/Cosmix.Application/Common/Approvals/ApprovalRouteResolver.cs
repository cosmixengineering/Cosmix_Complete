using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.Common.Approvals;

/// <summary>Every matching stage must be unique and contiguous. Ambiguity never chooses a convenient approver.</summary>
public sealed class ApprovalRouteResolver : IApprovalRouteResolver
{
    public static readonly IReadOnlySet<string> ConfigurableTypes = new HashSet<string>(StringComparer.Ordinal)
    { "allowance", "fuel", "vehicle-maintenance", "leave-encashment", "advance", "loan", "policy-exception" };
    public static void RequireType(string type)
    {
        if (type is "leave" or "employee-hiring" or "employee-status" or "attendance-correction" or "overtime")
            throw new AuthenticationFailure("fixed_approval_route", 409);
        if (!ConfigurableTypes.Contains(type)) throw new AuthenticationFailure("unsupported_approval_request_type", 400);
    }
    public ResolvedApprovalRoute Resolve(ApprovalRoutingContext subject, IReadOnlyList<ApprovalRouteRule> rules)
    {
        RequireType(subject.RequestType);
        if (subject.Amount is < 0 || subject.DurationMinutes is < 0 ||
            subject.Amount.HasValue && (subject.Currency is null || subject.Currency.Length != 3 ||
                subject.Currency.Any(c => c is < 'A' or > 'Z')))
            throw new AuthenticationFailure("invalid_approval_routing_context", 400);
        var current = rules.Where(r => r.RequestType == subject.RequestType && r.EffectiveFrom <= subject.On)
            .GroupBy(r => r.Code, StringComparer.Ordinal)
            .Select(g => g.OrderByDescending(r => r.EffectiveFrom).ThenByDescending(r => r.Version).First());
        var matching = current.Where(r => (!r.EffectiveTo.HasValue || subject.On < r.EffectiveTo) &&
            (!r.StaffCategoryId.HasValue || r.StaffCategoryId == subject.StaffCategoryId) &&
            (!r.DepartmentId.HasValue || r.DepartmentId == subject.DepartmentId) &&
            (!r.SiteId.HasValue || r.SiteId == subject.SiteId) &&
            (r.RiskCode is null || r.RiskCode == subject.RiskCode) &&
            (r.Currency is null || r.Currency == subject.Currency) &&
            (!r.MinimumAmount.HasValue || subject.Amount.HasValue && subject.Amount >= r.MinimumAmount) &&
            (!r.MaximumAmount.HasValue || subject.Amount.HasValue && subject.Amount <= r.MaximumAmount) &&
            (!r.MinimumDurationMinutes.HasValue || subject.DurationMinutes.HasValue && subject.DurationMinutes >= r.MinimumDurationMinutes) &&
            (!r.MaximumDurationMinutes.HasValue || subject.DurationMinutes.HasValue && subject.DurationMinutes <= r.MaximumDurationMinutes))
            .OrderBy(r => r.Stage).ThenBy(r => r.Id).ToArray();
        if (matching.Length == 0) throw new AuthenticationFailure("approval_route_unconfigured", 409);
        if (matching.Any(r => r.Stage is < 1 or > 20 || r.ApproverPermission != ApprovalPermissions.StageReview(r.Stage)))
            throw new AuthenticationFailure("approval_route_invalid_authority", 409);
        if (matching.GroupBy(r => r.Stage).Any(g => g.Count() != 1))
            throw new AuthenticationFailure("approval_route_ambiguous", 409);
        if (matching.Length > 20 || !matching.Select(r => r.Stage).SequenceEqual(Enumerable.Range(1, matching.Length)))
            throw new AuthenticationFailure("approval_route_incomplete", 409);
        var digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(matching.Select(r =>
            new { r.Id, r.Version, r.Stage, r.ApproverPermission }))));
        return new(digest, matching.Select(r => new ApprovalRouteStage(r.Stage, r)).ToArray());
    }
}
