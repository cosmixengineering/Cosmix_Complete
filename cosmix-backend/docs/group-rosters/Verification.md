# Site scheduling verification — updated 9 October 2026

## Approved isolated SQL and authenticated acceptance

The user explicitly approved a fresh isolated `Cosmix_Testing` database, synthetic
accounts/fixtures and private keys/files on 9 October. All **13 migrations**,
including `20261009064639_AddDirectSiteSchedulingAndTemporaryAmendments`, were
applied only to that newly created local testing database. A checksummed backup was
verified before acceptance fixtures. No existing development or production database
was modified. Credentials and detailed evidence remain outside the repository under
`%USERPROFILE%\AppData\Local\Cosmix\AccessAcceptance\20261009`.

| Actual isolated check | Executed result | Evidence boundary |
|---|---|---|
| Real web access provisioning | 65 checks passed | Password challenge, browser-bound local OTP, stored MFA sessions, authenticated CSRF, explicit roles and scoped grants; see [access verification](../access-provisioning/Verification.md) |
| Scheduling and geofence HTTP run | 62 checks passed | Two simultaneous identical assignment requests both returned the same 201 result; changed payload/stale tokens, CSRF, exact staff preview, temporary create/supersede/cancel/fresh root, publication, normal replace/end, self identity, scope denial and fixed-Admin geofence behavior |
| Actual SQL and encrypted private evidence | 83 checks passed | Persisted immutable identities, exact manifests, amendment links and receipts; 12 original scheduling audits and receipt results decrypted through the real private store and matched to HTTP outcomes |
| Main replay after real host restart | 13 operations / 26 additional checks passed; cumulative 88 HTTP checks | Original request/key pairs returned frozen scheduling/geofence results after fresh real login |
| Supplemental Site B grant | 9 checks passed | Separate explicit site grant; original Site A grant retained for revocation coverage |
| Transfer and pinned-holiday HTTP run | 42 checks passed | Actual HR transfer A→B, overnight assignment/publication rejection, exact-midnight adjacency and independently pinned original/successor calendar revisions |
| Supplemental boundary replay | 8 writes / 16 additional checks passed; cumulative 58 boundary checks | Fresh real login against the same running host; original transfer, calendar and scheduling results remain frozen |
| Revoked Site A scope replay | 9 denied replays passed; cumulative 97 main HTTP checks | SuperAdmin revoked only the original Site A grant; prior Admin session returned 401, and fresh real MFA login received 403 for all nine original site/publication replays |
| Actual SQL constraint audit | No violations; 0 disabled/untrusted foreign keys; 13 applied migrations | `DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS` against only `Cosmix_Testing` |
| Competing distinct-key shift updates | 9 checks passed | Two requests used the same expected rowversion and different keys; exactly one HTTP 200 and one 409, then GET confirmed only the winner and unchanged schedule history |
| Final Release solution build | 0 warnings, 0 errors | Completed after acceptance; owned HTTPS host stopped afterward |

The 83-check persistence run occurred **before** supplemental boundary rows were
added. Its exact-cardinality assertions describe that checkpoint; do not rerun it
against the later extended fixture as though the row counts were unchanged.
Counts are assertions, not distinct end-to-end workflows.

The scripts are [Invoke-IsolatedAcceptance.ps1](Invoke-IsolatedAcceptance.ps1),
[VerifyIsolatedPersistence.cs](VerifyIsolatedPersistence.cs),
[New-IsolatedEvidenceSql.ps1](New-IsolatedEvidenceSql.ps1) and
[Invoke-BoundaryAcceptance.ps1](Invoke-BoundaryAcceptance.ps1), plus
[Invoke-CompetingShiftUpdates.ps1](Invoke-CompetingShiftUpdates.ps1). The
[operator notes](Isolated-Acceptance-Runner.md) describe private evidence and the
limited resume checkpoint. The initial HTTP run exposed harness issues: problem
JSON sometimes arrived as bytes, and `WebRequestSession` retained a prior CSRF
header. The helper now decodes bytes and explicitly clears retained headers for
the missing-CSRF check. A bounded resume verified the existing three shifts/one
assignment and continued without recreating fixtures. These were acceptance-harness
corrections; production code was unchanged during this live acceptance continuation.

Real mobile/Bearer clients, real carrier delivery, deployment provider configuration
and large-data/load behavior remain outside this run. Local OTP used the guarded
test adapter. No face verification, punch processing, pay calculation or production
deployment is established by these results.

## Current gap-closure continuation

Local code now supports direct temporary cancellation/supersession, immutable amendment history and refreshed staff manifests. Assigned-site shift publication validates actual impacted intervals; all site writes share transfer validation. The user confirmed fixed Admin/SuperAdmin web role administration, with protected administrator targets controlled only by SuperAdmin. Required reasons and encrypted change evidence accompany permission grants and role assignments. No automatic permission grant or separate owner command was introduced.

| Current check | Executed result | Limit |
|---|---|---|
| Final Release solution build | 0 warnings, 0 errors | Compilation only |
| Site scheduling service/operation/resolver | 170 checks passed | Synthetic collaborators; includes amendments and incoming/outgoing transfers |
| Shift publication | 41 checks passed | Includes original-scope retry, first hire, final assignment, office and maximum-date boundaries |
| Actual publication query translation | 10 checks passed | No SQL connections |
| Scheduling operation | 132 checks passed | Includes current and frozen-original scope reauthorization on retry |
| Approval/model/migration guards | 153 checks passed | Snapshot equality, exact nine Up operations, protected Down, immutable amendment guards; no SQL |
| Scheduling infrastructure | 66 checks passed | Model/registration/query checks; no SQL |
| Geofence model and service regressions | 29 model + 55 service checks passed | No SQL/authenticated HTTP |
| Web authentication boundary regression | 70 checks passed | Component checks, no persisted sessions |
| Site/OpenAPI/anonymous HTTP | 104 checks passed | Ten site routes, access reason schemas, effective reads and retired writes |
| Geofence/OpenAPI/anonymous HTTP | 14 checks passed | Six geofence operations |

[Access verification](../access-provisioning/Verification.md) records the latest authority, reason, private-evidence and serializer checks. The preserved shift/legacy individual service harness also passed 135 checks during this continuation. The older counts below are dated baseline evidence, not additional current runs.

Regression-first failures found and fixed included blanket rejection of valid assigned-site version publication, absent amendment workflows, unchecked incoming/outgoing transfer spill, production-shaped missing pre-hire/post-employment assignments, a maximum-date transfer boundary, initial module permission delegation, missing grant reasons, eager storage construction on read routes, and an OpenAPI required-reason mismatch. Fake repositories were corrected to throw on absent HR assignments like the actual repository; this exposed the joining/ending defect.

### Migration prepared and applied only to the isolated testing database

The user separately approved local migration/snapshot preparation, then the isolated database application recorded above. `20261009064639_AddDirectSiteSchedulingAndTemporaryAmendments` contains exactly nine Up operations: the original six direct-site changes, one amendment table and two indexes. The snapshot matches the model; no DbContext or previously applied migration was edited. Down refuses to discard direct rows, pinned calendars or amendment evidence and never fabricates approval IDs. Independent source review found only the expected three changed entity mappings plus the new amendment entity.

The generated [idempotent SQL review artifact](DirectSiteScheduling.sql) upgrades from `20261008074438_AddEmployeeChangeJournal`; it is not a blank-database script. The fresh testing setup applied the full migration chain. Any other database requires its own authorized target/history review and verified backup. Schema readiness still returns `site_roster_schema_not_ready` on an old database.

### Remaining acceptance

The earlier offline missing-configuration finding was resolved by the explicitly approved fresh isolated setup. Actual schema application, authenticated HTTPS MFA/CSRF, concurrent identical writes and encrypted-disk restart replay now have the evidence above. This does not establish real carrier/mobile/provider or production deployment readiness. See the [isolated setup plan](../access-provisioning/Isolated-Acceptance-Plan.md) and [readiness checklist](../access-provisioning/Readiness-Checklist.md) for scope. Existing real accounts and development/production databases were not changed.

History remains bounded at 2,048 timeline/version rows and 2,000 staff; no large-data SQL/load test is claimed. Unsupported non-site populations and legacy approval-linked schedules remain explicit conflicts where applicable. Same-day/retrospective scheduling, pay calculations, punches and the separate policy feature are outside this batch. Access configuration uses GET review, required reason, expected versions and explicit stale/duplicate 409 reconciliation; it does not claim scheduling-style frozen idempotency replay.

The earlier anonymous contract checks initially hit the sandbox's loopback restriction, then ran with approved local socket access. Those hosts used disabled authentication and an unusable synthetic SQL target and were stopped by their own scripts. Their evidence remains limited to contracts/anonymous denial; the later approved run above supplies actual authenticated SQL evidence. No GitHub actions, commits, branch/worktree changes or automations were performed.

## Historical baseline — 8 October 2026

The following record preserves the earlier state. Its unimplemented-gap statements were superseded by the continuation above.

Executed in the existing `C:\Users\Affan.Ahmed\Desktop\cosmix-backend` checkout. Existing dirty work was preserved. The user's final rule is site-only, direct permission-controlled HR changes, with no separate approval flow. Read [Site Rosters](../Site-Rosters.md) for current routes and client examples; the earlier individual implementation is historical baseline evidence.

## Actual executed results

| Check | Result | What it establishes |
|---|---|---|
| Release solution build, `--no-restore --disable-build-servers -m:1 -tl:off` | Passed, zero warnings/errors | Final Domain/Application/Infrastructure/Api compile |
| `VerifyPopulationTimeline.cs` | 62 passed | Historical candidate identification, employee filtering, dated site/team/category/department matches, transfers, exclusive ends and malformed withdrawals; generic helper does not activate non-site workflows |
| `VerifyHolidayContext.cs` | 47 passed | Exact pinned revision, explicit unavailable states, boundary/corruption checks and no private evidence in public serialization |
| `VerifyApprovalModel.cs` | 131 passed | Actual SQL Server EF model, exactly six expected unapplied schema operations, legacy/direct factories, append-only guards, manifest atomicity guard, generic approval regression and query translation; zero SQL connections |
| `VerifySiteScheduling.cs` | 118 passed | Real site service, resolver, schedule rules and SchedulingOperation with synthetic collaborators; no separate approval creation |
| `VerifyHttp.ps1` | 79 passed | Six registered site routes, required request tokens/headers, public schemas, two site-based reads, three retired individual writes and anonymous 401s |
| `../shifts-rosters/VerifySchedule.cs` | 41 passed | Preserved legacy schedule/resolver primitives and overnight boundaries |
| `../shifts-rosters/VerifyOperations.cs` | 111 passed | Actual authorization, scope, retries, revocation and audit orchestration with in-memory IO |
| `../shifts-rosters/VerifyServices.cs` | 140 passed | Preserved shift/legacy individual service internals; does not assert retired HTTP writes are active |
| `../shifts-rosters/VerifyInfrastructure.cs` | 65 passed | New site DI graph, retained geofence registrations, exact model gap, append-only guards and bounded-version-query translation; only three SQL parameter declarations at the maximum tested ID set |
| `../site-geofences/VerifyBoundary.cs` | 32 passed | Existing actual geofence geometry and validation rules |
| `../site-geofences/VerifyService.cs` | 55 passed | Existing geofence service/operation behavior with synthetic collaborators |
| `../site-geofences/VerifyModel.cs` | 28 passed | Geofence mappings/guards/DI plus the exact expected site schema gap; zero SQL connections |
| `../shifts-rosters/VerifyHttp.ps1` | 34 passed | All 12 retained scheduling route registrations/anonymous protections, including retired routes; shift Location metadata and self-query identity contract |
| `../site-geofences/VerifyHttp.ps1` | 14 passed | Six existing geofence route contracts/anonymous protections |

Counts are assertions, not distinct end-to-end scenarios. The final site harness passed 118 checks with zero failures. Population and holiday helpers each had an absent-helper failing run followed by a passing run. The site service harness was introduced after service code existed and is not presented as a red-to-green implementation cycle.

One initial Release build passed. Later default parallel Release invocations exited unsuccessfully with zero reported compiler errors and no diagnostic identifying a source defect. The final build with build servers disabled and one MSBuild worker passed; the same solution then completed all listed regressions. This is the verified build invocation, not a claim that the unexplained default-build behavior was diagnosed.

## Reviewed defects fixed

1. **Final overnight duty exceeded site availability.** A finite assignment ending exclusively on 1 February could previously accept a 31 January overnight shift at a site ending on 31 January. Assignment creation/replacement now computes the actual last duty's occupied end before validating the site and locks. Regression checks reject that spill while accepting exact-midnight/rest-day boundaries.
2. **Unrelated legacy history blocked future site work.** Reading an old approval-linked ending used to throw even when it did not apply to the requested date. Immutable endings/history are now readable; only a selected legacy schedule or an actually affected legacy duty boundary remains unavailable. Duty-window validation resolves only affected boundaries, including a finite upper bound for endings and the union of old/new intervals for replacements. Checks cover past and future legacy history separated by a gap, direct ending/replacement within that gap, and rejection of selected/adjacent/overlapping legacy evidence requiring review.

Independent source review covered site authorization, timeline identities, legacy boundaries, immutable holiday pinning, temporary staff manifests and atomic staging. These fixes have targeted regression coverage; review is not a substitute for SQL acceptance.

## Behavioral coverage

The site harness exercises direct creation/replacement/ending, future dates, locked periods, one-time ending, stale timeline/shift/staff tokens, site-scoped allow/deny, permission revocation, retries and changed-key payloads. It verifies historical site inheritance for multiple employees, no individual fallback, immutable version/source explanations, overlapping assignments, overnight transfers, server-derived My Shift identity and pinned informational holidays.

Temporary checks use server-derived affected employees, private evidence objects, original/replacement version binding, immutable manifests, changed staff, empty staff, existing temporary conflicts and next-day site transfers. Current schema absence is injected as 503. These are real Application services with fake repository/auth/storage collaborators; they do not execute the Infrastructure SQL eligibility or metadata-readiness queries.

The HTTP scripts launch a temporary hidden localhost Release host with authentication providers disabled and a deliberately unusable synthetic SQL connection. They inspect OpenAPI and make anonymous requests only, then stop their own process in `finally`. They do not bypass authentication to make a successful business request, create accounts/fixtures, grant permissions or weaken production private storage. Authenticated retirement responses are documented and source-reviewed; anonymous 401 is the HTTP result actually executed.

## Historical 8 October deployment gaps (superseded by current evidence)

**The direct site APIs are not yet usable against the unchanged legacy database.** No migration was generated/applied and no DbContext/snapshot was edited. The [schema handoff](Decisions-and-Schema.md) specifies precisely three nullable approval references and the nullable exact holiday reference plus index/FK. Model-difference checks intentionally verify that gap; they do not claim the database has it. Runtime metadata inspection rejects an absent/incompatible schema with `site_roster_schema_not_ready` before selecting the new mapped column.

Also outstanding:

- Explicit first-grant provisioning remains unimplemented/unapproved from the earlier access task. Existing access management still requires the delegator to own the permission; no bootstrap/system-role grant was silently expanded. The user's module-access direction did not authorize changing real accounts.
- After separately authorized schema/provisioning work, run the proposed isolated SQL/private-storage setup for real web MFA/CSRF and mobile self requests, live scope revocation, schema metadata visibility, actual historical SQL membership queries, competing writes, rollback, unique constraints and encrypted-disk response replay.
- Temporary changes are create-once for a site/date. Cancellation, supersession or repairing a frozen manifest after later HR membership changes needs an append-only amendment design/schema; conflicting or changed-manifest cases fail closed today.
- Shift-version publication affecting existing population/temporary use retains its earlier safeguard. Use a separately prepared immutable shift/version for site replacements until full publication-impact validation is added.
- History reads are bounded at 2,048 timeline/version rows and 2,000 staff per temporary manifest. No large-data SQL/load test was performed; pagination/windowed resolution is a later capacity concern.

There were no SQL/data writes, private-disk fixtures, account permission changes, migrations, commits, branch/worktree changes, merges, remote operations, PRs or scheduled follow-ups. The separate policy feature was not integrated. Generic team/department/category scheduling and approval/rejection flows were intentionally excluded by the user's later site-only/direct-change decision. No pay calculation or punch flow was added.

## Reproduction

Use the existing cached .NET 10 dependencies. File harnesses are standalone source files; no test project was created. Build sequentially because they share project assemblies.

```powershell
$env:TEMP = Join-Path $PWD 'artifacts/site-geofences-temp'
$env:TMP = $env:TEMP
dotnet build Cosmix.Backend.sln --configuration Release --no-restore --disable-build-servers -m:1 -tl:off

dotnet run --file docs/group-rosters/VerifyPopulationTimeline.cs --configuration Release --property:RestoreSources=$PWD/src/Cosmix.Application --property:NuGetAudit=false
dotnet run --file docs/group-rosters/VerifyHolidayContext.cs --configuration Release --property:RestoreSources=$PWD/src/Cosmix.Application --property:NuGetAudit=false
dotnet run --file docs/group-rosters/VerifyApprovalModel.cs --configuration Release --property:RestoreSources=$PWD/src/Cosmix.Infrastructure --property:NuGetAudit=false
dotnet run --file docs/group-rosters/VerifySiteScheduling.cs --configuration Release --property:RestoreSources=$PWD/src/Cosmix.Application --property:NuGetAudit=false
./docs/group-rosters/VerifyHttp.ps1
```

HTTP verification requires loopback permission. Repository model readiness is read-only application code, but its actual SQL execution still belongs to the separately authorized database acceptance run.
