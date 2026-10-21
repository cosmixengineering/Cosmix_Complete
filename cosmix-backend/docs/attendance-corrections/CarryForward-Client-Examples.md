# Factual locked-month adjustment client flow

All calls use existing authenticated sessions. HR endpoints require web MFA and current correction
permissions; cookie writes retain existing CSRF checks. Self identity is always obtained from the
authenticated employee. There is no body employee ID or face requirement for these requests.

Use a new `Idempotency-Key` per intended write. Retry the exact same body/key after uncertain network
delivery. A new intent needs a fresh key; a successful replay still requires current authority.

## Routes and permissions

Self prefix: `/api/v1/me/attendance/carry-forward` with `attendance.corrections.self`.
HR prefix: `/api/v1/hr/employees/{employeeId}/attendance-carry-forward`.

| Suffix/method | HR permission | Result |
|---|---|---|
| `GET /sources?businessDate=2026-09-08` | `attendance.corrections.submit` | Frozen source, current virtual interpretation, next target month, fingerprint |
| `POST /requests` | `attendance.corrections.submit` | Pending immutable proposal |
| `GET /requests?from=2026-09-01&to=2026-10-01&pageSize=25` | `attendance.corrections.submit` | Own submitted requests, encrypted continuation |
| `GET /requests/{id}` | `attendance.corrections.submit` | Own protected proposal detail |
| `POST /requests/{id}/cancel` | `attendance.corrections.submit` | Own pending cancellation |
| `GET /months?month=2026-10-01&page=1&pageSize=25` | `attendance.corrections.review` | All approved factual postings to that month, then pagination |
| `GET /requests/{id}/review` | `attendance.corrections.review` | Reviewer detail (HR only) |
| `POST /requests/{id}/decision` | `attendance.corrections.decide` | Distinct HR reviewer decision (HR only) |

Global reviewer queue: `GET /api/v1/attendance/carry-forward/requests?from=2026-09-01&to=2026-10-01&scope=Employee&targetId={employeeId}`,
using `attendance.corrections.review`. All-scope queue requires actual All authority. Site, Department,
Team and Project filters use historical source membership; every row also checks original and current
source/target scope. `NextCursor` may accompany an empty page after hidden candidates are scanned.

## Submit a retained session time change

Preview first. Substitute the exact returned IDs, target, fingerprint and session key below. Enum
representations follow the API's existing JSON enum configuration; the numeric values below are stable.

```json
{
  "sourceBusinessDate": "2026-09-08",
  "kind": 2,
  "targetSessionKey": "<sessions[0].sessionKey>",
  "rawPunchId": null,
  "requestedTimeUtc": "2026-09-09T01:00:00Z",
  "requestedEndUtc": null,
  "expectedSourceLockEventId": "<sourceLockEventId>",
  "expectedSourceSummaryId": "<sourceSummaryId>",
  "expectedPriorAdjustmentId": null,
  "expectedTargetMonthStart": "2026-10-01",
  "expectedSourceFingerprint": "<sourceFingerprint>",
  "reason": "Correct documented end of duty",
  "writtenEvidence": "Contemporaneous written evidence explaining actual duty."
}
```

For ManualSession use `kind: 4`, `targetSessionKey: null`, and both UTC requested timestamps. Optional
`rawPunchId` must identify an own failed/unaccepted Check In on the retained business date; it is never
changed to accepted. For a prior adjustment, `expectedPriorAdjustmentId` must be the returned ID,
not null. Returned proposals may be submitted afresh using `resubmissionOfRequestId` and a new preview.

## Reviewer decision

```json
{
  "expectedVersion": "<request.version>",
  "decision": 1,
  "reason": "HR reviewed original locked context and written duty evidence"
}
```

Decision `1` approves, `2` returns, `3` rejects. Requester cannot decide their own request. Cancellation
uses only `expectedVersion` and `reason`. A changed source/prior/target yields a conflict; refresh the
preview and submit a new corrected request rather than silently changing this pending proposal.

Month responses label `interpretation: posted_adjustments_only` and `financialState: pending_policy`.
They do not replace the source month's history or create duty on a made-up target date. A first +2-hour
posting followed by -1 hour leaves both immutable entries; total impact is +1 hour. If the first
target month later locks, the next adjustment goes to the newly previewed first unlocked month.

Source requires a direct protected lock manifest, exact summary input and session membership. Legacy
locks lacking that evidence return `carry_forward_legacy_lock_evidence_unavailable`. The production
policy adapter and actual locked-month SQL acceptance remain separate prerequisites; synthetic
verification never establishes that production policy calculation is configured.
