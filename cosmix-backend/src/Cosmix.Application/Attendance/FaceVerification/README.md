# FaceVerification

Owner: Developer 2.

Source: SRS 7.2.1; FACE-001 to FACE-008; MD-002, MD-004, MD-013.

Authorized enrollment/re-enrollment/revocation and live one-to-one check-in verification. Provider remains undecided. Bind single-use results to employee, registered device and punch request; no success stub. Check Out never requires face verification.

Status: guarded local enrollment/revocation/history, challenge/result orchestration and provider contracts
are implemented. The default provider is unavailable, authority/settings are unconfirmed, and dedicated
template storage must be independently provisioned. No real liveness or biometric acceptance is claimed.
Approved alternative-process and retention/deletion/offboarding workflows remain blocked on decisions.
Canonical readiness distinguishes configuration from an active compatible enrollment. Enrollment
start/terminal metadata uses protected audit/receipts, sanitized failure classifications and one
terminal outcome; original success receipts remain readable. No provider work occurs under the SQL
authorization lock. Failed/in-flight retries cannot silently issue another provider call. The
process-owned start proof permits only bound audit failure after revocation, never business mutation.
Operational reconciliation and real SQL/provider acceptance remain outstanding.
Management enrollment-attempt history uses current web MFA/FaceRead plus current and original scopes,
bounded candidate scanning and encrypted account/filter cursors. Only safe outcome metadata is returned;
an unfinished start remains reconciliation_pending. It cannot expose templates, process proof or internal
actor authority, and it cannot reconstruct unrecorded legacy failures.

See `docs/Attendance-Capture.md` and `docs/attendance-capture/Verification.md` for the precise boundary.

Provider SDKs and encryption implementations remain in Infrastructure; services depend only on
Application contracts and Domain. Metadata DTOs never return templates or matching thresholds.
