# H2 Document Expiry Reliability

Date: 15 October 2026. Scope: the existing document/certification/employee-insurance
reminder worker. This record is not authenticated SQL acceptance or completion of
the full 19-task plan.

## Repair

- Retains the dirty 100-row source-ID cursor pagination. A fresh scope and explicit
  transaction per page bound tracked rows, saves, rollback and partial progress.
- Uses the existing `INotificationOutbox`/`NotificationOutbox` and durable handler,
  instead of inserting delivered notifications directly. No queue, schema,
  permissions or shared notification contract is added.
- An exclusive transaction-owned `cosmix:document-expiry-reminders` application
  lock serializes producers across instances. The existing unique outbox index on
  message type plus deduplication key remains the final duplicate constraint.
- The event is a deterministic 128-bit SHA-256-derived identifier of the invariant
  legacy source-kind/source-ID/expiry correlation. The existing outbox key also
  binds template and account. A changed expiry is a new event; retries are not.
- Existing delivered in-app reminders with the exact recipient and correlation
  suppress re-enqueueing. A different recipient or delivery channel cannot do so.
- Failed/cancelled pages do not advance the cursor; the next sweep replays from
  the beginning and deduplicates committed pages. Failure in one source kind does
  not prevent scanning the other kinds. Logs contain no exception/payload details.
- Read-committed transactions deliberately avoid holding legacy inbox range locks
  while waiting on a row owned by the existing dispatcher. Producer serialization
  is supplied by the application lock, not serializable inbox scans.

The existing inclusive UTC today through today+30 window, current-document,
active-certification and non-disabled-account filters remain. This task does not
choose new reminder timing, account eligibility or vehicle reminder recipients.
The event describes eligibility observed by the producer. The generic delivery
handler does not re-evaluate the source's later expiry/status changes.

## Evidence

Baseline Release passed with zero warnings/errors. The first standalone verifier
failed four of six checks against that build: no outbox records, inline delivery,
more than one page of tracked/saved notifications, and no producer transaction.
Retained baseline artifacts:
`D:\CosmixVerification\part-c-parent-20261013\managed-a44587fda326412289fcc567768fb527`.

An initial post-change solution build encountered 11 errors in concurrently edited
`HrReportService.cs`; those files remained with their owner. The subsequent
isolated Release build passed with zero warnings/errors in 1:56.24, before the
parent reserved the next build slot:
`D:\CosmixVerification\hr-expiry-h2-20261015\final`.

The expanded harness initially needed the EF relational query-root namespace
import. A direct compiler invocation also exceeded Windows' command-line limit;
the retained response-file approach resolved that setup error.

The first actual SQL translation check then caught a product defect in the new
projection: ordering through a positional record constructor did not translate.
It was changed to a property-initialized candidate class. The corrected worker
source, compiled alongside the verifier against frozen dependencies, passed 53/53
checks without another project/solution build. That intermediate result is not
added to the final count below.

The parent's subsequent combined Release includes the corrected projection.
Final verification against that exact Release passed **135 checks**, exit 0:

| Standalone suite | Checks |
| --- | ---: |
| VerifyDocumentExpiryReliability.cs | 53 |
| notifications/VerifyNotifications.cs | 78 |
| VerifyIsolatedWorkers.cs | 4 |

```powershell
& ./docs/attendance-gap-c/VerifyManaged.ps1 -ReleaseDirectory 'D:\CosmixVerification\hr-backend-continuation-20261015\posting-build\bin\Cosmix.Api\release' -Sources @(
  'docs/hr-srs-remediation/VerifyDocumentExpiryReliability.cs',
  'docs/attendance-completion/notifications/VerifyNotifications.cs',
  'docs/hr-srs-remediation/VerifyIsolatedWorkers.cs'
)
```

Frozen input hashes, compiler inputs, copies and logs:
`D:\CosmixVerification\part-c-parent-20261013\managed-4966ec2e76d147bbb3d12db73c7af4b5`.
The standalone compiler emitted two retained SqlClient CS1701 framework-reference
unification warnings. No new product build ran after the parent reserved the
build slot. No product source changed after this combined-build verification.

The new suite executes the real worker, `NotificationOutbox`,
`DurableJobRepository`, `NotificationRepository` and `InAppNotificationHandler`.
Only query execution, connection/transaction IO and protected bytes are synthetic.
It covers 602 records **per source**, page sizes 0/99/100/101/500/501, inclusive
expiry boundaries, null expiry, excluded statuses/recipients and vehicle-only
coverage, existing delivered reminders, new expiry identity, culture changes,
failed page commit/storage writes, cancellation, fresh scope replay, two producers,
failed completion, expired/reclaimed leases and competing completion of one claim.
Actual candidate SQL is generated for all three source kinds with/without cursor;
real connection attempts are forbidden. Existing unique account/outbox model
constraints are checked. The two retained regression suites cover additional
outbox/service rules and isolated host worker exclusion without launching a host.

Current SHA256:

- Worker: `732A98392D4AED2EED47242084BBAF54CF933F939037B6EA61CA1533352524ED`
- Verifier: `E5AA7826EFF5F2D5A6F310960726A1812DE1F3DD9C2497327E81E0CC28D833F4`

## Ownership and Integration

Changed files are limited to:

1. `src/Cosmix.Api/BackgroundJobs/DocumentExpiryReminderWorker.cs`
2. `docs/hr-srs-remediation/VerifyDocumentExpiryReliability.cs`
3. This evidence file.

No shared DI/Program/DbContext/migration/permission or global progress file was
edited. Existing registration already supplies `TimeProvider`, the notification
outbox, private storage and the durable delivery handler.

Parent follow-up: update the H2 progress row from this evidence, and verify the
deployment's existing durable-worker enablement and private storage configuration.
Queued reminders are delivered only when the existing durable worker is enabled;
this patch does not turn that deployment gate on. Stop old direct-insert worker
instances when rolling out: their old check-then-insert path does not honor the
new producer lock.

There is no existing document-expiry processor/repository file. The explicit edit
allow-list confines this repair to the existing worker, retaining its existing EF
layering debt. A clean-architecture extraction into a module-specific Application
processor and Infrastructure repository, with parent-owned DI, requires ownership
expansion. A handoff attempt through `send_message_to_thread` was rejected because
the target is this agent's native ancestor; no native messaging tool is exposed.
These requirements are therefore retained here and in the final handoff.

## Remaining Acceptance

No SQL connection, database/instance/seed/account/credential change, host launch,
external provider call or real private-storage operation was performed. No new
test project, Git branch/worktree/sync/commit or business default was introduced.

Synthetic relational IO exercises production control flow; it cannot prove SQL
Server lock scheduling, deadlocks, actual rowversion enforcement or crash recovery.
On an explicitly approved synthetic target, independently verify two producers,
two dispatchers, commit failure/restart, cancellation, legacy data and 501+ records
per source. Confirm one notification and one completed outbox event per identity.
Existing private-file writes precede the database commit and may leave unreferenced
encrypted objects after rollback; cleanup belongs to the existing storage lifecycle.
