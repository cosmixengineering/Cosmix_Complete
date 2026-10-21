# O1 outstation business acceptance

## Approved Scope

The user approved this bounded O1 batch on 20 October 2026 after the exact
scope was presented in chat. Approval does not include benefit/recovery or
historical vehicle fixture writes.

- Existing `Software-PC1\LOCALDB#9F3001F7 / Cosmix_Testing`, PID 25768,
  owner/operator `COSMIXRND\Huma.Khan`.
- Family `D9C34627-18A2-41CF-A804-821E39516303`; existing MDF/LDF unchanged.
- Exactly 25 existing migrations; no migration or schema operation.
- Reuse the existing synthetic HR/Admin and P1 permission assignments.
  No additional accounts, roles, grants, or permission extension.
- Nine new source rows: five TEST organization masters, one historical
  HR employment assignment, one allowance policy, one version and one
  employee-specific applicability assignment.
- Synthetic trip from 18 October 04:00 UTC to 19 October 12:00 UTC;
  explicitly configured calendar-day treatment, PKR 100/day, expected
  PKR 200. This is test data, not an approved company default.
- Through real APIs: one deployment, one allowance claim/submission,
  two approval headers and two independent Admin decisions.
- Maximum 100 HTTP requests and two local-MFA sessions; necessary
  auth/session/rate-limit/audit/receipt/encrypted evidence writes only.
- Owned host only `https://localhost:7481`; new private files only
  `C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261020O1`.
- Preserve old rows and private files. No cleanup, reset, payments,
  real face/external OTP or legal activation.

The historical employment and policy prerequisites are explicitly seeded
synthetic records. They do not prove the hiring or policy-publication API flow.
The employment assignment does not change the employee's status, joining date,
login, or permissions. The policy is restricted to this employee and historical
test dates.

## Current Read-Only Evidence

`Inspect-HrLivePrerequisites20261020.ps1` completed successfully during this
continuation: same target/family/files/owner, ONLINE, 25 matching migrations,
three known synthetic accounts and all five P1 permission catalog entries.
The Admin has four relevant All-scope grants; HR has employee-scoped deployment
read/request. Port 7481 was free.

No source assignment, policy, leave/recovery, trip/fuel, deployment or allowance
rows were present. The existing one vehicle/one custody from V1 must remain
unchanged. Preflight must repeat before execution.

## Offline Regression Evidence

Frozen input:
`D:\CosmixVerification\hr-backend-20261020\hr-p1-build17\bin\Cosmix.Api\release`.

- Outstation service/continuity: 71 passed, zero failed.
- Deployment policy boundaries: 52 passed, zero failed.
- Vehicle dated attribution: 173/173 passed.
- Vehicle maintenance boundary probes: 97/97 passed.
- Fuel reconciliation: 42/42 passed.

First three logs:
`D:\CosmixVerification\part-c-parent-20261013\managed-687543df5b21412dbdc666b616bcbdc9`.
The initial combined command then stopped because the remaining-vehicle suite
requires an explicit fixture-assembly environment binding. No business check
failed. The corrected invocation bound the just-verified assembly and SHA256
in the child process environment and restored previous values afterwards.
The last two suites passed under:
`D:\CosmixVerification\part-c-parent-20261013\managed-6f6bf91fc3e74794bfdc984a4c88e3d1`.

These are actual service checks with synthetic repositories/storage. They are
not HTTP, live SQL, physical device or provider acceptance.

## Execution Status

Final frozen Release:
`D:\CosmixVerification\hr-backend-20261020\hr-o1-build03\bin\Cosmix.Api\release`.
Build passed with zero warnings/errors. Final compiled standalone checks passed:
composition 86, boundary 48, fixture/domain/EF translation 363, outstation 71,
policy boundaries 52. Exact logs:
`D:\CosmixVerification\part-c-parent-20261013\managed-75bd204c5c2b4c9f9722f74be3fc031a`.
The PowerShell runner/target/audit/domain guard suite passed 185 checks.

Preparation caught and fixed a missing enum import, disabled-auth configuration
acceptance, and a two-attempt logout containment mismatch. The first read-only
Inspect attempt stopped before connecting because PowerShell treated
`SqlConnectionStringBuilder.ApplicationName` as an unsupported dictionary key.
Using the canonical `Application Name` key fixed it; an actual closed-connection
factory regression now covers this. No SQL/private writes came from that attempt.

Final read-only Inspect passed. Packet SHA256:
`C901514599FD3A1437A587E10957663CBAAE74C35A65BDDA85CCCDE2BE499D48`.
Private root absent, target/actors/active grants/schema/port checks passed;
no writes and no host startup during Inspect.

The Execute attempt stopped during fixture preparation. The actual
`CosmixDbContext.ValidateTrackedWrites` guard requires the Employee aggregate
to be tracked when adding an employment assignment. Its normal behavior also
increments `DraftRevision`; the original scope did not authorize that change.
No guard was bypassed. The transaction rolled back, with zero HTTP requests,
zero logins and zero source rows saved. Subsequent read-only reconciliation
confirmed all nine source IDs absent and the HR employee revision still 5.
The host exited and port 7481 was free. Original private files, failure logs
and preservation snapshots remain under the original O1 root.

## Explicit Retry Authorization

The user approved ONE bounded retry plus the normal metadata change on
synthetic HR employee `35d19922-70a8-4c80-8578-e1029360e9fc`:
`DraftRevision` 5 to 6 and a SQL-generated RowVersion. All other employee
fields, status, joining date, account and permissions must remain unchanged.
The original nine sources and business/API limits are unchanged.

The retry uses a fresh root:
`C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261020O1Retry01`.
The original O1 evidence must remain unchanged. Actual persistence-guard
regressions and read-only preflight must pass before the retry. An expired
permission, existing retry marker, changed parent revision, target drift or
failed guard stops execution. No automatic reset or further retry is allowed.

### Retry Preparation Evidence

Release build05 passed with zero warnings/errors:
`D:\CosmixVerification\hr-backend-20261020\hr-o1-build05\bin\Cosmix.Api\release`.
Build04 encountered a concurrent AdvancesLoans controller/service mismatch;
those unrelated edits were preserved, and build05 compiled the current source.

The fixture now tracks the exact approved Employee aggregate and uses normal
`ValidateTrackedWrites`, requiring nine inserts and one parent metadata update.
It checks every other employee property before commit. `SaveChanges(false)`
requires EF's current store-generated RowVersion, not the pre-accept CLR value;
both tracked and database-readback comparisons use that current value.

Build05 checks: composition 86/86 and boundary 48/48, retained under
`D:\CosmixVerification\part-c-parent-20261013\managed-f111375319384f038d6433add68ba1c4`.
The initial fixture verifier had five test-state failures: resetting an entry to
Unchanged restored its old revision, and replacing a generated-value overlay with
the CLR value could be a no-op. Fresh state and explicit input assertions corrected
these tests without weakening production guards.

Final fixture/persistence checks: 462/462; outstation service: 71/71;
dated policy boundaries: 52/52. Exact output:
`D:\CosmixVerification\part-c-parent-20261013\managed-0189ce1100a140b48d0afe6864bf1cc3`.
These are offline checks, not SQL acceptance.

Retry read-only Inspect then stopped on metadata encoding before any private
write, host or login. Actual SELECT diagnostics confirmed the HR row still had
revision 5, active/approved status and unchanged joining date. Its RowVersion
column is SQL timestamp (8 bytes); the original varchar conversion emitted raw
characters. Explicit conversion through binary(8) returned a valid 16-character
hex value. This requires a read-only query correction, not a database alteration.

### Retry01 Actual Outcome and Reconciliation

Read-only Inspect and Execute matched packet
`7037F61DB6CCEF1CF64F6037A18422D212405E97E1EE804CB2B25E9976CC57D8`.
The host exited before listening or authenticating: zero HTTP requests and
zero login sessions. Its primary failure was `RequireParentPreserved` after
`SaveChanges(false)`, not a business API failure. The final preservation query
then timed out and masked the original exception in console output.
Host PID 35764 is recorded as exited; no instance stop or cleanup was performed.

On continuation, SELECT-only reconciliation verified the same database identity,
owner/family and 25 migrations. All nine source IDs are absent, deployment/claim/
O1 receipt counts are zero, and HR DraftRevision remains 5 with its original
RowVersion. All 158 protected SQL snapshot hashes and prior private-file
snapshots match the retained baseline. This is verified rollback/preservation,
not a successful outstation business test.

The first ad hoc read-only reconciliation wrapper omitted variables required by
the prior-file helper, so that file check did not complete. The corrected wrapper
initialized all helper inputs and passed both snapshot comparisons; it performed
no writes.

The exact EF failure was reproduced offline: its provider-default
`SetStoreGeneratedValue` marks RowVersion modified, whereas the earlier verifier
forced `setModified:false`. RED evidence: 465/466 on build05 under
`D:\CosmixVerification\part-c-parent-20261013\managed-deae442278fa406fb477ccca6babc57f`.
The fixture now distinguishes provider-generated current values from client
mutation and checks the actual complete post-save write set. The runner retains
primary errors separately from cleanup/reconciliation errors and never reports
preservation as passed before verification.

### Retry02 Authorization

The user explicitly approved ONE fresh retry with the unchanged O1 business and
metadata scope. Only the new private root changes to
`C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261020O1Retry02`.
Both original O1 and Retry01 evidence must remain unchanged. Build, actual
persistence regressions, target/permission guards and a fresh packet inspection
must pass before execution. No migration, permission extension, real provider,
payment, cleanup or additional business fixture is authorized by this retry.

### Retry02 Actual Outcome

Build06 passed with zero warnings/errors. Offline checks passed: composition
86, boundary 48, fixture 470, outstation service 71 and dated policy boundaries
52 (727 total), plus 245 PowerShell runner/profile checks. These checks do not
establish business SQL acceptance.

Inspect and Execute matched packet
`7C1E2CCAECA5D0A665D64401F95EB61B17F241DFF78780DDCB4319349D960A97`.
The nine approved synthetic source rows committed successfully; the synthetic
HR employee's DraftRevision changed from 5 to 6 with SQL-generated RowVersion
`0000000000338B54`. No other employee fields were authorized to change.

Both synthetic HR/Admin accounts completed actual login and local MFA.
Permission reads returned 200, while unauthenticated/pre-MFA checks returned
401. Eighteen HTTP requests were made. The first two concurrent deployment
creation requests both returned 503; execution stopped before any successful
business operation. SELECT-only checks found zero deployments, claims,
deployment approval records and O1 receipts. No private business evidence file
was created. Host PID 20768 exited; no database/instance cleanup occurred.
The two synthetic sessions were created, but logout/revocation was not reached.

Retained evidence is under `Huma20261020O1Retry02`: `results/start.json`,
`seed-snapshot.json`, `failure.json`, `post-host-preservation.json`,
`host-final.json` and the host logs. The host logs were empty: the boundary
masked the original failure, so the exact live exception was not captured.
Do not reseed, reuse/reset this root or automatically repeat this execution.
A future approved continuation must reuse and verify the committed sources.

Record counts after this run: 25 migrations, 3 accounts, 136 sessions,
197 refresh tokens, 142 OTP challenges, 142 delivery records, 117 rate buckets
and 2664 audit records. Relative to the start snapshot, the deltas were two
sessions/refresh tokens/challenges/deliveries, 12 rate buckets and 12 audits.
No migration was applied; local MFA did not call a real SMS provider.

### Retry02 Preservation Follow-Up

The runner's final protected SQL check was recorded as Unknown rather than
success. A subsequent SELECT-only comparison matched 157 of 158 hashes; only
`unrelated-rates` differed. Investigation found a verifier omission, not an
authorization change: WebAuthenticationService also consumes shared delivery
budgets `request-ip` and `request-phone` during web MFA.

Using the existing run's private verifier key in memory, the exact localhost
IP key and two approved synthetic-phone keys were derived. All three matching
buckets were created within the run window, and the total of 12 new buckets
remained within the approved limit. Excluding only these exact derived keys
in addition to the original allowed keys produced 158/158 matching protected
hashes. No wildcard/prefix exclusion, database write or reset was used.
The original failure report remains unchanged.

Prior private files, committed seed rows, employee metadata, row-count bounds
and host exit checks passed. These preservation results do not mean the
deployment/claim workflow passed.

### Confirmed Query Defect

An offline reproduction against build06 found that DeploymentRepository applies
filters to a constructed DeploymentRow record. EF SQL Server cannot translate
the overlap predicate, which deployment creation executes before evidence
storage. Find, approval lookup and paged-history query shapes are affected too.
The regression recorded five passing controls and five failures with zero
connection attempts:
`D:\CosmixVerification\part-c-parent-20261013\managed-b766c64a62e34691a30a193ef9d17d3b`.

This independently confirmed defect is consistent with the observed 503 and
zero business writes, but the live exception was not captured. Repair and
bounded diagnostic improvements are being verified separately. No successful
outstation business acceptance is claimed.

### Verified Repair and Approved Continuation

Release build07 passed with zero warnings/errors. DeploymentRepository now
filters/orders/pages joined entities before constructing its response record.
It retains server-side employee/approval/interval filtering; no client-side
evaluation or authorization bypass was introduced.

The frozen build passed 804 targeted checks: composition 86, boundary 82,
fixture 470, outstation 71, policy boundaries 52, deployment query translation
15 and allowance query translation 28. Evidence:
`D:\CosmixVerification\part-c-parent-20261013\managed-a82ea2c0cb524107b69f8db4cc5cef2d`.
Vehicle/benefit query compilation also passed 60 checks covering 73 repository
expressions, with zero opened SQL connections:
`D:\CosmixVerification\part-c-parent-20261013\managed-07f765f26ce44c7abf44c0eaef4c0723`.
The PowerShell profile/verifier passed 256 checks, including exact shared MFA
rate-key derivation and rejection of unrelated keys. None of these compile-only
checks establish successful SQL persistence.

The guarded test boundary now preserves a bounded failure event, validated
server trace, static stage, original downstream status and allowlisted exception
category. It does not log exception messages, stacks, request bodies, credentials
or cookies. Diagnostics cannot disable fail-closed handling.

The user subsequently approved ONE bounded continuation using
`C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261020O1Continue01`.
The same verified target and localhost:7481 host remain mandatory. Reuse only
the exact nine committed sources and HR revision 6: no reseeding or employee
metadata changes. Preserve all prior evidence and sessions. Maximum 100 HTTP
requests and two new synthetic HR/Admin local-MFA sessions; one deployment and
one claim with independent review, retry/concurrency and persistence checks.
No migrations, new grants, payments, real face/SMS or legal activation.

Continuation startup must validate rather than seed. The retained Retry02
`results/seed-snapshot.json` has SHA256
`8CC27CD2B130AD8F1280E81ABEFE2B57DBE0D452C38D2D777B022827F908ACDC`;
its exact source hashes and post-seed employee version are the reuse baseline,
not values silently re-captured from a changed database. Fresh offline guards
and read-only preflight must pass before this one execution; drift or execution
failure stops the run without reset, repair or automatic repetition.

### Continue01 Executed: HTTP Passed, Final Count Query Timed Out

The approved continuation executed once against the same verified target.
Release build08 passed with zero warnings/errors. Its composition checks (86),
boundary checks (82), corrected source-reuse fixture checks (614), and the
PowerShell guards (307) passed before execution. Fixture evidence is retained at
`D:\CosmixVerification\part-c-parent-20261013\managed-3a9e2c8bdab3432399b74f9ca25b8fec`.
The reviewed/executed packet SHA256 was
`04827FD43379985B51EEFFB36308F6D0910BAD701A65EEC7EAC13139962C35E0`.

All **38 named checks passed across 45 actual HTTP requests**, using exactly
two new synthetic HR/Admin local-MFA sessions. This included independent
Admin deployment and claim approval; simultaneous requests over separate
connections; same-key response replay; changed payload, duplicate, stale-version
and self-review rejection; retained policy calculation/fingerprint; evidence
binding; CSRF-protected logout and rejection of retained logged-out cookies.
The synthetic effective-dated policy produced PKR 200 for two explicitly
configured full calendar days. This is not a company default or a payment.

The runner then failed in the final SELECT-only SQL row-bound verification,
before writing `acceptance.json`. A diagnostic reproduced the 20-second timeout
specifically on the audit count using `NOT IN (OPENJSON(original IDs))`.
A bounded primary-key read plus local identity comparison returned 2,696 IDs:
all 2,664 original audit IDs remained present and 32 were new. No SQL writes
or repeated HTTP requests were used for that diagnostic. The exact SQL
optimizer cause has not been established; the slow verification query shape
must not be described as a failed business operation or silently marked passed.

The original `failure.json` remains unchanged. Completed final preservation
checks passed for protected SQL, previous private files, all nine retained
sources and employee metadata (revision 6, unchanged rowversion). Host PID
34620 exited; LocalDB was not stopped or cleaned up. Final totals were
25 migrations, 3 accounts, 138 sessions, 199 refresh tokens, 144 challenges,
144 deliveries, 129 rate buckets and 2,696 audits.

A separate SELECT-only reconciliation subsequently verified the saved business,
approval, authentication and receipt records. It did not restart the host,
reseed, change the employee, repeat requests, increase budgets, overwrite old
evidence, or claim that the original runner exited successfully.

### Continue01 Supplemental Reconciliation: Passed

`Invoke-HrOutstationO1Reconcile.ps1` exited 0. It uses bounded primary-key reads
and exact in-memory identity differences, rejecting duplicate IDs, missing
originals, malformed IDs and populations above 10,000. All original IDs were
retained. The new helper passed 37 offline checks. Strict reconstruction of
32 audit expectations from the captured 45-request sequence passed 44 offline
checks, including impossible commit/replay ordering and wrong actor/role/target.
An independent source review found and verified fixes for array cardinality,
private output-directory ACL checks and original-write eligibility.

The first supplemental SELECT attempt exposed another frozen-verifier defect:
`auth.RefreshTokens` has no `RevokedAt` column. Actual refresh validation uses
the parent `UserSession` revocation. The corrected SELECT checks the two
unrotated tokens' exact revoked session relationships, issuance dates and
replacement/consumption state. No schema or authentication behavior changed;
the failed read created no report or database rows.

The corrected supplemental run verified:

| Persisted item | New count / result |
| --- | --- |
| Deployment / claim / submission | 1 / 1 / 1; exact source, dates, policy and PKR 200 |
| Approval headers / independent Admin decisions | 2 / 2; exact protected references and links |
| Completed idempotency receipts | 4; exact operations, keys and actors |
| Sessions / refresh tokens / challenges / deliveries | 2 each; local MFA consumed, delivery payload cleared, sessions revoked |
| Audit events | 32; exact SQL-correlated expectations matched, including four original writes |
| Rate buckets | 12, within the reviewed bound |
| Private business evidence files | 12, within the reviewed bound and owner-only ACL |
| Nine source tables | Zero new rows; original source preservation passed |
| HR employee | Revision 6 and retained rowversion unchanged |
| Database migrations / accounts | 25 / 3, unchanged |
| Settlement / payments | Claim remains NotScheduled (0); no settlement or payment |

Supplemental private evidence:
`Huma20261020O1Continue01\results\read-only-reconciliation.json`, SHA256
`0036996729C8067252F5092847ABCCB8878C84FF66F303DB2719C19803C65E3E`.
It retains hashes of the original start/failure/preservation/host-final files.
SQL-correlated reconstruction does not recover lost response bodies:
intermediate versions, exact response equality and calculation fingerprints
are supported by the original 38 passed runtime checks, not newly observed
responses. No request was repeated during reconciliation.

Final host state: no listener on 7481; SQL PID 25768 remains running. No cleanup,
migration, reseeding, extra grant or real provider call occurred.

**Outcome:** this one synthetic single-policy/no-home-break outstation lifecycle
has passed HTTP business checks and supplemental persisted-state reconciliation.
The original combined runner remains a recorded failure at its final verifier,
not a fabricated exit-0 run. Benefit/recovery fulfilment, historical vehicle
chains, outstation home-break/cross-policy/resubmission cases and wider HR
acceptance are not established by O1. Real face/OTP/legal acceptance remains
excluded.

### Changed Files in This Continuation

- `src/Cosmix.Infrastructure/Repositories/HR/DeploymentRepository.cs`: translatable server-side repository queries.
- `src/Cosmix.Api/Development/HrOutstationO1Boundary.cs`: bounded redacted failure diagnostics.
- `src/Cosmix.Api/Development/HrOutstationO1Guard.cs`, `HrOutstationO1Fixture.cs` and `src/Cosmix.Api/Program.cs`: exact source-reuse continuation boundary; no startup reseeding.
- O1 profile, runner, manifest and standalone verifiers: exact source hashes, employee version, shared MFA rate-budget keys, composition and translation checks.
- `Invoke-HrOutstationO1Reconcile.ps1`, `O1ReadOnlyReconciliation.Helpers.ps1`, `O1RetainedAudit.Helpers.ps1` and their standalone verifiers: supplemental SELECT-only reconciliation.
- This verification record, business acceptance matrix and SRS module map: executed results and remaining scope.
