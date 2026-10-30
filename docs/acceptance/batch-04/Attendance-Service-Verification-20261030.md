> **SQL continuation:** The later [Attendance SQL verification](Attendance-Sql-Verification-20261030.md) passed a fresh-database production-repository check-in/retry/checkout/history scenario. The service-only evidence and its limits below remain historical; the new report is the authority for SQL persistence coverage.

# Attendance service verification — 30 October 2026

## Correction to earlier acceptance evidence

The older `MobileAttendanceWorkflowAcceptanceTests` contained five tests, four of which constructed response objects or repeated a local distance formula without invoking the attendance services. The claimed 18-step login/camera/SQL/mobile acceptance is withdrawn. A configured timeout ceiling does not establish the cause or resolution of an actual face-server timeout.

## Implemented correction

Direct Check In and Check Out could skip location-consent checks when the optional consent repository was unavailable. Submission also accepted any non-null consent returned by the repository without binding its employee, notice ID/version and timestamps. Preflight already checked most of these conditions, but preflight is not authority for a later submission.

Both entry points now share the same strict check: the repository must be available; the notice must be active and published by server time; consent must belong to the authenticated employee and exact notice ID/version and cannot be future-dated. These failures occur before retaining GPS, punches, assessments, work sessions or consuming the face proof. No migration or company policy change is involved.

## Executed evidence

- The pre-fix missing-repository tests reproduced the bug for Check In and Check Out: the expected exception was not thrown. Evidence: `D:/CosmixReleases/product-readiness-20261030/attendance-service-before.trx`.
- Fresh isolated Release build: zero errors and zero warnings. `attendance-service-build-final.log`.
- **76 focused tests passed, zero failures/skips**. `attendance-service-final.trx` and `.log`.
- Replacement workflow tests invoke production `AttendanceCaptureService` for site and office preflight, check-in, exact retry, state, radius-only checkout, history and closed-session duration. The repository and verified face proof remain synthetic. Separate negative cases cover outside-radius submission after successful preflight, forbidden checkout face evidence, authoritative server time and 18 mismatched/unpublished/future consent cases.
- Existing preflight, location consent, single-device, transport cancellation/timeout diagnostics and punch-audit privacy tests also ran in the focused set.

## Acceptance limits

These tests do not prove real SQL persistence, production SMS/mobile authentication, camera capture, biometric matching/liveness, employee/device acceptance or a production deployment. The existing locally guarded OTP flow is not production SMS delivery. Physical employee acceptance remains pending and must not be inferred from synthetic proofs or the service tests.

At the start of this check ADB had no device and no configured AVD. Installed Android system images and WHPX acceleration are available. An isolated `Cosmix_Attendance_Verification_20261030` AVD was created and started hidden with camera/audio disabled; its boot/runtime result is recorded separately in the emulator logs. The existing debug APK is dated 20 October and is not proof of the current native source or a production signed release.

The broader non-SQL backend regression run passed **677/677, zero failures/skips** after the fix (`attendance-expanded-regression.trx`). SQL/provider acceptance suites were explicitly excluded. The first emulator start failed because C: had approximately 2.18 GiB free; only the newly created owned AVD was moved to `D:/CosmixVerification/android-avds/Cosmix_Attendance_Verification_20261030.avd`, with its AVD registry path updated. No existing AVD/user data was deleted. The installed system image remains in the SDK.

The owned emulator remained `offline` in ADB after startup and a diagnostic retry; Android boot completion and app execution were **not verified**. It was stopped to release RAM during concurrent build verification; its owned AVD on D: remains for subsequent testing. Diagnostic logs: `attendance-emulator-d.log`, `attendance-emulator-d-error.log`, `attendance-emulator-kernel.log`, `attendance-emulator-kernel-error.log`. No camera was opened and no employee/device acceptance is claimed.

Fresh trusted-TLS connectivity checks: CompreFace `https://192.168.1.50:8000/core/status` returned 200 (approximately 1.36 seconds), and local API `https://localhost:7240/health/live` returned 200. These are health checks, not biometric/attendance verification, and the running API must be rebuilt/restarted to load the new consent correction.

