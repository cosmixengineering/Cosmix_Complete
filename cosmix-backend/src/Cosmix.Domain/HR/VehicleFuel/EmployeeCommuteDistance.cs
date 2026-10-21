using Cosmix.Domain.Common;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Domain.HR.VehicleFuel;

/// <summary>Home commute distance uses append-only revisions so changes or ended validity retain the route evidence used by earlier claims.</summary>
public sealed class EmployeeCommuteDistance : IAppendOnlyEntity
{
    private EmployeeCommuteDistance() { }

    public EmployeeCommuteDistance(Guid employeeId, decimal usualHomeOfficeKm, string routePrivateReference, DateOnly effectiveFrom, DateOnly? effectiveTo, string recordedByUserId, DateTimeOffset recordedAt, int revision = 1, Guid? supersedesEmployeeCommuteDistanceId = null)
    {
        ModelGuard.EffectiveDates(effectiveFrom, effectiveTo);
        Id = Guid.NewGuid();
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        Revision = HrModelGuard.Positive(revision, nameof(revision));
        if ((revision == 1 && supersedesEmployeeCommuteDistanceId.HasValue) || (revision > 1 && !supersedesEmployeeCommuteDistanceId.HasValue))
            throw new ArgumentException("An initial revision has no predecessor; later revisions require one.", nameof(supersedesEmployeeCommuteDistanceId));
        SupersedesEmployeeCommuteDistanceId = supersedesEmployeeCommuteDistanceId.HasValue
            ? ModelGuard.Id(supersedesEmployeeCommuteDistanceId.Value, nameof(supersedesEmployeeCommuteDistanceId)) : null;
        UsualHomeOfficeKm = HrModelGuard.Distance(usualHomeOfficeKm, nameof(usualHomeOfficeKm));
        RoutePrivateReference = ModelGuard.Required(routePrivateReference, 500, nameof(routePrivateReference));
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        RecordedByUserId = ModelGuard.Required(recordedByUserId, 200, nameof(recordedByUserId));
        RecordedAt = ModelGuard.Utc(recordedAt);
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public int Revision { get; private set; }
    public Guid? SupersedesEmployeeCommuteDistanceId { get; private set; }
    public decimal UsualHomeOfficeKm { get; private set; }
    public string RoutePrivateReference { get; private set; } = string.Empty;
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public string RecordedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }
    public Employee Employee { get; private set; } = null!;
    public EmployeeCommuteDistance? SupersedesEmployeeCommuteDistance { get; private set; }
}
