namespace Cosmix.Domain.Common.Approvals;

/// <summary>An immutable version of routing configuration. No routing defaults are seeded.</summary>
public sealed class ApprovalRouteRule : IAppendOnlyEntity
{
    private ApprovalRouteRule() { }
    public ApprovalRouteRule(string code, int version, string requestType, string approverPermission,
        int stage, DateOnly effectiveFrom, DateOnly? effectiveTo, string approvedByUserId,
        Guid? staffCategoryId = null, Guid? departmentId = null, Guid? siteId = null,
        decimal? minimumAmount = null, decimal? maximumAmount = null, string? currency = null,
        int? minimumDurationMinutes = null, int? maximumDurationMinutes = null, string? riskCode = null)
    {
        if (version < 1 || stage < 1) throw new ArgumentOutOfRangeException(nameof(version));
        if (minimumAmount is < 0 || maximumAmount is < 0 || maximumAmount < minimumAmount)
            throw new ArgumentException("Invalid amount bounds.");
        if (minimumDurationMinutes is < 0 || maximumDurationMinutes is < 0 || maximumDurationMinutes < minimumDurationMinutes)
            throw new ArgumentException("Invalid duration bounds.");
        ModelGuard.EffectiveDates(effectiveFrom, effectiveTo);
        Id = Guid.NewGuid(); Code = ModelGuard.Code(code); Version = version;
        RequestType = ModelGuard.Required(requestType, 100, nameof(requestType));
        ApproverPermission = ModelGuard.Required(approverPermission, 150, nameof(approverPermission));
        Stage = stage; EffectiveFrom = effectiveFrom; EffectiveTo = effectiveTo;
        ApprovedByUserId = ModelGuard.Required(approvedByUserId, 200, nameof(approvedByUserId));
        StaffCategoryId = staffCategoryId.HasValue ? ModelGuard.Id(staffCategoryId.Value, nameof(staffCategoryId)) : null;
        DepartmentId = departmentId.HasValue ? ModelGuard.Id(departmentId.Value, nameof(departmentId)) : null;
        SiteId = siteId.HasValue ? ModelGuard.Id(siteId.Value, nameof(siteId)) : null;
        MinimumAmount = minimumAmount; MaximumAmount = maximumAmount;
        Currency = minimumAmount.HasValue || maximumAmount.HasValue
            ? ModelGuard.Required(currency!, 3, nameof(currency)).ToUpperInvariant() : ModelGuard.Optional(currency, 3, nameof(currency))?.ToUpperInvariant();
        if (Currency is not null && (Currency.Length != 3 || Currency.Any(c => c is < 'A' or > 'Z')))
            throw new ArgumentException("Currency must be a three-letter code.");
        MinimumDurationMinutes = minimumDurationMinutes; MaximumDurationMinutes = maximumDurationMinutes;
        RiskCode = ModelGuard.Optional(riskCode, 50, nameof(riskCode));
    }
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public string RequestType { get; private set; } = string.Empty;
    public string ApproverPermission { get; private set; } = string.Empty;
    public int Stage { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public string ApprovedByUserId { get; private set; } = string.Empty;
    public Guid? StaffCategoryId { get; private set; }
    public HR.StaffCategories.StaffCategory? StaffCategory { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public HR.Departments.Department? Department { get; private set; }
    public Guid? SiteId { get; private set; }
    public Attendance.Sites.Site? Site { get; private set; }
    public decimal? MinimumAmount { get; private set; }
    public decimal? MaximumAmount { get; private set; }
    public string? Currency { get; private set; }
    public int? MinimumDurationMinutes { get; private set; }
    public int? MaximumDurationMinutes { get; private set; }
    public string? RiskCode { get; private set; }
}
