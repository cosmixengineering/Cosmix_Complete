using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.Punches;

/// <summary>Append-only received evidence, not an accepted attendance result.</summary>
public sealed class RawPunchEvent : IAppendOnlyEntity
{
    private RawPunchEvent() { }

    public RawPunchEvent(Guid employeeId, PunchAction action, string registeredDeviceId,
        string idempotencyKey, string requestSha256, DateTimeOffset receivedAtUtc,
        bool isOffline, DateTimeOffset? deviceCapturedAtUtc = null,
        decimal? latitude = null, decimal? longitude = null, decimal? accuracyMeters = null,
        Guid? siteId = null, Guid? geofenceVersionId = null, Guid? employeeAssignmentId = null,
        Guid? shiftVersionId = null, string? verificationAttemptReference = null, string? correlationId = null,
        Guid? evidenceFileId = null, DateOnly? businessDate = null, Guid? siteRosterAssignmentId = null,
        Guid? temporaryShiftChangeId = null, Guid? holidayCalendarId = null,
        Guid? branchId = null, Guid? officeRosterAssignmentId = null, Guid? officeGeofenceVersionId = null)
    {
        if (!Enum.IsDefined(action)) throw new ArgumentOutOfRangeException(nameof(action));
        if (latitude.HasValue != longitude.HasValue)
            throw new ArgumentException("Latitude and longitude must be supplied together.");
        if (latitude is < -90 or > 90) throw new ArgumentOutOfRangeException(nameof(latitude));
        if (longitude is < -180 or > 180) throw new ArgumentOutOfRangeException(nameof(longitude));
        if (accuracyMeters is < 0 or > 99999999.99m) throw new ArgumentOutOfRangeException(nameof(accuracyMeters));
        if (latitude.HasValue && decimal.Round(latitude.Value, 7) != latitude.Value ||
            longitude.HasValue && decimal.Round(longitude.Value, 7) != longitude.Value ||
            accuracyMeters.HasValue && decimal.Round(accuracyMeters.Value, 2) != accuracyMeters.Value)
            throw new ArgumentException("Location precision exceeds the immutable storage contract.");
        if (geofenceVersionId.HasValue && !siteId.HasValue)
            throw new ArgumentException("A geofence version requires its site.", nameof(siteId));
        requestSha256 = ModelGuard.Required(requestSha256, 64, nameof(requestSha256)).ToUpperInvariant();
        if (requestSha256.Length != 64 || !requestSha256.All(Uri.IsHexDigit))
            throw new ArgumentException("A 64-character SHA-256 hexadecimal fingerprint is required.", nameof(requestSha256));
        Id = Guid.NewGuid();
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        Action = action;
        RegisteredDeviceId = ModelGuard.Required(registeredDeviceId, 200, nameof(registeredDeviceId));
        IdempotencyKey = ModelGuard.Required(idempotencyKey, 100, nameof(idempotencyKey));
        RequestSha256 = requestSha256;
        ReceivedAtUtc = ModelGuard.Utc(receivedAtUtc);
        DeviceCapturedAtUtc = deviceCapturedAtUtc.HasValue ? ModelGuard.Utc(deviceCapturedAtUtc.Value) : null;
        IsOffline = isOffline;
        Latitude = latitude;
        Longitude = longitude;
        AccuracyMeters = accuracyMeters;
        SiteId = siteId.HasValue ? ModelGuard.Id(siteId.Value, nameof(siteId)) : null;
        GeofenceVersionId = geofenceVersionId.HasValue ? ModelGuard.Id(geofenceVersionId.Value, nameof(geofenceVersionId)) : null;
        EmployeeAssignmentId = employeeAssignmentId.HasValue ? ModelGuard.Id(employeeAssignmentId.Value, nameof(employeeAssignmentId)) : null;
        ShiftVersionId = shiftVersionId.HasValue ? ModelGuard.Id(shiftVersionId.Value, nameof(shiftVersionId)) : null;
        VerificationAttemptReference = ModelGuard.Optional(verificationAttemptReference, 200, nameof(verificationAttemptReference));
        CorrelationId = ModelGuard.Optional(correlationId, 100, nameof(correlationId));
        EvidenceFileId = evidenceFileId.HasValue ? ModelGuard.Id(evidenceFileId.Value, nameof(evidenceFileId)) : null;
        BusinessDate = businessDate;
        SiteRosterAssignmentId = AttendanceModelGuard.OptionalId(siteRosterAssignmentId, nameof(siteRosterAssignmentId));
        TemporaryShiftChangeId = AttendanceModelGuard.OptionalId(temporaryShiftChangeId, nameof(temporaryShiftChangeId));
        HolidayCalendarId = AttendanceModelGuard.OptionalId(holidayCalendarId, nameof(holidayCalendarId));
        BranchId = AttendanceModelGuard.OptionalId(branchId, nameof(branchId));
        OfficeRosterAssignmentId = AttendanceModelGuard.OptionalId(officeRosterAssignmentId, nameof(officeRosterAssignmentId));
        OfficeGeofenceVersionId = AttendanceModelGuard.OptionalId(officeGeofenceVersionId, nameof(officeGeofenceVersionId));
        if (BranchId.HasValue && (SiteId.HasValue || GeofenceVersionId.HasValue || SiteRosterAssignmentId.HasValue || TemporaryShiftChangeId.HasValue || !businessDate.HasValue) ||
            !BranchId.HasValue && (OfficeRosterAssignmentId.HasValue || OfficeGeofenceVersionId.HasValue))
            throw new ArgumentException("Office evidence must retain a real office, not a site identity.");
        if ((SiteRosterAssignmentId.HasValue || TemporaryShiftChangeId.HasValue) && (!SiteId.HasValue || !businessDate.HasValue))
            throw new ArgumentException("Site scheduling evidence requires a site and business date.");
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Cosmix.Domain.HR.Employees.Employee Employee { get; private set; } = null!;
    public PunchAction Action { get; private set; }
    public string RegisteredDeviceId { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RequestSha256 { get; private set; } = string.Empty;
    public DateTimeOffset ReceivedAtUtc { get; private set; }
    public DateTimeOffset? DeviceCapturedAtUtc { get; private set; }
    public bool IsOffline { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public decimal? AccuracyMeters { get; private set; }
    public Guid? SiteId { get; private set; }
    public Cosmix.Domain.Attendance.Sites.Site? Site { get; private set; }
    public Guid? GeofenceVersionId { get; private set; }
    public Cosmix.Domain.Attendance.Sites.SiteGeofenceVersion? GeofenceVersion { get; private set; }
    public Guid? EmployeeAssignmentId { get; private set; }
    public Cosmix.Domain.HR.EmployeeAssignments.EmployeeAssignment? EmployeeAssignment { get; private set; }
    public Guid? ShiftVersionId { get; private set; }
    public Cosmix.Domain.Attendance.Shifts.ShiftVersion? ShiftVersion { get; private set; }
    public string? VerificationAttemptReference { get; private set; }
    public string? CorrelationId { get; private set; }
    public Guid? EvidenceFileId { get; private set; }
    /// <summary>Server-selected shift start date. Null only on legacy evidence.</summary>
    public DateOnly? BusinessDate { get; private set; }
    public Guid? SiteRosterAssignmentId { get; private set; }
    public Guid? TemporaryShiftChangeId { get; private set; }
    public Guid? HolidayCalendarId { get; private set; }
    public Guid? BranchId { get; private set; }
    public Guid? OfficeRosterAssignmentId { get; private set; }
    public Guid? OfficeGeofenceVersionId { get; private set; }
    public Cosmix.Domain.Common.Files.StoredFile? EvidenceFile { get; private set; }
}
