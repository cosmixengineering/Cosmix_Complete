# Attendance exception visibility

The existing capture rules append immutable exception evidence. These reads do not resolve an
exception, approve a punch, change attendance, or grant access. An exception is a historical
detection record; the collection is not an unresolved-work count. Missing-checkout/excessive-duty
timers and their numerical configuration remain separate pending business decisions.

## Registered contracts

| Method and route | Permission | Contract |
|---|---|---|
| `GET /api/v1/me/attendance/exceptions` | `attendance.exceptions.self` | Existing own notices list |
| `GET /api/v1/me/attendance/exceptions/{id}` | `attendance.exceptions.self` | Own safe notice, current employee resolved from session |
| `GET /api/v1/attendance/exceptions` | `attendance.exceptions.read` | Bounded scoped detection history |
| `GET /api/v1/attendance/employees/{employeeId}/exceptions/{id}` | `attendance.exceptions.read` | Scoped detail including confidential detection reason |

The new read permission is sensitive, management-only and requires a current web MFA session.
It is added to the catalog, not automatically granted to an account, role or bootstrap owner.
Correction review permission does not implicitly grant exception visibility. The existing access
module and delegation rules control its assignment; permission registration alone is not a grant.
All reads use the existing attendance authorization transaction and audit journal.

Self detail hides the confidential kind/reason and returns the same safe notice shape as the own
list. It exposes no coordinates, biometric information, approval internals or private-file reference.
An ID belonging to a different employee returns 404. Missing referenced evidence returns 409.

## Historical scope and continuation

Queue dates are `from` inclusive and `to` exclusive, at most 366 days. `scope` is one of
`All`, `Employee`, `Site`, `Department`, `Team`, `Project`; every scope except All requires
`targetId`. All requires an All grant. `kind` is an optional exact exception-kind filter.
`pageSize` is 1–100 (default 25).

Every returned exception requires each historical employee/date scope and its original retained
punch/session site. A new Check-In may reveal an old missing Check-Out after a site transfer:
both dates and both sites remain relevant. Retroactively changing membership cannot substitute
authority over the retained old site. A legacy source without a site needs employee/All authority.
Missing or mismatched linked evidence, malformed history, or unavailable historical membership
fails closed. A queue skips such rows; detail returns `attendance_exception_source_unavailable`.
Unexpected storage/server failures propagate rather than producing a misleading empty success.

The repository bounds each candidate scan at 201 rows and the service examines at most 200.
Rows are ordered by detection timestamp and ID. Authorization occurs before disclosure, and there
is no hidden total count. `nextCursor` is opaque, protected by the existing application cursor
service and bound to account, purpose and exact filters (including page size). A cursor grants no
authority: every page rechecks the current session, grants and historical evidence. An empty page
may have a continuation when its bounded candidates were inaccessible. Continue until it is null.

Example (authenticated web session with current MFA and explicit site-scoped read permission):

```http
GET /api/v1/attendance/exceptions?from=2026-10-01&to=2026-11-01&scope=Site&targetId={siteId}&kind=MissingCheckOut&pageSize=25
GET /api/v1/attendance/exceptions?from=2026-10-01&to=2026-11-01&scope=Site&targetId={siteId}&kind=MissingCheckOut&pageSize=25&cursor={nextCursor}
GET /api/v1/attendance/employees/{employeeId}/exceptions/{exceptionId}
```

Use the returned ID with `GET /api/v1/me/attendance/exceptions/{exceptionId}` for own detail;
the client cannot supply a different self employee identity. These are reads, with no fabricated
decision/resolve action and no new approval chain.

Capture now classifies an explicit `overlapping_schedule` result as `OverlappingShift` and keeps
the punch queued for review. It never selects one of the conflicting schedules. Other unavailable
rosters do not fabricate an overlap, and offline evidence remains Unverified.

## Verification boundaries

On 12 October 2026, `docs/attendance-corrections/VerifyExceptions.cs` passed **54 checks** against the actual service and attendance
authorization boundary with synthetic in-memory collaborators. It covers self confidentiality,
foreign IDs, missing permission/MFA, revocation, original and transferred sites, malformed sources,
bounded empty pages, cursor binding and validation. It does not execute SQL, HTTP or private IO.

On the same date, `docs/attendance-corrections/VerifyExceptionQueries.cs` passed **35 checks** translating the actual repository queries
with SQL Server EF and a connection-blocking interceptor. It covers employee filters, historical
half-open membership, retained sites, terminated memberships and bounded keyset pagination.
Translation does not prove SQL execution, indexes or concurrent authorization behavior.

`docs/attendance-capture/VerifyCaptureRules.cs` covers factual overlap classification and the
existing fail-closed capture gates. Executed counts belong in the latest verification ledger.

No schema, migration, permission grant, database fixture or real account is changed by this slice.
Authenticated SQL/HTTP exception read/revocation acceptance remains to be run in an approved
isolated fixture. Existing anonymous route verification is recorded separately by the root pass.
