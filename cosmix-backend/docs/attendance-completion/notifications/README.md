# Attendance notifications and durable jobs

This slice implements SRS section 3.1 after-commit processing, APR-005 safe content,
and section 13 notification reads/read status. It supplies the outbox mechanism
needed by REP-001; it does not itself generate a report or decide business recipients.

The existing `communication.Notifications` and `jobs.OutboxMessages` tables are reused.
No new model, mapping, migration, DbContext or snapshot change is required by this slice.

## Client API

All three routes use the current validated account. There is no client-supplied employee,
recipient or account filter and no administrative inbox impersonation.

| Method | Route | Contract |
|---|---|---|
| GET | `/api/v1/me/notifications?page=1&pageSize=25&unreadOnly=true` | Bounded page (1–10000), size (1–100), recipient-filtered before count/pagination |
| GET | `/api/v1/me/notifications/{id}` | Safe content; another account's ID and missing ID both return 404 |
| POST | `/api/v1/me/notifications/{id}/read` | `{ "expectedVersion": "AAAAAAAAAAE=" }`; obtain the actual version from GET |

Web requests require the existing current MFA session; the existing middleware requires
CSRF for unsafe cookie-authenticated calls. Mobile bearer requests retain current registered
device/session/lifecycle checks through the existing authentication service. No new role is
granted and no business-management permission is inferred from access to a personal inbox.

An unread stale rowversion returns 409. Marking read is a monotonic operation: once read,
a retry (including the original version) returns the preserved timestamp with `changed=false`
and creates no second mutation/audit. There is no mark-unread operation, so this replay cannot
overwrite a competing choice. Invalid version encodings are rejected even on retries.

List/detail/first mark-read record audit with current identity and correlation. Reads do not
implicitly mark read. Only delivered InApp rows are visible; other channels and undelivered
rows are excluded. DTOs never contain payload/storage references or recipient/employee IDs.

Example list item:

```json
{
  "id": "00000000-0000-0000-0000-000000000001",
  "templateCode": "attendance.shift-changed",
  "title": "Site schedule updated",
  "body": "Open My Shift to check your applicable site schedule.",
  "createdAt": "2026-10-09T12:00:00+00:00",
  "readAt": null,
  "version": "AAAAAAAAAAE="
}
```

The identifiers/version above illustrate shape only; they do not identify a fixture/account.
Notification content is selected from server-owned static templates. There is no caller text
interpolation, sensitive rejection reason, medical/bank/CNIC/salary value or protected attachment.
The detail feature always performs its own current authorization. Unknown historical template
codes receive generic safe content instead of rendering their stored text.

## Producer transaction contract

Inject `INotificationOutbox` into the use case that already owns business authorization and
the write transaction. Resolve recipients on the server using the business event's approved
scope. Stage one intent per recipient before the use case's final save/commit:

```csharp
await notificationOutbox.StageAsync(
    new NotificationIntent(immutableBusinessEventId, recipientAccountId,
        NotificationTemplates.ShiftChanged, siteId),
    requestContext, cancellationToken);
// Existing business/audit/idempotency SaveChanges and outer transaction commit follow.
```

`StageAsync` never saves or commits. The repository rejects staging without an active producer
transaction. It uses the same scoped DbContext as that business transaction. Payload bytes are
written through the existing encrypted private store with an event/recipient-bound purpose;
only the protected reference is stored in the outbox. An unsuccessful SQL transaction can leave
an encrypted orphan object; do not delete keys or files to simulate rollback. Existing retention
policy must govern orphan cleanup.

The tuple template/event/recipient is the deduplication identity (also enforced by the existing
SQL unique index). The event ID must identify immutable evidence, not a fresh random ID on each
retry. Same identity/same resource stages nothing again; altered resource conflicts. A failed
outbox record is retained and is not silently recreated by producer retries.

Supported safe templates: attendance exception, missing punch, decision update, shift change,
report ready, zone notice, site report update. Supporting a template does not prove its producer
workflow is implemented. See the overall requirement checklist for actual integrations.

## Worker and retry guarantees

`AttendanceOutboxWorker` uses `DurableJobProcessor`; it is separately configured from the
existing OTP and employee lifecycle workers. `AttendanceJobs.Enabled` defaults to **false**.
Registration alone cannot poll or write a database. Explicit enablement is operational setup
after the target schema, private storage and producer integration are verified.

The processor durably claims one available row whose exact message type belongs to its registered
handlers. Unrelated pending messages and expired leases remain untouched for their owning workers.
The explicit ownership list allows 1–32 unique lower-case identifiers of up to 100 characters;
there is no wildcard/prefix ownership. SQL binary comparison and a trailing-space sentinel preserve
the same exact-name semantics as the handler registry, including case and padded aliases.
SQL transaction-owned application locks bound
competing claims; SQL rowversion guards races. Expired leases can be reclaimed. A completion
transaction reloads the row under update lock and rechecks the exact token/expiry both before
and after the handler. Notification insert, outbox completion and audit save atomically. A crash
before commit leaves no notification; a crash after commit leaves Completed and cannot redeliver.
This applies to in-database effects, not exactly-once external services.

Handlers must stage changes in the same unit of work, must not start/commit another transaction,
and must not send external messages. Registered handlers create in-app notifications and private
factual CSV/XLSX/PDF exports (see `Reports.md`). If a claimed row no longer matches a registered
handler, it fails with `unsupported_message_type`, never fake success. This defensive post-claim
check does not authorize taking another module's unregistered work from the shared queue.
Malformed or incorrectly bound notification payloads fail; private-store unavailability retries.
Transient failures use bounded exponential delay; MaximumAttempts defaults to 5. Repeated worker
crashes/expired leases also stop once the attempt bound is exceeded. Cancellation leaves the
persisted lease for later recovery. No public retry/dead-letter administration route is invented.

Operational settings under `AttendanceJobs`: Enabled=false, PollSeconds=5, BatchSize=10,
LeaseSeconds=120, MaximumAttempts=5, RetryBaseSeconds=5, RetryMaximumSeconds=300. Bounds are
validated at registration. These are processing limits, not attendance/pay rules.

## Verification boundary

The standalone `VerifyNotifications.cs` passed **78 assertions** in the coordinated local run.
It exercises the real Application services and Domain objects using in-memory repository/file
doubles, checks registered-type ownership (including untouched unrelated pending/expired rows,
case/padded aliases and fail-closed handler mismatch), and inspects the real EF model and actual
claim-query translation with the SQL connection closed. No new SQL acceptance was authorized
or executed for this slice. It does not substitute for authenticated HTTP/SQL execution, real competing SQL workers,
database rollback, encrypted storage failures or host restart. No test project was created.

Run from repository root with cached packages:

```powershell
dotnet run --file docs/attendance-completion/notifications/VerifyNotifications.cs --configuration Release --property:RestoreSources="$PWD/src/Cosmix.Infrastructure" --property:NuGetAudit=false
```

Real isolated acceptance needs separate authorization for new notification/outbox fixtures and
the worker's DB writes, beyond the earlier access/scheduling/geofence setup. The approved target
must stay distinct from real accounts/data. Add a business event and outbox in one transaction,
verify rollback leaves neither committed, run two workers, interrupt/restart one between claim
and completion, verify single delivery, revoke a recipient session, and check MFA/CSRF and
cross-account reads/mark-read over the registered HTTP routes.
