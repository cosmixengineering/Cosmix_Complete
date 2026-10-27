using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.FaceVerification;
using Cosmix.Application.Attendance.Punches;
using Cosmix.Application.Attendance.Offices;
using Cosmix.Application.Attendance.Punches.CaptureValidation;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Attendance.Sites;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Attendance.FaceVerification;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.Sites;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;

using Cosmix.Application.Attendance.LocationConsent;
using Cosmix.Domain.Attendance.LocationConsent;
using Xunit;
namespace Cosmix.Tests.Preflight;

public class CheckInPreflightTests
{
    private static Task<CheckInPreflightResponse> Run(PreflightFixture fixture, decimal latitude = 24m,
        decimal accuracy = 5m, int ageSeconds = 0) => fixture.Service.PreflightAsync(
            new(latitude, 67m, accuracy, fixture.Clock.Now.AddSeconds(-ageSeconds)), fixture.Context, default);
    private static void NoBusinessWrites(PreflightFixture f)
    {
        Assert.Empty(f.Raw); Assert.Empty(f.Assessments); Assert.Empty(f.Exceptions);
        Assert.Empty(f.ValidationEvidence); Assert.Equal(0, f.ProviderCalls); Assert.Empty(f.Files);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Inside_configured_current_site_or_office_allows_camera_without_attendance_or_face_work(bool office)
    {
        var f = new PreflightFixture(); if (office) f.UseOffice();
        var result = await Run(f);
        Assert.True(result.CanOpenCamera);
        Assert.Equal(f.Clock.Now, result.CheckedAtUtc);
        Assert.Single(f.AccessAudits); Assert.Empty(f.Sessions); NoBusinessWrites(f);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Outside_current_site_or_office_is_denied_before_camera(bool office)
    {
        var f = new PreflightFixture(); if (office) f.UseOffice();
        var error = await Assert.ThrowsAsync<AuthenticationFailure>(() => Run(f, latitude:25m));
        Assert.Equal("check_in_location_outside_boundary", error.Code); NoBusinessWrites(f);
    }
    [Theory] [InlineData("accuracy")] [InlineData("stale")] [InlineData("future")] [InlineData("capture-policy")]
    [InlineData("consent")] [InlineData("locked")] [InlineData("active-session")] [InlineData("membership")]
    [InlineData("roster")] [InlineData("office-boundary")] [InlineData("session-revoked")] [InlineData("web")]
    [InlineData("permission")]
    public async Task Required_evidence_or_authority_failure_never_allows_camera(string scenario)
    {
        var f = new PreflightFixture(); decimal accuracy = 5m; int age = 0;
        switch(scenario) {
            case "accuracy": accuracy = 21m; break;
            case "stale": age = 31; break;
            case "future": age = -6; break;
            case "capture-policy": f.CapturePolicy = null; break;
            case "consent": f.ConsentGranted = false; break;
            case "locked": f.Locked = true; break;
            case "active-session": f.Sessions.Add((WorkSession)Activator.CreateInstance(typeof(WorkSession), true)!); break;
            case "membership": f.CurrentSiteMatches = false; break;
            case "roster": f.Roster = f.Roster with {Status="unassigned"}; break;
            case "office-boundary": f.UseOffice(); f.OfficeBoundary=null; break;
            case "session-revoked": f.Current = false; break;
            case "web": f.Actor = f.Actor with {Client=SessionClient.Web}; break;
            case "permission": f.Grants.Clear(); break;
        }
        await Assert.ThrowsAsync<AuthenticationFailure>(()=>Run(f,accuracy:accuracy,ageSeconds:age)); NoBusinessWrites(f);
    }
    [Fact]
    public async Task Missing_active_notice_or_consent_repository_denies_before_camera()
    {
        var noNotice = new PreflightFixture { NoticePolicy = null };
        var missing = await Assert.ThrowsAsync<AuthenticationFailure>(() => Run(noNotice));
        Assert.Equal("location_notice_not_configured", missing.Code); NoBusinessWrites(noNotice);
        var unavailable = new PreflightFixture(consentAvailable: false);
        var error = await Assert.ThrowsAsync<AuthenticationFailure>(() => Run(unavailable));
        Assert.Equal(503, error.StatusCode); NoBusinessWrites(unavailable);
    }
    [Theory] [InlineData("device")] [InlineData("session")]
    public async Task Authority_revoked_during_policy_IO_cannot_allow_camera(string authority)
    {
        var f = new PreflightFixture();
        f.OnCaptureValidation = () => {
            if (authority == "device") f.Device.Revoke("SYNTHETIC revocation", f.Clock.Now);
            else PreflightFixture.Set(f.Session, "ExpiresAt", f.Clock.Now);
        };
        var error = await Assert.ThrowsAsync<AuthenticationFailure>(() => Run(f));
        Assert.Equal(401, error.StatusCode); NoBusinessWrites(f);
    }
    [Fact]
    public void Preflight_HTTP_contract_rejects_client_identity_face_and_missing_location()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        const string valid = "{\"latitude\":24,\"longitude\":67,\"accuracyMeters\":5,\"deviceCapturedAtUtc\":\"2026-10-09T04:00:00Z\"}";
        Assert.NotNull(JsonSerializer.Deserialize<CheckInPreflightRequest>(valid, options));
        foreach (var field in new[] {"employeeId", "siteId", "faceAttemptId", "isOffline"})
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CheckInPreflightRequest>(valid[..^1] + ",\"" + field + "\":null}", options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CheckInPreflightRequest>("{}", options));
    }
    [Fact]
    public async Task Policy_IO_that_outlasts_sample_or_revokes_session_is_rechecked()
    {
        var stale = new PreflightFixture {AdvanceDuringCaptureValidation=true};
        await Assert.ThrowsAsync<AuthenticationFailure>(()=>Run(stale)); NoBusinessWrites(stale);
        var revoked = new PreflightFixture(); revoked.OnCaptureValidation=()=>revoked.Current=false;
        await Assert.ThrowsAsync<AuthenticationFailure>(()=>Run(revoked)); NoBusinessWrites(revoked);
    }
}

public sealed class Clock : TimeProvider
{ public DateTimeOffset Now = new(2026, 10, 9, 4, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }
public class Proxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var value = Create<T, Proxy>(); ((Proxy)(object)value).Handler = handler; return value; }
}
public sealed class PreflightFixture
{
    public static void Set(object entity, string name, object? value) => entity.GetType().GetProperty(name)!.SetValue(entity, value);
    public bool ConsentGranted = true;
    public LocationNoticePolicy? NoticePolicy;
    public int ProviderCalls;
    public Clock Clock = new();
    public AuthenticatedAccount Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Mobile, false);
    public RegisteredDevice Device;
    public UserAccount Account;
    public UserSession Session;
    public FaceEnrollment Enrollment;
    public FaceVerificationAttempt Attempt;
    public string Key = "capture-key-001", Secret = "synthetic-face-proof";
    public SubmitPunchRequest Request;
    public AuthRequestContext Context = new("127.0.0.1", "synthetic-capture");
    public AttendanceCaptureService Service;
    public AttendanceOperation Operation;
    public EffectiveRosterResponse Roster;
    public OfficeBoundaryVersionResponse? OfficeBoundary;
    public CaptureValidationPolicyEntry? CapturePolicy;
    public List<CaptureValidationEvidence> ValidationEvidence = [];
    public bool AdvanceDuringCaptureValidation;
    public TimeSpan? ValidationDelay;
    public Action? OnCaptureValidation;
    public bool AdvanceDuringFinalGrantRead;
    private int accessReads;
    public int AccessReads => accessReads;
    public int? DelayedAccessRead;
    public List<RawPunchEvent> Raw = [];
    public List<PunchAssessment> Assessments = [];
    public List<WorkSession> Sessions = [];
    public List<AttendanceException> Exceptions = [];
    public List<PermissionGrant> Grants = [];
    public List<AuditEvent> AccessAudits = [];
    public List<IdempotencyReceipt> Receipts = [];
    public Dictionary<string, (string Purpose, byte[] Content)> Files = [];
    public IPrivateFileStore PrivateFiles = null!;
    public FaceRetirementService Retirement = null!;
    public bool InTransaction;
    public int TransactionCount;
    public Action<int>? OnTransactionStart;
    public bool Current = true, ProviderReady = true, AdvanceDuringLockCheck, CurrentSiteMatches = true, Locked;
    public PreflightFixture(bool consentAvailable = true)
    {
        var account = Account = (UserAccount)Activator.CreateInstance(typeof(UserAccount), true)!;
        Set(account, "Id", Actor.AccountId); Set(account, "EmployeeId", Actor.EmployeeId); Set(account, "Status", AccountStatus.Active);
        Device = new(Actor.AccountId, "synthetic-device", "android", "Synthetic", Clock.Now.AddDays(-1)); Device.MarkVerified(Clock.Now.AddDays(-1));
        var session = Session = (UserSession)Activator.CreateInstance(typeof(UserSession), true)!;
        Set(session, "Id", Actor.SessionId); Set(session, "UserAccountId", Actor.AccountId); Set(session, "SecurityVersion", 1);
        Set(session, "Client", SessionClient.Mobile); Set(session, "RegisteredDeviceId", Device.Id);
        Set(session, "CreatedAt", Clock.Now.AddMinutes(-1)); Set(session, "ExpiresAt", Clock.Now.AddDays(1));
        Enrollment = new(Actor.EmployeeId, "synthetic-template", "synthetic-key", "synthetic", "1", "actor", Device.Id.ToString(), "Synthetic", Clock.Now.AddHours(-1), "synthetic");
        Attempt = new(Enrollment, account, Device, Key, FaceService.Digest(Secret), "synthetic", "1", Clock.Now.AddSeconds(-10), Clock.Now.AddMinutes(2), "synthetic");
        Attempt.RecordProviderResult(true, true, true, "VERIFIED", Clock.Now.AddSeconds(-5)); Set(Attempt, "Enrollment", Enrollment);
        Request = new(false, null, 24m, 67m, 5m, Attempt.Id, Secret);
        var site = Guid.NewGuid(); var shift = Guid.NewGuid(); var membership = Guid.NewGuid();
        Roster = new(Actor.EmployeeId, new(2026, 10, 9), "scheduled", "Asia/Karachi", site, Guid.NewGuid(), Guid.NewGuid(), shift, 1,
            Clock.Now.AddHours(-1), Clock.Now.AddHours(8), 540, 0, 0, new("site", Guid.NewGuid(), membership, null, null, "site-only"));
        var boundary = new SiteGeofenceVersion(site, 1, new(2026, 10, 1), 24m, 67m, 100m, 20m);
        foreach (var code in new[] { AttendanceCapturePermissions.Capture, AttendanceCapturePermissions.SelfRead, AttendanceCapturePermissions.FaceSelf })
            Grants.Add(new(code, false, Guid.NewGuid(), AccessScopeKind.Self, Actor.EmployeeId)
                { AuthorityTimingKnown = true, ValidFromUtc = Clock.Now.AddDays(-1) });
        var current = Proxy.Make<ICurrentAuthentication>((m, a) => Actor);
        var access = Proxy.Make<IAccountAccessReader>((m, a) => {
            var values = Grants.Where(x => !x.AuthorityTimingKnown || x.IsEffectiveAt((DateTimeOffset)a[1]!)).ToArray();
            if (++accessReads == 3 && AdvanceDuringFinalGrantRead) Clock.Now = Clock.Now.AddSeconds(30);
            if (DelayedAccessRead == accessReads) Clock.Now = Clock.Now.AddSeconds(30);
            return Task.FromResult<IReadOnlyList<PermissionGrant>>(values);
        });
        var authorization = Proxy.Make<IAccessManagementRepository>((m, a) => m.Name switch
        {
            "InTransactionAsync" => typeof(PreflightFixture).GetMethod(nameof(RunTransaction))!.MakeGenericMethod(m.GetGenericArguments()[0]).Invoke(this, [a[0], a[1]]),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("SyntheticSelfRole"),
            _ => throw new Exception("Unexpected auth " + m.Name)
        });
        var authentication = Proxy.Make<IAuthenticationRepository>((m, a) => m.Name switch
        {
            "GetSessionAsync" => Task.FromResult<UserSession?>(session),
            "GetDeviceAsync" => Task.FromResult<RegisteredDevice?>(Device),
            _ => throw new Exception("Unexpected identity " + m.Name)
        });
        var receipts = Receipts;
        var journal = Proxy.Make<ISchedulingJournalRepository>((m, a) =>
        {
            switch (m.Name)
            {
                case "IsCurrentSessionAsync": return Task.FromResult(Current);
                case "FindReceiptAsync": return Task.FromResult(receipts.SingleOrDefault(x => x.ActorUserId == (string)a[0]! && x.Operation == (string)a[1]! && x.Key == (string)a[2]!));
                case "AddReceipt": receipts.Add((IdempotencyReceipt)a[0]!); return null;
                case "AddAudit": AccessAudits.Add((AuditEvent)a[0]!); return null;
                case "SaveChangesAsync": return Task.CompletedTask;
                default: throw new Exception(m.Name);
            }
        });
        var files = Proxy.Make<IPrivateFileStore>((m, a) =>
        {
            if (m.Name == "WriteAsync")
            {
                var data = ((byte[])a[0]!).ToArray(); var storage = Guid.NewGuid().ToString("N") + ".enc"; Files.Add(storage, ((string)a[1]!, data));
                return Task.FromResult(new PrivateFileReference(storage, Guid.NewGuid().ToString("N"), Convert.ToHexString(SHA256.HashData(data)), "synthetic", data.Length));
            }
            var reference = (PrivateFileReference)a[0]!; var value = Files[reference.StorageKey];
            if (value.Purpose != (string)a[1]!) throw new Exception("Wrong purpose"); return Task.FromResult(value.Content.ToArray());
        });
        Operation = new AttendanceOperation(current, access, authorization, journal, authentication, Clock, files);
        PrivateFiles = files;
        var retirementRepository = Proxy.Make<IFaceRetirementRepository>((m, a) => m.Name switch
        {
            "RequireTransaction" => InTransaction ? null : throw new InvalidOperationException("Retirement staging outside authorization"),
            "FindIntentAsync" => Task.FromResult(AccessAudits.SingleOrDefault(x => x.EventType == FaceRetirementService.IntentEvent &&
                x.TargetType == (string)a[0]! && x.TargetId == ((Guid)a[1]!).ToString())),
            "Add" => AddRetirementAudit((AuditEvent)a[0]!),
            _ => throw new Exception("Unexpected retirement adapter " + m.Name)
        });
        Retirement = new(retirementRepository, PrivateFiles, Clock);
        var repository = Proxy.Make<IAttendanceCaptureRepository>((m, a) =>
        {
            switch (m.Name)
            {
                case "IdentityAsync": return Task.FromResult(new CaptureIdentity(account, Device));
                case "FindPunchAsync": return Task.FromResult(Raw.SingleOrDefault(x => x.EmployeeId == (Guid)a[0]! && x.IdempotencyKey == (string)a[1]!));
                case "PunchAsync": return Task.FromResult(Raw.SingleOrDefault(x => x.EmployeeId == (Guid)a[0]! && x.Id == (Guid)a[1]!));
                case "LatestAssessmentAsync": return Task.FromResult(Assessments.Where(x => x.RawPunchEventId == (Guid)a[0]!).OrderByDescending(x => x.Revision).FirstOrDefault());
                case "ActiveSessionsAsync": return Task.FromResult<IReadOnlyList<WorkSession>>(Sessions.GroupBy(x => x.SessionKey).Select(x => x.MaxBy(s => s.Revision)!).Where(x => x.EndsAtUtc is null).ToArray());
                case "IsLockedAsync": if (AdvanceDuringLockCheck) Clock.Now = Clock.Now.AddMinutes(3); return Task.FromResult(Locked);
                case "AttemptAsync": return Task.FromResult<FaceVerificationAttempt?>((Guid)a[1]! == Attempt.Id ? Attempt : null);
                case "ActiveEnrollmentAsync": return Task.FromResult<FaceEnrollment?>(Enrollment.IsActive ? Enrollment : null);
                case "Add":
                    switch (a[0]) { case RawPunchEvent v: Raw.Add(v); break; case PunchAssessment v: Assessments.Add(v); break; case WorkSession v: Sessions.Add(v); break; case AttendanceException v: Exceptions.Add(v); break; }
                    return null;
                default: throw new Exception("Unexpected repository " + m.Name);
            }
        });
        var rosters = Proxy.Make<IIndividualRosterResolver>((m, a) => Task.FromResult((DateOnly)a[1]! == Roster.BusinessDate ? Roster :
            new EffectiveRosterResponse(Actor.EmployeeId, (DateOnly)a[1]!, "unassigned", "Asia/Karachi")));
        var geo = Proxy.Make<ISiteGeofenceRepository>((m, a) => m.Name switch
        {
            "VersionAsync" or "EffectiveAsync" => Task.FromResult<SiteGeofenceVersion?>(boundary),
            "EmployeeSitesAsync" => Task.FromResult<IReadOnlyList<EmployeeAttendanceSiteAssignment>>([new(membership, CurrentSiteMatches ? site : Guid.NewGuid())]),
            _ => throw new Exception("Unexpected geofence " + m.Name)
        });
        var provider = Proxy.Make<IFaceVerificationProvider>((m, a) => ++ProviderCalls >= 0 && m.Name == "get_Capability"
            ? new FaceProviderCapability(ProviderReady, "synthetic", "1", ProviderReady ? "ready" : "unavailable")
            : throw new Exception("Capture must not call a provider"));
        var policy = new FaceVerificationPolicy { AuthorityConfirmed = true, ApprovalReference = "synthetic-only", RetentionPolicyReference = "synthetic-only", ChallengeLifetimeSeconds = 120, MaximumAttemptsPerPunch = 3 };
        var templates = Proxy.Make<IFaceTemplateStore>((m, a) => m.Name == "get_IsConfigured" ? true : throw new Exception("Capture must not read a template"));
        var notifications = Proxy.Make<INotificationOutbox>((m, a) => Task.FromResult(true));
        var offices = Proxy.Make<IOfficeAttendanceSourceReader>((m,a) => m.Name switch
        {
            "ReadAsync" => Task.FromResult(Roster.BranchId is null ? new OfficeAttendanceContext("not_office",null,null) :
                OfficeBoundary is null ? new OfficeAttendanceContext("office_boundary_not_configured",null,null) :
                new OfficeAttendanceContext("configured", OfficeAttendanceSource.Capture((Guid)a[0]!, (DateOnly)a[1]!,
                    (EffectiveRosterResponse)a[2]!, OfficeBoundary), OfficeBoundary)),
            "CurrentMembershipMatchesAsync" => Task.FromResult(CurrentSiteMatches),
            _ => throw new Exception(m.Name)
        });
        var syntheticSettings = new CaptureValidationSettings(new(false,null),new(false,null),new(false,null,null,null),new(false,null,null));
        CapturePolicy = new(Guid.NewGuid(),1,null,Clock.Now.AddDays(-1),Clock.Now.AddDays(-2),Actor.AccountId.ToString(),
            "SYNTHETIC explicit disabled groups approval",syntheticSettings,"SYNTHETIC fixture only",new string('0',64),"synthetic-private-policy");
        CapturePolicy = CapturePolicy with { SourceFingerprint = CaptureValidationPolicyRules.SourceFingerprint(CapturePolicy) };
        var capturePolicies = Proxy.Make<ICaptureValidationPolicyReader>((m,a) => {
            if (AdvanceDuringCaptureValidation) Clock.Now = Clock.Now.AddMinutes(3);
            if (ValidationDelay.HasValue) Clock.Now += ValidationDelay.Value;
            OnCaptureValidation?.Invoke();
            return Task.FromResult(CapturePolicy);
        });
        var captureEvidence = Proxy.Make<ICaptureValidationEvidenceRepository>((m,a) => m.Name switch {
            "RecentAsync" => Task.FromResult<IReadOnlyList<RecentCaptureEvidence>>(Raw.Where(x => x.EmployeeId == (Guid)a[0]! &&
                x.ReceivedAtUtc >= (DateTimeOffset)a[1]! && x.ReceivedAtUtc <= (DateTimeOffset)a[2]!)
                .Select(x => new RecentCaptureEvidence(x,Assessments.LastOrDefault(y => y.RawPunchEventId == x.Id))).ToArray()),
            "PunchAsync" => Task.FromResult(Raw.Where(x => x.EmployeeId == (Guid)a[0]! && x.Id == (Guid)a[1]!)
                .Select(x => new RecentCaptureEvidence(x,Assessments.LastOrDefault(y => y.RawPunchEventId == x.Id))).SingleOrDefault()),
            "FinalAsync" => Task.FromResult(ValidationEvidence.SingleOrDefault(x => x.EmployeeId == (Guid)a[0]! && x.RawPunchEventId == (Guid)a[1]!)),
            "Add" => AddValidation((CaptureValidationEvidence)a[0]!),
            _ => throw new Exception(m.Name)
        });
        var validation = new CaptureValidationEvaluator(capturePolicies,captureEvidence,Clock);
        NoticePolicy = new(1, "SYNTHETIC published location notice", "synthetic", Clock.Now.AddDays(-2));
        NoticePolicy.Publish("synthetic", Clock.Now.AddDays(-1));
        var consent = Proxy.Make<ILocationConsentRepository>((m,a) => m.Name switch {
            "GetActivePolicyAsync" => Task.FromResult(NoticePolicy),
            "GetConsentAsync" => Task.FromResult<EmployeeLocationConsent?>(ConsentGranted && NoticePolicy is not null
                ? new(Actor.EmployeeId, NoticePolicy.Id, NoticePolicy.Version, Clock.Now.AddHours(-1)) : null),
            _ => throw new Exception("Unexpected consent " + m.Name)
        });
        Service = new(Operation, repository, rosters, geo, provider, policy, Clock, access, templates, notifications,
            offices,validation,capturePolicies,captureEvidence,authentication,journal,consentAvailable ? consent : null);
    }
    public Task<PunchResponse> In(string? key = null) => Service.SubmitAsync(PunchAction.CheckIn, Request, key ?? Key, Context, default);
    public void UseOffice()
    {
        var branch = Guid.NewGuid(); var assignment = Guid.NewGuid();
        Roster = Roster with { SiteId = null, BranchId = branch, AssignmentId = assignment,
            Explanation = Roster.Explanation! with { Source = "office", SiteAssignmentId = null,
                TemporaryChangeId = null, OfficeRosterAssignmentId = assignment },
            Holiday = new("configured", Roster.BusinessDate, Guid.NewGuid(), "SYNTHETIC office calendar", 1, []) };
        OfficeBoundary = new(Guid.NewGuid(),branch,1,Roster.BusinessDate.AddDays(-1),24m,67m,100m,20m,
            "PK-SD","SYNTHETIC boundary management approval",Actor.AccountId.ToString(),Clock.Now.AddDays(-2));
    }
    private object? AddRetirementAudit(AuditEvent audit) { if (!InTransaction) throw new InvalidOperationException(); AccessAudits.Add(audit); return null; }
    private object? AddValidation(CaptureValidationEvidence evidence) { if (!InTransaction) throw new InvalidOperationException(); ValidationEvidence.Add(evidence); return null; }
    public void VerifyFreshProof(string key)
    {
        Attempt = new(Enrollment, Account, Device, key, FaceService.Digest(Secret), "synthetic", "1",
            Clock.Now.AddSeconds(-1), Clock.Now.AddMinutes(2), "synthetic");
        Attempt.RecordProviderResult(true, true, true, "VERIFIED", Clock.Now);
        Set(Attempt, "Enrollment", Enrollment);
        Request = Request with { IsOffline = false, DeviceCapturedAtUtc = null, FaceAttemptId = Attempt.Id, Proof = Secret };
    }
    public async Task<T> RunTransaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        if (InTransaction) throw new Exception("Nested transaction");
        OnTransactionStart?.Invoke(++TransactionCount);
        InTransaction = true; try { return await action(ct); } finally { InTransaction = false; }
    }
}
