using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.Zones;

/// <summary>Explicit employee duty/field-work window and visible notice, required for monitoring evidence.</summary>
public sealed class DutyMonitoringWindow : IAppendOnlyEntity
{
    private DutyMonitoringWindow() { }
    public DutyMonitoringWindow(Guid employeeId, Guid zoneAssignmentId, DateTimeOffset startsAtUtc, DateTimeOffset endsAtUtc,
        DateTimeOffset noticeShownAtUtc, Guid retentionPolicyVersionId, string dutyReference)
    {
        AttendanceModelGuard.Window(startsAtUtc, endsAtUtc);
        if (noticeShownAtUtc > startsAtUtc) throw new ArgumentException("Notice must be shown before monitoring starts.");
        Id = Guid.NewGuid(); EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId)); ZoneAssignmentId = ModelGuard.Id(zoneAssignmentId, nameof(zoneAssignmentId));
        StartsAtUtc = ModelGuard.Utc(startsAtUtc); EndsAtUtc = ModelGuard.Utc(endsAtUtc); NoticeShownAtUtc = ModelGuard.Utc(noticeShownAtUtc);
        RetentionPolicyVersionId = ModelGuard.Id(retentionPolicyVersionId, nameof(retentionPolicyVersionId)); DutyReference = ModelGuard.Required(dutyReference, 200, nameof(dutyReference));
    }
    public DutyMonitoringWindow(Guid employeeId, ZoneAssignment assignment, ZoneMonitoringConfigurationVersion configuration,
        Guid acceptedCheckInId, Guid sessionKey, Guid registeredDeviceId, DateTimeOffset startsAtUtc, DateTimeOffset endsAtUtc)
    {
        AttendanceModelGuard.Window(startsAtUtc,endsAtUtc);
        if(startsAtUtc<configuration.EffectiveFromUtc || endsAtUtc>configuration.EffectiveToUtc ||
            startsAtUtc<assignment.EffectiveFromUtc || endsAtUtc>assignment.EffectiveToUtc ||
            endsAtUtc-startsAtUtc>TimeSpan.FromSeconds(configuration.MaximumMonitoringSeconds))
            throw new ArgumentException("Monitoring must fit the explicit configuration and assignment window.");
        Id=Guid.NewGuid(); EmployeeId=ModelGuard.Id(employeeId,nameof(employeeId)); ZoneAssignmentId=assignment.Id;
        StartsAtUtc=ModelGuard.Utc(startsAtUtc); EndsAtUtc=ModelGuard.Utc(endsAtUtc); NoticeShownAtUtc=StartsAtUtc;
        ConfigurationVersionId=configuration.Id; AcceptedCheckInId=ModelGuard.Id(acceptedCheckInId,nameof(acceptedCheckInId));
        WorkSessionKey=ModelGuard.Id(sessionKey,nameof(sessionKey)); RegisteredDeviceId=ModelGuard.Id(registeredDeviceId,nameof(registeredDeviceId));
        DutyReference=$"accepted-check-in:{acceptedCheckInId:D}";
    }
    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid ZoneAssignmentId { get; private set; }
    public DateTimeOffset StartsAtUtc { get; private set; }
    public DateTimeOffset EndsAtUtc { get; private set; }
    public DateTimeOffset NoticeShownAtUtc { get; private set; }
    public Guid? RetentionPolicyVersionId { get; private set; }
    public Guid? ConfigurationVersionId { get; private set; }
    public Guid? AcceptedCheckInId { get; private set; }
    public Guid? WorkSessionKey { get; private set; }
    public Guid? RegisteredDeviceId { get; private set; }
    public string DutyReference { get; private set; } = string.Empty;
}
