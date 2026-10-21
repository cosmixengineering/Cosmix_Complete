# Temporary site schedule amendments

This continuation uses direct `attendance.rosters.manage` authority with current web MFA and Site/All scope. Cancellation and supersession append evidence; no approval request, mutable status flag, or deletion is introduced.

The user approved local migration/snapshot preparation on 9 October 2026. Migration `20261009064639_AddDirectSiteSchedulingAndTemporaryAmendments` combines the existing six direct-site operations with this table and two indexes. No database application or DbContext edit occurred. Review and isolated SQL acceptance remain necessary before deployment; its guarded rollback refuses to discard direct scheduling evidence.

## Exact additional schema

Add `attendance.TemporaryShiftChangeAmendments`:

| Column | SQL type | Nullability |
|---|---|---|
| Id | uniqueidentifier | not null, primary key, application-generated |
| OriginalChangeId | uniqueidentifier | not null |
| ReplacementChangeId | uniqueidentifier | nullable |
| RecordedBy | nvarchar(200) | not null |
| RecordedAtUtc | datetimeoffset(7) | not null |
| WrittenEvidenceReference | nvarchar(500) | not null |
| Reason | nvarchar(2000) | not null |

Both change IDs reference `attendance.TemporaryShiftChanges.Id` with Restrict/NO ACTION. `IX_TemporaryShiftChangeAmendments_OriginalChangeId` is unique. `IX_TemporaryShiftChangeAmendments_ReplacementChangeId` is unique, filtered with `[ReplacementChangeId] IS NOT NULL`. Check constraints prohibit self-supersession, require nonblank actor/evidence/reason, and require UTC recording time. A null successor records cancellation. A non-null successor records supersession.

The new entity implements `IAppendOnlyEntity`, so the existing generic context guard rejects edits/deletions. Configuration assembly discovery and `db.Set<TemporaryShiftChangeAmendment>()` require no DbSet/context edit. The site repository additionally enforces that a superseding parent, its nonempty exact staff manifest and its amendment are Added in the same SaveChanges call. The existing context guard continues to reject manifest additions after a parent was saved. No old rows or approval IDs are backfilled.

## Behavior and evidence

Active changes are change rows without an outgoing amendment. All loaded links are checked for missing records, cross-site/date links, duplicate sources/successors, cycles and legacy approval-linked targets. At most one active change may exist for a site/date. Multiple historical roots are allowed after cancellation, allowing cancel → change normal schedule → create a new exception without rewriting the previous chain.

Every amendment retains its written explanation, previous manifest, newly resolved actual staff, staff token, current base and prior/replacement version identities through the encrypted private store. Cancellation has no new `TemporaryShiftChange` employee rows; its exact affected set is frozen in that protected evidence and protected audit/result receipt. Supersession also creates the normal immutable manifest on its new change. History exposes amendment IDs/links, actor/time and derived statuses; it never exposes private storage references.

Amendment previews and writes resolve current site membership independently of the old frozen manifest. This permits a future supersession to repair stale membership evidence. Expected timeline and staff tokens are rechecked inside the existing serializable authorization transaction. Retries use operation-specific idempotency receipts and recheck current permission/scope before returning the frozen result.

Cancellation restores the current base schedule only after validating that schedule, adjacent duty windows, occupied dates, site availability, current staff and any overnight next-day membership. Locks cover dates occupied by both the previous temporary duty and the resulting duty. An active temporary original-version pin still prevents incompatible normal assignment/version changes. Cancelled/superseded historical rows do not remain effective scheduling constraints. Retrospective or same-day changes, amendment of legacy approval records and amendment of a terminal change are rejected.

## HTTP contracts

All suffixes below are under `/api/v1/hr/sites/{siteId}/temporary-shift-changes/{changeId}`:

- `GET /cancel/preview`: returns current timeline/staff tokens, previous/current affected IDs and restored base version.
- `POST /cancel`: `{ expectedTimeline, expectedStaff, writtenEvidence, reason }`, with `Idempotency-Key`.
- `GET /supersede/preview?replacementShiftVersionId={id}`: returns the same preview plus the exact new replacement version.
- `POST /supersede`: `{ replacementShiftVersionId, expectedTimeline, expectedStaff, writtenEvidence, reason }`, with `Idempotency-Key`.

Successful writes return the new timeline, immutable amendment and previous/new change details. Cancellation returns no replacement change. Existing site history includes derived `active`, `cancelled` or `superseded` statuses and amendment links. Stale tokens, inactive targets, invalid restoration, locked dates and malformed chains return 409; incompatible/unapplied schema returns `503 site_roster_schema_not_ready` before querying the new table.

Offline harness checks are synthetic application/pure-rule evidence only. Real SQL contention/constraints, authenticated HTTP, encrypted-disk replay, migration application and permission provisioning remain separate acceptance work.

## Executed application checks, 9 October 2026

`VerifySiteScheduling.cs` first reported 118 passing checks and one intended missing-amendment-contract failure. After implementation it passed 159 checks. Additional incoming/outgoing transfer regressions then exposed three actual acceptance failures in normal assignment/replacement and temporary creation (160 passing checks, three failed scenarios). All normal and temporary candidate changes now use the shared `SiteRosterImpactValidator`; the resulting harness passed **170 checks with zero failures**, exit 0.

The additions cover immutable supersession/cancellation, staff-manifest repair, exact protected evidence, active history links, cancellation then normal replacement and a new temporary root, stale timeline/staff tokens, replay and revoked access, schema absence, future dates, restored overnight locks/transfers/adjacency, exact-midnight/rest-day boundaries, incoming prior-site overnight duties, and cyclic/duplicate/missing amendment links. These are real Application services with synthetic repository/auth/storage collaborators. No test project, SQL fixture, database application, live account grant or private-disk fixture was introduced by this harness.
