namespace Cosmix.Domain.Attendance.LocationConsent;

public enum LocationTrackingPurpose
{
    AttendancePunch = 1,
    ZoneDutyMonitoring = 2
}

public enum ConsentStatus
{
    NotRequired = 0,
    PendingConsent = 1,
    Consented = 2
}
