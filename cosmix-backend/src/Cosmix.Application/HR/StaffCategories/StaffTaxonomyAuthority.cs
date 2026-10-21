using Cosmix.Application.Authentication;

namespace Cosmix.Application.HR.StaffCategories;

public sealed class StaffTaxonomyAuthority(StaffTaxonomyAuthorityOptions options)
{
    public StaffTaxonomyAuthorityStamp Require(DateOnly today, DateOnly effectiveFrom)
    {
        if (options.Mode != StaffTaxonomyAuthorityMode.IndependentFixedAdministrator ||
            string.IsNullOrWhiteSpace(options.ApprovedVersion) || options.ApprovedVersion.Length > 100 ||
            options.ApprovedVersion != options.ApprovedVersion.Trim() || options.ApprovedVersion.Any(char.IsControl) ||
            options.EffectiveFrom is not DateOnly start || start == default || start == DateOnly.MaxValue ||
            options.EffectiveTo <= start || options.EffectiveTo == DateOnly.MaxValue ||
            today < start || options.EffectiveTo <= today || effectiveFrom < start || options.EffectiveTo <= effectiveFrom)
            throw new AuthenticationFailure("staff_taxonomy_authority_approval_required", 503);
        return new(options.Mode, options.ApprovedVersion, start, options.EffectiveTo);
    }

    public static bool Valid(StaffTaxonomyAuthorityStamp? stamp, DateOnly requestedOn, DateOnly effectiveFrom) =>
        stamp is { Mode: StaffTaxonomyAuthorityMode.IndependentFixedAdministrator } &&
        !string.IsNullOrWhiteSpace(stamp.ApprovedVersion) && stamp.ApprovedVersion.Length <= 100 &&
        stamp.ApprovedVersion == stamp.ApprovedVersion.Trim() && !stamp.ApprovedVersion.Any(char.IsControl) &&
        stamp.EffectiveFrom != default && stamp.EffectiveFrom < DateOnly.MaxValue &&
        stamp.EffectiveTo != DateOnly.MaxValue && (!stamp.EffectiveTo.HasValue || stamp.EffectiveTo > stamp.EffectiveFrom) &&
        stamp.EffectiveFrom <= requestedOn && (!stamp.EffectiveTo.HasValue || requestedOn < stamp.EffectiveTo) &&
        stamp.EffectiveFrom <= effectiveFrom && (!stamp.EffectiveTo.HasValue || effectiveFrom < stamp.EffectiveTo);

    public static DateOnly BusinessDate(DateTimeOffset at) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, TimeZoneInfo.FindSystemTimeZoneById("Asia/Karachi")).DateTime);
}
