# Attendance correction intake and own exception notices

Local continuation, 9 October 2026. SRS 7.3, ATT-008 and ATT-010.
Employee self intake/history/cancel, supervisor requests and one permission-authorized HR reviewer
are implemented locally. Approved valid-duty time corrections, missing checkout and explicit manual
duty append session/summary evidence atomically. New proposals return `state: Pending`,
`routingStatus: hr_review_required`, `changesAttendance: false` until actual application.
Financial DayStatus calculations and the separate locked-period carry-forward remain unavailable.
## API and access

| Method / route | Permission | Behaviour |
|---|---|---|
| GET `/api/v1/me/attendance/corrections/sources?businessDate=2026-10-08&rawPunchId={id}` | `attendance.corrections.self` | Own immutable raw/latest assessment/latest summary identities, retained site roster and lock context |
| POST `/api/v1/me/attendance/corrections` | `attendance.corrections.self` | Immutable typed proposal with bounded written evidence |
| GET `/api/v1/me/attendance/corrections?from=2026-10-01&to=2026-11-01&page=1&pageSize=25` | `attendance.corrections.self` | Own requester history, scoped before pagination |
| GET `/api/v1/me/attendance/corrections/{id}` | `attendance.corrections.self` | Own proposal and protected written evidence |
| POST `/api/v1/me/attendance/corrections/{id}/cancel` | `attendance.corrections.self` | Original requester withdraws Pending proposal using current version |
| GET `/api/v1/me/attendance/exceptions?from=2026-10-01&to=2026-11-01` | `attendance.exceptions.self` | Safe own exception notices; no confidential reasons or risk classifications |

Self identity is derived from the current authenticated account. Web requests use the existing
web MFA/cookie/CSRF path; mobile requests use the existing current-session bearer path. No employee,
site, approver or role can be supplied in a body. A face proof or registered mobile device is not
required merely to explain or withdraw evidence. Permissions use the supported explicit access
configuration path, with no automatic startup grant.

Dates are `From` inclusive and `To` exclusive, at most 366 days, page size 1–100.
Requests return `Cache-Control: no-store`. Writes require `Idempotency-Key`.

## Client sequence and example

First obtain the source preview for the employee's business date and, for a punch-time correction,
the immutable raw punch ID. Copy its `sourceFingerprint` into `expectedSourceFingerprint`. Include
both expected source ID fields explicitly; use JSON `null` if the preview reports no corresponding
revision. Re-fetch after `correction_source_version_conflict`, re-review the changed source and submit
with a new idempotency key. The fingerprint covers exact latest sessions, including a checkout
arriving after preview even when the selected punch assessment and saved summary are unchanged.

```http
POST /api/v1/me/attendance/corrections
Idempotency-Key: correction-20261008-001
Content-Type: application/json

{
  "businessDate": "2026-10-08",
  "kind": 1,
  "rawPunchId": "<own-raw-id>",
  "expectedAssessmentId": "<latest-assessment-id-or-null>",
  "expectedSummaryId": "<latest-summary-id-or-null>",
  "expectedSourceFingerprint": "<64-hex-sourceFingerprint-from-preview>",
  "requestedTimeUtc": "2026-10-08T04:00:00Z",
  "requestedEndUtc": null,
  "requestedStatus": null,
  "reason": "Please review the recorded start time",
  "writtenEvidence": "My written account of the event and the supporting facts."
}
```

Replace placeholders with returned GUIDs or actual JSON `null`. Numeric kind values are `1` CheckInTime,
`2` CheckOutTime, `3` DayStatus and `4` ManualSession. Punch-time proposals require the matching raw
action and only `requestedTimeUtc`. `DayStatus` requires an existing summary, a `requestedStatus`
numeric enum value (`1` Unverified, `2` Present, `3` Absent, `4` HalfDay, `5` Hourly, `6` Leave,
`7` Holiday, `8` RestDay, `9` Incomplete), no raw punch and no proposed times. A proposed status is an employee's requested value,
not an authorized policy result. `ManualSession` requires both ordered times and no requested status; an optional failed Check-In raw reference retains its original evidence. Missing checkout alternatively targets the exact open session with null raw/assessment IDs.

`expectedSourceFingerprint` is required for every **new** proposal. Missing it returns `400
correction_source_fingerprint_required`; malformed SHA-256 text returns `400
invalid_correction_source_fingerprint`. It remains nullable in the wire schema only for backwards
compatibility: an exact, still-valid pre-fingerprint idempotency receipt may replay without it under
current authentication/access. Omission cannot create a new request. Keep the original body and key
when reconciling a previously completed legacy submission.

The retained site roster establishes the business date for timed proposals. Its immutable
ShiftVersion, site membership, temporary-change and holiday explanation are frozen in protected
evidence. An overnight manual session may begin after midnight on the following calendar date
while remaining on the roster's start business date. A manual window must overlap that roster's
duty window; no numerical early/late/OT limit is invented. A raw-time proposal retains the raw
punch's recorded business date. Missing/ambiguous roster context is explicitly unavailable.
Future business dates/times and pre-employment dates are rejected. A source summary without a
PolicyVersion is labelled `factual_only` with no asserted policy status.

```http
POST /api/v1/me/attendance/corrections/<request-id>/cancel
Idempotency-Key: withdraw-correction-20261008-001
Content-Type: application/json

{ "expectedVersion": "<returned-base64-version>", "reason": "I need to correct my written explanation" }
```

There is no proposal update endpoint. Withdraw a pending request and submit a new one when values
or evidence need correction. Cancellation is permitted for one's own Pending request even if the
period subsequently locks. Original evidence remains retained. No evidence erasure policy is
invented.

## Integrity and transaction boundaries

- Latest assessment and latest summary IDs are compared under the existing serializable attendance
  authorization transaction. The raw employee/date/action is validated server-side. A canonical
  SHA-256 source fingerprint additionally pins all latest WorkSession IDs/keys/revisions, their
  start/end and original punch/correction/site/shift/policy links, historical roster and lock state.
  Session sources are selected with a bounded latest-revision SQL anti-join. No derived session
  or summary is written by correction intake.
- Protected schema-v3 source evidence retains canonically ordered historical resource groups for
  this employee/business date. Original raw site/date/assignment/shift/geofence/roster references
  remain explicit; a raw site absent from that day's scopes adds its own Employee/Site requirement,
  and legacy site-less raw evidence adds an Employee-only requirement. Authority paths are private,
  while the employee preview contains its fingerprint and own session/source context. The
  reviewer authorizes both retained and then-current scope through the explicitly configured permission.
- Existing schema-v1 protected proposals and old receipts remain readable/replayable. They do not
  gain fabricated session/scope evidence. Application requires a new reviewed
  proposal or explicit refresh/resubmission for those legacy requests before applying a correction.
- New intake rejects currently locked periods with correction_locked_period_adjustment_required. The confirmed separate path uses the first available unlocked month; this workflow never reopens a month.
- A fresh per-submission GUID is stored in both `ApprovalRequest.SubjectId` and protected evidence.
  It is a request correlation identity, not another employee/master. Protected detail reads validate
  that identity, current self requester, proposal values and frozen source IDs to prevent swapping
  otherwise identical cancelled/re-submitted proposals.
- `ApprovalRequest` starts Pending, stage 1, with null route rule. One HR reviewer uses the explicit scoped decide permission. Existing correction/session/summary tables are reused without schema changes.
- The correction row is append-only. Submit/cancel, approval state, audit and protected idempotency
  receipt commit together in the shared transaction. Cancellation checks rowversion. Same-key replay
  rechecks current authentication/access and returns the retained response; changed payload fails.
- An equivalent Pending proposal for the same employee/date/type/source/time/status is rejected
  even with another key. Independent SQL competing-write acceptance is still required.
- Evidence is text only: reason up to 1,000 characters, written evidence up to 4,000. No arbitrary
  file path, unscreened attachment, external URL fetch or client-provided protected reference is accepted.
- Exception inbox returns safe notice codes and own immutable evidence identifiers. Confidential
  investigation text and risk classification are withheld. It does not resolve the exception.

## Remaining workflow and acceptance

One HR reviewer, scoped supervisor submissions and manual duty authority are confirmed. The
implemented local interpretation changes remain subject to authenticated SQL/private-storage
acceptance, concurrent-write rollback and deployment verification. Policy-dependent pay/DayStatus,
scanned supporting attachments and the separate first-unlocked-month carry-forward workflow remain.
Decision notifications stage atomically for the original requester. No real account permissions,
SQL fixtures, provider calls or private storage keys were changed by these synthetic checks.
See [verification](attendance-corrections/Verification.md) for executed checks and limits. A Release
build or synthetic service check is not authenticated SQL/HTTP acceptance.

## Confirmed one-reviewer continuation (local code; acceptance pending)

The user confirmed one authorized HR reviewer and allowed explicit manual duty for missing/failed
Check-In. Custom role names are not interpreted as authority: Admin/SuperAdmin configure the explicit
`attendance.corrections.submit`, `.review` and `.decide` permissions with their allowed scopes. No grant
runs at startup. Requesters cannot decide their own requests. This includes a supervisor submitting
for another employee; the beneficiary and requester remain separate identities.

Eight additional web MFA/CSRF operations cover supervisor source preview, submit, own request history,
detail and cancel; the HR queue, evidence detail and decision. Controllers describe exact routes.
Queue/history use encrypted actor/filter-bound cursors, a 200-candidate scan budget and no raw count.
All current and originally saved employee/date/source scopes must be authorized; Team/Project historical
membership is an alternative within each date slice. Every slice still requires authority. The next
cursor advances to the last scanned candidate even when that candidate is not visible.

New source snapshots use schema 3 and include associated Check-In's latest assessment and consumed
verification proof for every session. Old schema 1/2 snapshots and exact old receipts remain readable;
schema 1 has no inferred scope and needs owner cancellation/new submission, and schema 2 requires a
fresh submission before approval. For new changes read sources again and copy the fingerprint. Optional
`targetWorkSessionId` and `resubmissionOfCorrectionId` are omitted when null, preserving old digests.
A returned request remains terminal; resubmission creates a new record linked to the same requester's
Returned predecessor. Pending requests cannot be edited.

A missed checkout uses `kind: 2`, `rawPunchId: null`, `expectedAssessmentId: null`, the source's exact
open `targetWorkSessionId`, and `requestedTimeUtc`. Approval closes that interpreted session with a
new revision retaining its original Check-In/site/shift/business date; no checkout raw event is created.
A Check-In time correction must start inside its retained shift window, including a valid overnight
next-calendar-day start. Closing time retains the existing capture rule without an invented OT cutoff.

Manual duty uses `kind: 4`, requested start/end, historical roster, reason and written evidence. A
missing raw event uses null raw/assessment IDs; an explicitly referenced failed Check-In is retained
as evidence. HR approval requires historical approved employment and active status during the duty,
its original assigned site/immutable shift, one unambiguous assignment covering the interval, and no
intersecting latest employee session on any business date. The resulting session has a CorrectionId
and no synthetic raw Check-In/face proof. Response interpretation is `hr_approved_manual_duty`.

Approval, immutable decision, session revision, factual summary/session links, protected audit,
idempotency receipt and the original requester's safe in-app notification stage share one outer
transaction. The intermediate save only makes staged approval provenance visible to SQL summary
queries; it does not commit. Any later summary/evidence/outbox failure rolls the transaction back.
`changesAttendance` requires actual applied session AND summary provenance, not Approved state alone.
The response includes their IDs. Payable/late/OT stay unavailable/null when no policy calculator exists.

Raw punches are immutable. Existing face enrollment revocation does not erase historical consumed
proof. Stale associated assessment, new checkout/session, source scope, roster, lock or original target
changes prevent application. No open sessions are silently summarized. Locked months remain denied;
the confirmed next-month adjustment workflow is separate and financial rules are still unresolved.
DayStatus changes remain unavailable until approved calculation semantics exist.