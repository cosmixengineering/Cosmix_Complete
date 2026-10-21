# Correction intake verification

9 October 2026. The root agent's coordinated runs passed **97 service checks** and **33 SQL query
translation checks**. The initial real SQL
translation run found an EF projection composition defect before any connection was opened: composing
date filters after a constructor-only `CorrectionRecord` projection did not translate. The query now
uses explicit member bindings; the corrected actual `ToQueryString()` rerun passed. No SQL connection, database write, provider
call or real fixture is authorized by these standalone checks.

## Checks and evidence

- `VerifyCorrectionService.cs`: actual `AttendanceCorrectionService` and `AttendanceOperation` with
  in-memory repository/private store/current-auth boundaries. Exercises immutable submit/history/detail,
  unchanged raw/summary sources, stale source IDs, own identity/permission/session denial, locked dates,
  pending cancellation/version/retry, equivalent proposal dedup, protected unique submission binding,
  overnight roster context, factual-only labels, bounded validation and confidential exception redaction.
- Executed source-proof regressions cover: newly added/closed
  session changes while assessment/summary IDs stay unchanged, refreshed proof success, historical
  scope change, canonical ordering, missing/malformed fingerprint rejection, private scope tamper,
  old schema-v1 snapshot readability and exact legacy request bytes/digest/replay with live revocation.
- `VerifyCorrectionQueries.cs`: actual EF SQL Server `ToQueryString()` for scoped joined history/detail,
  filtering after record projection, pagination, raw/latest assessment/latest summary and Pending
  equivalent-proposal queries. Uses a deliberately unusable server address; no database operation runs.
  Latest-session anti-join/bound checks and an explicit no-connection interceptor passed.

The service runner executes the real Application service and shared authorization/idempotency
operation against synthetic in-memory dependencies. The query runner executes the real repository's
query expressions with EF Core's SQL Server translator. These checks distinguish client-side tests
from the demonstrated SQL translation defect and repair; they do not prove SQL transaction isolation.
The coordinated Release build passed with 0 warnings/errors. Historical eligibility passed 25 standalone checks; summary61 and notifications78 regressions also passed. The coordinated anonymous HTTP suite passed316 checks across48 attendance operations and58 employee operations. These deny/contract checks do not prove a real authenticated SQL success.

## Remaining real acceptance

Authenticated web MFA/CSRF and mobile SQL/private-storage acceptance; concurrent submit/cancel and
source replacement under SQL serializable isolation; transaction rollback when evidence/audit/receipt
storage fails; revoked access on persisted restart replay; isolated encrypted evidence tamper testing;
and live approval/source races remain outstanding. The local decision workflow now exists; pay calculation and locked-month carry-forward remain separate.

The first legacy replay fixture rerun failed because parsing and reserializing date/time JSON changed exact UTF-8 escaping. A pinned typed legacy payload now verifies exact bytes and digest; the corrected rerun passed. Production digest verification was not relaxed. New HR-review checks executed successfully, including missing checkout/manual duty, associated CheckIn stale proof, actual provenance, self-decision denial, overnight boundaries, terminal return/resubmission, scoped queue, retries and synthetic summary/outbox rollback. An initial duplicate local variable in the expanded harness was corrected before the successful97-check run.
