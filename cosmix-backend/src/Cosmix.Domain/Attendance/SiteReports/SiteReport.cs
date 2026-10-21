using Cosmix.Domain.Common;
using Cosmix.Domain.Authentication;

namespace Cosmix.Domain.Attendance.SiteReports;

public enum SiteReportKind { Arrival = 1, Departure = 2, Delay = 3, AccessIssue = 4, Incident = 5 }

/// <summary>Immutable received report; review status is derived from its separate review trail.</summary>
public sealed class SiteReport : IAppendOnlyEntity
{
    private SiteReport() { }
    public SiteReport(Guid employeeId, Guid siteId, Guid employeeAssignmentId, SiteReportKind kind,
        string? taskReference, string notes, UserAccount account, RegisteredDevice device, string idempotencyKey, DateTimeOffset receivedAtUtc,
        DateTimeOffset? deviceCapturedAtUtc, decimal? latitude, decimal? longitude, decimal? accuracyMeters,
        bool isOffline, string? privateEvidenceReference = null)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (account.EmployeeId != employeeId || device.UserAccountId != account.Id) throw new ArgumentException("Device and account must belong to this employee.");
        AttendanceModelGuard.Coordinates(latitude, longitude, accuracyMeters);
        Id = Guid.NewGuid(); EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId)); SiteId = ModelGuard.Id(siteId, nameof(siteId));
        EmployeeAssignmentId = ModelGuard.Id(employeeAssignmentId, nameof(employeeAssignmentId)); Kind = kind;
        TaskReference = ModelGuard.Optional(taskReference, 200, nameof(taskReference)); Notes = ModelGuard.Required(notes, 4000, nameof(notes));
        UserAccountId = account.Id; RegisteredDeviceId = device.Id; IdempotencyKey = ModelGuard.Required(idempotencyKey, 100, nameof(idempotencyKey));
        ReceivedAtUtc = ModelGuard.Utc(receivedAtUtc); DeviceCapturedAtUtc = deviceCapturedAtUtc?.ToUniversalTime();
        Latitude = latitude; Longitude = longitude; AccuracyMeters = accuracyMeters; IsOffline = isOffline;
        PrivateEvidenceReference = ModelGuard.Optional(privateEvidenceReference, 500, nameof(privateEvidenceReference));
    }
    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid SiteId { get; private set; }
    public Guid EmployeeAssignmentId { get; private set; }
    public SiteReportKind Kind { get; private set; }
    public string? TaskReference { get; private set; }
    public string Notes { get; private set; } = string.Empty;
    public Guid RegisteredDeviceId { get; private set; }
    public Guid UserAccountId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public DateTimeOffset ReceivedAtUtc { get; private set; }
    public DateTimeOffset? DeviceCapturedAtUtc { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public decimal? AccuracyMeters { get; private set; }
    public bool IsOffline { get; private set; }
    public string? PrivateEvidenceReference { get; private set; }
    public IReadOnlyCollection<SiteReportReview> Reviews => _reviews;
    private readonly List<SiteReportReview> _reviews = [];
}
