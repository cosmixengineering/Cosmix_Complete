# Organization Masters Verification

Date: 7 October 2026. Baseline: `966bfbd9e3462a084bc3e67e6658739651e17c1c`.
Work is local on `codex/organization-masters`; no commit/push or frontend changes are included.

## Scope and Results

| Check group | Actual result | Boundary |
|---|---|---|
| Six Domain master lifecycles | 150 passed, 0 failed | Constructors, immutable codes, updates/status, invalid input, no partial mutation |
| Holiday Domain/EF guards | 37 passed, 0 failed | Immutable revisions, intervals, restrictive mapping, sync/async append-only guards; no SQL connection |
| Organization service checks | 571 passed, 0 failed | Real services and permission evaluator with in-memory repository/auth fixtures |
| API/HTTP/OpenAPI checks | 789 passed, 0 failed | All 41 route/verb cases; actual API pipeline, MVC, CSRF and OpenAPI with substituted auth/persistence |
| SQL workflow checks | 40 passed, 0 failed | Actual services, EF repository, SQL transactions, indexes and rowversions; identity/grant reads substituted |
| SQL schema checks | 8 passed, 0 failed | History/model state, table count, trusted constraints, unique indexes, rowversions, restrictive FKs, no owner account |
| Existing web authentication regressions | 18 passed, 0 failed | Password/SMS/session/refresh policy using offline persistence, no SMS |
| Existing fixed-role regressions | 67 passed, 0 failed | Reserved roles, assignment/delegation/mobile restrictions; offline |
| Existing mobile enrollment regressions | 22 cases / 56 assertions, 0 failures | Approved enrollment gating and unchanged web behavior; offline |

Release solution build passed with **0 warnings and 0 errors**. EF model/snapshot comparison reports
no pending model changes. Build success is reported separately from behavioral verification above.
The repository still contains four application projects and no test projects or seeded accounts.

## Confirmed Site Master Continuation

The PM explicitly selected Organization settings plus Admin Site create/edit APIs, with employee
assignment deferred to onboarding. Six Site operations are now implemented without schema changes.
The prior result table records the original Organization slice; the continuation checks below were
executed against the updated code, not inferred from build success.

| Check group | Actual result | Boundary |
|---|---|---|
| Site feature availability | 5 passed | RED: service/gate/permissions absent; GREEN: implemented contracts |
| Site Domain lifecycle | 36 passed | Text/date/ID validation, immutable code, atomic invalid edits, status |
| Paired fixed-role operation gate | 29 passed | Same-assignment All permission, negative paths, chosen-role audit, retry reauthorization; eligibility fixtures |
| Site HTTP/OpenAPI | 277 passed | Six routes, MVC, schemas, Location, CSRF, authorization, versions/references; substituted auth/persistence |
| Site SQL workflow/predicates | 33 passed | Actual SQL fixed-role and supervisor predicates; real Site services, transactions, FK/rowversion, audit/retries |
| Existing Organization service rerun | 571 passed | Default gate behavior and all prior service scenarios |
| Existing Organization HTTP rerun | 789 passed | Original 41 operations preserved; OpenAPI additionally allows the six approved Site operations |
| Existing Organization SQL / schema rerun | 40 / 8 passed | Same approved database, original workflow checks and exact cleanup |
| Existing web / fixed-role / mobile regressions | 18 / 67 / 22 cases passed | Offline fixtures; mobile suite evaluates 56 assertions |

Site HTTP checks ran the real API pipeline but no real login, SQL or SMS. Site SQL service checks
substituted current identity/session/grant inputs, while the fixed-role query and supervisor eligibility
were also separately exercised with rollback-only employee/account fixtures. No usable login credentials
were created. SQL denies future/ended/revoked/custom/other-account assignments and inactive fixed roles.

SQL RED reproduced two Site defects: future-joining employees and effective suspensions could pass the
new supervisor lookup. The fix reuses existing business-timezone and effective employment-history rules.
The same SQL scenarios then passed alongside active-supervisor acceptance. Static review also checked
invalid-timezone and ambiguous latest-history handling; those two conditions were not separate SQL cases.

Site persistence checks verified create/projection, clearing an optional project on edit, SQL rowversion,
stale edit rejection, immutable original retry response, changed-payload key reuse, duplicate code,
missing/inactive references, status reload, inactive-reference retention and reactivation validation,
attributable history, filtering/pagination, injected audit-save rollback and parallel identical retries.
Site writes created no employee assignments. Exact fixture cleanup preserved row counts across all 124
tables. Rollback-only employee inserts may consume identity values; no identity reseeding was attempted.

The same database remains at eight applied migrations with no pending migrations/model drift. No Site
migration was generated, no earlier migration changed, and no frontend, employee assignment or geofence
workflow was added. Independent read-only review found no additional actionable issue in this narrow
Site/authorization scope, including the supervisor fix. This is not a whole-repository security audit.

Temporary verification setup issues were corrected without production workarounds: a duplicate fixture
role/start timestamp violated the expected unique constraint; a create assertion initially forgot code
uppercasing; the lifecycle harness needed explicit compile items to exclude its generated artifacts; and
the older role fixture needed the new interface member (it throws if that Site-only method is called).
All results above are from successful reruns after these setup fixes.

## Defects Reproduced and Fixed

The initial feature-availability checks failed before implementation. Domain lifecycle/revision checks
also recorded missing-behavior failures before the corresponding methods were implemented.

Additional executed negative checks found and then verified fixes for:

1. Missing successful 200 response schemas on 33 OpenAPI operations. Explicit typed response metadata
   now matches actual JSON. The eight create/revision responses also document their Location header.
2. Omitted `holidays` silently becoming an empty active revision. Missing/null arrays now return 400;
   an explicitly supplied `[]` remains a deliberate supported value. Required dates are documented.
3. Oversized Unicode audit snapshots throwing an uncontrolled ArgumentException. They now return
   `400 organization_snapshot_too_large`. A real SQL check confirms calendar/holiday writes roll back.
4. An idempotency key ending in newline passing a `$`-anchored regex, then being trimmed in persistence.
   Absolute end-of-string validation now rejects it before mutation. The complete service suite was rerun.

An independent read-only review checked tracked/untracked implementation against the plan and baseline.
Its OpenAPI finding was reproduced and fixed. Follow-up found no remaining important code issue in its
narrow scope; its missing-verification-document note is addressed by this file. That review was static,
not a claim of a repository-wide security assessment or production acceptance.

## Development Database

The existing configured LocalDB target was independently queried and verified as `Cosmix_Development`.
It had exactly seven applied migrations ending at
`20261007090713_AddAccountRecoveryAndOwnerInitialization`, with exactly one pending organization migration.
No alias-based replacement/attach operation was used, and `master` was not an application target.

A new `COPY_ONLY, CHECKSUM` backup passed `RESTORE VERIFYONLY ... WITH CHECKSUM` before EF applied only
`20261007122140_AddOrganizationMastersAndHolidayRevisions`. The backup is outside the repository,
named `Cosmix_Development_before_organization_20261007_c2d31700068f4b05b968545026c8b801.bak` in the local
user profile. Connection configuration and the transient LocalDB pipe remain outside Git.

The database now has eight history entries, **123 business tables plus EF history**, no pending
migrations, and no model drift. The migration adds one OrganizationChanges table and holiday revision
metadata; no earlier migration was edited. The incremental SQL script is under `docs/sql` and must not
be reapplied to this already upgraded target. Downgrade is guarded, not a recovery procedure.

Real SQL checks exercised:

- Office-to-city FK/projection and creation of the approved OFFICE/SITE categories.
- Master create/edit/deactivate/reactivate, actual rowversion changes and stale-edit conflicts.
- Case-normalized duplicate-code rejection and SQL search/sort/pagination.
- Atomic audit snapshots, retry of the original response after later edits, and two simultaneous
  identical requests committing only one record.
- Calendar/holiday persistence, inactive successor revision reloaded from SQL, latest-only query,
  preserved predecessor, and rejection of a second successor from an outdated revision.
- Injected audit-save failure after the business save: no business row or retry receipt committed.
- Oversized Unicode snapshot: controlled 400 and rollback of calendar, holidays and receipt.

Synthetic writes were attributed to a random verification actor and cleaned using exact created IDs
and that actor's audit/receipt keys. Cleanup ran in a transaction. Row counts across **all 124 tables**
matched the pre-check counts after each run. No real owner, employee, SMS or demo account was created.
SQL rowversion counters may advance during rolled-back/deleted verification writes.

## What the Tests Do Not Prove

HTTP checks run the actual `Program` with WebApplicationFactory/TestServer, real model binding,
serialization, ProblemDetails, no-store headers, CreatedAtAction routing, antiforgery and OpenAPI.
They cover anonymous 401, synthetic mobile/no-MFA/narrow-scope/revoked-session 403, missing/wrong CSRF,
invalid/missing DTO fields, stale versions, bounded pagination, successful locations and absence of
DELETE routes. They substitute the authentication handler and repositories, so they do **not** prove
real JWT validation, SQL-backed login, end-user browser login or SMS delivery in this run.

SQL checks use the real OrganizationOperation, services, OrganizationRepository and the real serializable
authentication transaction/application lock. Current identity, account eligibility and permission reads
are fixture inputs; the checks do not create or validate a real owner/session or an auth-revocation race.
The 8 schema checks are focused checks, not a new exhaustive audit of all existing schema constraints.

Real selected-provider SMS, actual first-owner setup (deferred by PM), deployed cookies/CORS/proxy policy,
Kestrel request-size behavior, load testing, backup restoration drill and end-user acceptance remain
deployment work. No general upload/scanning, calendar applicability/payroll calculation, employee
onboarding, Admin site assignment, mobile business workflow or frontend integration is included here.

## Reproducing Local Checks

No test projects were added to Git, as requested. Temporary harnesses are local evidence and are not
guaranteed to survive TEMP cleanup or exist on another developer's computer. Review each harness before
using it; do not copy these commands onto production. The SQL `test` mode writes bounded fixtures and
cleans them, while `schema` is read-only. The one-time `apply` mode was used only after target review.

```powershell
dotnet build Cosmix.Backend.sln -c Release --no-restore
dotnet ef migrations has-pending-model-changes --project src/Cosmix.Infrastructure --startup-project src/Cosmix.Api --configuration Release --no-build -- --environment Development

dotnet run --project "$env:TEMP/cosmix-master-lifecycle-20261007-8b7d/MasterLifecycleChecks.csproj" -c Release --no-restore
dotnet run --project "$env:TEMP/cosmix-holiday-revisions-20261007/HolidayRevisionChecks.csproj" -c Release --no-restore
& "$env:TEMP/cosmix-organization-offline-20261007-b93a/Run.ps1"
dotnet run --project "$env:TEMP/cosmix-organization-http-checks/OrganizationHttpChecks.csproj" -c Release --no-restore
dotnet run --project "$env:TEMP/cosmix-organization-checks/SqlChecks.csproj" -c Release --no-restore -- schema
dotnet run --project "$env:TEMP/cosmix-organization-checks/SqlChecks.csproj" -c Release --no-restore -- test

dotnet run --project "$env:TEMP/cosmix-web-core-checks/WebCoreChecks.csproj" -c Release --no-restore
dotnet run --project "$env:TEMP/cosmix-system-role-checks-20261007/Checks.csproj" -c Release --no-restore
dotnet run --project "$env:TEMP/cosmix-mobile-enrollment-guard-20261007/EnrollmentGuard.csproj" -c Release --no-restore
```

The temporary SQL cleanup uses a closed, hardcoded table-name switch plus parameterized record IDs;
its interpolation analyzer warning is confined to that harness. The production solution build is clean.
For a new developer's routine checks, use the explicit manual acceptance order in
[Organization Masters](Organization-Masters.md); retained automated repo tests require PM approval.

Site continuation harnesses (local TEMP only):

```powershell
dotnet run --project "$env:TEMP/cosmix-site-checks/Checks.csproj" -c Release
dotnet run --project "$env:TEMP/cosmix-site-checks/SiteSqlChecks.csproj" -c Release
dotnet run --project "$env:TEMP/cosmix-site-http-checks/SiteHttpChecks.csproj" -c Release --no-restore
dotnet run --project "$env:TEMP/cosmix-site-lifecycle-20261007-f14a/SiteLifecycleChecks.csproj" -c Release --artifacts-path "$env:TEMP/cosmix-site-lifecycle-20261007-f14a/artifacts"
dotnet run --project "$env:TEMP/cosmix-operation-gate-20261007-e28c/OperationGateChecks.csproj" -c Release --artifacts-path "$env:TEMP/cosmix-operation-gate-20261007-e28c/artifacts" --property:RestoreConfigFile="$env:TEMP/cosmix-operation-gate-20261007-e28c/NuGet.Config" --property:NuGetAudit=false
```

The Site SQL harness is explicitly guarded to the approved integrated-security LocalDB database. It
refuses synthetic fixed-role checks when real system roles already exist; do not weaken that guard to
rerun it on a populated deployment. It never creates/applies migrations, invokes SMS or seeds an owner.
