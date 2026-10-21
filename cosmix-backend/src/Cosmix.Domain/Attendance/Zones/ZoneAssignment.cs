using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.Zones;

public enum ZonePopulationKind { Employee = 1, Role = 2, Team = 3, Site = 4 }

public sealed class ZoneAssignment : IAppendOnlyEntity
{
    private ZoneAssignment() { }
    public ZoneAssignment(Guid zoneVersionId, ZonePopulationKind populationKind, DateTimeOffset effectiveFromUtc,
        DateTimeOffset effectiveToUtc, Guid? approvalRequestId, string approvedBy, DateTimeOffset approvedAtUtc,
        Guid? employeeId = null, Guid? roleId = null, Guid? teamId = null, Guid? siteId = null, bool isEmployeeOverride = false,
        string? directAuthorityEvidenceReference = null)
    {
        AttendanceModelGuard.Window(effectiveFromUtc, effectiveToUtc);
        if (!Enum.IsDefined(populationKind) || new[] { employeeId, roleId, teamId, siteId }.Count(x => x.HasValue) != 1
            || populationKind == ZonePopulationKind.Employee && !employeeId.HasValue || populationKind == ZonePopulationKind.Role && !roleId.HasValue
            || populationKind == ZonePopulationKind.Team && !teamId.HasValue || populationKind == ZonePopulationKind.Site && !siteId.HasValue
            || isEmployeeOverride && populationKind != ZonePopulationKind.Employee) throw new ArgumentException("Exactly one matching assignment population is required.");
        Id = Guid.NewGuid(); ZoneVersionId = ModelGuard.Id(zoneVersionId, nameof(zoneVersionId)); PopulationKind = populationKind;
        EffectiveFromUtc = ModelGuard.Utc(effectiveFromUtc); EffectiveToUtc = ModelGuard.Utc(effectiveToUtc); IsEmployeeOverride = isEmployeeOverride;
        if (!approvalRequestId.HasValue && string.IsNullOrWhiteSpace(directAuthorityEvidenceReference)) throw new ArgumentException("Recorded authority is required.");
        ApprovalRequestId = AttendanceModelGuard.OptionalId(approvalRequestId, nameof(approvalRequestId)); ApprovedBy = ModelGuard.Required(approvedBy, 200, nameof(approvedBy));
        DirectAuthorityEvidenceReference=ModelGuard.Optional(directAuthorityEvidenceReference,500,nameof(directAuthorityEvidenceReference));
        ApprovedAtUtc = ModelGuard.Utc(approvedAtUtc); EmployeeId = AttendanceModelGuard.OptionalId(employeeId, nameof(employeeId));
        RoleId = AttendanceModelGuard.OptionalId(roleId, nameof(roleId)); TeamId = AttendanceModelGuard.OptionalId(teamId, nameof(teamId)); SiteId = AttendanceModelGuard.OptionalId(siteId, nameof(siteId));
    }
    public Guid Id { get; private set; }
    public Guid ZoneVersionId { get; private set; }
    public ZonePopulationKind PopulationKind { get; private set; }
    public Guid? EmployeeId { get; private set; }
    public Guid? RoleId { get; private set; }
    public Guid? TeamId { get; private set; }
    public Guid? SiteId { get; private set; }
    public bool IsEmployeeOverride { get; private set; }
    public DateTimeOffset EffectiveFromUtc { get; private set; }
    public DateTimeOffset EffectiveToUtc { get; private set; }
    public Guid? ApprovalRequestId { get; private set; }
    public string? DirectAuthorityEvidenceReference { get; private set; }
    public string ApprovedBy { get; private set; } = string.Empty;
    public DateTimeOffset ApprovedAtUtc { get; private set; }
    public ZoneVersion ZoneVersion { get; private set; } = null!;
}
