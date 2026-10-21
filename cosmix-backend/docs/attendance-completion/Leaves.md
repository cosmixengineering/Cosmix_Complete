# Direct Admin leave workflow

This local implementation follows the 12 October 2026 confirmation: an employee submits directly to a fixed Admin/SuperAdmin with an explicit leave-review permission. HR publishes leave settings directly with its separately granted configure permission. No intermediate HR approval, invented route chain, default permission, leave entitlement seed, salary change or payroll posting is created.

## API and authority

All routes require a current authenticated session. `attendance.leaves.self` resolves employee identity on the server and supports mobile/self web access. Review uses current web MFA and an All-scope `attendance.leaves.review` grant on an active fixed Admin/SuperAdmin assignment itself. Both requester and beneficiary are denied every decision. The same authority check runs before replaying protected retry receipts. `attendance.leaves.configure` uses web MFA and All scope; it does not require a fixed Admin role.

| Route | Use |
|---|---|
| `POST /api/v1/leaves/mine` | Submit exact days/intervals and optional private evidence; optional `previousRequestId` resubmits one returned application as a new immutable revision |
| `GET /api/v1/leaves/mine` | Own applications, bounded inclusive From/To range and pagination |
| `GET /api/v1/leaves/mine/types` | Explicitly configured leave types |
| `GET /api/v1/leaves/mine/policies` | Own dated applicable settings catalogue; explicit LeaveTypeId and inclusive From/To, no automatic selection |
| `GET /api/v1/leaves/mine/balance` | Own exact current ledger balance within the fiscal year selected by AsOf, under explicit type/policy selection |
| `POST /api/v1/leaves/mine/preview` | Read-only check of proposed exact intervals and treatment; no application/reservation or uploaded evidence |
| `GET /api/v1/leaves/mine/{id}` | Own details, earlier revision, decisions and cancellation history |
| `GET /api/v1/leaves/mine/{id}/evidence/{kind}` | Own scanned private evidence download |
| `POST /api/v1/leaves/mine/{id}/cancellations` | Request separate cancellation of approved leave |
| `GET /api/v1/leaves/admin/queue` | Admin queue; PendingOnly includes pending cancellations |
| `GET /api/v1/leaves/admin/{id}` | Authorized Admin detail/history |
| `GET /api/v1/leaves/admin/{id}/policies` | Review-authorized applicable settings selection; no HR configure permission needed |
| `GET /api/v1/leaves/admin/{id}/evidence/{kind}` | Authorized private evidence download |
| `POST /api/v1/leaves/admin/{id}/decisions` | Approve, Return or Reject a pending request |
| `POST /api/v1/leaves/admin/{id}/cancellations/{cancellationId}/decisions` | Controlled cancellation decision |
| `GET /api/v1/hr/leave-settings/types` | HR type catalogue |
| `POST /api/v1/hr/leave-settings/types` | Create a type with immutable meaning and SRS evidence flags |
| `GET /api/v1/hr/leave-settings/policies` | Immutable explicit settings history |
| `POST /api/v1/hr/leave-settings/policies` | Direct HR publication with a unique code and exclusive effective ending |

Every write needs `Idempotency-Key`; decision/cancellation inputs also carry the current base64 `ExpectedApprovalVersion`. Settings publication uses the strict 32 KiB JSON boundary, rejects duplicate/unknown/missing required settings and numeric strings. Lists accept at most 366 dates and 100 rows per page. Attachment input is base64 PNG/JPEG/PDF, maximum four attachments, 10 MiB each and 20 MiB combined. Medical, exam admit-card/date-sheet and injury-rest evidence requirements are checked. A real scanner must report Clean with a scanner version before files are made available; rejected/unavailable scanning fails closed. File purpose, size and hash are rechecked on download. Files and protected snapshots never become public URLs.

The Admin detail flow reads `admin/{id}/policies` to obtain published version IDs and settings under the review permission alone. This checks dated employee/category and pinned calendar applicability, retains explicit reviewer selection and never grants publication rights. It bounds the catalogue at 500 versions and the dated candidates at 50. It does not mutate requests, policies or balances; approval still revalidates service eligibility, evidence, quota, holiday treatment and the pinned schedule.

## Duration, policy and balances

Each day explicitly supplies its business date and whole-minute UTC start/end. The server verifies the Asia/Karachi start date, positive interval, absence of duplicate days/overlaps, historical scheduled shift window and selected shift version. It computes minutes from the interval, never by dividing an aggregate duration. A missing/changed schedule blocks submission or approval with a concrete conflict. The current site-only roster resolver supplies these schedules; office employees without a resolvable assigned-site schedule cannot receive fabricated office duty hours.

Intake is possible without published leave settings and stores a nullable policy FK with PendingReview treatment. Approval of a concrete paid/unpaid treatment requires an explicit policy version. Incomplete configuration leaves the request pending; it never changes salary records or creates zero base pay. Casual leave must be requested and approved before its first interval. Half-day emergency approval requires evidence; no automatic KPI percentage is inferred.

The published settings explicitly confirm the SRS July–June fiscal year, annual 18 days, six sick days, twelve casual days and four-month service boundary. HR must provide eligible staff-category IDs, standard-day minutes, a specific immutable government-notified holiday calendar revision, holiday count/exclude treatment and paid kinds. There are no automatically eligible categories or default day durations. Requests crossing a fiscal-year boundary must be separated; excluded holidays must be removed from the submitted days. Approval verifies one unambiguous effective employment category throughout the requested period, applicable policy dates, service eligibility and fiscal-year ledger usage. Sick/casual limits aggregate all types of the same kind, preventing multiple type codes from multiplying their allowance.

Paid regular leave appends one utilization record with exact requested minutes. Quota comparison uses exact rational day fractions from each ledger entry's own immutable policy day duration. Twelve days under a 480-minute policy remain twelve days after a 540-minute policy is selected; reversing one old day restores exactly one old day. Missing unit provenance and malformed reversals fail closed. Configured paid injury rest retains DoesNotReduceRegularLeave in leave responses and produces Paid attendance intervals only after explicit paid-kind evidence has been verified. It never consumes regular balance. Approved cancellation appends an equal positive reversal referencing the original usage; it preserves the request, original decision and all files. Returned/rejected cancellation does not remove the approved leave; a new cancellation request may follow. Locked periods block approval and cancellation rather than rewriting frozen attendance.

## Self balance and preapproval context

The three self reads derive employee identity from the current session and require the explicit self permission. `mine/policies` accepts an explicit leave type and a bounded date range, returning eligible published versions with no automatic winner. `mine/balance` requires type/policy selection; omitting the policy returns `unavailable` and null balances. `AsOf` defaults to the current Asia/Karachi date and selects employment/policy applicability and the July–June fiscal year. Usage is the **currently recorded ledger for that entire fiscal year**, including future approved leave and currently recorded cancellations. It is not a reconstructed historical snapshot at the selected date. Pending applications do not reserve quota.

Each balance exposes exact reduced `numerator`/`denominator` day quantities. Historical policy day lengths and cancellation reversals use the same fraction calculation as approval. A verified empty ledger is zero usage; missing or conflicting unit provenance produces null balances with reasons. Paid-kind configuration and the four-month service test are separate flags, so a displayed balance does not promise paid leave. A non-consuming type has no type quota. Negative remaining balance is reported exactly if already-recorded usage exceeds the selected policy cap, rather than concealing that state.

`mine/preview` is a read-only POST, requiring no idempotency key. It accepts only type, explicit selected policy/treatment and exact proposed days; no employee identity, permission assertion, attachment bytes or totals are accepted. It reuses intake allocation and approval policy/quota, holiday, service, current schedule, overlap, advance-notice and locked-period checks. `available` describes checked current context only; `conditional` means evidence must still be provided and scanned at submission. `unavailable`/`conflict` carry concrete blockers and never turn missing data into zero leave. `approvalGuaranteed` is always false. The read creates no application, reservation, ledger entry, approval, file or receipt; ordinary access auditing remains. Submission and the independent Admin decision each revalidate, so a later lock, transfer, policy or usage change can block either step. This preview describes a new application; returned revisions retain the existing explicit resubmission workflow.

## Attendance and persistence

The internal `ILeaveAttendanceContextReader` returns Complete/Pending/Conflict, exact approved day effects and a digest. Complete with no effects means the bounded query verified no active leave. The reader validates the protected original submission, Admin decision, explicit policy content, pinned holiday dates and cancellation snapshots. It checks current historical applicability against the approval snapshot: a later category/join-date change invalidates stale approval applicability, even if the shift is unchanged. Balance validation uses the protected preapproval usage, so the request's own later ledger debit is not counted twice. Attendance also checks each effect's pinned ShiftVersionId against its resolved schedule. Pending review/cancellation, unavailable provenance and conflicting allocations never become guessed zero leave.

The existing authorization transaction encloses business writes, protected audit and retry receipts. Collection queries batch cancellation/decision/successor reads, enforce self predicates before pagination and cap retained history. Submitted request metadata cannot change; a financial treatment transition needs the matching tracked pending-to-approved request and one new fixed Admin decision. Exact-day rows and ledger entries are append-only. A returned revision has a unique predecessor link. No database migration was applied as part of this work.

## Verification and remaining boundaries

Executed on 12 October 2026:

| Check | Result | Boundary |
|---|---|---|
| `dotnet build Cosmix.Backend.sln -c Release --no-restore -m:1 /nodeReuse:false` | Passed, zero warnings/errors | Compilation; later integration build is recorded by the root task |
| `VerifyLeaves.cs` | 65 passed | Actual services with synthetic authentication/repository/private-file adapters; identity isolation, Admin/MFA/replay checks, revised requests, decisions, reviewer policy discovery, quota/day-unit changes, eligibility, holiday exclusion, scan failures, private evidence download/tamper, approval/cancellation locks, reversals and revalidation after preview |
| `VerifyLeavePersistence.cs` | 15 passed | Offline EF metadata and persistence guards, immutable submitted fields/children/cancellations and matching approval evidence |
| `VerifyLeaveQueries.cs` | 36 passed | Actual production query SQL translation with guards proving zero connection opens and zero command executions |
| `VerifyLeaveSelfReads.cs` | 43 passed | Dedicated real-service checks for self authority, exact historical fractions/reversals, unavailable provenance, conditional evidence and no business/file mutation |

These are standalone file-based checks, not test projects. Package restoration used only the existing local package cache; temporary outputs stayed under ignored `artifacts/attendance-policy-leave/temp`. No database migration, account change, Git mutation or live provider call was performed.

These checks do not establish real authenticated SQL transaction/rollback, concurrency, scanner deployment, encrypted-storage outage recovery or payroll disbursement acceptance. The additional six-day/100%-KPI benefit, KPI event rules, encashment and long-leave recovery settlement remain management-controlled separate work. No guessed benefit, KPI charge, carry-forward, payroll amount or compensation mutation is introduced.

## 19 October: Configured Benefits and Actual Recovery

The earlier pending implementation statement above is superseded for the HR
benefit/recovery workflows described in
[the current handoff](../hr-srs-remediation/HR-Benefits-Recovery-Vehicles-20261019.md).
`leave-settings-v4` adds optional explicit `Benefits` and `RecoveryFulfilment`
sections. Historical v1-v3 meanings and absent-credit evidence hashes remain
unchanged. SRS example numbers are not new policy defaults.

An independently permitted Admin can award the exact configured benefit after
finalized KPI/source/dispute checks. Immutable earned credits feed ordinary
leave balances and, only when eligible, the existing encashment reservation
flow. Recorded usage stays distinct from entitlement. Unused reversal is guarded;
already consumed awards do not acquire an invented refund/recovery treatment.

Recovery now has verified actual-work sources, exact interval claims, independent
review and partial/full obligation progress. A promise alone does not fulfil it,
and retained work cannot also produce OT or make-up credit. Approval uses the
existing leave-review authority and transactional notifications, with fresh
authority checks after evidence IO. No salary settlement is performed.

[Client routes and acceptance cases](../hr-srs-remediation/HR-Benefits-Recovery-Client-Contract.md)
describe configuration, scopes, versions and retry behavior. These additions
have standalone verification, not live SQL/client acceptance. No migration was
applied and no company policy or access grant was automatically seeded.
