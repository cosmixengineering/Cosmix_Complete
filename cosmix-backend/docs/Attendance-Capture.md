# Attendance capture, face lifecycle and factual sessions

This local slice adds registered mobile evidence capture, self state/history, factual work-session
pairing, and guarded face lifecycle/provider contracts. **Real employee Check In is not ready:** no
face provider, biometric authority/settings, retention/deletion process or real biometric acceptance
has been approved. The default provider cannot enroll or verify anyone. A schema migration is
required; creation of the combined migration does not imply database application.

The implementation follows the confirmed site-only schedule. It never reintroduces individual,
department or team shift precedence and does not fetch or duplicate the separate policy feature.

## API contracts

| Method and route | Permission | Behavior |
|---|---|---|
| POST `/api/v1/me/attendance/check-in` | `attendance.punches.self` plus current `attendance.face.self` for accepted proof | Record immutable mobile evidence; accept only with current, unexpired, consumed face proof, site schedule and reliable in-boundary coordinates |
| POST `/api/v1/me/attendance/check-out` | `attendance.punches.self` | Face-free closure of one valid active session; retains review evidence for invalid sequence/location/transfer/lock |
| GET `/api/v1/me/attendance/state` | `attendance.history.self` | Current factual session, next action and explicit provider/policy/storage availability |
| GET `/api/v1/me/attendance/punches` | `attendance.history.self` | Own evidence and latest interpretation; includes rejected/unverified events |
| GET `/api/v1/me/attendance/sessions` | `attendance.history.self` | Latest immutable session revisions and factual elapsed minutes |
| GET `/api/v1/attendance/face/capability` | `attendance.face.self` | Explicit availability and own enrollment state; Check Out never requires face |
| POST `/api/v1/attendance/face/challenges` | `attendance.face.self` | Registered-mobile-device/punch-key-bound random challenge, only after approved setup |
| POST `/api/v1/attendance/face/challenges/{id}/verify` | `attendance.face.self` | Approved adapter performs one-to-one live verification; failure/outage/expiry cannot create accepted attendance |
| GET `/api/v1/hr/employees/{employeeId}/face-enrollments` | `attendance.face.read` | Scoped metadata without template, raw capture or threshold disclosure |
| GET `/api/v1/hr/employees/{employeeId}/face-enrollments/attempts` | `attendance.face.read` | Scoped attempt outcomes/provider versions/time metadata |
| GET `/api/v1/hr/employees/{employeeId}/face-enrollments/enrollment-attempts` | `attendance.face.read` | Bounded enrollment start/outcome history, including safe failures and explicit unresolved starts; current and original scopes required |
| POST `/api/v1/hr/employees/{employeeId}/face-enrollments` | `attendance.face.manage` | Controlled enrollment/replacement using provider evidence, target registered device, reason and expected active identity/version |
| POST `/api/v1/hr/employees/{employeeId}/face-enrollments/{id}/revoke` | `attendance.face.manage` | Reasoned version-protected revocation; future consumption rejects revoked enrollment |

Administrative routes require current web MFA and explicit Employee/Site/All scope. They do not
grant biometric authority merely because a permission code exists: the recorded authority/settings
remain an additional gate. Custom roles use the existing access module, with no bootstrap expansion.
Mobile submission derives employee/account/device from the current session; its body has no employee,
site, schedule or server-time field. Unknown JSON properties are rejected.

All writes require `Idempotency-Key`. Capture uses one operation across Check In/Out, so the same key
cannot silently change action. Protected response receipts last 24 hours; raw employee/key uniqueness
outlives the receipt. An expired receipt requires reconciliation through history, not blind resubmission.
Replay rechecks current permissions, session and registered device. HTTP 200 means **evidence was
recorded**; clients must inspect `Outcome` before displaying attendance success.

## Client examples

After an approved provider integration issues and verifies a challenge, submit the matching punch key:

```http
POST /api/v1/me/attendance/check-in
Authorization: Bearer <mobile-access-token>
Idempotency-Key: mobile-punch-20261009-001
Content-Type: application/json

{
  "isOffline": false,
  "deviceCapturedAtUtc": "2026-10-09T04:00:00Z",
  "latitude": 24.8607000,
  "longitude": 67.0011000,
  "accuracyMeters": 8.25,
  "faceAttemptId": "<server-issued-attempt-id>",
  "proof": "<server-issued-challenge-secret>"
}
```

The example coordinates are illustrative, not a configured company site. Latitude/longitude accept
at most seven decimal places and accuracy two, preventing SQL rounding from silently changing evidence.
Check Out uses the same location/offline fields and **omits** `faceAttemptId` and `proof`.
The server rejects face fields on Check Out. History uses inclusive `from`, exclusive `to`, a maximum
366-day interval and page sizes 1–100.

The state API's `nextAction` agrees with its action flags: a locked period or multiple active
sessions returns `request_correction`; no currently applicable schedule/window returns
`view_shift`; an available Check In path returns `start_face_challenge`. A permitted active
unlocked duty returns `check_out` regardless of face-provider availability. These actions are
guidance only: submission rechecks current permissions, device, location, schedule and lock state.

```http
GET /api/v1/me/attendance/punches?from=2026-10-01&to=2026-11-01&page=1&pageSize=25
GET /api/v1/me/attendance/sessions?from=2026-10-01&to=2026-11-01&page=1&pageSize=25
GET /api/v1/me/attendance/state
```

Management enrollment outcomes use a separate history from Check In face-verification attempts:

```http
GET /api/v1/hr/employees/{employeeId}/face-enrollments/enrollment-attempts?fromUtc=2026-10-01T00:00:00Z&toUtc=2026-11-01T00:00:00Z&pageSize=25
Cookie: <current web session with MFA>
```

The response has `items` and `nextCursor`, with no total count. Each item has only `attemptId`,
`startedAtUtc`, nullable `completedAtUtc`, canonical `stage`/`code`, `provider` and `providerVersion`.
For example, an unfinished start returns `stage: reconciliation_pending` and
`code: face_enrollment_reconciliation_pending`; elapsed time never changes that into a failure.
Provider rejection, provider outage, missing approved setup, local storage failure and finalization
denial retain distinct sanitized outcomes. A successful enrollment outcome does not imply attendance
success, a current enrollment, or provider readiness today.

Intervals must explicitly use UTC, inclusive `fromUtc`/exclusive `toUtc`, maximum 366 days. Page sizes
are 1–100. At most 100 indexed start candidates are scanned per call. A read needs both today's
employee scope and every originally recorded start resource; inaccessible starts are omitted before
their terminal metadata is read. Follow an opaque `nextCursor` with **identical** filters/page size,
even after an empty page. The cursor is bound to the current account and filters, expires after
30 minutes, and never replaces current MFA/session/grant checks. Missing or corrupt protected
start/result evidence fails closed, rather than being labelled pending. All successful reads are audited.

This API covers retained durable journal attempts. Older successful generic enrollment receipts retain
their existing replay contract and successful enrollment history; absent legacy failure records are
not reconstructed. No actor roles, devices, capture tokens, template bytes, process proof, hashes,
private-storage references or provider exception text appear in the new history DTO.

`Accepted` creates an open session or a new closing revision; it does not mean payable hours or
approved overtime. `QueuedForReview`, `Rejected` and `Unverified` never open/close an accepted session.
Offline events always remain unverified in this slice; there is no invented offline trust protocol.
Out-of-boundary events retain computed distance and an exception. Missing/poor coordinates never
silently qualify. Outside-schedule Check In remains reviewable until early/late/unscheduled rules are
confirmed; Check Out has no face requirement or invented scheduled-end cutoff.

## Evidence, sessions and transaction semantics

- Raw receipt time is server UTC; business date uses `Asia/Karachi` and the duty's start date.
  Device time is evidence only. An overnight active session retains its opening business date.
- New raw rows pin historical employee assignment, site, geofence version, shift version, site-roster
  assignment, temporary-change identity and selected holiday-calendar revision when resolved.
  Legacy raw rows keep null business date; history labels their receipt-date fallback explicitly as
  `legacy_received_date`, without rewriting evidence.
- A successful Check In consumes exactly its employee/device/punch-bound proof in the same SQL
  transaction as raw evidence, assessment, factual session, audit and protected receipt. Consumption
  uses a fresh server timestamp after dependencies, not the earlier request-start time.
- Closure appends a new `WorkSession` revision. Prior open revisions remain stored; active-session
  queries consider only the highest revision. More than one closed session per day is supported.
- Face/provider calls run outside the authentication transaction. Receipt reconciliation and stale
  version validation happen before remote work; finalization repeats authorization/version/enrollment
  checks. New enrollment calls use the persisted server attempt UUID as provider idempotency identity,
  avoiding collisions when different authorized operators reuse the same client key.
- The current implementation reuses the existing global authentication lock for correctness. This is
  not production peak-punch throughput acceptance. No external provider call runs under that lock.
- Factual sessions and assessments may have null policy IDs. No dummy policy, payroll formula,
  continuous-duty reset, paid-break assumption or automatic deduction is introduced.
- Locked periods retain incoming evidence for review and do not create new accepted interpretations.
  New Check In against an open session creates a sequence exception; an attempt after its scheduled
  end also records missing checkout. A periodic missing-checkout sweep is not supplied by this slice.
- Exception notifications stage only to the submitting account through the durable outbox in the
  same business transaction. They use static safe templates; staging is not delivery. Arbitrary
  device-integrity strings are retained as observations, not automatically labelled risky.

## Biometric isolation and remaining provider work

`IFaceTemplateStore` is separate from `IPrivateFileStore`. The encrypted implementation reuses the
AES-GCM envelope code but requires independently provisioned `FaceTemplateStorage` directory/keyring.
It rejects overlapping document/template directories, shared key references or paths, and identical
key material even under different filenames. Missing configuration never falls back to document keys.
The ordinary store continues to hold protected audit/receipt metadata; it does not hold templates.

Template configuration defaults disabled. No files/keys have been provisioned by this implementation.
The application policy is explicitly bound to the `FaceVerificationPolicy` configuration section and
defaults unconfirmed: authority, approval reference, retention reference,
challenge lifetime and retry count must be explicitly supplied after management decisions. The selected
adapter must enforce its approved enrollment, liveness and matching policy and cryptographically verify
provider evidence. The browser/mobile client never supplies trusted `livenessPassed`/`matchPassed`
flags, receives a template, or chooses another employee's identity. Empty/malformed provider identity
fails closed. Provider-supplied reason text is never used as an availability message.

`GET /api/v1/attendance/face/capability` retains its existing fields and adds
`enrollmentCompatible`, `checkInFaceReady` and `availabilityKind: configuration_only`.
`available` describes configured policy/provider/template storage; `checkInFaceReady` additionally
requires the employee's active enrollment to match the configured provider/version. Neither field
proves storage/provider operational acceptance or authorizes a punch. Attendance state uses the same
canonical evaluator; actual capture still revalidates permissions, device, proof, roster and location.

## Enrollment failure audit and retries

Authorized enrollment attempts now use the existing `AuditEvent`, `IdempotencyReceipt` and protected
private metadata store without a schema change. No provider is selected or enabled by this change.

- A persisted immutable start binds the authenticated operator/role, target employee, requested
  device, provider/version, input fingerprint, server time, original scopes and correlation. Device
  ownership and eligibility must pass preflight before provider invocation. A rejected requested
  device is recorded as requested context, not as a verified device.
- Readiness/precondition rejection, provider rejection, invalid provider result, provider outage,
  local template-storage failure, finalization denial and caller cancellation have distinct canonical
  terminal outcomes. Raw provider evidence, templates, provider reason text and exception details are
  absent from the journal. Returned template bytes are cleared after every result path.
- A process-owned random finalization proof is returned only to the caller that created the start;
  only its hash is retained. Existing in-flight retries cannot retrieve that proof or dispatch another
  provider call. They return `face_enrollment_reconciliation_pending`. A process crash therefore
  requires deliberate future reconciliation through the approved adapter; no blind provider retry
  or fabricated terminal result is made.
- One terminal receipt arbitrates success/failure under the existing global authentication lock.
  Enrollment/replacement, successful terminal evidence and audit share a transaction. A prior failed
  terminal prevents later enrollment changes; a late failure cannot overwrite committed success.
- If permission/session changes after the provider call, enrollment finalization is denied. A
  separate audit-only transaction may record failure using the original process proof and persisted
  start; it cannot add/modify enrollments, templates, permissions or arbitrary business records. Its
  actor/target metadata comes from the protected persisted start, never a client-supplied identity.
- Every public replay and business operation still requires current authority, including original
  recorded and current employee scopes. Recorded terminal failure replays the same canonical error
  without a new provider call. Previously stored successful generic enrollment receipts are read
  first using their original protected shape.
- The existing attendance receipt lifetime is 24 hours. This limits retry reuse; it is **not** audit
  retention or template deletion policy. No record/file cleanup or deletion is introduced. SQL or
  protected audit-store outage cannot provide durable auditing; such failures remain unavailable.

After a recorded failure, a genuinely new capture/request needs a new `Idempotency-Key`; changing
the body under the old key is rejected. Resolving an in-flight/crashed attempt is a separate operational
step, not a reason to repeatedly send fresh enrollment keys. Failure metadata is retained in the audit
journal and is readable through the scoped `enrollment-attempts` API. Existing enrollment/verification
histories keep their original DTO meanings. No operational reconciliation command is claimed.

Provider enrollment/verification success is only a contract plus service orchestration at present,
not an approved SDK integration. Approved medical/accessibility alternatives, operational failure
reconciliation, active retention/deletion/offboarding processing, real provider
liveness testing and mobile-device acceptance remain required. Enrollment rejection does not create
a successful template or a successful attendance result. Do not enable real staff attendance yet.

See [verification](attendance-capture/Verification.md) for actual checks and limits.
The coordinated continuation passed Release build (0 warnings/errors), 183 feature assertions and
the parent's 316 route/anonymous-HTTP checks across 48 Attendance and 58 existing employee operations.
The history API is included in that verified checkpoint. Shared encrypted-cursor checks passed 44
assertions separately. Real provider and authenticated SQL/storage acceptance remain unexecuted.

## Provider options considered, not selected

The 9 October 2026 discussion considered AWS Rekognition Face Liveness plus one-to-one CompareFaces,
and commercial on-premises options FaceTec and Innovatrics DOT. No adapter, cloud account, biometric
upload, licensing purchase or provider enablement is authorized by recording these options. Provider
selection, deployment/data rules, a React Native native bridge and actual-device proof of concept remain
separate decisions. Pricing/licensing references supplied in that discussion: [AWS pricing](https://aws.amazon.com/rekognition/pricing/),
[AWS FAQs](https://aws.amazon.com/rekognition/faqs/), [FaceTec](https://www.facetec.com/pricing),
[Innovatrics DOT terms](https://developers.innovatrics.com/digital-onboarding/docs/eula/).
