# Proposed additional isolated Attendance acceptance

Status: **reviewable proposal only — not approved or executed**, 12 October 2026.
This extends, and does not reuse, the bounded run recorded in
[Isolated Acceptance](Isolated-Acceptance-Plan.md) and
[Narrow Web Acceptance](Narrow-Web-Acceptance.md). Existing journals remain partial where
they stopped. No runner, fixture, permission, configuration or database was changed by this document.

## Outcome and fixed target

Exercise genuine authenticated report requests, durable SQL dispatch, encrypted private output,
three real file formats, recipient inbox isolation and stale/repeated notification reads. Also
prove that an export cannot be generated using a session logged out after it was requested.
The existing grants are sufficient; this proposal adds **no roles or permissions**.

Only Windows-authenticated `(localdb)\MSSQLLocalDB` / **Cosmix_Testing**, migration 17
(`20261012082833_RetainZoneWarningTransitions`), is in scope. Reuse the three retained
`@example.invalid` accounts and private keys under:

`C:\Users\Affan.Ahmed\AppData\Local\Cosmix\AccessAcceptance\20261009`

Baseline is `ACC202610098E9C6465` plus the reconciled attendance run
`ATT20261012R45B3DDA713` and its `web-continuation-1`. Preserve their settings, keys,
backups, access history, revoked SiteA roster grant and single October 9 factual summary.
Use a new named child of `runs`, a fresh manifest, a verified COPY_ONLY/CHECKSUM backup,
and new one-use journals. Never rerun the initializer or overwrite a prior run.

## Exact business additions and limits

All reports select **only the retained HR employee, 2026-10-09 through 2026-10-09**.
This is a factual no-duty report; zero worked time is not proof of absence, payable time,
face verification or a policy-ready month. The existing Admin Employee-scoped
`attendance.reports.read/export` grant covers this employee. Inbox access is recipient-derived.

| Resource | Maximum additions | Expected successful result |
|---|---:|---|
| ReportExportJob | 4 | CSV, XLSX and PDF complete; a fourth CSV remains without generated output and its dispatch fails for the logged-out original session |
| Report-dispatch OutboxMessage | 4 | 3 Completed; 1 Failed with `report_requester_session_unavailable` |
| Notification-dispatch OutboxMessage | 3 | One `attendance.report-ready` intent per completed report; all complete once |
| StoredFile | 3 | One encrypted generated artifact per completed report |
| Notification | 3 | Three InApp deliveries to the requesting synthetic Admin only |
| IdempotencyReceipt | 4 | One per unique export request; repeated requests create no extra receipt or job |
| Protected private objects | 21 | 4 filter snapshots + 4 receipts + 4 write-audit envelopes + 3 output files + 3 output manifests + 3 notification intents |
| Existing notification updates | 0 | Only the three new run-owned notifications may be marked read |
| New notification read transitions | 3 | Each ReadAt is set at most once; repeat/concurrent requests preserve it |
| AuditEvent | At most 256 total | Only documented report/inbox/authentication/worker actions, bound to recorded request correlations and these synthetic identities |

The failed fourth job is **not** expected to change `ReportExportJob.State` to Failed in
the current implementation: its OutboxMessage holds the failure, and the supported status
API projects `state=failed` from that message. No fake output, ready notification or StoredFile
may exist for it. Retain this distinction in assertions and the result report.

No employee, hiring/assignment/roster/geofence/holiday, policy, compensation, grant, device,
raw punch, face proof, duty session, correction, OT, period lock, carry-forward, zone or
site-report row may be added or changed. The existing saved summary remains unchanged.
No migrations or schema writes are included. No real/private biometric or location data is used.

Normal supported password/MFA logins necessarily write authentication evidence. Bound this to
**three fresh web sessions**: two separate browser sessions for synthetic Admin and one for
synthetic HR. Allow at most three new OTP challenges, three LocalTesting OTP deliveries,
three sessions and three refresh-token rows. Do not resend OTP, rotate credentials, create an
account, use logout-all, revoke an old session or add a mobile device. Only the newly created
second Admin session is logged out to test failed generation; ordinary account-login/phone
timestamps, rate-limit buckets and its session revocation are expected updates. Inventory and
bind those exact keys before/after; no arbitrary authentication-table changes are permitted.
If an MFA login fails or needs a replacement challenge, stop rather than expanding this budget.

## Private configuration and host boundaries

1. Build/review the eventual runner first. Pin all runner/helper/Release artifact hashes,
   exact schema history, baseline resource identities, settings/key hashes and permitted deltas.
   Its default must inspect offline only. Human approval of the concrete scope is required
   before creating the run child, taking the SQL backup or executing any writes.
2. Before mutation, SELECT-only inventory must match the retained synthetic baseline and
   approved prior-run deltas. Require zero existing report jobs and zero pending/processing
   attendance-owned outbox messages. Existing unrelated or new fixture data is a stop condition,
   not a reason to delete it. Review pending lifecycle/OTP work before starting any host.
3. Copy the original settings into an owner-private file inside the fresh run child, retaining
   the **same** SQL target, identities, key references, object store, certificate and authentication
   settings. Change only the reviewed `AttendanceJobs` settings in this copy. The original
   `settings.json`, Test-Accounts.json and key files stay byte-for-byte unchanged.
4. Explicitly enable AttendanceJobs in that private overlay, because LocalTesting loads its
   private JSON after normal environment/command-line configuration. Environment variables alone
   cannot establish the effective setting. Use PollSeconds=1, BatchSize=1, LeaseSeconds=30,
   MaximumAttempts=2, RetryBaseSeconds=1, RetryMaximumSeconds=1 as bounded operational test
   settings. Require zone monitoring, zone storage and retention **false**, no external SMS,
   and unchanged unavailable face/scanner/provider adapters. Do not modify production DI.
5. Start the normal HTTPS API hidden on a preflight-free localhost port, recording PID and
   actual process start time. Stop only the owned matching process. No public binding or
   pre-existing host is used. The process may be restarted once for lease recovery using
   the exact same overlay; it does not become a scheduled/background follow-up.

## Ordered acceptance phases

### A. Genuine authentication and bounded request writes

Authenticate the three sessions using the existing guarded LocalTesting OTP helper and real
web password/MFA/cookie/CSRF flow. Secrets, OTP, cookies, CSRF and private plaintext stay
in memory; journals contain IDs, statuses, hashes and safe codes only.

Before queuing exports, a short-lived separate SQL connection holds the existing production
`cosmix:attendance-outbox:claim` application lock exclusively, with Session ownership. This
coordinates the normal worker without a fake handler, changing a timestamp or editing a row.
The database must have no other active application client. The gate is always released in
finally, and a 60-second deadline stops the owned host if the queue phase does not finish.
Worker claim timeout warnings while the gate is held are expected and do not count as dispatch.

- First Admin session: request CSV, XLSX and PDF with three new run-prefixed keys and a
  documented synthetic acceptance reason. Submit two identical CSV requests concurrently;
  both must resolve to the same job. Replay XLSX and PDF once each. Thus there are four
  unique successful create operations across the whole phase, seven successful HTTP request
  attempts including the second Admin request below, and three replay attempts.
- Same first session: one changed-payload retry under the CSV key must conflict; one request
  without CSRF must fail without a job. Synthetic HR's own session must be denied an export
  request under its existing self-only grants. No permissions are added to make it pass.
- Second Admin session: request one more CSV using a fourth key; immediately call ordinary
  current-session logout. Verify that same browser session can no longer use protected routes.
  The first Admin session must remain valid. Never call logout-all.
- Stop the owned API while the claim gate is still held, confirm the matching process exited,
  release the gate, and verify exactly four pending report messages and no generated outputs.

If any report was generated before this checkpoint, retain evidence and stop: the logout or
lease proof cannot be inferred after a race. Do not reset the rows and retry.

### B. Lease expiry without fabricated dates or additional jobs

With the API stopped, a finite local helper uses the **existing production**
`DurableJobRepository` with real SQL and TimeProvider.System. It inventories and locks the
known queue before calling ClaimNextAsync for the exact report message type. The oldest
candidate must be the first CSV created in phase A. Claim that message once with a 30-second
lease, record its actual claim identity privately, and exit without dispatching it. No handler
is replaced and no direct Outbox UPDATE is allowed.

Wait for natural lease expiry in short bounded intervals. A fresh helper process calls
CompleteAsync and FailAsync with the expired claim: both must return false, with no action
callback, output, failure audit or business change. Do not fast-forward the clock or edit
LeaseExpiresAt. The forthcoming normal worker must reclaim that same row as attempt 2.
If the queue differs from the four recorded IDs, stop before claiming anything.

### C. Normal worker generation and delivery

Restart the owned normal API with the same private overlay. Allow at most 60 seconds and
at most 8 status reads per job. The worker processes only the registered
`attendance.report-csv.v1` and `notification.in-app.v1` work from this run.

Expected final queue is exactly seven messages: three successful report dispatches, one
permanently failed report dispatch, and three successful notification dispatches. The recovered
CSV has Attempts=2; the other six messages have Attempts=1. Any other retry, terminal reason,
extra message or output is a stop condition requiring diagnosis; do not silently relax counts.
No additional write is authorized merely because a dispatch failed.

Use the first Admin's still-valid session to read all four job statuses. Confirm the revoked
session's job reports its exact permanent failure and has no download. A fresh current session
for the same account is not a substitute for the original generation session. Test direct HR
requests for a known export ID and download: they must not reveal another recipient's report.

Download the three successful files once each through the supported API. Check status,
attachment/no-store/nosniff headers, expected media type and actual format (CSV records,
valid OpenXML ZIP package, PDF document structure). Read the generated report's metadata,
employee/date, factual zero-duty values, saved-summary identity/fingerprint and explicit
factual/not-payroll marking. Independently decrypt/verify the three protected manifests and
file hashes using the unchanged private store. Download bytes belong in memory or the private
run child, never in Git or a public path. Do not assert payroll completion from these files.

### D. Inbox, stale writes and deduplication

Read the first Admin's inbox and bind the three new ReportReady notification identities to
the three completed jobs via protected intents and outbox evidence. The existing notification
DTO intentionally contains no resource ID or private payload; do not infer the binding from
display text alone. Synthetic HR receives none of these notifications; a known Admin
notification ID returns the same 404 for HR as an unknown ID.

For the first unread notification, send a well-formed but different eight-byte rowversion;
expect `notification_version_conflict` and no read transition. Next submit two concurrent
mark-read requests with the actual version. Exactly one transition/audit is allowed; both
responses retain the same ReadAt and the repeat has Changed=false. Mark the other two read
once and replay the first again with its original pre-read version. No further read audit or
timestamp change is permitted for repeats. An unsafe cookie request without CSRF must fail.

Observe another worker poll/restart-safe idle interval and prove no duplicate notification,
file, report or outbox row appears. This proves request/dispatch deduplication for this run;
it does not prove every producer or arbitrary crash location. Inbox/detail/read operations
are capped at 24 HTTP requests; report/status/download/negative checks are capped at 56.
Do not enable a new grant solely to test exception or dashboard routes in this acceptance.

### E. Reconciliation and stop

Stop the owned host. A separate SELECT-only/private-read verifier reconciles the exact row
budgets, original revoked roster grant, original roles/assignments, unchanged summary, no new
duty/policy rows, all seven dispatch states, three monotonic read transitions, cryptographic
references and settings/key hashes. Record any encrypted orphan as a failure requiring review;
do not auto-delete private objects or restore the database over preserved evidence.

The final evidence distinguishes actual HTTP/SQL/filesystem checks, deliberately simulated
process interruption before dispatch, natural lease expiry, and still-unexercised crash points.
On any failure retain backup, private overlay, journal and IDs; no automatic reset/retry, cleanup
of business rows, schema rollback, session-wide logout or silent permission change follows.

## Separate future fixtures: not included in this approval

The retained employees were hired October 9; the relevant early dates lack a historical shift,
and OT eligibility can only be configured prospectively through the current APIs. Therefore an
11-hour manual-duty/2-hour OT success test cannot be honestly added to the existing October 9
summary. No backdating existing employment/roster/eligibility or seeding a fake face proof,
approved policy, manual duty, OT approval or locked-month manifest belongs to this proposal.

A later dedicated fixture proposal must first enumerate a fresh synthetic employee (no new login
account is necessary for management testing), approved onboarding/history provenance, effective
site/shift/geofence/OT eligibility, exact dated base salary and source day. It must choose either
real elapsed future time after supported configuration, or a separately reviewed historical
fixture construction whose evidence is clearly synthetic rather than normal API acceptance.
List additional scope grants if any, private objects, correction/decision/session/OT rows, expected
notifications and genuine no-self-approval actors before asking approval. Keep original fixtures
untouched. Locked-month success additionally needs the actual approved policy/leave/calculation
integration described in [Policy Integration Contract](Policy-Integration-Contract.md).

Real face/liveness provider acceptance, mobile Check-In/Check-Out, location monitoring, policy-ready
month locking, financial carry-forward, salary/leave/payroll handoff, changed-grant generation denial,
arbitrary competing worker/process failures and load/UAT remain separate. This bounded test cannot
claim full Attendance production readiness or authorize those dependencies.

## User approval wording

“Sirf alag `Cosmix_Testing` database mein existing dummy Admin se 3 asli CSV/Excel/PDF reports
banane, ek fourth report ko requester logout ke baad deny hone ka test, notifications aur worker
restart/retry test chala sakta hoon? Pehle naya verified backup banega; original settings aur keys
same rahengi. Sirf is test ke private settings copy mein report worker temporary ON hoga. Maximum
4 report requests, 7 jobs aur 3 notifications banengi; koi naya role/access, real employee,
attendance duty ya salary record nahi banega. Test ke baad apna local host band kar dunga.”

Approval is pending. A source manifest/hash or this written plan is not approval; implement and
review the bounded runner before presenting its executable scope for that final decision.
