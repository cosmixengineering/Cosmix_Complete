#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyFaceReadiness.packages.lock.json
using Cosmix.Application.Attendance.FaceVerification;
using Cosmix.Domain.Attendance.FaceVerification;

// Written before the readiness repair; coordinated execution follows implementation. No provider or storage IO.
var checks = 0;
void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
var policy = new FaceVerificationPolicy();
var provider = new FaceProviderCapability(true, "synthetic", "1", "raw provider text must not leak");
Check(!FaceReadiness.Evaluate(policy, provider, true, null).SystemAvailable, "default policy remains unavailable");
policy.AuthorityConfirmed = true; policy.ApprovalReference = "synthetic"; policy.RetentionPolicyReference = "synthetic";
policy.ChallengeLifetimeSeconds = 120; policy.MaximumAttemptsPerPunch = 3;
var noEnrollment = FaceReadiness.Evaluate(policy, provider, true, null);
Check(noEnrollment.SystemAvailable && !noEnrollment.CheckInFaceReady && noEnrollment.ReasonCode == "face_enrollment_required", "configuration readiness is not enrolled readiness");
var enrollment = new FaceEnrollment(Guid.NewGuid(), "synthetic-template", "separate-key", "synthetic", "1",
    "actor", Guid.NewGuid().ToString(), "Synthetic", DateTimeOffset.UtcNow, "synthetic");
Check(FaceReadiness.Evaluate(policy, provider, true, enrollment).CheckInFaceReady, "matching active enrollment ready only when all configuration gates pass");
var mismatch = FaceReadiness.Evaluate(policy, provider with { Version = "2" }, true, enrollment);
Check(mismatch.EnrollmentActive && !mismatch.EnrollmentCompatible && mismatch.ReasonCode == "face_enrollment_provider_mismatch", "incompatible enrollment is explicit");
Check(FaceReadiness.Evaluate(policy, provider with { Available = false }, true, enrollment).ReasonCode == "face_provider_not_configured", "provider reason is canonical not arbitrary adapter text");
Check(FaceReadiness.Evaluate(policy, provider, false, enrollment).ReasonCode == "face_template_storage_unavailable", "vault unavailable consistently reported");
Check(!FaceReadiness.Evaluate(policy, provider with { Provider = "" }, true, enrollment).SystemAvailable, "malformed provider identity fails closed");
Check(!FaceReadiness.Evaluate(policy, provider with { Version = "version\nsecret" }, true, enrollment).SystemAvailable, "control characters in provider identity fail closed");
enrollment.Revoke("actor", "Synthetic", DateTimeOffset.UtcNow.AddSeconds(1));
Check(!FaceReadiness.Evaluate(policy, provider, true, enrollment).EnrollmentActive, "revoked enrollment is not active");
Check(FaceReadiness.Evaluate(policy, provider, true, enrollment).ReasonCode != provider.ReasonCode, "arbitrary reason never exposed");
Console.WriteLine($"Face readiness checks passed: {checks}. No provider/store/SQL activity.");
