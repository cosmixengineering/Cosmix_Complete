# Employee Hiring Verification - 7 October 2026

## Post-hire Status Workflow

The PM subsequently accepted HR request followed by fixed Admin approval. The earlier authority question
below is historical, not an outstanding blocker for this slice. Backend APIs now cover suspension, resignation
and termination requests, approve/return/reject, requester cancellation, effective status, private details,
paged queue and immutable history. No client UI or external provider was added.

Confirmed database upgrade: `20261007164427_LinkEmployeeStatusApprovals`, after
`20261007151945_CompleteEmployeeHiringApproval`, on the same original LocalDB `Cosmix_Development`.
COPY_ONLY/CHECKSUM backup plus RESTORE VERIFYONLY preceded application. Eleven history entries, 125 tables
including EF history, zero accounts, no pending model/migration changes, and trusted restrictive FKs were verified.
The change is one nullable approval reference/index/FK on existing status history, not another table.
Backup and SQL range are recorded in Migration-Review.md and docs/sql/README.md.

Fresh checks during this continuation include Release build with zero warnings/errors; 111 form checks,
55 hiring service checks, 22 approval workflow checks, 45 materialization checks and 18 web authentication
checks. Existing SQL approval regression passed seven rollback-only scenarios and the real serializable
transaction/exclusive-applock forced-rollback probe. All 125 table counts returned to their empty baseline.
Sequence/rowversion allocations advance even when test data rolls back; they are not reset or reseeded.
Actual hosted OpenAPI returned 200 with 35 employee operations across 30 paths, web security metadata and
anonymous 401/403 responses. That does not prove authenticated client acceptance.

New temporary service checks reproduced and fixed missing direct-service pagination/reason-length validation
(positional record parameter attributes are not property validation). Same-effective-instant changes are rejected
to avoid ambiguous authentication history. Tests cover web/current-state/All-scope/fixed-role authorization,
separation of requester/approver/target, exact retry behavior, stale versions, immediate and scheduled changes,
private snapshots, rejected/returned/cancelled history, evidence validation and account/session/OTP revocation.
Boundary repositories/storage/grants are explicit substitutes, not SQL or provider acceptance.

Parent reruns passed 76 domain/EF-metadata checks and 115 lifecycle service checks. Regression tests include
the worker isolating previous-status mismatches as controlled conflicts and progressing past 25 invalid subjects
to a valid 26th employee, then resetting its cursor without duplicate effects. Evidence is retained outside the
repository in `%TEMP%/cosmix-lifecycle-domain-20261007-b4c28c` and `%TEMP%/Cosmix.EmployeeLifecycle-20261007`.

Review identified a protected-role timing gap: assigning a fixed role after future offboarding was approved.
The access service now checks cached/current/future employment eligibility in its existing grant transaction;
custom-role behavior is unchanged. Parent rerun passed 37 targeted service/repository/SQL-translation checks
with connections suppressed. The temporary harness is `%TEMP%/cosmix-system-role-lifecycle-20261007-f67a`.
The final Release build and 115 lifecycle checks passed again after that patch; hosted OpenAPI/denial checks
also passed again. No business policy, owner account or automatic permission upgrade was introduced.

SQL lifecycle checks use actual EF repositories/services and authentication effective-status reads, with
test authorization/grants, savepoint transaction adaptation and memory storage. They establish real query,
rowversion, FK/unique/append-only and rollback behavior, not real SMS login or production successful-commit races.
Seven scenarios (87 assertions) passed, including employee-number cursor ordering and continuation. During
the rerun after per-employee transaction isolation, the test adapter incorrectly forwarded generic
`InTransactionAsync<bool>` to the real transaction owner inside its outer rollback transaction. The expected
nested-transaction rejection caused one test failure. Explicitly dispatching all generic transaction calls to
the savepoint adapter resolved this harness defect; no product safety guard or assertion was weakened.
The guarded rerun and the parent's independent final rerun passed all seven scenarios, restored all 125 table
counts and left zero accounts. Evidence and the preserved failing
stack are in `%TEMP%/Cosmix.LifecycleSql-20261007`. Active sessions/OTP revocation are covered by service doubles,
not populated real-login SQL fixtures; production successful commits and simultaneous API replicas remain unverified.
Actual owner setup, SMS delivery, scanner acceptance and attendance punch endpoints remain outside this evidence.
Reactivation/rehire, retirement and payroll/asset settlement are not implemented by this continuation.
All temporary harnesses are outside Git. No repository test project, real account, frontend, commit or push was added.

## Hiring Validation and Audit Follow-Up

This continuation fixes three bounded gaps in the existing hiring flow; it does not add a new lifecycle workflow.

- Profile/financial date validation now rejects the default `0001-01-01` sentinel while preserving incomplete
  drafts with omitted/null dates. Eleven new negative checks failed before the fix; the resulting form suite
  has 111 passing checks, including the null-date regression.
- Submission validation reuses the existing asset-return receiver existence/business-date check. An invalid
  receiver cannot lock the draft into PendingApproval. Approval retains its independent current-state recheck.
- Historical financial/medical read audit targets now contain the immutable submission ID and version, while
  response permissions, protected values and employee scope are unchanged.

The service regression suite reproduced six failures before the latter two fixes and then passed all 55 checks
(the original 47 plus eight regression scenarios). The receiver checks use the real repository with EF in-memory;
the other boundaries retain the documented authorization/private-storage test substitutes. RED/GREEN evidence
is in `%TEMP%/Cosmix.EmployeeServices-20261007`; no repository test project was added.
The parent independently reran all 55 checks against the current project. Its first `--no-restore` attempt
failed to compile because the two TEMP harness projects shared an assets cache; restoring `Checks.csproj`
resolved that harness configuration issue. No product-code change or test assertion relaxation was needed.

Fresh verification also passed the Release build (zero warnings/errors), 22 decision workflow checks, 45
materialization checks, seven rollback-only SQL approval scenarios, the production transaction/applock rollback
probe, and the running API's 27 employee operations/23 paths with anonymous denial. SQL table counts matched
the empty baseline after rollback; ten applied migrations and 125 tables are unchanged. No migration, real
employee/account, SMS delivery, frontend change, commit or push was performed in this follow-up.

Post-hire suspension/termination/resignation authority is still awaiting PM confirmation: HR request followed
by Admin approval, or direct Admin-only changes. SRS MD-001 is not resolved by hiring approval alone. No status
mutation route, automatic reactivation, offboarding worker or new authority was invented while that decision
remains open. Actual SMS/scanner acceptance and the other previously documented delivery limits still apply.

## Hiring Approval Continuation

The earlier intake-only results below are retained as historical evidence. This continuation implements
Admin approve/return/reject, approved related records, dormant-account creation, explicit access setup,
and date-based site assignment/history. It does not implement the complete post-hire employee lifecycle.

Fresh parent verification during this continuation:

| Check | Observed result | Boundary |
|---|---|---|
| Release solution build | 0 warnings/errors | Not business acceptance |
| Existing form checks | 99 passed | No database/provider |
| Existing intake service regression | 47 passed | Test authorization/storage/scanner sources |
| New decision contract checks | 8 passed | Compiled contract/controller metadata only |
| New decision workflow checks | 22 passed | Actual services/domain with offline persistence/auth substitutes |
| Site assignment service checks | 54 passed | Actual service/domain; fake persistence/authentication |
| Materialization/asset/date checks | 45 passed, 0 failed | Optional office mapping, protected/unverified bank records, real receiver references, expired/current/future site boundaries; EF in-memory, not SQL for these predicates |
| SQL approval scenarios | 7 passed | Actual SQL/EF/protected storage; explicit identity/grant/scanner substitutes |
| Production transaction rollback probe | Passed | Real AccessManagementRepository transaction and exclusive applock; forced failure before commit |
| Running API smoke | 27 operations, 23 paths; OpenAPI 200 | Actual Program/middleware, anonymous denial and documented web security |
| Existing web authentication regression | 18 passed | In-memory, no live SMS |
| EF model/snapshot | No pending model changes | Tenth migration applied to original development DB |
| Whitespace check | Passed | Existing CRLF normalization warning only |

The SQL scenarios exercise approval/materialization/dormant identity/exact replay; Site Staff without an office;
return/correction/resubmission/history; terminal rejection; self-approval/HR-role/stale-version denial; and failures
after the decision save and after materialization/account save. Two injected storage failures roll back all affected
decision, employee, account and related rows. The production transaction probe enters the real serializable gate
with a clean context, verifies its exclusive authentication applock, completes the synthetic approval inside it,
then unconditionally throws before CommitAsync. It verifies rollback, tracker cleanup and transaction disposal.
This proves the failure path, not real authenticated HTTP login or independent successful-commit races.

All 125 database table counts matched their baseline afterward, all 124 business tables stayed empty, account
count stayed zero, and migration history stayed at ten. No actual owner, employee, phone enrollment or SMS was
created. Sequence and rowversion counters can advance across rollback; no database reset or reseed occurred.

Parent rerun harness: `%TEMP%/Cosmix.EmployeeDecisions-20261007-sidecar/README.md` contains guarded commands,
assembly hashes and scope boundaries. Other new temporary checks are under
`%TEMP%/Cosmix.SiteAssignments-20261007`. No test project was added to the repository.

The only schema change is nullable EmployeeAssignments.BranchId. It follows a verified COPY_ONLY/CHECKSUM
backup recorded in Migration-Review.md; earlier applied migrations were not changed. Initial site is currently
optional while the PM's approval-time requirement is pending. Same-start-day and already-scheduled site replacements
are deliberately rejected, not silently rewritten. Real SMS, scanner acceptance, owner setup, client UAT,
expiry notifications, shift/face/attendance and post-hire status/offboarding remain outside these checks.

Independent scoped review identified that a past joining date could select a site that had already ended by
the approval date. The materializer now checks site eligibility at joining and at the current business date
when joining is in the past, while preserving valid future hires. The failure was reproduced and the corrected
current/expired/future/inclusive-end cases passed. The form now captures a real asset-return receiver rather than
rejecting every return or fabricating the approving Admin as receiver. Tests are under
`%TEMP%/cosmix-approval-materializer-checks-20261007/ApprovalChecks.csproj`.
The old equal-date form fixture initially failed because it omitted the now-required return receiver/condition.
It was updated with valid synthetic return details, preserving the equal-date assertion; all 99 form checks then
passed. Separate negative tests still reject a return without its actual receiver. No validation was weakened.

Scope: backend employee hiring draft/profile, separate confidential proposals, private documents,
submission validation, immutable HR submission and fixed Admin review queue. No frontend or Git push.

## Fresh verification results

| Check | Observed result | Boundary |
|---|---|---|
| Release solution build | 0 warnings, 0 errors | Compilation is not workflow acceptance |
| DTO/form checks | 99 passed, 0 failed | Incomplete drafts, formats, dates, decimal bounds, manifest and unknown-field validation |
| Offline service checks | 47 passed, 0 failed | Actual services/domain/controllers; authorization, storage and scanner test doubles; some EF in-memory checks |
| Private file/scanner adapter checks | 39 passed, 0 failed | Actual AES-GCM/path/ACL/rotation behavior; synthetic loopback TCP scanner replies, not malware-detection acceptance |
| Real SQL hiring workflow | 72 passed | Actual EF/SQL/services/private encrypted storage; explicit authentication/grant/scanner substitutes |
| Running API smoke | 20 operations on 16 paths; OpenAPI 200 | Production Program/middleware, documented web security and anonymous denial; not authenticated HTTP UAT |
| Existing web-core regression | 18 passed | In-memory authentication/session behavior; no real SMS |
| Existing Organization authorization regression | 29 passed | Existing All-scope/current fixed-role gate and replay behavior |
| Database schema checks | 6 passed | Ninth migration, 125 tables including history, 8 restrictive new FKs, trusted checks, SQL rowversion, no cascades/owner |
| EF model/snapshot check | No pending model changes | Applied migration history has no pending migration |
| Git whitespace check | Passed | Existing line-ending normalization warning only |

The initial missing hiring-draft persistence contract was reproduced before adding the row/mapping.
The separate form and storage workers used temporary RED checks. Not every new workflow test was written
before its implementation; do not describe this entire feature as a comprehensive test-first suite.
No repository test project was added. Temporary harnesses live outside Git.

## High-signal verified behavior

- New employees stay Draft with a server-generated number and no account. Required-field validation is separate from draft saving.
- Active Office branch selection is supported; Site Staff cannot select an office. Invalid masters/cities/managers are rejected.
- Normalized identity and official-email duplicates fail both service checks and real SQL unique indexes.
- Financial/medical reads and medical file metadata are separately permissioned; ordinary profile/list/submission responses exclude them.
- All-scope, current web MFA eligibility, same-assignment fixed Admin review and authorization on replay are enforced by the service gate.
- SQL parent rowversion changes after child writes. Stale versions fail, including sibling financial/profile edits.
- Failed scanner results cannot be verified, reviewed or downloaded. Clean files still need human verification for ordinary download.
- Explicit clean-file review allows the verifier to inspect a file before approving it; rescans invalidate earlier verification.
- Exact upload/rescan retries return their original result before scanner/storage I/O and still recheck permissions and request digest.
- Current HR form values, separate sensitive sections and selected document metadata are retained in an immutable encrypted submission.
- Submission becomes PendingApproval, never Active, and profile/financial/document edits are rejected afterward.
- Request/audit/receipt storage contains protected references instead of plaintext sensitive audit bodies.
- File review/download audit records identify the actual document and rowversion.
- SQL review queue excludes the owner-bootstrap fixture and selects the employee's current HR submission.

Independent review reproduced three issues: missing document permission on submission metadata, scanner I/O before
completed receipt replay, and missing document-specific read audit evidence. All three were fixed and their scenarios
were rerun successfully. This is a scoped correctness/access review, not a full security audit or penetration test.

## Original database and rollback evidence

Verified target: existing LocalDB `Cosmix_Development`, not `master`, not a new or replacement database.
The ninth migration followed a COPY_ONLY/CHECKSUM backup and successful RESTORE VERIFYONLY:

`C:\Users\Huma.Khan\Cosmix_Development_before_employee_hiring_20261007_8222ed1acf9d44178f693ae64374cc1d.bak`

The SQL workflow harness used one outer Serializable transaction, real EF contexts and savepoints for expected
constraint failures. It always rolled back. The parent reran the final harness independently after the worker run:
all 125 table counts matched baseline, all 124 business tables remained empty, migration history stayed at 9,
and no owner/account/phone binding/SMS was created. Synthetic encrypted objects remain in isolated private TEMP
fixtures, not in application storage or Git. Sequence/rowversion values advance normally across rollback; no reseed occurred.

The transaction/authentication adapter was a test substitute to enlist all calls in that outer rollback-only transaction.
This does not prove independent transaction commit/failure behavior or real session/role database eligibility.
The stale-write scenario used interleaved contexts, not two concurrent SQL sessions. Real scanner verdicts were not used.

## Reproduction locations

- `%TEMP%/Cosmix.EmployeeForms-20261007/EmployeeForms.Checks.csproj`
- `%TEMP%/Cosmix.EmployeeServices-20261007/Checks.csproj`
- `%TEMP%/cosmix-private-files-20261007/verify/Verify.csproj`
- `%TEMP%/cosmix-employee-hiring-sql-checks/Checks.csproj` with `run --parent-ready`; see its RESULTS.md for target guards and rollback conditions
- `%TEMP%/cosmix-employee-checks-20261007/HttpSmoke.ps1` starts and stops only its own local API process
- `%TEMP%/cosmix-employee-checks-20261007/Database.csproj` with `schema` for read-only post-upgrade verification

Run `dotnet build Cosmix.Backend.sln -c Release` and
`dotnet ef migrations has-pending-model-changes --project src/Cosmix.Infrastructure --startup-project src/Cosmix.Api --configuration Release --no-build`
from the repository. Temporary test projects are not a maintained CI suite.

## Not claimed complete

Real owner setup remains deferred; real SMS and live ClamAV deployment/detection acceptance are pending.
The actual scanner is disabled, so real uploads remain unavailable until configured/scanned and manually verified.
Production requires private service-identity ACLs, protected key backups/rotation, transport/network controls,
upload capacity/rate limits and an approved orphan/retention process. Scanner results cannot guarantee a file is harmless.
At the time of the intake-only checks above, Admin decisions and finalization were the next slice.
Their later implementation and new verification are recorded in the Hiring Approval Continuation above.
Post-hire lifecycle APIs still remain.
Face, attendance, expiry notifications, payroll and client UI are not delivered by this work.
