# Private employee document lifecycle

Part A02 extends the existing employee document APIs and encrypted private file store. It does not
configure a scanner, generate keys, provision storage, enable cleanup or activate an account.

## Consumer contract

Base route: `/api/v1/hr/employees/{employeeId}/documents`. Existing current web MFA and All-scope
employee/document grants apply. Medical files additionally require their separate medical grant.
Every write takes an `Idempotency-Key`; successful identical retry returns the receipt after current
authorization is checked. A changed request using that key returns a conflict.

| Method and suffix | Behavior |
| --- | --- |
| `GET /` | Private metadata; availability uses the configured business date, lifecycle and scan state |
| `GET /{documentId}` | Current lifecycle, scan-attempt, conflict, revision and safe metadata |
| `GET /{documentId}/history?afterRevision=0&take=50` | Ordered retained metadata events; maximum page size 100 |
| `POST /{documentId}/rescan` | Commit unavailable scan-attempt state, scan privately, then reauthorize completion |
| `POST /{documentId}/verification` | Authoritative manual decision against the current document version |
| `POST /{documentId}/expire` | Explicit terminal expiry, with reason and versioned history |
| `POST /{documentId}/revoke` | Explicit terminal revocation, including evidence awaiting hiring review |
| `POST /{documentId}/archive` | Logical archival; retains the encrypted object and history |
| `GET /completeness?expiringWithinDays=14` | Required/missing/expiring standard evidence and certification metadata |

Rescan, expire, revoke and archive bodies now require both versions and a printable reason:

```json
{
  "expectedVersion": "<employee rowversion>",
  "expectedDocumentVersion": "<document rowversion>",
  "reason": "<authorized action reason>"
}
```

Verification bodies require `decision`, `expectedVersion`, `expectedDocumentVersion` and optional
`reason`. A Rejected decision requires a nonblank reason. Both versions are canonical eight-byte
SQL rowversions encoded as 12-character Base64 strings. Read current metadata after successful
mutation for the next document version. Unknown JSON properties remain rejected.

List metadata adds backward-compatible `lifecycleStatus` (Current 0, Expired 1, Revoked 2, Archived 3),
`isScanInProgress` and `historyRevision`. Old frozen submission snapshots deserialize using their
defaults; approval still checks each exact current document rowversion and current availability.

The web document component sends the document version, requires action reasons, displays scanning
and lifecycle states, and presents expire/revoke/archive only with the corresponding granted
permission. Existing in-memory uncertain-request keys and payloads remain in use.

## State and interruption behavior

Extension, MIME, signature and the existing 10 MiB upload ceiling remain enforced. The service owns
an immutable upload copy and passes a separate copy to the scanner. A scanner exception, unknown
state or malformed scanner version becomes Failed, never Clean. Caller cancellation remains
cancellation. Canceled initial scanning creates no accepted document; invalid uploads perform no
scanner or storage I/O. An encrypted object written before an uncertain SQL outcome is retained
for authorized operational reconciliation.

A rescan start commits an actor/resource/key-bound attempt and clears the current manual decision
before scanner I/O. In-progress evidence is unavailable for ordinary download, reviewer download,
manual verification and completeness. Scanner cancellation or private-object failure leaves that
committed attempt unavailable. Retry the same unchanged request/key to continue it. Use a new key,
current versions and reason to explicitly replace an interrupted attempt; the old attempt cannot
complete the replacement. A completed Failed/Rejected result needs a new rescan key for another
attempt. Receipt expiry also requires a new, current intent.

Only the active attempt can complete. Completion refreshes database metadata, checks its captured
document version and exact object identity, and reauthorizes the current actor. A clean rescan
requires a new manual verification. Equal-time conflicting scan evidence stays unavailable.
Rejected files are logically quarantined in the private encrypted store; no endpoint serves those
bytes or accepts a client-supplied clean result.

Expire/revoke/archive do not remove objects. Current evidence can be explicitly expired, revoked
or archived. Expired/revoked evidence can subsequently be archived. No action silently restores
withdrawn evidence; replacement uses a new uploaded document and the applicable approved selection
or certification workflow. These terminal actions can invalidate pending approval evidence without
rewriting the frozen employee profile. Approval then requires review because the document version
has changed.

## Download and completeness

Download authorizes metadata first, reads and verifies the encrypted object outside the SQL
authorization transaction, then checks current web authority, live grants, document state/version
and business-date validity again before returning bytes. Denied content is cleared from the service
buffer. Attachment/no-store/nosniff/sandbox headers and disabled range processing remain in place.
Reviewer downloads additionally require the verification grant and clean current evidence.

Issue dates and expiry dates are inclusive: future-issued and expired evidence cannot satisfy
current availability. Explicit withdrawal and in-progress/conflicting/failed/rejected scans also
block availability. Required profile photo and identity evidence reuse the established hiring
requirements. Missing/unavailable selected evidence and incomplete/expired certification requirements
are activation blockers; a still-valid expiry inside the requested reminder horizon is a reminder.
Draft certification evidence must be selected. An explicitly approved current certification revision
can use its exact verified standard employee document even outside that original manifest. This
does not change the selected manifest or suppress unavailable selected files: an older selected
attachment may need its own approved document-selection change if it serves another requirement.
The query horizon must be explicitly supplied between 1 and 365 days; this query does not approve a
notification policy or send notifications. Current certification versions use the business date and
their retained status; a superseded historical certificate is not treated as current.

This standard-evidence projection returns no medical, bank, salary, identity-number or private
storage payload. Medical completeness remains in the separately authorized hiring validation path.
Category-specific required-document policies and operational notification schedules still need their
approved configuration and Part G integration.

## Persistence and operations

`EmployeeDocuments` adds lifecycle/attempt metadata and a history revision. `hr.EmployeeDocumentHistory`
is append-only with restrictive employee/document relationships and a unique per-document revision.
The persistence guard prevents deletion or changes to upload identity, privacy, dates and private
object metadata. Every mutation must retain exact ordered before/after state events in the same
transaction. Journal DTOs never include object references, filenames, keys or plaintext.

Root-owned DI, permission registry, DbContext hooks and migration/snapshot integration must include
the document repository, three lifecycle permissions, guard and new mappings. No migration was
applied by A02. Existing records keep revision zero until their first retained lifecycle event;
historical events are not fabricated during upgrade.

Real ClamAV engine/signature coverage, storage/key/ACL configuration, backup/restore evidence and
authorized SQL race acceptance remain deployment checks. No scanner is enabled, no keys generated,
no provider chosen and no real employee file used by this increment. Cleanup stays disabled until
approved retention, holds, orphan reconciliation and restore policy exist. Unreferenced or partial
objects are not sufficient evidence for deletion. Face template/capture storage remains separate.
