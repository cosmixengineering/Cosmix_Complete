#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyCaptureService.packages.lock.json
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

// Real application service + permission/replay boundary; in-memory repository and IO.
// Synthetic preverified proof is a fixture, not a biometric provider simulation or acceptance.
var checks = 0;
void Check(bool valid, string name) { checks++; if (!valid) throw new Exception(name); }
async Task Deny(Func<Task> action, string code)
{
    checks++;
    try { await action(); } catch (AuthenticationFailure e) when (e.Code == code) { return; }
    throw new Exception("Expected " + code);
}
var office = new Fixture(); office.UseOffice();
var officeIn = await office.In();
Check(officeIn.Outcome == "Accepted" && office.Raw.Single().BranchId == office.Roster.BranchId &&
    office.Raw.Single().SiteId is null && office.Sessions.Single().BranchId == office.Roster.BranchId &&
    office.Sessions.Single().OfficeGeofenceVersionId == office.OfficeBoundary!.Id,
    "office check in uses actual branch boundary and retains office provenance, never a fake site");
office.Clock.Now = office.Clock.Now.AddHours(2); office.ProviderReady = false;
var officeOut = await office.Service.SubmitAsync(PunchAction.CheckOut, new(false,null,24m,67m,5m),
    "office-check-out", office.Context, default);
Check(officeOut.Outcome == "Accepted" && office.Sessions.Count == 2 &&
    office.Sessions[1].OfficeRosterAssignmentId == office.Sessions[0].OfficeRosterAssignmentId &&
    office.Sessions[1].EmployeeAssignmentId == office.Sessions[0].EmployeeAssignmentId &&
    office.Sessions[1].HolidayCalendarId == office.Sessions[0].HolidayCalendarId,
    "office checkout is face-free and retains pinned opening provenance");
var noOfficeBoundary = new Fixture(); noOfficeBoundary.UseOffice(); noOfficeBoundary.OfficeBoundary = null;
var noBoundaryIn = await noOfficeBoundary.In();
Check(noBoundaryIn.Outcome == "QueuedForReview" && noOfficeBoundary.Raw.Count == 1 &&
    noOfficeBoundary.Sessions.Count == 0 && noOfficeBoundary.Attempt.ConsumedAtUtc is null,
    "missing configured office boundary retains unverified evidence, not an accepted duty");
var officeTransfer = new Fixture(); officeTransfer.UseOffice(); officeTransfer.CurrentSiteMatches = false;
Check((await officeTransfer.In()).Outcome == "QueuedForReview" && officeTransfer.Sessions.Count == 0,
    "office current membership is revalidated before accepting historical roster");
var main = new Fixture();
var accepted = await main.In();
Check(accepted.Outcome == "Accepted" && main.Raw.Count == 1 && main.Sessions.Count == 1, "accepted Check In creates evidence and factual session");
Check(main.Attempt.ConsumedByPunchId == accepted.Id, "same operation consumes exact proof");
Check(main.Raw[0].EmployeeId == main.Actor.EmployeeId && main.Raw[0].RegisteredDeviceId == main.Device.Id.ToString("D"), "server identity and device");
Check(main.Raw[0].SiteRosterAssignmentId == main.Roster.Explanation!.SiteAssignmentId && main.Raw[0].BusinessDate == main.Roster.BusinessDate, "immutable schedule pins");
Check(main.Sessions[0].PolicyVersionId is null && main.Assessments[0].PolicyVersionId is null, "factual capture independent of policy");
Check(main.Assessments[0].CalculationVersion == "capture-factual-v3", "office and policy gates have a distinct retained capture calculation version");
Check(main.ValidationEvidence.Single().FinalAssessment.Decision.Outcome == main.Assessments.Single().Outcome &&
    main.ValidationEvidence.Single().FinalAssessment.PolicyVersionId == main.CapturePolicy!.Id &&
    main.ValidationEvidence.Single().RawPunchEventId == accepted.Id,
    "final validation retains exact policy and punch binding inside authorization transaction");
var noCapturePolicy = new Fixture(); noCapturePolicy.CapturePolicy = null;
var noPolicyIn = await noCapturePolicy.In();
Check(noPolicyIn.Outcome == "QueuedForReview" && noPolicyIn.ReasonCode == "CAPTURE_POLICY_NOT_CONFIGURED" &&
    noCapturePolicy.Raw.Count == 1 && noCapturePolicy.Sessions.Count == 0 && noCapturePolicy.Attempt.ConsumedAtUtc is null,
    "missing policy is not silently interpreted as every validation rule disabled");
var noPolicyState = await noCapturePolicy.Service.StateAsync(noCapturePolicy.Context, default);
Check(!noPolicyState.CheckInEnabled && !noPolicyState.CheckOutEnabled && noPolicyState.NextAction == "contact_attendance_support",
    "state does not advertise usable punches when approved capture policy is absent");
var expiredDuringPolicy = new Fixture { AdvanceDuringCaptureValidation = true };
var expiredPolicyIn = await expiredDuringPolicy.In();
Check(expiredPolicyIn.Outcome == "Rejected" && expiredDuringPolicy.Attempt.ConsumedAtUtc is null &&
    expiredDuringPolicy.Sessions.Count == 0 && expiredDuringPolicy.ValidationEvidence.Single().CandidateDecision.Outcome == PunchAssessmentOutcome.Accepted &&
    expiredDuringPolicy.ValidationEvidence.Single().FinalAssessment.Decision.Outcome == PunchAssessmentOutcome.Rejected,
    "proof expiration during asynchronous validation retains rejected final evidence, not the accepted candidate");
var sessionExpiry = new Fixture { ValidationDelay = TimeSpan.FromSeconds(30) };
Fixture.Set(sessionExpiry.Session,"ExpiresAt",sessionExpiry.Clock.Now.AddSeconds(15));
await Deny(() => sessionExpiry.In(),"invalid_authentication");
Check(sessionExpiry.Attempt.ConsumedAtUtc is null && sessionExpiry.Raw.Count == 0 && sessionExpiry.Sessions.Count == 0,
    "session expiring during policy work cannot authorize a new punch even if its face proof is still valid");
var lateRevocation = new Fixture(); lateRevocation.OnCaptureValidation = () => lateRevocation.Current = false;
await Deny(() => lateRevocation.In(),"invalid_authentication");
Check(lateRevocation.Raw.Count == 0 && lateRevocation.ValidationEvidence.Count == 0 && lateRevocation.Attempt.ConsumedAtUtc is null,
    "revocation during validation prevents every new punch/final-evidence mutation");
var lateCaptureGrant = new Fixture();
lateCaptureGrant.OnCaptureValidation = () => lateCaptureGrant.Grants.RemoveAll(x => x.Permission == AttendanceCapturePermissions.Capture);
await Deny(() => lateCaptureGrant.In(),"access_denied");
Check(lateCaptureGrant.Raw.Count == 0 && lateCaptureGrant.Attempt.ConsumedAtUtc is null,
    "retained face permission cannot substitute for a revoked capture grant at finalization");
var lateEnrollment = new Fixture();
lateEnrollment.OnCaptureValidation = () => lateEnrollment.Enrollment.Revoke("synthetic","SYNTHETIC revocation during evidence IO",lateEnrollment.Clock.Now);
Check((await lateEnrollment.In()).Outcome == "Rejected" && lateEnrollment.Attempt.ConsumedAtUtc is null &&
    lateEnrollment.Sessions.Count == 0 && lateEnrollment.ValidationEvidence.Single().FinalAssessment.Decision.Outcome == PunchAssessmentOutcome.Rejected,
    "enrollment revocation during evidence IO cannot consume an earlier accepted candidate");
var captureExpiry = new Fixture { AdvanceDuringFinalGrantRead = true };
var captureGrantIndex = captureExpiry.Grants.FindIndex(x => x.Permission == AttendanceCapturePermissions.Capture);
captureExpiry.Grants[captureGrantIndex] = captureExpiry.Grants[captureGrantIndex] with { ValidUntilUtc = captureExpiry.Clock.Now.AddSeconds(15) };
await Deny(() => captureExpiry.In(),"access_denied");
Check(captureExpiry.Raw.Count == 0 && captureExpiry.ValidationEvidence.Count == 0 && captureExpiry.Attempt.ConsumedAtUtc is null,
    "capture grant expiring during the final read cannot authorize later mutation");
var faceGrantExpiry = new Fixture { AdvanceDuringFinalGrantRead = true };
var faceGrantIndex = faceGrantExpiry.Grants.FindIndex(x => x.Permission == AttendanceCapturePermissions.FaceSelf);
faceGrantExpiry.Grants[faceGrantIndex] = faceGrantExpiry.Grants[faceGrantIndex] with { ValidUntilUtc = faceGrantExpiry.Clock.Now.AddSeconds(15) };
Check((await faceGrantExpiry.In()).Outcome == "Rejected" && faceGrantExpiry.Sessions.Count == 0 && faceGrantExpiry.Attempt.ConsumedAtUtc is null,
    "face grant expiring during the final read cannot be substituted by an earlier valid proof");
var replay = await main.In();
Check(JsonSerializer.Serialize(replay) == JsonSerializer.Serialize(accepted) && main.Raw.Count == 1 && main.Sessions.Count == 1,
    "retry returns semantically identical frozen receipt without duplicated session");
var replayExpiry = new Fixture(); await replayExpiry.In();
var replayGrantIndex = replayExpiry.Grants.FindIndex(x => x.Permission == AttendanceCapturePermissions.Capture);
replayExpiry.Grants[replayGrantIndex] = replayExpiry.Grants[replayGrantIndex] with { ValidUntilUtc = replayExpiry.Clock.Now.AddSeconds(15) };
replayExpiry.DelayedAccessRead = replayExpiry.AccessReads + 2;
await Deny(() => replayExpiry.In(),"access_denied");
Check(replayExpiry.Raw.Count == 1 && replayExpiry.Sessions.Count == 1 && replayExpiry.ValidationEvidence.Count == 1,
    "expired replay authority cannot disclose frozen result or produce another duty");
await Deny(() => main.Service.SubmitAsync(PunchAction.CheckIn, main.Request with { Latitude = 25m }, main.Key, main.Context, default), "idempotency_key_reused");
Check(main.Raw.Count == 1, "changed retry did not create evidence");
Check(main.Files.Values.All(x => !Encoding.UTF8.GetString(x.Content).Contains(main.Secret, StringComparison.Ordinal)), "proof secret not persisted to capture audit");
var second = await main.In("second-check-in");
Check(second.Outcome == "QueuedForReview" && second.ReasonCode == "SEQUENCE_ALREADY_CHECKED_IN" && main.Sessions.Count == 1,
    "competing logical check in cannot open another session");
main.Clock.Now = main.Clock.Now.AddHours(3);
main.ProviderReady = false;
var checkout = await main.Service.SubmitAsync(PunchAction.CheckOut, new(false, null, 24m, 67m, 5m), "checkout-001", main.Context, default);
Check(checkout.Outcome == "Accepted" && main.Sessions.Count == 2 && main.Sessions[0].EndsAtUtc is null && main.Sessions[1].EndsAtUtc.HasValue,
    "checkout closes by new revision without face provider");
Check(main.Sessions[0].SessionKey == main.Sessions[1].SessionKey && main.Sessions[1].Revision == 2, "revision chain preserved");
var breaks = new Fixture(); var firstDuty = await breaks.In(); var firstProof = breaks.Attempt;
breaks.Clock.Now = breaks.Clock.Now.AddHours(1);
var breakOut = await breaks.Service.SubmitAsync(PunchAction.CheckOut, new(false, null, 24m, 67m, 5m), "legit-break-out", breaks.Context, default);
breaks.Clock.Now = breaks.Clock.Now.AddMinutes(15);
var queuedAfterBreak = await breaks.Service.SubmitAsync(PunchAction.CheckIn,
    breaks.Request with { IsOffline = true, DeviceCapturedAtUtc = breaks.Clock.Now.AddMinutes(-1) }, "offline-old-proof", breaks.Context, default);
Check(queuedAfterBreak.Outcome == "Unverified" && breaks.Sessions.Count == 2 &&
    firstProof.ConsumedByPunchId == firstDuty.Id, "offline replay of consumed/expired proof cannot reopen a closed duty");
breaks.VerifyFreshProof("legit-break-in");
var secondDuty = await breaks.In("legit-break-in");
Check(breakOut.Outcome == "Accepted" && secondDuty.Outcome == "Accepted" && breaks.Raw.Count == 4 && breaks.Sessions.Count == 3 &&
    breaks.Sessions[0].SessionKey != breaks.Sessions[2].SessionKey && breaks.Attempt.ConsumedByPunchId == secondDuty.Id,
    "a legitimate second session after a break retains both duties and all raw events");
var state = await main.Service.StateAsync(main.Context, default);
Check(state.State == "checked_out" && !state.CheckInEnabled, "state reports unavailable provider");
var nextAction = new Fixture();
nextAction.Locked = true;
var lockedState = await nextAction.Service.StateAsync(nextAction.Context, default);
Check(!lockedState.CheckInEnabled && lockedState.NextAction == "request_correction", "locked duty never directs employee into a face challenge");
nextAction.Locked = false; nextAction.Roster = nextAction.Roster with { Status = "site_not_assigned" };
var unassignedState = await nextAction.Service.StateAsync(nextAction.Context, default);
Check(!unassignedState.CheckInEnabled && unassignedState.NextAction == "view_shift", "missing schedule directs employee to shift information");
nextAction.Roster = nextAction.Roster with { Status = "scheduled", StartsAt = nextAction.Clock.Now.AddHours(1), EndsAt = nextAction.Clock.Now.AddHours(10) };
var earlyState = await nextAction.Service.StateAsync(nextAction.Context, default);
Check(!earlyState.CheckInEnabled && earlyState.NextAction == "view_shift", "outside permitted schedule window never prompts check in");
var openLocked = new Fixture(); await openLocked.In(); openLocked.Locked = true;
var openLockedState = await openLocked.Service.StateAsync(openLocked.Context, default);
Check(!openLockedState.CheckOutEnabled && openLockedState.NextAction == "request_correction", "locked active session does not advertise enabled checkout");
main.Current = false;
await Deny(() => main.In(), "invalid_authentication");
var expired = new Fixture(); expired.AdvanceDuringLockCheck = true;
var expiredResult = await expired.In();
Check(expiredResult.Outcome == "Rejected" && expired.Attempt.ConsumedAtUtc is null && expired.Sessions.Count == 0,
    "proof expiring during repository work cannot be backdated");
Check(expired.Raw[0].ReceivedAtUtc < expired.Assessments[0].AssessedAtUtc, "receipt and assessment clocks distinguished");
var mismatch = new Fixture(); Fixture.Set(mismatch.Enrollment, "ProviderVersion", "other");
Check((await mismatch.In()).Outcome == "Rejected", "enrollment version mismatch fails closed");
var replacedEnrollment = new Fixture(); Fixture.Set(replacedEnrollment.Attempt, "FaceEnrollmentId", Guid.NewGuid());
Check((await replacedEnrollment.In()).Outcome == "Rejected" && replacedEnrollment.Attempt.ConsumedAtUtc is null,
    "a substituted enrollment reference cannot reach proof consumption");
var missingEnrollment = new Fixture(); Fixture.Set(missingEnrollment.Attempt, "Enrollment", null);
Check((await missingEnrollment.In()).Outcome == "Rejected" && missingEnrollment.Sessions.Count == 0,
    "missing retained enrollment fails closed without a successful punch or null-reference crash");
foreach (var resultField in new[] { "LivenessPassed", "MatchPassed" })
    foreach (bool? result in new bool?[] { null, false })
    {
        var incompleteProof = new Fixture(); Fixture.Set(incompleteProof.Attempt, resultField, result);
        Check((await incompleteProof.In()).Outcome == "Rejected" && incompleteProof.Sessions.Count == 0 &&
            incompleteProof.Attempt.ConsumedAtUtc is null, "passed status cannot substitute for authoritative " + resultField);
    }
var outside = new Fixture(); outside.Request = outside.Request with { Latitude = 25m };
Check((await outside.In()).ReasonCode == "OUTSIDE_GEOFENCE" && outside.Raw.Count == 1 && outside.Sessions.Count == 0 &&
    outside.Exceptions.Single().Kind == AttendanceExceptionKind.OutsideGeofence, "outside evidence and exception retained");
var checkoutBoundary = new Fixture(); await checkoutBoundary.In(); checkoutBoundary.Clock.Now = checkoutBoundary.Clock.Now.AddHours(2);
checkoutBoundary.ProviderReady = false;
var outsideCheckout = await checkoutBoundary.Service.SubmitAsync(PunchAction.CheckOut, new(false,null,25m,67m,5m),"outside-checkout-key",checkoutBoundary.Context,default);
Check(outsideCheckout.Outcome == "QueuedForReview" && outsideCheckout.ReasonCode == "OUTSIDE_GEOFENCE" &&
    checkoutBoundary.Sessions.Count == 1 && checkoutBoundary.Sessions[0].EndsAtUtc is null,
    "outside-radius Checkout preserves open duty and records review evidence");
var inaccurateCheckout = await checkoutBoundary.Service.SubmitAsync(PunchAction.CheckOut,new(false,null,24m,67m,100m),"inaccurate-checkout-key",checkoutBoundary.Context,default);
Check(inaccurateCheckout.Outcome == "QueuedForReview" && inaccurateCheckout.ReasonCode == "LOCATION_UNVERIFIED" &&
    checkoutBoundary.Sessions.Count == 1 && checkoutBoundary.Sessions[0].EndsAtUtc is null,
    "poor-accuracy Checkout cannot close open duty even at site coordinates");
var validCheckout = await checkoutBoundary.Service.SubmitAsync(PunchAction.CheckOut,new(false,null,24m,67m,5m),"valid-checkout-key",checkoutBoundary.Context,default);
Check(validCheckout.Outcome == "Accepted" && checkoutBoundary.Sessions.Count == 2 && checkoutBoundary.Sessions[1].EndsAtUtc == checkoutBoundary.Clock.Now &&
    checkoutBoundary.Raw.Count == 4 && !checkoutBoundary.ProviderReady,
    "subsequent in-radius accurate Checkout closes by new revision without face while rejected evidence remains");
var offline = new Fixture(); offline.Request = offline.Request with { IsOffline = true, DeviceCapturedAtUtc = offline.Clock.Now.AddHours(-2) };
Check((await offline.In()).Outcome == "Unverified" && offline.Sessions.Count == 0 && offline.Attempt.ConsumedAtUtc is null,
    "offline does not consume live proof or trust device time");
var futureCapture = new Fixture(); futureCapture.Request = futureCapture.Request with { DeviceCapturedAtUtc = futureCapture.Clock.Now.AddSeconds(1) };
var futureResult = await futureCapture.In();
Check(futureResult.Outcome == "QueuedForReview" && futureResult.ReasonCode == "CAPTURE_AFTER_RECEIPT" &&
    futureCapture.Raw.Single().DeviceCapturedAtUtc == futureCapture.Request.DeviceCapturedAtUtc &&
    futureCapture.Sessions.Count == 0 && futureCapture.Attempt.ConsumedAtUtc is null,
    "future client capture is retained for review without accepting attendance or consuming proof");
Check(JsonSerializer.Serialize(await futureCapture.In()) == JsonSerializer.Serialize(futureResult) && futureCapture.Raw.Count == 1 && futureCapture.Assessments.Count == 1 &&
    futureCapture.Exceptions.Count == 1, "suspicious chronology retry retains one raw event and one review exception");
var beforeRegistration = new Fixture(); beforeRegistration.Request = beforeRegistration.Request with {
    IsOffline = true, DeviceCapturedAtUtc = beforeRegistration.Device.RegisteredAt.AddTicks(-1) };
var beforeRegistrationResult = await beforeRegistration.In();
Check(beforeRegistrationResult.Outcome == "Unverified" && beforeRegistrationResult.ReasonCode == "CAPTURE_PRECEDES_DEVICE_REGISTRATION" &&
    beforeRegistration.Raw.Count == 1 && beforeRegistration.Sessions.Count == 0 && beforeRegistration.Attempt.ConsumedAtUtc is null,
    "offline capture cannot claim verified evidence before its registered device existed");
var beforeVerification = new Fixture(); Fixture.Set(beforeVerification.Device, "VerifiedAt", beforeVerification.Clock.Now.AddHours(-1));
beforeVerification.Request = beforeVerification.Request with { IsOffline = true, DeviceCapturedAtUtc = beforeVerification.Clock.Now.AddHours(-2) };
Check((await beforeVerification.In()).ReasonCode == "CAPTURE_PRECEDES_DEVICE_VERIFICATION" && beforeVerification.Sessions.Count == 0 &&
    beforeVerification.Attempt.ConsumedAtUtc is null, "offline chronology separately identifies pre-verification evidence");
var futureOffline = new Fixture(); futureOffline.Request = futureOffline.Request with {
    IsOffline = true, DeviceCapturedAtUtc = futureOffline.Clock.Now.AddMinutes(1) };
Check((await futureOffline.In()).ReasonCode == "CAPTURE_AFTER_RECEIPT" && futureOffline.Sessions.Count == 0 &&
    futureOffline.Attempt.ConsumedAtUtc is null, "offline does not hide contradictory claimed time behind generic pending status");
var validClaim = new Fixture(); validClaim.Request = validClaim.Request with { DeviceCapturedAtUtc = validClaim.Clock.Now };
Check((await validClaim.In()).Outcome == "Accepted", "equal claimed and received timestamps preserve online capture semantics");
var suspectCheckout = new Fixture(); await suspectCheckout.In(); suspectCheckout.Clock.Now = suspectCheckout.Clock.Now.AddHours(2);
var suspectOut = await suspectCheckout.Service.SubmitAsync(PunchAction.CheckOut,
    new(false, suspectCheckout.Clock.Now.AddSeconds(1), 24m, 67m, 5m), "suspect-out-time", suspectCheckout.Context, default);
Check(suspectOut.ReasonCode == "CAPTURE_AFTER_RECEIPT" && suspectCheckout.Sessions.Count == 1 &&
    suspectCheckout.Sessions.Single().EndsAtUtc is null, "contradictory checkout time cannot silently close accepted duty");
foreach (var changedSource in new[] { "membership", "roster", "temporary_change", "calendar" })
{
    var changedDuty = new Fixture(); await changedDuty.In(); changedDuty.Clock.Now = changedDuty.Clock.Now.AddHours(2);
    var explanation = changedDuty.Roster.Explanation!;
    changedDuty.Roster = changedSource switch
    {
        "membership" => changedDuty.Roster with { Explanation = explanation with { MembershipRecordId = Guid.NewGuid() } },
        "roster" => changedDuty.Roster with { Explanation = explanation with { SiteAssignmentId = Guid.NewGuid() } },
        "temporary_change" => changedDuty.Roster with { Explanation = explanation with { TemporaryChangeId = Guid.NewGuid() } },
        _ => changedDuty.Roster with { Holiday = new("configured", changedDuty.Roster.BusinessDate, Guid.NewGuid(), "synthetic", 1, []) }
    };
    var changedOut = await changedDuty.Service.SubmitAsync(PunchAction.CheckOut, new(false, null, 24m, 67m, 5m),
        "changed-duty-" + changedSource, changedDuty.Context, default);
    Check(changedOut.Outcome == "QueuedForReview" && changedOut.ReasonCode == "SESSION_EVIDENCE_CONFLICT" &&
        changedDuty.Sessions.Count == 1 && changedDuty.Raw.Count == 2 && changedDuty.Sessions.Single().EndsAtUtc is null,
        "same site and shift cannot hide changed opening " + changedSource + " provenance");
}
var transfer = new Fixture(); transfer.CurrentSiteMatches = false;
Check((await transfer.In()).Outcome == "QueuedForReview" && transfer.Sessions.Count == 0, "transfer relation checked again");
var overnight = new Fixture(); overnight.Clock.Now = overnight.Clock.Now.AddHours(14);
overnight.Roster = overnight.Roster with { StartsAt = overnight.Clock.Now.AddHours(-2), EndsAt = overnight.Clock.Now.AddHours(7) };
overnight.VerifyFreshProof("overnight-in-key");
var nightIn = await overnight.In("overnight-in-key"); overnight.Clock.Now = overnight.Clock.Now.AddHours(3);
overnight.ProviderReady = false;
var nightOut = await overnight.Service.SubmitAsync(PunchAction.CheckOut, new(false, null, 24m, 67m, 5m), "overnight-out-key", overnight.Context, default);
Check(nightIn.Outcome == "Accepted" && nightOut.Outcome == "Accepted" && overnight.Sessions.Count == 2 &&
    nightOut.BusinessDate == nightIn.BusinessDate && RosterSchedule.Today(nightOut.ReceivedAtUtc) > nightOut.BusinessDate &&
    overnight.Sessions[0].SessionKey == overnight.Sessions[1].SessionKey,
    "unchanged historical site provenance permits face-free overnight closure on the original business date");
var locked = new Fixture(); locked.Locked = true;
Check((await locked.In()).ReasonCode == "PERIOD_LOCKED" && locked.Sessions.Count == 0, "locked history only records pending evidence");
var web = new Fixture(); web.Actor = web.Actor with { Client = SessionClient.Web, MfaSatisfied = true };
await Deny(() => web.In(), "access_denied"); Check(web.Raw.Count == 0, "web cannot impersonate a mobile punch");
var noGrant = new Fixture(); noGrant.Grants.Clear(); await Deny(() => noGrant.In(), "access_denied");
var revokedFace = new Fixture(); revokedFace.Grants.RemoveAll(x => x.Permission == AttendanceCapturePermissions.FaceSelf);
Check((await revokedFace.In()).Outcome == "Rejected" && revokedFace.Attempt.ConsumedAtUtc is null, "revoked face self access invalidates unused proof at capture");
var invalid = new Fixture();
await Deny(() => invalid.Service.HistoryAsync(new(new(2026, 10, 1), new(2026, 10, 10), 0, 25), invalid.Context, default), "attendance_history_range_invalid");
await Deny(() => invalid.Service.SessionsAsync(new(new(2026, 10, 1), new(2026, 10, 10), 1, 101), invalid.Context, default), "attendance_history_range_invalid");
await Deny(() => invalid.Service.SubmitAsync(PunchAction.CheckOut, invalid.Request, "out-face-001", invalid.Context, default), "invalid_punch_evidence");
await Deny(() => invalid.Service.SubmitAsync(PunchAction.CheckIn, invalid.Request with { Latitude = 24.12345678m }, "precision-lat", invalid.Context, default), "invalid_punch_evidence");
await Deny(() => invalid.Service.SubmitAsync(PunchAction.CheckIn, invalid.Request with { Longitude = 67.12345678m }, "precision-lng", invalid.Context, default), "invalid_punch_evidence");
await Deny(() => invalid.Service.SubmitAsync(PunchAction.CheckIn, invalid.Request with { AccuracyMeters = 5.123m }, "precision-acc", invalid.Context, default), "invalid_punch_evidence");
var orphan = new Fixture();
Check((await orphan.Service.SubmitAsync(PunchAction.CheckOut, new(false, null, 24m, 67m, 5m), "orphan-out-001", orphan.Context, default)).ReasonCode ==
    "SEQUENCE_NOT_CHECKED_IN", "orphan checkout retained as sequence exception");
var faceFixture = new FaceFixture();
var faceResponse = await faceFixture.Enroll();
Check(faceFixture.ProviderCalls == 1 && faceFixture.Active?.Id == faceResponse.Id && faceFixture.TemplateWrites == 1, "authorized enrollment stages independently protected template");
var retiredFaceFixture = new FaceFixture();
var replacedResponse = await retiredFaceFixture.Enroll();
var priorReference = retiredFaceFixture.Active!.EncryptedTemplateReference;
var replacementResponse = await retiredFaceFixture.Service.EnrollAsync(retiredFaceFixture.Base.Actor.EmployeeId,
    retiredFaceFixture.Request with { ExpectedActiveEnrollmentId = replacedResponse.Id, ExpectedActiveVersion = replacedResponse.Version },
    "replacement-b05-key", retiredFaceFixture.Base.Context, default);
var replacementIntentAudit = retiredFaceFixture.Base.AccessAudits.Single(x => x.EventType == FaceRetirementService.IntentEvent);
var replacementIntent = await retiredFaceFixture.Base.Retirement.ReadIntentAsync(replacementIntentAudit, default);
Check(replacementIntent.SubjectId == replacedResponse.Id && replacementIntent.ReplacementEnrollmentId == replacementResponse.Id &&
    JsonSerializer.Serialize(replacementIntent.Reference) == priorReference, "replacement stages exact prior enrollment intent in authorized finalization");
await retiredFaceFixture.Service.RevokeAsync(retiredFaceFixture.Base.Actor.EmployeeId, replacementResponse.Id,
    new(replacementResponse.Version, "Synthetic B05 revocation"), "revocation-b05-key", retiredFaceFixture.Base.Context, default);
Check(retiredFaceFixture.Base.AccessAudits.Count(x => x.EventType == FaceRetirementService.IntentEvent) == 2,
    "actual revoke consumer stages a separate exact retirement intent");
faceFixture.ProviderReady = false;
Check(await faceFixture.Enroll() == faceResponse && faceFixture.ProviderCalls == 1 && faceFixture.TemplateWrites == 1,
    "enrollment retry resolves receipt before calling unavailable provider again");
await Deny(() => faceFixture.Service.EnrollAsync(faceFixture.Base.Actor.EmployeeId,
    faceFixture.Request with { ProviderEvidenceToken = "changed-synthetic-token" }, "enroll-key-001", faceFixture.Base.Context, default), "idempotency_key_reused");
Check(faceFixture.ProviderCalls == 1, "changed enrollment retry denied before provider");
faceFixture.ProviderReady = true;
await Deny(() => faceFixture.Service.EnrollAsync(faceFixture.Base.Actor.EmployeeId, faceFixture.Request,
    "stale-enroll-002", faceFixture.Base.Context, default), "face_enrollment_version_conflict");
Check(faceFixture.ProviderCalls == 1, "stale enrollment preflight never calls provider");
var noVault = new FaceFixture(); noVault.TemplateReady = false;
await Deny(() => noVault.Enroll(), "face_template_storage_unavailable"); Check(noVault.ProviderCalls == 0, "missing separate vault fails before provider");
Check(noVault.JournalData.Audits.Count == 2 && noVault.JournalData.Audits.Last().EventType.EndsWith("provider_not_run"), "failed readiness is durably audited before error response");
var rejectedEnrollment = new FaceFixture { ProviderReject = true };
await Deny(() => rejectedEnrollment.Enroll(), "face_enrollment_not_verified");
await Deny(() => rejectedEnrollment.Enroll(), "face_enrollment_not_verified");
Check(rejectedEnrollment.ProviderCalls == 1 && rejectedEnrollment.Active is null && rejectedEnrollment.JournalData.Audits.Count == 2,
    "rejected enrollment replay does not call provider again or invent a successful enrollment");
Check(rejectedEnrollment.LastTemplate!.All(x => x == 0), "rejected provider template bytes are zeroed");
Check(rejectedEnrollment.Base.Files.Values.All(x => !Encoding.UTF8.GetString(x.Content).Contains(rejectedEnrollment.Request.ProviderEvidenceToken, StringComparison.Ordinal) &&
    !Encoding.UTF8.GetString(x.Content).Contains("raw-provider-reason", StringComparison.Ordinal)), "failed enrollment audit has no raw token or arbitrary provider reason");
var unavailableEnrollment = new FaceFixture { ProviderOutage = true };
await Deny(() => unavailableEnrollment.Enroll(), "face_provider_unavailable");
await Deny(() => unavailableEnrollment.Enroll(), "face_provider_unavailable");
Check(unavailableEnrollment.ProviderCalls == 1 && unavailableEnrollment.JournalData.Audits.Last().EventType.EndsWith("provider_unavailable"), "outage is a canonical persisted result with safe retry");
var storageFailure = new FaceFixture { FailTemplateWrite = true };
await Deny(() => storageFailure.Enroll(), "face_template_storage_unavailable");
Check(storageFailure.Active is null && storageFailure.JournalData.Audits.Last().EventType.EndsWith("storage_unavailable"), "our storage failure is not reported as vendor failure");
var revokedAfterProvider = new FaceFixture { RevokeAfterProvider = true };
await Deny(() => revokedAfterProvider.Enroll(), "invalid_authentication");
Check(revokedAfterProvider.Active is null && revokedAfterProvider.JournalData.Audits.Last().EventType.EndsWith("finalization_denied"), "post-provider revocation blocks mutation but retains bound audit-only failure");
var inFlight = new FaceFixture { ProviderGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
var firstEnrollment = inFlight.Enroll();
await Deny(() => inFlight.Enroll(), "face_enrollment_reconciliation_pending");
Check(inFlight.ProviderCalls == 1, "in-flight duplicate cannot make another provider call");
inFlight.ProviderGate.SetResult(true); await firstEnrollment;
Check(inFlight.Active is not null && inFlight.JournalData.Audits.Count == 2, "original in-flight owner finalizes one success");
var legacyEnrollment = new FaceFixture(); var legacyResult = await legacyEnrollment.SeedLegacyReceipt();
legacyEnrollment.ProviderReady = false; legacyEnrollment.TemplateReady = false;
Check(await legacyEnrollment.Enroll() == legacyResult && legacyEnrollment.ProviderCalls == 0 && legacyEnrollment.JournalData.Receipts.Count == 0,
    "actual old generic success receipt shape replays before new journal/provider work");
var staleVerification = new FaceFixture();
staleVerification.Base.Actor = staleVerification.Base.Actor with { Client = SessionClient.Mobile, MfaSatisfied = false };
staleVerification.Base.Grants.Clear(); staleVerification.Base.Grants.Add(new(AttendanceCapturePermissions.FaceSelf, false,
    Guid.NewGuid(), AccessScopeKind.Self, staleVerification.Base.Actor.EmployeeId));
Fixture.Set(staleVerification.Base.Attempt, "Outcome", FaceVerificationOutcome.Pending);
Fixture.Set(staleVerification.Base.Attempt, "RowVersion", new byte[8]);
Fixture.Set(staleVerification.Base.Enrollment, "EncryptedTemplateReference", JsonSerializer.Serialize(new PrivateFileReference(
    "synthetic-template.enc", "1", new string('A', 64), "synthetic-biometric-key", 3)));
await Deny(() => staleVerification.Service.VerifyAsync(staleVerification.Base.Attempt.Id,
    new(staleVerification.Base.Secret, "synthetic-provider-evidence", Convert.ToBase64String(BitConverter.GetBytes(7L))),
    "verify-stale-001", staleVerification.Base.Context, default), "scheduling_version_conflict");
Check(staleVerification.ProviderCalls == 0, "stale pending verification denied before external provider");
var replayReadinessFailures = new List<string>();
foreach (var scenario in new[] { "expired", "expires-during-reauthorization", "consumed", "revoked", "replaced", "provider-unavailable", "provider-changed", "storage-unavailable", "policy-unconfirmed" })
{
    var verifiedReplay = new VerificationFixture();
    var originalVerification = await verifiedReplay.Verify();
    Check(originalVerification.Outcome == "Passed" && originalVerification.CanSubmitCheckIn && verifiedReplay.ProviderCalls == 1,
        "fresh synthetic provider result is usable before replay scenario " + scenario);
    switch (scenario)
    {
        case "expired": verifiedReplay.Base.Clock.Now = originalVerification.ExpiresAtUtc; break;
        case "expires-during-reauthorization":
            verifiedReplay.Base.Clock.Now = originalVerification.ExpiresAtUtc.AddSeconds(-15);
            verifiedReplay.Base.DelayedAccessRead = verifiedReplay.Base.AccessReads + 4; break;
        case "consumed": Check((await verifiedReplay.Base.In()).Outcome == "Accepted", "consume synthetic verified proof through actual punch service"); break;
        case "revoked": verifiedReplay.Base.Enrollment.Revoke("synthetic", "Synthetic replay revocation", verifiedReplay.Base.Clock.Now); break;
        case "replaced": verifiedReplay.Active = new(verifiedReplay.Base.Actor.EmployeeId, "synthetic-replacement-template", "synthetic-key",
            "synthetic", "1", "synthetic", verifiedReplay.Base.Device.Id.ToString(), "Synthetic replacement", verifiedReplay.Base.Clock.Now, "synthetic"); break;
        case "provider-unavailable": verifiedReplay.ProviderReady = false; break;
        case "provider-changed": verifiedReplay.ProviderVersion = "2"; break;
        case "storage-unavailable": verifiedReplay.TemplateReady = false; break;
        case "policy-unconfirmed": verifiedReplay.Policy.AuthorityConfirmed = false; break;
    }
    var replayedVerification = await verifiedReplay.Verify();
    checks++;
    if (replayedVerification.CanSubmitCheckIn) replayReadinessFailures.Add(scenario);
    Check(replayedVerification with { CanSubmitCheckIn = false } == originalVerification with { CanSubmitCheckIn = false } &&
        verifiedReplay.ProviderCalls == 1, "verification replay preserves historical result and never repeats provider: " + scenario);
}
Check(replayReadinessFailures.Count == 0, "verification replay must refresh readiness: " + string.Join(", ", replayReadinessFailures));
var readyVerificationReplay = new VerificationFixture();
var stillReadyVerification = await readyVerificationReplay.Verify();
Check(await readyVerificationReplay.Verify() == stillReadyVerification && readyVerificationReplay.ProviderCalls == 1,
    "current unconsumed compatible verification remains usable on exact replay");
readyVerificationReplay.Base.Current = false;
await Deny(() => readyVerificationReplay.Verify(), "invalid_authentication");
readyVerificationReplay.Base.Current = true; readyVerificationReplay.Base.Grants.Clear();
await Deny(() => readyVerificationReplay.Verify(), "access_denied");
Check(readyVerificationReplay.ProviderCalls == 1, "revoked session or permission cannot disclose a replay or call provider again");
var history = new EnrollmentHistoryFixture();
var failureHistory = await history.Start(EnrollmentFailure.ProviderRejected);
history.Base.Clock.Now = history.Base.Clock.Now.AddMinutes(1);
var pendingHistory = await history.Start();
history.Base.Clock.Now = history.Base.Clock.Now.AddMinutes(1);
var successHistory = await history.Start(success: true);
var historyPage = await history.Read();
Check(historyPage.Items.Count == 3 && historyPage.NextCursor is null && history.QueryCount == 1 && history.LastTake == 101,
    "history uses one bounded candidate query without total counts");
Check(historyPage.Items.Single(x => x.AttemptId == failureHistory.Start.ReceiptId).Code == "face_enrollment_not_verified",
    "canonical provider rejection is user-readable without raw provider reason");
var pendingItem = historyPage.Items.Single(x => x.AttemptId == pendingHistory.Start.ReceiptId);
Check(pendingItem.Stage == "reconciliation_pending" && pendingItem.CompletedAtUtc is null,
    "unfinished attempt remains pending rather than inferred success/failure");
Check(historyPage.Items.Single(x => x.AttemptId == successHistory.Start.ReceiptId).Stage == "succeeded",
    "history success requires actual terminal evidence");
var safeHistory = JsonSerializer.Serialize(historyPage);
Check(!new[] { "Actor", "Device", "Reference", "Sha256", "Enrollment\"", "Template", "FinalizationProof", "Correlation" }
    .Any(x => safeHistory.Contains(x, StringComparison.Ordinal)), "history DTO does not expose internal authority, private metadata or biometric evidence");
Check(history.Base.AccessAudits.Last().EventType == "attendance.face.enrollment.attempt.history",
    "successful history reads are audited through the normal attendance boundary");
history.Base.Clock.Now = history.Base.Clock.Now.AddHours(25);
Check((await history.Read()).Items.Count == 3, "24-hour retry expiry does not erase retained outcome history");
history.Base.Actor = history.Base.Actor with { MfaSatisfied = false };
await Deny(() => history.Read(), "access_denied");
history.Base.Actor = history.Base.Actor with { MfaSatisfied = true, Client = SessionClient.Mobile };
await Deny(() => history.Read(), "access_denied");
history.Base.Actor = history.Base.Actor with { Client = SessionClient.Web };
history.Base.Grants.Clear(); await Deny(() => history.Read(), "access_denied");
var scopedHistory = new EnrollmentHistoryFixture();
var visibleHistory = await scopedHistory.Start(EnrollmentFailure.ProviderUnavailable);
for (var i = 0; i < 100; i++)
{ scopedHistory.Base.Clock.Now = scopedHistory.Base.Clock.Now.AddSeconds(1); await scopedHistory.Start(EnrollmentFailure.ProviderRejected, originalSite: Guid.NewGuid()); }
var emptyHistory = await scopedHistory.Read();
Check(emptyHistory.Items.Count == 0 && emptyHistory.NextCursor is not null,
    "100 original-scope-denied candidates produce no metadata but retain bounded scan continuation");
var retainedGrant = scopedHistory.Base.Grants.Single(); scopedHistory.Base.Grants.Clear();
await Deny(() => scopedHistory.Read(scopedHistory.Request with { Cursor = emptyHistory.NextCursor }), "access_denied");
scopedHistory.Base.Grants.Add(retainedGrant);
var nextHistory = await scopedHistory.Read(scopedHistory.Request with { Cursor = emptyHistory.NextCursor });
Check(nextHistory.Items.Single().AttemptId == visibleHistory.Start.ReceiptId && nextHistory.NextCursor is null,
    "continuation advances by last scanned candidate and reaches an older authorized attempt");
await Deny(() => scopedHistory.Read(scopedHistory.Request with { Cursor = emptyHistory.NextCursor, PageSize = 24 }), "invalid_attendance_cursor");
scopedHistory.Base.Actor = scopedHistory.Base.Actor with { AccountId = Guid.NewGuid() };
await Deny(() => scopedHistory.Read(scopedHistory.Request with { Cursor = emptyHistory.NextCursor }), "invalid_attendance_cursor");
scopedHistory.CurrentSite = Guid.NewGuid(); await Deny(() => scopedHistory.Read(), "access_denied");
var invalidHistory = new EnrollmentHistoryFixture();
await Deny(() => invalidHistory.Read(invalidHistory.Request with { PageSize = 101 }), "invalid_face_enrollment_history_request");
await Deny(() => invalidHistory.Read(invalidHistory.Request with { FromUtc = invalidHistory.Request.ToUtc }), "invalid_face_enrollment_history_request");
await Deny(() => invalidHistory.Read(invalidHistory.Request with { ToUtc = invalidHistory.Request.FromUtc.AddDays(367) }), "invalid_face_enrollment_history_request");
await Deny(() => invalidHistory.Read(invalidHistory.Request with { FromUtc = invalidHistory.Request.FromUtc.ToOffset(TimeSpan.FromHours(5)) }), "invalid_face_enrollment_history_request");
Check(invalidHistory.QueryCount == 0, "invalid ranges and bounds are rejected before repository work");
var corruptHistory = new EnrollmentHistoryFixture(); await corruptHistory.Start(EnrollmentFailure.ProviderRejected);
Fixture.Set(corruptHistory.Data.Receipts.Single(x => x.Operation == FaceEnrollmentJournal.TerminalOperation), "ProtectedResultReference", null);
await Deny(() => corruptHistory.Read(), "face_enrollment_journal_unavailable");
var replacedHistory = new EnrollmentHistoryFixture(); await replacedHistory.Start();
Fixture.Set(replacedHistory.Data.Audits.Single(), "ProtectedChangesReference", "substituted-private-reference");
await Deny(() => replacedHistory.Read(), "face_enrollment_journal_unavailable");
var camera = new Fixture();
Fixture.Set(camera.Attempt, "Outcome", FaceVerificationOutcome.Pending);
Fixture.Set(camera.Attempt, "CompletedAtUtc", null);
Fixture.Set(camera.Attempt, "RowVersion", new byte[8]);
var cameraProtector = new CaptureProtectorFixture();
var cameraOptions = new FaceCaptureOptions { Enabled = true, LifetimeSeconds = 60, ApprovalReference = "synthetic PoC only" };
var cameraPolicy = new FaceVerificationPolicy { AuthorityConfirmed = true, ApprovalReference = "synthetic authority",
    RetentionPolicyReference = "synthetic retention", ChallengeLifetimeSeconds = 120, MaximumAttemptsPerPunch = 3 };
var cameraStore = Proxy.Make<IFaceTemplateStore>((method, args) => method.Name switch {
    "get_IsConfigured" => true,
    "WriteAsync" => camera.PrivateFiles.WriteAsync((byte[])args[0]!, (string)args[1]!, (CancellationToken)args[2]!),
    "ReadAsync" => camera.PrivateFiles.ReadAsync((PrivateFileReference)args[0]!, (string)args[1]!, (CancellationToken)args[2]!),
    _ => throw new Exception(method.Name)
});
var cameraRepository = Proxy.Make<IFaceRepository>((method, args) => method.Name switch {
    "AttemptAsync" => Task.FromResult<FaceVerificationAttempt?>((Guid)args[0]! == camera.Actor.EmployeeId && (Guid)args[1]! == camera.Attempt.Id ? camera.Attempt : null),
    "EmployeeDeviceAsync" => (Guid)args[0]! == camera.Actor.EmployeeId && (Guid)args[1]! == camera.Device.Id
        ? Task.FromResult(new FaceEmployeeDevice((UserAccount)Activator.CreateInstance(typeof(UserAccount), true)!, camera.Device))
        : throw AttendanceOperation.Denied(),
    _ => throw new Exception(method.Name)
});
var cameraService = new FaceCaptureService(camera.Operation, cameraRepository, cameraStore, cameraProtector,
    cameraOptions, cameraPolicy, camera.Clock);
var cameraReader = new FaceCaptureEvidenceReader(camera.Operation, cameraRepository, cameraStore, cameraProtector,
    cameraOptions, cameraPolicy, camera.Clock, camera.Retirement);
// Header fixture exercises upload/claim boundaries only. Actual codecs/provider tests reject incomplete images.
var cameraImage = new byte[] { 137,80,78,71,13,10,26,10 };
var cameraRequest = new UploadVerificationFaceCaptureRequest(camera.Secret, Convert.ToBase64String(new byte[8]), Convert.ToBase64String(cameraImage));
var uploaded = await cameraService.UploadVerificationAsync(camera.Attempt.Id, cameraRequest, "camera-upload-001", camera.Context, default);
Check(uploaded.ExpiresAtUtc == camera.Clock.Now.AddSeconds(60), "Explicit capture TTL and no attendance success result");
var cameraBinding = new FaceCaptureBinding(camera.Actor.AccountId, camera.Actor.SessionId, camera.Actor.EmployeeId,
    camera.Device.Id, camera.Attempt.Id, FaceService.Digest(camera.Secret));
var cameraOperationKey = $"verification:{camera.Attempt.Id:N}";
var imageRead = await cameraReader.ReadAsync(uploaded.ProviderEvidenceToken, cameraBinding, cameraOperationKey, default);
Check(imageRead.SequenceEqual(cameraImage), "Server capture claim reads only its protected bound image");
Check(camera.AccessAudits.Count(x => x.EventType == FaceRetirementService.IntentEvent) == 1,
    "capture claim transaction stages one exact intent while challenge is pending");
CryptographicOperations.ZeroMemory(imageRead);
var cameraRetry = await cameraService.UploadVerificationAsync(camera.Attempt.Id, cameraRequest, "camera-upload-001", camera.Context, default);
Check(cameraRetry == uploaded, "Exact capture upload replay retains original token and expiry");
cameraOptions.Enabled = false;
await Deny(() => cameraService.UploadVerificationAsync(camera.Attempt.Id, cameraRequest, "camera-upload-001", camera.Context, default), "face_capture_not_configured");
cameraOptions.Enabled = true;
await Deny(() => cameraReader.ReadAsync(uploaded.ProviderEvidenceToken, cameraBinding, "enrollment:changed-operation", default), "idempotency_key_reused");
var beforeCameraReplay = camera.Files.Count;
imageRead = await cameraReader.ReadAsync(uploaded.ProviderEvidenceToken, cameraBinding, cameraOperationKey, default);
CryptographicOperations.ZeroMemory(imageRead);
Check(camera.Files.Count == beforeCameraReplay, "Claim replay cannot allocate another capture or receipt");
Fixture.Set(camera.Attempt, "Outcome", FaceVerificationOutcome.Passed);
await Deny(() => cameraReader.ReadAsync(uploaded.ProviderEvidenceToken, cameraBinding, cameraOperationKey, default), "face_challenge_already_completed");
Fixture.Set(camera.Attempt, "Outcome", FaceVerificationOutcome.Pending);
var revokeAtTransaction = camera.TransactionCount + 2;
camera.OnTransactionStart = count => {
    if (count == revokeAtTransaction) camera.Enrollment.Revoke("synthetic", "Synthetic revocation between preflight and replay", camera.Clock.Now);
};
await Deny(() => cameraReader.ReadAsync(uploaded.ProviderEvidenceToken, cameraBinding, cameraOperationKey, default), "face_enrollment_revoked");
camera.OnTransactionStart = null;
await Deny(() => cameraReader.ReadAsync(uploaded.ProviderEvidenceToken, cameraBinding, cameraOperationKey, default), "face_enrollment_revoked");
camera.Grants.Clear();
await Deny(() => cameraService.UploadVerificationAsync(camera.Attempt.Id, cameraRequest, "camera-upload-001", camera.Context, default), "access_denied");
await Deny(() => Task.FromResult(FaceCaptureImage.Decode("https://example.invalid/frame.jpg")), "invalid_face_capture_image");
await Deny(() => Task.FromResult(FaceCaptureImage.Decode(Convert.ToBase64String(new byte[524289]))), "invalid_face_capture_image");
await Deny(() => Task.FromResult(FaceCaptureImage.Decode(Convert.ToBase64String("not an image"u8.ToArray()))), "invalid_face_capture_image");
Check(camera.AccessAudits.Any(x => x.EventType == "attendance.face.capture.claim") && camera.Raw.Count == 0,
    "Capture claims audited without fabricating raw punches or face success");
Console.WriteLine($"Capture service checks passed: {checks}. Real application logic; no SQL, HTTP, live auth or provider acceptance claimed.");

public sealed class CaptureProtectorFixture : IFaceCaptureProtector
{
    private readonly Dictionary<string, FaceCaptureEnvelope> envelopes = [];
    public string Protect(FaceCaptureEnvelope envelope)
    { var key = Guid.NewGuid().ToString("N"); envelopes.Add(key, envelope); return key; }
    public FaceCaptureEnvelope Unprotect(string token, FaceCaptureBinding binding) =>
        envelopes.TryGetValue(token, out var value) && value.Binding == binding ? value : throw AttendanceOperation.Denied();
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
public sealed class Fixture
{
    public static void Set(object entity, string name, object? value) => entity.GetType().GetProperty(name)!.SetValue(entity, value);
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
    public Dictionary<string, (string Purpose, byte[] Content)> Files = [];
    public IPrivateFileStore PrivateFiles = null!;
    public FaceRetirementService Retirement = null!;
    public bool InTransaction;
    public int TransactionCount;
    public Action<int>? OnTransactionStart;
    public bool Current = true, ProviderReady = true, AdvanceDuringLockCheck, CurrentSiteMatches = true, Locked;
    public Fixture()
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
            "InTransactionAsync" => typeof(Fixture).GetMethod(nameof(RunTransaction))!.MakeGenericMethod(m.GetGenericArguments()[0]).Invoke(this, [a[0], a[1]]),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("SyntheticSelfRole"),
            _ => throw new Exception("Unexpected auth " + m.Name)
        });
        var authentication = Proxy.Make<IAuthenticationRepository>((m, a) => m.Name switch
        {
            "GetSessionAsync" => Task.FromResult<UserSession?>(session),
            "GetDeviceAsync" => Task.FromResult<RegisteredDevice?>(Device),
            _ => throw new Exception("Unexpected identity " + m.Name)
        });
        var receipts = new List<IdempotencyReceipt>();
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
        var provider = Proxy.Make<IFaceVerificationProvider>((m, a) => m.Name == "get_Capability"
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
        Service = new(Operation, repository, rosters, geo, provider, policy, Clock, access, templates, notifications,
            offices,validation,capturePolicies,captureEvidence,authentication,journal);
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

public sealed class FaceFixture
{
    public Fixture Base = new();
    public FaceEnrollment? Active;
    public FaceService Service;
    public FaceEnrollmentRequest Request;
    public int ProviderCalls, TemplateWrites;
    public bool ProviderReady = true, TemplateReady = true;
    public bool ProviderReject, ProviderOutage, FailTemplateWrite, RevokeAfterProvider;
    public TaskCompletionSource<bool>? ProviderGate;
    public byte[]? LastTemplate;
    public FaceJournalData JournalData;
    private int version;
    public FaceFixture()
    {
        Base.Actor = Base.Actor with { Client = SessionClient.Web, MfaSatisfied = true };
        Base.Grants.Clear(); Base.Grants.Add(new(AttendanceCapturePermissions.FaceManage, true, Guid.NewGuid(), AccessScopeKind.Employee, Base.Actor.EmployeeId));
        var repository = Proxy.Make<IFaceRepository>((m, a) => m.Name switch
        {
            "ResourcesAsync" => Task.FromResult<IReadOnlyList<AccessResource>>([new(EmployeeId: Base.Actor.EmployeeId)]),
            "EmployeeDeviceAsync" => Task.FromResult(new FaceEmployeeDevice(null!, Base.Device)),
            "ActiveAsync" => Task.FromResult(Active),
            "EnrollmentAsync" => Task.FromResult(Active?.Id == (Guid)a[1]! ? Active : null),
            "AttemptAsync" => Task.FromResult<FaceVerificationAttempt?>(Base.Attempt),
            "Add" => Add((FaceEnrollment)a[0]!),
            "SaveChangesAsync" => Save(),
            _ => throw new Exception("Unexpected face repository " + m.Name)
        });
        var provider = Proxy.Make<IFaceVerificationProvider>((m, a) =>
        {
            if (m.Name == "get_Capability") return new FaceProviderCapability(ProviderReady, "synthetic", "1", ProviderReady ? "ready" : "unavailable");
            if (m.Name != "EnrollAsync") throw new Exception("Unexpected provider call");
            return Provide((FaceProviderEnrollmentInput)a[0]!);
        });
        var templates = Proxy.Make<IFaceTemplateStore>((m, a) =>
        {
            if (m.Name == "get_IsConfigured") return TemplateReady;
            if (m.Name != "WriteAsync") throw new Exception("Unexpected template call");
            TemplateWrites++; var bytes = (byte[])a[0]!;
            if (FailTemplateWrite) throw new AuthenticationFailure("private_storage_unavailable", 503);
            return Task.FromResult(new PrivateFileReference(Guid.NewGuid().ToString("N") + ".enc", Guid.NewGuid().ToString("N"), Convert.ToHexString(SHA256.HashData(bytes)), "separate-synthetic-biometric-key", bytes.Length));
        });
        JournalData = new(Base);
        Service = new(Base.Operation, repository, provider, new FaceVerificationPolicy { AuthorityConfirmed = true,
            ApprovalReference = "synthetic-only", RetentionPolicyReference = "synthetic-only", ChallengeLifetimeSeconds = 120, MaximumAttemptsPerPunch = 3 }, templates, Base.Clock,
            new(JournalData, Base.PrivateFiles, Base.Clock), Base.Retirement);
        Request = new(Base.Device.Id, "synthetic-signed-provider-evidence", null, null, "Synthetic controlled enrollment");
    }
    private object? Add(FaceEnrollment enrollment) { Active = enrollment; return null; }
    private Task Save() { if (Active is not null) Fixture.Set(Active, "RowVersion", BitConverter.GetBytes(++version * 1L)); return Task.CompletedTask; }
    public Task<FaceEnrollmentResponse> Enroll() => Service.EnrollAsync(Base.Actor.EmployeeId, Request, "enroll-key-001", Base.Context, default);
    private async Task<FaceProviderEnrollmentResult> Provide(FaceProviderEnrollmentInput input)
    {
        if (Base.InTransaction) throw new Exception("Provider call held authorization transaction");
        ProviderCalls++;
        if (!Guid.TryParseExact(input.IdempotencyKey.Replace("enrollment:", "", StringComparison.Ordinal), "N", out _))
            throw new Exception("Provider enrollment needs immutable server attempt identity");
        if (ProviderGate is not null) await ProviderGate.Task;
        if (RevokeAfterProvider) Base.Current = false;
        if (ProviderOutage) throw new HttpRequestException("raw-provider-reason with sensitive diagnostic context");
        LastTemplate = [1, 2, 3];
        return new(!ProviderReject, "synthetic", "1", input.EmployeeId, input.DeviceId, LastTemplate, "raw-provider-reason");
    }
    public Task<FaceEnrollmentResponse> SeedLegacyReceipt()
    {
        var payload = new { Request.RegisteredDeviceId, Request.ExpectedActiveEnrollmentId, Request.ExpectedActiveVersion,
            EvidenceDigest = FaceService.Digest(Request.ProviderEvidenceToken) };
        var response = new FaceEnrollmentResponse(Guid.NewGuid(), Base.Actor.EmployeeId, "synthetic", "1", Base.Clock.Now, true, null, null, "AAAAAAAAAAE=");
        return Base.Operation.WriteAsync(AttendanceCapturePermissions.FaceManage, "attendance.face.enrollment", Base.Actor.EmployeeId,
            "enroll-key-001", payload, Request.Reason, Base.Context, (_, _) => Task.FromResult(new AttendanceChange<FaceEnrollmentResponse>(response.Id, null, response)),
            default, resources: (_, _) => Task.FromResult<IReadOnlyList<AccessResource>>([new(EmployeeId: Base.Actor.EmployeeId)]));
    }
}

public sealed class VerificationFixture
{
    public Fixture Base = new();
    public FaceEnrollment? Active;
    public FaceVerificationPolicy Policy = new() { AuthorityConfirmed = true, ApprovalReference = "synthetic-only",
        RetentionPolicyReference = "synthetic-only", ChallengeLifetimeSeconds = 120, MaximumAttemptsPerPunch = 3 };
    public bool ProviderReady = true, TemplateReady = true;
    public string ProviderVersion = "1";
    public int ProviderCalls;
    private readonly FaceService service;
    private readonly VerifyFaceRequest request;
    private int version = 1;
    public VerificationFixture()
    {
        Active = Base.Enrollment;
        Fixture.Set(Base.Attempt, "Outcome", FaceVerificationOutcome.Pending);
        Fixture.Set(Base.Attempt, "LivenessPassed", null); Fixture.Set(Base.Attempt, "MatchPassed", null);
        Fixture.Set(Base.Attempt, "CompletedAtUtc", null); Fixture.Set(Base.Attempt, "ResultReasonCode", null);
        Fixture.Set(Base.Attempt, "RowVersion", BitConverter.GetBytes(1L));
        var template = new byte[] { 1, 2, 3 };
        Fixture.Set(Base.Enrollment, "EncryptedTemplateReference", JsonSerializer.Serialize(new PrivateFileReference(
            Guid.NewGuid().ToString("N") + ".enc", Guid.NewGuid().ToString("N"), Convert.ToHexString(SHA256.HashData(template)), "synthetic-key", template.Length)));
        var repository = Proxy.Make<IFaceRepository>((method, args) => method.Name switch
        {
            "AttemptAsync" => Task.FromResult<FaceVerificationAttempt?>((Guid)args[0]! == Base.Actor.EmployeeId &&
                (Guid)args[1]! == Base.Attempt.Id ? Base.Attempt : null),
            "ActiveAsync" => Task.FromResult(Active?.IsActive == true ? Active : null),
            "SaveChangesAsync" => Save(),
            _ => throw new Exception("Unexpected verification repository " + method.Name)
        });
        var provider = Proxy.Make<IFaceVerificationProvider>((method, args) => method.Name switch
        {
            "get_Capability" => new FaceProviderCapability(ProviderReady, "synthetic", ProviderVersion, ProviderReady ? "ready" : "unavailable"),
            "VerifyAsync" => Provide((FaceProviderVerificationInput)args[0]!),
            _ => throw new Exception("Unexpected verification provider " + method.Name)
        });
        var templates = Proxy.Make<IFaceTemplateStore>((method, args) => method.Name switch
        {
            "get_IsConfigured" => TemplateReady,
            "ReadAsync" => Task.FromResult(template.ToArray()),
            _ => throw new Exception("Unexpected verification template store " + method.Name)
        });
        service = new(Base.Operation, repository, provider, Policy, templates, Base.Clock,
            new(new FaceJournalData(Base), Base.PrivateFiles, Base.Clock), Base.Retirement);
        request = new(Base.Secret, "synthetic-provider-evidence", Convert.ToBase64String(BitConverter.GetBytes(1L)));
    }
    private Task Save() { Fixture.Set(Base.Attempt, "RowVersion", BitConverter.GetBytes(++version * 1L)); return Task.CompletedTask; }
    private Task<FaceProviderVerificationResult> Provide(FaceProviderVerificationInput input)
    {
        if (Base.InTransaction) throw new Exception("Verification provider call held authorization transaction");
        ProviderCalls++;
        return Task.FromResult(new FaceProviderVerificationResult(true, true, true, "synthetic", "1", input.AttemptId,
            input.EmployeeId, input.DeviceId, FaceService.Digest(input.Challenge), "synthetic-verified"));
    }
    public Task<FaceVerificationResponse> Verify() => service.VerifyAsync(Base.Attempt.Id, request, "verify-replay-001", Base.Context, default);
}

public sealed class FaceJournalData(Fixture fixture) : IFaceEnrollmentJournalRepository
{
    public List<IdempotencyReceipt> Receipts = []; public List<AuditEvent> Audits = [];
    public void RequireTransaction() { if (!fixture.InTransaction) throw new Exception("Missing authorization transaction"); }
    public Task<IdempotencyReceipt?> FindAsync(string actor, string operation, string key, CancellationToken ct) =>
        Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == actor && x.Operation == operation && x.Key == key));
    public Task<IdempotencyReceipt?> StartAsync(Guid id, CancellationToken ct) => Task.FromResult(Receipts.SingleOrDefault(x => x.Id == id));
    public void Add(IdempotencyReceipt receipt) { RequireTransaction(); Receipts.Add(receipt); }
    public void Add(AuditEvent audit) { RequireTransaction(); Audits.Add(audit); }
    public Task SaveChangesAsync(CancellationToken ct) { RequireTransaction(); return Task.CompletedTask; }
    public Task<T> InAuditTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) => fixture.RunTransaction(action, ct);
}

public sealed class EnrollmentHistoryFixture
{
    public Fixture Base = new();
    public Guid CurrentSite = Guid.NewGuid();
    public FaceJournalData Data;
    public FaceEnrollmentJournal Journal;
    public FaceEnrollmentHistoryService Service;
    public FaceEnrollmentHistoryRequest Request = new(new(2026, 10, 9, 0, 0, 0, TimeSpan.Zero), new(2026, 10, 10, 0, 0, 0, TimeSpan.Zero));
    public int QueryCount, LastTake;
    public EnrollmentHistoryFixture()
    {
        Base.Actor = Base.Actor with { Client = SessionClient.Web, MfaSatisfied = true };
        Base.Grants.Clear(); Base.Grants.Add(new(AttendanceCapturePermissions.FaceRead, true, Guid.NewGuid(), AccessScopeKind.Site, CurrentSite));
        Data = new(Base); Journal = new(Data, Base.PrivateFiles, Base.Clock);
        var employees = Proxy.Make<IFaceRepository>((m, a) => m.Name == "ResourcesAsync"
            ? Task.FromResult<IReadOnlyList<AccessResource>>([new(EmployeeId: Base.Actor.EmployeeId, SiteId: CurrentSite)]) : throw new Exception(m.Name));
        var access = Proxy.Make<IAccountAccessReader>((m, a) => Task.FromResult<IReadOnlyList<PermissionGrant>>(Base.Grants.ToArray()));
        var repository = Proxy.Make<IFaceEnrollmentHistoryRepository>((m, a) =>
        {
            QueryCount++; LastTake = (int)a[4]!;
            var before = (AttendanceCursorPosition?)a[3];
            var rows = Data.Audits.Where(x => x.EmployeeScopeId == (Guid)a[0]! && x.EventType == "attendance.face.enrollment.started" &&
                x.OccurredAt >= (DateTimeOffset)a[1]! && x.OccurredAt < (DateTimeOffset)a[2]!);
            if (before is not null) rows = rows.Where(x => x.OccurredAt < before.CreatedAtUtc || x.OccurredAt == before.CreatedAtUtc && x.Id.CompareTo(before.Id) < 0);
            return Task.FromResult<IReadOnlyList<AuditEvent>>(rows.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id).Take(LastTake).ToArray());
        });
        var retained = new Dictionary<string, (Guid Account, string Purpose, string Fingerprint, AttendanceCursorPosition Position)>();
        var cursors = Proxy.Make<IAttendanceCursorProtector>((m, a) =>
        {
            if (m.Name == "Protect")
            {
                var key = "synthetic-opaque-" + Guid.NewGuid().ToString("N");
                retained.Add(key, ((Guid)a[0]!, (string)a[1]!, (string)a[2]!, (AttendanceCursorPosition)a[3]!)); return key;
            }
            if (!retained.TryGetValue((string)a[0]!, out var stored) || stored.Account != (Guid)a[1]! ||
                stored.Purpose != (string)a[2]! || stored.Fingerprint != (string)a[3]!) throw AttendanceOperation.Invalid("invalid_attendance_cursor");
            return stored.Position;
        });
        Service = new(Base.Operation, employees, repository, Journal, access, cursors);
    }
    public Task<FaceEnrollmentAttemptPage> Read(FaceEnrollmentHistoryRequest? request = null) =>
        Service.HistoryAsync(Base.Actor.EmployeeId, request ?? Request, Base.Context, default);
    public Task<EnrollmentExecution> Start(EnrollmentFailure? failure = null, bool success = false, Guid? originalSite = null) =>
        Base.RunTransaction(async ct =>
        {
            var start = await Journal.BeginAsync(new(Base.Actor, "SyntheticOperator", Base.Clock.Now, null), Base.Actor.EmployeeId,
                Base.Device.Id, Guid.NewGuid().ToString("N"), new string('A', 64), new(true, "synthetic", "1", "not retained"),
                [new(EmployeeId: Base.Actor.EmployeeId, SiteId: originalSite ?? CurrentSite)], Base.Context, ct);
            if (failure.HasValue) await Journal.CompleteFailureInTransactionAsync(start.Handle!, failure.Value, ct);
            else if (success) await Journal.CompleteSuccessAsync(start.Handle!, new(Guid.NewGuid(), Base.Actor.EmployeeId, "synthetic", "1",
                Base.Clock.Now, true, null, null, "AAAAAAAAAAE="), null, ct);
            return start;
        }, default);
}
