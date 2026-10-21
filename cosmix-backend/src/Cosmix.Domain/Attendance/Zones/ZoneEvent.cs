using Cosmix.Domain.Common;
using Cosmix.Domain.Authentication;

namespace Cosmix.Domain.Attendance.Zones;

public enum ZoneEventKind { Approach = 1, EntryAttempt = 2, Entered = 3, Exited = 4, WarningShown = 5 }
public enum ZoneEventSource { ForegroundLocation = 1, BackgroundLocation = 2, EmployeeReport = 3 }

public sealed class ZoneEvent : IAppendOnlyEntity
{
    private ZoneEvent() { }
    public ZoneEvent(DutyMonitoringWindow dutyWindow, ZoneAssignment assignment, ZoneEventKind kind, ZoneEventSource source,
        UserAccount account, RegisteredDevice device, string idempotencyKey, DateTimeOffset receivedAtUtc, DateTimeOffset? deviceCapturedAtUtc,
        decimal latitude, decimal longitude, decimal accuracyMeters, bool isOffline, string? shownInstruction = null)
    {
        if (!Enum.IsDefined(kind) || !Enum.IsDefined(source)) throw new ArgumentException("Unknown event kind/source.");
        if (account.EmployeeId != dutyWindow.EmployeeId || device.UserAccountId != account.Id) throw new ArgumentException("Device and account must belong to this employee.");
        var evidenceAt = deviceCapturedAtUtc ?? receivedAtUtc;
        if (dutyWindow.ZoneAssignmentId != assignment.Id || assignment.EmployeeId.HasValue && assignment.EmployeeId != dutyWindow.EmployeeId
            || evidenceAt < dutyWindow.StartsAtUtc || evidenceAt >= dutyWindow.EndsAtUtc
            || evidenceAt < assignment.EffectiveFromUtc || evidenceAt >= assignment.EffectiveToUtc)
            throw new ArgumentException("Zone evidence must belong to this employee's authorized duty and assignment window.");
        AttendanceModelGuard.Coordinates(latitude, longitude, accuracyMeters);
        Id = Guid.NewGuid(); EmployeeId = dutyWindow.EmployeeId; DutyMonitoringWindowId = dutyWindow.Id;
        ZoneAssignmentId = assignment.Id; ZoneVersionId = assignment.ZoneVersionId; Kind = kind; Source = source;
        UserAccountId = account.Id; RegisteredDeviceId = device.Id; IdempotencyKey = ModelGuard.Required(idempotencyKey, 100, nameof(idempotencyKey));
        ReceivedAtUtc = ModelGuard.Utc(receivedAtUtc); DeviceCapturedAtUtc = deviceCapturedAtUtc?.ToUniversalTime();
        Latitude = latitude; Longitude = longitude; AccuracyMeters = accuracyMeters; IsOffline = isOffline;
        ShownInstruction = ModelGuard.Optional(shownInstruction, 2000, nameof(shownInstruction));
    }
    /// <summary>New evidence retains exact location only in the separately expiring encrypted payload.</summary>
    public ZoneEvent(Guid id,DutyMonitoringWindow dutyWindow,ZoneAssignment assignment,ZoneEventKind kind,ZoneEventSource source,
        UserAccount account,RegisteredDevice device,string idempotencyKey,DateTimeOffset receivedAtUtc,
        string locationPrivateReference,DateTimeOffset locationExpiresAtUtc,string shownInstruction)
    {
        if(!Enum.IsDefined(kind) || source is not (ZoneEventSource.ForegroundLocation or ZoneEventSource.BackgroundLocation) ||
            account.EmployeeId!=dutyWindow.EmployeeId || device.UserAccountId!=account.Id || dutyWindow.RegisteredDeviceId!=device.Id ||
            dutyWindow.ZoneAssignmentId!=assignment.Id || receivedAtUtc<dutyWindow.StartsAtUtc || receivedAtUtc>=dutyWindow.EndsAtUtc ||
            receivedAtUtc<assignment.EffectiveFromUtc || receivedAtUtc>=assignment.EffectiveToUtc || locationExpiresAtUtc<=receivedAtUtc)
            throw new ArgumentException("An active accepted duty and matching current assignment are required.");
        Id=ModelGuard.Id(id,nameof(id)); EmployeeId=dutyWindow.EmployeeId; DutyMonitoringWindowId=dutyWindow.Id;
        ZoneAssignmentId=assignment.Id; ZoneVersionId=assignment.ZoneVersionId; Kind=kind; Source=source;
        UserAccountId=account.Id; RegisteredDeviceId=device.Id; IdempotencyKey=ModelGuard.Required(idempotencyKey,100,nameof(idempotencyKey));
        ReceivedAtUtc=ModelGuard.Utc(receivedAtUtc); LocationExpiresAtUtc=ModelGuard.Utc(locationExpiresAtUtc);
        LocationPrivateReference=ModelGuard.Required(locationPrivateReference,500,nameof(locationPrivateReference));
        ShownInstruction=ModelGuard.Required(shownInstruction,2000,nameof(shownInstruction));
    }
    /// <summary>A historical employee claim, never a GPS observation or verified boundary transition.</summary>
    public static ZoneEvent EmployeeReport(Guid id, DutyMonitoringWindow window, ZoneAssignment assignment,
        ZoneEventKind eventKindHint, UserAccount account, RegisteredDevice device, string key,
        DateTimeOffset receivedAtUtc, DateTimeOffset capturedAtUtc, string writtenClaimPrivateReference,
        DateTimeOffset writtenClaimExpiresAtUtc)
    {
        ModelGuard.Utc(receivedAtUtc); ModelGuard.Utc(capturedAtUtc); ModelGuard.Utc(writtenClaimExpiresAtUtc);
        if (!Enum.IsDefined(eventKindHint) || account.EmployeeId != window.EmployeeId || device.UserAccountId != account.Id ||
            window.RegisteredDeviceId != device.Id || window.ZoneAssignmentId != assignment.Id ||
            window.ConfigurationVersionId is null || window.AcceptedCheckInId is null || window.WorkSessionKey is null ||
            window.RetentionPolicyVersionId.HasValue || window.NoticeShownAtUtc > capturedAtUtc ||
            capturedAtUtc < window.StartsAtUtc || capturedAtUtc >= window.EndsAtUtc || capturedAtUtc > receivedAtUtc ||
            capturedAtUtc < assignment.EffectiveFromUtc || capturedAtUtc >= assignment.EffectiveToUtc ||
            assignment.EmployeeId.HasValue && assignment.EmployeeId != window.EmployeeId ||
            device.RevokedAt.HasValue || device.VerifiedAt is null || capturedAtUtc < device.RegisteredAt || capturedAtUtc < device.VerifiedAt ||
            writtenClaimExpiresAtUtc <= receivedAtUtc || writtenClaimExpiresAtUtc <= capturedAtUtc)
            throw new ArgumentException("A claim requires the original employee/device/notice and historical assignment window.");
        return new ZoneEvent {
            Id = ModelGuard.Id(id, nameof(id)), EmployeeId = window.EmployeeId, DutyMonitoringWindowId = window.Id,
            ZoneAssignmentId = assignment.Id, ZoneVersionId = assignment.ZoneVersionId,
            Kind = eventKindHint, Source = ZoneEventSource.EmployeeReport, IsOffline = true,
            UserAccountId = account.Id, RegisteredDeviceId = device.Id,
            IdempotencyKey = ModelGuard.Required(key, 100, nameof(key)), ReceivedAtUtc = receivedAtUtc,
            DeviceCapturedAtUtc = capturedAtUtc, WrittenClaimExpiresAtUtc = writtenClaimExpiresAtUtc,
            WrittenClaimPrivateReference = ModelGuard.Required(writtenClaimPrivateReference, 500, nameof(writtenClaimPrivateReference))
        };
    }
    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid DutyMonitoringWindowId { get; private set; }
    public Guid ZoneAssignmentId { get; private set; }
    public Guid ZoneVersionId { get; private set; }
    public ZoneEventKind Kind { get; private set; }
    public ZoneEventSource Source { get; private set; }
    public Guid RegisteredDeviceId { get; private set; }
    public Guid UserAccountId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public DateTimeOffset ReceivedAtUtc { get; private set; }
    public DateTimeOffset? DeviceCapturedAtUtc { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public decimal? AccuracyMeters { get; private set; }
    public string? LocationPrivateReference { get; private set; }
    public DateTimeOffset? LocationExpiresAtUtc { get; private set; }
    public string? WrittenClaimPrivateReference { get; private set; }
    public DateTimeOffset? WrittenClaimExpiresAtUtc { get; private set; }
    public bool IsOffline { get; private set; }
    public string? ShownInstruction { get; private set; }
    public ZoneVersion ZoneVersion { get; private set; } = null!;
}
