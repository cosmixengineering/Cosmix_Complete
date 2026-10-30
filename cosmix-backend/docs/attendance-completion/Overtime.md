# Overtime — implemented local backend, 12 October 2026

Latest focused verification: [30 October missing-checkout and three-hour OT checks](Missing-Checkout-OT-Verification-20261030.md). The current registered evaluator uses explicit published OT rules; the nine-hour formula below describes the earlier confirmed contract. Open duty cannot produce eligible OT, and daily totals use approved awards rather than automatically awarding every post-shift minute.

Twenty-three registered operations implement request/review, separate financial reads and dated
eligibility settings. These APIs have synthetic service, offline SQL translation and anonymous
HTTP verification. They are **not yet accepted against authenticated SQL or enabled for real staff**.

## Confirmed rules

- One permission-authorized HR reviewer approves, returns or rejects. Neither the requester nor
  the benefiting employee may decide. Custom role names do not grant authority by themselves.
- Office staff are always ineligible. A common setting explicitly enables/disables outside (`SITE`)
  staff. An employee profile setting can disable that employee; enabling it restores inheritance
  and cannot bypass common OFF. No common setting means `eligibility_not_configured`.
- Settings are immutable dated revisions with reason, actor, private evidence, preview fingerprint,
  expected revision, audit and idempotency. Past dates and locked periods cannot be rewritten.
  No permission or eligibility is enabled automatically at startup.
- The confirmed formula is monthly **base** salary / actual source-month days / 9 × approved hours.
  February/leap years and 30/31-day months use their actual calendar length. Only the final amount
  rounds to the nearest whole PKR, midpoint up; there is no intermediate rounding. Exact duty ticks
  are retained even when display minutes are truncated. Monthly PKR compensation is supported;
  other wage bases/currencies remain explicitly unavailable.
- Nine hours of actual closed duty must be completed. Configured break durations are not deducted
  for this OT formula: eleven elapsed duty hours qualify for two hours. Gaps between sessions are
  not worked time. Canonical same-business-date sessions are aggregated once; overlaps fail.
  Accepted early arrival is preserved but counting starts at the scheduled start; it cannot invalidate valid post-shift OT or manufacture pre-shift OT. Non-nine-hour schedules and unverified/incompatible duty remain explicitly unavailable.
- Work must be evidenced. Company travel does not automatically qualify. Named bulk submission/
  decision accepts 1–25 entries, validates the whole set and retains one request per employee.
- An approved OT award is not payroll settlement. The absent general attendance/payroll calculator
  is not fabricated or integrated. Pending OT blocks month locking; approved OT also blocks it
  until the payroll adapter can include the retained award. Locked-source OT is not rewritten.

## APIs and permissions

All prefixes below include `/api/v1`. Management uses current web MFA/CSRF. Self routes derive
employee identity from the live session and support documented web/mobile access. Writes require
`Idempotency-Key`; retries still recheck current/original scope and revocation.

| Prefix | Operations | Permission |
|---|---|---|
| `/me/overtime` | GET sources; POST/GET requests; GET request detail; POST cancel | `attendance.overtime.self` |
| `/me/overtime/requests/{id}/financial` | GET own award | `attendance.overtime.financial.self` |
| `/attendance/employees/{employeeId}/overtime` | GET sources; POST/GET requests; GET detail; POST cancel | `attendance.overtime.submit` |
| `/attendance/overtime` | GET review; GET employee request detail | `attendance.overtime.review` |
| `/attendance/overtime/employees/{employeeId}/requests/{id}/decide` | POST approve/return/reject | `attendance.overtime.decide` |
| Same employee/request prefix plus `/financial` | GET protected amount | `attendance.overtime.financial.read` |
| `/attendance/overtime/bulk` | POST requests / decide | submit / decide respectively |
| `/attendance/overtime/eligibility` | GET preview/history; POST setting | eligibility.read / eligibility.manage with All scope |
| `/attendance/employees/{employeeId}/overtime/eligibility` | GET preview/history; POST profile setting | eligibility.read / eligibility.manage within retained/current employee scope |

Ordinary request, decision and replay DTOs contain no salary or payable amount. Amounts can reveal
salary through reverse calculation, so only the separate current-authorized financial read exposes
an award. Its protected proof pins the historical compensation record, formula, month, exact ticks
and result. It never reads today's salary as a substitute for the duty-date record.

Request queues use actor/filter-bound encrypted cursors, up to 100 visible rows and 200 scanned
candidates. Follow `nextCursor` even on an empty page; no unauthorized total is exposed. Eligibility
history is paginated, with a 512-revision safety bound that fails explicitly rather than truncating
rule selection. A later revision wins only when effective dates match; future changes do not rewrite
past selection. Employee history retains original scopes as well as current historical scopes.

## Client examples

1. Read `GET /api/v1/attendance/overtime/eligibility/preview?asOf=2026-10-12`.
2. To enable outside staff, POST the following to `/api/v1/attendance/overtime/eligibility` with a
   fresh idempotency key and web CSRF header. Copy both preview values exactly:

```json
{
  "effectiveFrom": "2026-10-12",
  "isEnabled": true,
  "expectedRevision": 0,
  "expectedTimeline": "<64-character timelineFingerprint from preview>",
  "reason": "Approved common outside-staff OT setting"
}
```

3. To disable one employee, preview that employee's `/overtime/eligibility/preview` then POST to
   the employee eligibility URL with `isEnabled:false`, its own revision/fingerprint and reason.
   To remove the restriction later, append `isEnabled:true`; common OFF still prevails.
4. After closed duty, read `/api/v1/me/overtime/sources?businessDate=2026-10-12`.
   Select the exact returned session and eligible interval. POST `/api/v1/me/overtime/requests`:

```json
{
  "businessDate": "2026-10-12",
  "workSessionId": "<closed session ID from source>",
  "startsAtUtc": "2026-10-12T13:00:00Z",
  "endsAtUtc": "2026-10-12T15:00:00Z",
  "activity": 1,
  "expectedSourceFingerprint": "<64-character sourceFingerprint>",
  "reason": "Additional assigned work after completed duty",
  "writtenEvidence": "Describe the work and its supporting reference",
  "jobReference": "<optional job reference>"
}
```

Dates/times above are illustrative, not a fixture or authority to claim duty. A different authorized
reviewer reads the queue/detail, then POSTs `/attendance/overtime/employees/{employeeId}/requests/{id}/decide`
with `expectedVersion`, `decision` (the documented ApprovalState enum) and reason. Clients use the
published enum rather than inferring a numeric approval value. A stale source, changed eligibility,
competing approved interval or changed version conflicts. Return creates a terminal proposal;
resubmission is a new request with `resubmissionOfRequestId`. Pending cancellation is requester-only.

## Persistence and actual verification

Migration 16 `20261012081503_AddOvertimeEligibilityAndDutyZones` adds the eligibility history table
and allows nullable OT policy/rate references for pending intake and this independently confirmed
formula. It preserves existing non-null legacy rows; no fake policy ID is created. The exact
employee/session foreign key remains. Eligibility history is append-only, and global NULL employee
revisions have an unfiltered unique SQL Server index. Down refuses retained new evidence.

Executed locally: **26** formula/domain checks, **67** real-service checks with synthetic I/O,
**46** actual SQL translation/model/migration checks without a connection. Included: common/profile
ON/OFF, inheritance, office denial, dated history, stale writes, locked dates, original/current scope,
MFA/CSRF/revocation, exact replay versus changed payload, no self-decision, bulk rejection and actual
confirmed-duration/eligibility/formula integration. The shared HTTP harness passed **587** assertions
across **107** new Attendance and **58** existing employee operations, anonymously only.

Still unverified: authenticated new SQL flows, simultaneous database writers, private-store rollback/
restart receipts, real mobile device acceptance and payroll integration. Migration 16 is applied to
isolated `Cosmix_Testing`, but the bounded factual-summary acceptance does not create OT fixtures
or verify authenticated OT decisions. See [verification](Verification.md).
