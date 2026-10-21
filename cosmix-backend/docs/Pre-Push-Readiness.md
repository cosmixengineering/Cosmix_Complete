# Local pre-push review — 12 October 2026

This record covers the existing dirty checkout. No commit, push, fetch, merge, branch/worktree,
GitHub change or automation was performed. Local changes are prepared for source review;
this is not approval to enable real employee Attendance.

## Changes from this pass

This is the earlier pre-push checkpoint. Subsequent local changes implement HR report review,
shift-configured reminders, direct HR policy publication, Admin leave workflows and daily
time/classification, configurable payroll-input revisions and SuperAdmin company/site jurisdictions.
Current evidence and remaining limits are in
[Attendance verification](attendance-completion/Verification.md) and
[policy/leave implementation](attendance-completion/Policy-Leave-Implementation.md).
The current verification record identifies the local unapplied migrations; the recorded isolated
baseline is 17. Calculation inputs do not activate legal payroll penalties or release salary.

- Fixed SQL Server CHECK compilation against newly added columns in unapplied migrations 15/16.
  The predicates and transactions are unchanged. Applied migration 14 and all earlier migrations
  were preserved. Regenerated complete and incremental SQL artifacts.
- Hardened isolated acceptance configuration guards and pinned its HTTP helper. Retained failed
  runs and verified backups; added explicit, bounded continuation instead of replaying writes.
- Corrected fixture expectations: unassigned employees cannot supply correction or month-lock
  readiness evidence. Separate denied-site scope checks exercise authorization.
- Excluded local database/backup/log/test-account files and generated acceptance manifests from
  normal Git inclusion. Corrected stale setup/schema documentation.

## Executed evidence

| Check | Result and limits |
|---|---|
| Release build | 0 warnings, 0 errors |
| Hosted anonymous API/OpenAPI/traffic sweep | 587 assertions, 107 new Attendance and 58 existing employee operations |
| Encrypted zone-location storage | 43 checks using a disposable owner-only OS-temp fixture; encryption/reopen, identity/purpose, expiry/deletion/index and cross-adapter lease cancellation |
| Affected model/query checks | Carry-forward 35, OT 46, zones 27; no pending EF model difference |
| Actual isolated migrations | Verified backups, error reproduction, rollback-only fixed 15–17 check, then guarded upgrade to migration 17 in `Cosmix_Testing` only |
| Authenticated factual web slice | Initial 21 successful assertions included 3 scoped synthetic grants and old-session revocation. Checks-only continuation passed 39 assertions, including one saved summary, exact replay, changed-payload/stale rejection, self identity, CSRF and denied-site scope. Counts overlap and are not summed. |
| Month-lock acceptance limit | Global preview returned `409 employee_assignment_unavailable` because retained dummy administrators lack historical duty assignments. No lock was created; finalization success/not-ready preview was not exercised. |
| Independent SQL/private evidence | 59 read-only checks passed. Exactly 3 scoped test grants, 11 permission bindings, retained old revocation, one summary/receipt/audit; 3 encrypted objects independently decrypted and matched. No raw punches, duty, corrections, carry-forward, locks, outbox/export jobs or monitoring windows were created. |
| File hygiene | Pattern review found no real credentials/private keys/databases/binaries/real employee emails in includable files. Concrete local manifests are ignored; source SRS retained. `git diff --check` passed. |

The HTTP run remains partial at the known month-readiness fixture limit; the 59-check persistence
pass does not turn it into full HTTP acceptance. The owned host stopped and its port was free.
Read [the detailed verification record](attendance-completion/Verification.md) for exact limitations.
All private receipts, settings, keys,
backup files and diagnostic logs remain outside normal Git inclusion.

## Still required before production attendance

- Select and integrate the real face/liveness provider and complete enrollment/device acceptance.
- Complete ordinary monthly financial calculations and payroll integration. Policy publication,
  approved leave effects and daily time/classification now exist, but return `PayrollReady=false`.
  Positive month-lock and historical carry-forward acceptance need suitable approved fixtures.
- Confirm numeric punch/exception/offline rules. Site-report HR review authority was subsequently
  confirmed and its workflows implemented; authenticated acceptance remains outstanding.
- Complete authenticated OT/zone/correction duty flows, simultaneous SQL writers, worker restart/load,
  separate-process storage crash recovery, mobile clients and UAT.
- Approve zone operating settings before activation. Jobs, monitoring and retention remain disabled.

The original development database was not upgraded. A future target requires inspection of its own
history and a reviewed pending migration range; do not run the retained acceptance scripts as setup.
