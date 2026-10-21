using Cosmix.Application.Authentication;
using Cosmix.Application.HR.FoundationPolicies;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Application.HR.Employees;

/// <summary>New transitions remain unavailable until management supplies an approved version and effective window.</summary>
public sealed class EmployeeLifecycleOptions
{
    public const string SectionName = "EmployeeLifecycle";
    public string? ApprovedVersion { get; init; }
    public DateTimeOffset? EffectiveFrom { get; init; }
    public DateTimeOffset? EffectiveTo { get; init; }
    public bool AllowRetirement { get; init; }
    public bool AllowSuspensionRelease { get; init; }
}

public sealed class EmployeeLifecyclePolicy(EmployeeLifecycleOptions options, IHrFoundationPolicyReader? publishedPolicies = null,
    MobileAuthenticationOptions? authenticationOptions = null)
{
    public async Task<string?> ValidateTransitionAsync(EmployeeStatus previous, EmployeeStatus next, DateTimeOffset now,
        DateTimeOffset? effectiveAt, CancellationToken ct)
    {
        // Preserve the approved fixed baseline. Rehire remains its separate unapproved workflow.
        if (publishedPolicies is null || previous is EmployeeStatus.Resigned or EmployeeStatus.Terminated or EmployeeStatus.Retired ||
            (previous == EmployeeStatus.Active && next is EmployeeStatus.Suspended or EmployeeStatus.Resigned or EmployeeStatus.Terminated) ||
            (previous == EmployeeStatus.Suspended && next is EmployeeStatus.Resigned or EmployeeStatus.Terminated))
            return ValidateTransition(previous, next, now, effectiveAt);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(authenticationOptions?.BusinessTimeZoneId ?? "Asia/Karachi");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var selected = await publishedPolicies.GetLifecycleAsync(today, ct);
        if (selected.State == "none") return ValidateTransition(previous, next, now, effectiveAt);
        var identity = EmployeePublishedPolicyOptions.Require(selected, today);
        var intendedDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(effectiveAt ?? now, zone).DateTime);
        if (intendedDate != today)
        {
            var intended = await publishedPolicies.GetLifecycleAsync(intendedDate, ct);
            if (EmployeePublishedPolicyOptions.Require(intended, intendedDate).VersionId != identity.VersionId)
                throw EmployeeOperation.Conflict("employee_lifecycle_policy_changed_before_effective_date");
        }
        DateTimeOffset Midnight(DateOnly date) => new(TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), zone));
        var effective = new EmployeeLifecycleOptions { ApprovedVersion = EmployeePublishedPolicyOptions.Token(identity),
            EffectiveFrom = Midnight(identity.EffectiveFrom), EffectiveTo = identity.EffectiveTo is { } end ? Midnight(end) : null,
            AllowRetirement = selected.Rules!.AllowRetirement, AllowSuspensionRelease = selected.Rules.AllowSuspensionRelease };
        return new EmployeeLifecyclePolicy(effective).ValidateTransition(previous, next, now, effectiveAt);
    }

    /// <returns>The exact approval policy version for new transitions; null for the retained fixed Admin baseline.</returns>
    public string? ValidateTransition(EmployeeStatus previous, EmployeeStatus next, DateTimeOffset now, DateTimeOffset? effectiveAt)
    {
        if (previous is EmployeeStatus.Resigned or EmployeeStatus.Terminated or EmployeeStatus.Retired && next == EmployeeStatus.Active)
            throw new AuthenticationFailure("employee_rehire_requires_approved_employment_episode_policy", 503);
        if ((previous == EmployeeStatus.Active && next is EmployeeStatus.Suspended or EmployeeStatus.Resigned or EmployeeStatus.Terminated) ||
            (previous == EmployeeStatus.Suspended && next is EmployeeStatus.Resigned or EmployeeStatus.Terminated)) return null;
        var retirement = previous is EmployeeStatus.Active or EmployeeStatus.Suspended && next == EmployeeStatus.Retired;
        var release = previous == EmployeeStatus.Suspended && next == EmployeeStatus.Active;
        if (!retirement && !release) throw EmployeeOperation.Invalid("unsupported_employee_lifecycle_transition");
        if (!(retirement ? options.AllowRetirement : options.AllowSuspensionRelease) ||
            string.IsNullOrWhiteSpace(options.ApprovedVersion) || options.ApprovedVersion != options.ApprovedVersion.Trim() ||
            options.ApprovedVersion.Length > 100 || options.ApprovedVersion.Any(char.IsControl) ||
            !options.EffectiveFrom.HasValue || options.EffectiveTo <= options.EffectiveFrom ||
            now < options.EffectiveFrom || options.EffectiveTo <= now ||
            (effectiveAt ?? now) < options.EffectiveFrom || options.EffectiveTo <= (effectiveAt ?? now))
            throw new AuthenticationFailure("employee_lifecycle_policy_approval_required", 503);
        return options.ApprovedVersion;
    }
}
