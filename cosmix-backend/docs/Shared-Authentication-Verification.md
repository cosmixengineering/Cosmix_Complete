# Shared Authentication Verification

Dates: 6-7 October 2026. Branch: `codex/shared-mobile-authentication`.
Base commit: `34e8e077283039a830cff7258bb37c2514f73d57`.

## Develop Handoff Checks - 7 October 2026

The PM authorized sharing the current backend work on `develop`. Earlier local-only/no-push
statements below describe previous work sessions and no longer prohibit this handoff.

Fresh pre-push checks passed:

- Release solution build: zero warnings and errors.
- Web core: 18/18 offline checks; protected system roles: 67/67 offline checks.
- Mobile enrollment guard: 22/22 scenarios, 56 assertions. No SQL or SMS writes were performed.
- The running Visual Studio API returned 200 for live/readiness, Swagger and OpenAPI;
  OpenAPI 3.1.1 exposed 42 paths. Anonymous Department access returned 401.
- Changed-file inventory and selected credential-pattern checks found no local database,
  binaries, temporary harnesses or credential values to include. Connection/signing-key
  settings remain empty in tracked configuration. `git diff --check` passed.

This handoff does not establish live SMS or production acceptance. Actual owner setup and
provider configuration remain pending. Other developers must configure their own private
settings and review/apply the existing migration chain to their approved development target.

## Visual Studio User Secrets Isolation Follow-Up - 7 October 2026

The reported blank `ConnectionStrings:CosmixDatabase` and Swagger definition HTTP 500 were
reproduced in the actual Visual Studio Debug process, not inferred from CLI behavior.
The process used the correct solution, Development environment, account and compiled
UserSecretsId. Its secrets provider nevertheless resolved `secrets.json` against the API
directory: the expected User Secrets directory was absent from that process's filesystem view.

A read-only final-file-handle check showed that the settings previously written inside Codex
actually lived under the Windows Codex package's `LocalCache/Roaming` directory. The same
apparent `%APPDATA%` path was virtualized. Fresh CLI Debug DLL and apphost runs passed their
checks in the Codex context, but those passes did not establish Visual Studio readiness.
This corrects the scope of the earlier Visual Studio configuration verification below.

The existing confirmed development connection was set with the official `dotnet user-secrets`
command in the Visual Studio process's normal Windows user context. The secret was not printed
or added to a repository file. Visual Studio debugging was restarted, and the actual
`https://localhost:7240` process passed all five checks:

- `/health/live`: 200.
- `/health/ready`: 200 against the configured existing database.
- `/swagger/index.html`: 200.
- `/openapi/v1.json`: 200 with parseable OpenAPI and nonempty paths.
- `/api/v1/hr/departments`: 401 without authentication, as required.

Release build passed with zero warnings/errors. Temporary diagnostic API hosts were stopped;
the user's Visual Studio run was left running. No application behavior, database/schema,
authentication settings, real accounts or SMS provider was changed. Temporary debugger
breakpoints were removed. Only the local configuration and these setup/verification notes
were changed; nothing was committed or pushed.

## Current Closure Review - 7 October 2026

The latest PM decisions permit audited initial-password reset only before any successful login,
and administrator-reviewed replacement of a revoked installation followed by fresh SMS OTP.
Actual CEO/SuperAdmin creation is explicitly deferred until approved details are available.
No frontend work, repository test project, commit or push is included.

Independent parent verification after the review fixes:

- 22 mobile-enrollment guard scenarios (56 assertions) passed. An approved web-only submission
  cannot be silently turned into mobile enrollment; legacy inconsistent flags fail closed before
  OTP activation. Clear expected channel denial is 409, not an uncaught domain exception.
- 64 initial-password reset scenarios (178 assertions) passed: separate fixed administrator,
  private evidence reference, permission/delegation and protected-target guards, current versions,
  and exclusion of active/revoked/expired sessions or any historical phone verification. Real
  Identity hashing is used; no password/evidence appears in ordinary audit or responses.
- Device recovery passed 42 approval scenarios/94 assertions and 11 fresh-OTP scenarios/26
  assertions. Independent source review found no additional approval/controller blockers.
  Checks cover separate fixed-admin authority, delegation, stale versions, protected targets,
  duplicate approval, unverified replacement, old-history preservation, fresh context-bound
  proof, verification audit and current-first repository ordering. These are offline checks,
  including simulated concurrent approvals and SQL translation, not recovery-specific live SQL races.
- Focused session/audit checks passed: 101 active sessions span two bounded pages, invalid page
  inputs are denied, and logout audit identifies the actual target session plus actor role,
  employee and initiating session/device. Current-permission reads record actual-role audit
  and reject stale account versions. Persistence is simulated for these focused checks.
- Actual generated OpenAPI checks passed for mobile bearer versus browser cookies/CSRF,
  anonymous first-factor routes, refresh without an access cookie and session pagination.
- The actual API/TestServer suite passed 27 HTTP checks, including the new reviewed reset/device
  routes: required CSRF, MVC validation, real service resolution and mobile-management denial.
  Successful recovery writes are covered separately by service checks; the HTTP checks reach
  deliberate missing-target responses with synthetic persistence, not live SQL or SMS.
- Regression suites passed: 18 web core; 101 initial-credential scenarios/526 assertions;
  67 fixed-role checks; 89 mobile scenarios/292 assertions; 57 password service/HTTPS checks;
  34 phone service checks. These counts overlap and are not unique acceptance-test counts.
- 180 offline provider assertions and 62 dispatch scenarios/153 assertions passed. Dispatch
  rechecks current eligibility after claiming, verifies encrypted payload against its challenge,
  and bounds HTTP by the remaining lease. Invalid bearer-token configuration fails closed.
  Fake HTTP handlers exercise statuses/timeouts; no live SMS/gateway was contacted.
- The original LocalDB SQL suite passed on seven migrations: activation, wrong/reused proof,
  issuance/save rollback, restrictive FK/phone uniqueness and separate-context verification,
  refresh/replay, logout/refresh, device-revoke/verify and resend/verify races. Latest run ID:
  `fae3857be1c34d989a0b96e2bede79ac` after final integration. All 18 touched table counts returned to zero using the exact-key
  cleanup journal. Sequences/rowversions are not reset. Test crypto/issuer were substitutes;
  this is not production-crypto or SQL-backed HTTP/SMS acceptance.
- Read-only schema comparison passed 4,241 assertions: 122 tables, 1,400 columns, 277 foreign
  keys, 349 CHECK constraints, 378 non-key indexes, seven migrations. No new schema was required
  by the review fixes. Expression-level schema comparison remains outside this harness.
- Eight actual API checks with persisted Development User Secrets passed: SQL readiness and
  Swagger/OpenAPI succeeded, protected routes denied anonymous access and disabled OTP remained
  503. The temporary process was stopped; no real provider was enabled or owner created.
- Final Release solution build passed with zero warnings/errors; EF model construction and
  the seven-migration snapshot agree. `git diff --check` passed with only existing line-ending
  normalization notices. Temporary verification hosts/processes ended.

Some older temporary harnesses needed fixture updates for the new audit/history repository
contracts; production guards were not weakened to make fixtures pass. The obsolete broad
`cosmix-service-offline/checks.cs` suite still contains legacy MFA/self-permission assumptions
and is not claimed passing. Current focused suites above were rerun against current source.
Harnesses remain outside Git under the workstation TEMP directory.

Current operating gates: company-selected SMS, approved limits/keys, actual owner initialization,
real browser/mobile acceptance, SQL-backed HTTP and recovery races, distributed/load checks,
production operations and integration into the future HR approval service. Older sections below
are dated historical evidence, not the current list of missing code.

## Visual Studio Configuration Follow-Up - 7 October 2026

The reported missing-connection exception came from a blank runtime connection setting, not
missing tables. Earlier migration commands used a process-only override. The independently
verified existing `Cosmix_Development` connection is now persisted in the API's Development
User Secrets, outside Git. Its explicit LocalDB pipe must be re-identified after an instance
restart; no replacement database was created. Restart Visual Studio debugging to reload it.

- Eight actual API HTTP checks passed using persisted User Secrets with no process connection
  override: live/readiness and Swagger/OpenAPI returned 200, protected routes returned 401,
  and unconfigured mobile OTP remained unavailable (503). The temporary host was stopped.
- Review reproduced rejection of a valid 1,024-character password serialized with JSON escapes
  by the web login's 4 KB body limit. Login/resend now allow a bounded 16 KB body, matching the
  recovery controller. A separate oversized-request reproduction exposed 500 instead of 413;
  the exception handler now preserves 413 without leaking request data. Real Kestrel HTTPS
  checks passed for escaped-password reset, both login factors, password change and oversized
  rejection; the full password suite passed 57 checks. Persistence is simulated; no SMS sent.
- Independently reran 40 first-owner offline checks, including private storage ACLs, encrypted
  evidence round-trip, immutable/provenance guards, staged identity setup and safe failure
  cleanup. These do not initialize an actual owner or connect to a database.
- Release build passed with zero warnings/errors. No frontend changes, commits or pushes.

## Backend Recovery Completion - 7 October 2026

The PM explicitly limited this continuation to backend work. No frontend edits, commits or pushes.
The original database and MDF/LDF were identified again through the observed SSMS pipe; no
replacement database or file attachment was used. The sixth and seventh migrations were applied
in order after separately verified COPY_ONLY CHECKSUM backups (RESTORE VERIFYONLY, not a restore drill):

- `C:\Users\Huma.Khan\Cosmix_Development_before_web_auth_20261007_084403_d21c5d2d.bak`
- `C:\Users\Huma.Khan\Cosmix_Development_before_recovery_20261007_090830_110ae020.bak`

Latest applied migration: `20261007090713_AddAccountRecoveryAndOwnerInitialization`.
Actual read-only model/schema comparison passed **4,241 assertions**: seven history entries,
122 business tables, 1,400 columns, 277 restrictive enabled/trusted foreign keys, 349 enabled/trusted
CHECK constraints and 378 non-key indexes. This checks names, types, nullability, FK targets and
index unique/filter presence, not semantic equality of every CHECK/default/index-column expression.
EF reports no pending model changes. All ten previously recorded migration/designer hashes match.

Independent parent reruns:

- 18 web core, 101 initial-credential scenarios (526 assertions), 67 fixed-role checks and
  89 mobile scenarios (292 assertions) passed after recovery integration.
- 14 password domain checks and 57 password service/HTTPS checks passed. Real Identity hashing,
  OTP crypto, JWT cookies and antiforgery were exercised; persistence was simulated and SMS was not sent.
- 17 phone domain assertions passed. The missing recovery contract was first reproduced before code.
- Phone service review reproduced three failing checks across two defects: malformed phone input
  escaped as ArgumentException, and shared account/phone OTP verification budgets were omitted.
  Both were fixed; parent reran all **34 phone service checks**, now passing. Covers other-Admin
  authorization, protected-target SuperAdmin rule, stale versions/approver authority, revocation,
  purpose/context binding, budgets, expiry/resend and no automatic session.
- The separate actual API/TestServer suite passed **24 HTTP checks**, adding CSRF protection for
  every anonymous browser-recovery route and rejection of web platform on mobile recovery routes.
  Its first new mobile-route check hit the harness's deliberate database-resolution trap because
  the new repository was not substituted; updating only the temporary fixture resolved that failure.
- An actual SQL rollback-only check executed the real first-owner service and repository through
  all staged saves, then a synthetic ordinary HR target, replacement phone binding, OTP completion,
  password reset with the recovered number, and consumed web-SMS session persistence. The test
  wrapper intentionally threw before commit; the real repository rollback succeeded. Every one
  of the 122 business tables was empty afterward. No actual owner/test account was committed.
  The snapshot store was an in-memory test double for this SQL check; no SMS/provider was contacted.
  Employee-number sequence and rowversion counters may advance, so this is not byte-for-byte restoration.
- Release build passed with zero warnings/errors. First-owner command `--help` ran without SQL/HTTP startup.

Remaining acceptance limits: actual owner initialization needs real approved identity/evidence and
private key/storage provisioning; company-selected SMS delivery and approved settings are absent.
These are not replaced by default passwords, fake accounts, permissive providers or public setup.
The new SQL check does not establish full SQL-backed HTTP authorization/concurrent recovery races,
production load, real-browser/mobile behavior, backup restoration or real SMS delivery. Historical
verification sections below remain evidence of earlier slices, not substitutes for these limits.

## Web SMS and Protected Roles - 7 October 2026

This continuation implements the PM's updated web password + registered-number SMS policy,
staff mobile self-service and fixed web-only Admin/SuperAdmin roles. Admin-controlled initial
staff web credentials were subsequently approved and added. The evidence below describes this
new scope separately from the earlier live-database checks; those older checks do not validate
the new web schema or credentials endpoint.

- Release solution build passed with zero warnings/errors after initial credential integration.
- Seven domain policy checks passed; the original old behavior failed the relevant scenarios.
  Coverage: web-only approved phone, HR mobile without web MFA, no password-only web session,
  single-use/purpose-bound web SMS proof, account-version and revoked-phone rejection.
- Parent independently reran 67/67 fixed-role checks: reserved identities, immutable system
  grants, scoped delegation, protected-account administration, mobile prohibition, and EF index.
- Parent independently reran 89 mobile/service scenarios, 292 assertions: HR/manager self-only
  access, both protected roles denied, proof/resend isolation, cross-channel refresh protection
  and current-state lifecycle regressions. In-memory repositories only, no SQL or SMS.
- Parent independently reran 18/18 web core checks: real Identity hasher validation, password
  lockout, generic requests, OTP consumption, browser binding, rate controls, session revocation,
  refresh/replay and web/mobile isolation. In-memory persistence, no provider calls.
- Actual HTTPS API disabled-configuration checks passed 12 assertions, including secure cookies,
  no authenticated cookie before both factors, CSRF rejection, safe unavailable responses,
  anonymous denial, OpenAPI and CORS. The temporary host stopped. TLS certificate trust was
  bypassed only in the local test client; no OS certificate trust setting changed.
- A fresh read-only review identified HTTP-only development origins conflicting with Strict
  secure cookies. Explicit HTTPS localhost frontend origins were added; their configuration
  check failed before and passed after. Frontend HTTPS/certificate setup is still separate,
  and this is not real-browser acceptance.
- Initial credential domain reproduction failed before implementation (missing supported
  transition), then passed: approved account activates for web without inventing invitation,
  phone verification or mobile entitlement; password alone still cannot create a session.
- Initial-credential contract checks failed five missing requirements before implementation,
  then passed 5/5. Parent independently reran the full 101-scenario, 526-assertion credential
  suite: current fixed administrator/delegation, account/phone/access eligibility, stale
  versions, email/phone duplicates, no overwrite/rebind, preserved rejected state, real Identity
  hashing and secret-free output/audit. Repository proxies do not verify SQL transactions.
- Parent independently reran 22 enabled HTTP integration checks using the actual API startup,
  MVC controllers, JWT validation/issuer and antiforgery under TestServer. Verified both factors,
  secure cookie attributes/token-free JSON, current-state /me, wrong transport/browser rejection,
  mobile management denial, CSRF changes after sign-in/access expiry, refresh rotation/replay,
  logout, and credential-endpoint MVC/CSRF/real service resolution. This host uses synthetic
  in-memory persistence and a no-send provider; zero database resolutions or SMS sends.
  It does not prove physical TLS or real-browser SameSite enforcement. The credential endpoint
  HTTP check reaches a deliberate missing-target 404; successful writes are covered separately
  by the service suite, not claimed as SQL-backed HTTP acceptance.
- Separate read-only review found no concrete defect in the credential increment; runtime
  and SQL results must still be read separately from that source review.

Offline EF validation reports no model changes since the generated migration. SHA-256 checks
confirm all ten previous migration/designer files (five migrations) remain unchanged.
Fresh EF SQL generation is byte-identical to the reviewed incremental SQL artifact.
Final Release build passed with zero warnings/errors; `git diff --check` passed with only
Git line-ending normalization warnings. All temporary hosts and verification processes ended.

Prepared migration: `20261007080926_AddWebSmsAuthenticationAndProtectedRoles`.
SQL: `docs/sql/20261007080926_AddWebSmsAuthenticationAndProtectedRoles.sql`.
Exact range: `20261006144249_AddSharedMobileAuthentication` ->
`20261007080926_AddWebSmsAuthenticationAndProtectedRoles`.
Two added fields, three indexes, one same-account restrictive FK/alternate key and CHECK updates;
no new tables, seeds, forward data deletion or role promotion. Initial credential setup adds no schema.
Down loses the added evidence and may fail on existing WebLogin/web-only-phone rows. Prefer
reviewed forward correction or tested backup recovery, not a blind downgrade.

**Not applied:** this continuation made no database changes. The last verified existing
`Cosmix_Development` state remains five migrations/121 business tables. The new migration needs
explicit application and SQL integration checks on that same independently verified target.

Temporary verification programs are under the workstation's TEMP folders, outside the repo:
`cosmix-mobile-domain-checks`, `cosmix-system-role-checks-20261007`,
`cosmix-mobile-policy-checks`, `cosmix-web-core-checks`, `cosmix-web-provisioning-checks`
and `cosmix-web-http-checks`. No repo test project was added.
These suites overlap; their counts are not a count of unique production acceptance tests.

Outstanding: SMS vendor/gateway and real delivery, explicit operating/password settings,
first-admin bootstrap, password/phone recovery, actual HR approval service integration,
new-schema SQL constraints/concurrency and browser/mobile acceptance. No commit or push.

## Live SQL Follow-Up - 7 October 2026

The PM authorized the development target and clarified that yesterday's existing database
must be used. The database was present in SSMS. The earlier CLI missing-database observation
was on another running LocalDB instance under the same `(localdb)\MSSQLLocalDB` alias.
SSMS process/connection discovery identified `Software-PC1\LOCALDB#EE16F2FB` and its pipe;
read-only SQL confirmed the SSMS session, `Cosmix_Development`, original MDF/LDF paths,
four applied migrations and 117 business tables with no business rows. The earlier claim
of a missing user database is superseded by this evidence; no data recovery was necessary.

An initial EF creation attempt on the other instance stopped with SQL 5170 because the
existing MDF path was protected. It did not create a replacement or alter the original files.
No file was attached, renamed, overwritten or deleted. The subsequent authorized work used
the verified SSMS pipe as a process-local environment override, not a committed configuration.
Pipe IDs are ephemeral and must be rediscovered/verified after a restart. User secrets remain
unchanged. An EF `--connection` argument attempt failed before connecting; passing the same
verified target through the process environment succeeded. No database guard was bypassed.

- Backup: `C:/Users/Huma.Khan/Cosmix_Development_before_auth_20261007_8f27c41a.bak`.
  SQL `COPY_ONLY, CHECKSUM` completed and `RESTORE VERIFYONLY WITH CHECKSUM` reported valid.
  This is backup verification, not a performed restore test.
- Applied **only** `20261006144249_AddSharedMobileAuthentication` using EF Core database update.
  Existing history was preserved; the full-chain script was not applied to this database.
- Live schema: five migrations, 121 business tables plus EF history, 1,376 columns,
  272 foreign keys, 341 CHECK constraints and 367 non-key indexes.
- 4,160 read-only schema assertions passed: model/snapshot/history agreement, table/column
  sets, SQL types/nullability, FK names/targets/restrictive actions/trust, CHECK names/trust,
  index names/unique/enabled flags and filtered-index presence. This does not compare all
  CHECK/default expressions or index/FK column ordering semantically.
- Nine rollback-only SQL checks passed: SQL rowversion generation/change, stale EF write
  rejection without overwriting the winner, nonnegative rate count, ordered rate window and
  absence of the exact temporary row after rollback. No committed rate fixture remained.
- Seven actual API HTTP checks passed using the verified SQL target: live/readiness and
  OpenAPI returned 200, protected routes rejected anonymous requests with 401 and disabled
  OTP returned safe 503. The temporary API host stopped; SMS was never enabled. Readiness
  checks the foundation Department table, not every authentication workflow.
- The separate real-repository service harness passed activation, consumed/wrong OTP denial,
  refresh rotation/replay-family revocation, rollback after an issuer fault and after SQL save,
  and a two-context OTP verification race with exactly one success. Real EF, services and
  repositories ran with test-only crypto/issuer/no-send substitutes. This is not live SMS or
  production-crypto/HTTP end-to-end acceptance.
- Live phone-index isolation returned SQL 2601 for `IX_LoginPhoneBindings_PhoneNumber`
  across different accounts; the binding/challenge restrictive FK returned SQL 547 on an
  exact fixture-parent deletion. Both negative transactions rolled back and retained evidence.
  The parent independently reran the complete extended suite successfully, run ID
  `338bacff02fa43f191062dc29c0b5288`, against unchanged Release assemblies.
- Final `DBCC CHECKDB` completed without reported errors. Final independent catalog checks
  found 121 business tables, zero business rows, five history entries and zero disabled,
  untrusted or cascading foreign keys/check violations. All temporary hosts/check sessions ended.

Synthetic fixtures use exact-key journals, guarded empty-table preconditions and `finally`
cleanup. The successful run restored all 18 touched table counts to zero and left no captured
key. Employee-number sequences and rowversion counters may advance; they are not reset.
An initial fixture correctly failed the no-self-approval domain guard; only the temporary
fixture was corrected to use separate submitter/approver identities. No product bypass or
permanent administrator/demo account was added. Full details and runnable checks are in the
workstation-local temporary `cosmix-sql-auth-20261007` directory, outside Git.

Additional parent checks are file-based programs under the existing temporary
`cosmix-mobile-domain-checks` directory: `SchemaChecks.cs`, `RateBucketSqlChecks.cs`, and
`LocalDbApiSmoke.ps1`. They require the independently verified process-local target; do not
reuse a stale pipe guard after an instance restart. No test project was added to the solution.

Still unverified against SQL: concurrent refresh/logout/revocation, resend/verify contention,
provisioning idempotency and dispatch races, multi-process/load behavior and connection loss
at commit. Web/MFA/bootstrap, SMS/provider, browser/mobile acceptance remain separate gates.

## Original Offline and HTTP Results - 6 October 2026

| Verification | Result |
|---|---|
| Full solution Release build | Passed, zero warnings/errors |
| Domain transitions | 16/16 checks passed: approved selection, invitation recovery, mobile activation, retained web credentials, MFA/channel guards, OTP/delivery lifecycles |
| EF mapping construction | 30 assertions passed; 121 entity types constructed offline |
| Provider helpers | 165 assertions passed: normalization, keyed verification, expiry/tampering, missing configuration, HTTP adapter semantics with a test-only handler |
| Authentication/session/dispatch services | 109 scenarios, 306 assertions passed with test-only in-memory repositories, controlled clock and real domain/crypto helpers |
| Access management | 75 assertions passed: paired scopes, effective dates, delegation, versions, employment history/timezone and channel guards |
| HR authentication-side provisioning | 17 offline scenarios passed: first/retry enrollment, retained web identity, delegated authority, no inferred contact phone, denied actors, duplicate phone and revoked binding |
| Active-account access replacement | 117 service/regression scenarios, 321 assertions passed, including 8 new role-lifecycle cases |
| Device lookup and pagination | 115 service/loopback-HTTP scenarios, 361 assertions passed, including older-device discovery/revocation and bounds |
| Actual JWT middleware and current-state HTTP | 171 checks passed across 301 HTTP requests; 183 captured source files matched live source; zero provider calls |
| Access HTTP/MVC | 23 checks passed using an isolated local test host and test-only authentication dependencies |
| Actual API HTTP/OpenAPI | 14 checks passed against the real API: disabled mobile configuration, anonymous denial, unavailable errors and registered routes |
| Migration/model/SQL | 55 assertions passed; zero model/snapshot differences; zero connection-opening attempts under a rejecting interceptor |
| Existing migration history | All four existing migrations and designers unchanged |
| Working diff whitespace | `git diff --check` passed; line-ending normalization warnings only |

Service scenarios include approved first/returning login, unknown/unapproved users,
wrong/expired/reused OTP, resend cooldown/supersession/device binding, single-use verification,
refresh rotation/replay, revoked devices, suspension/termination, conflicting status history,
security-version changes, role revocation, MFA bypass denial, rate budgets, cross-user/scope
denial and current/all-session logout. Eight dispatch scenarios verify post-commit sending,
accepted/rejected/unknown/unavailable outcomes, payload erasure and no resend of expired,
superseded, interrupted or ineligible deliveries.

Concurrent verification/refresh tests serialize the in-memory repository's critical section.
They demonstrate service sequencing, **not SQL Server race/locking correctness**. No real SMS,
gateway, company employee data or database integration was used.

## Review Corrections

Independent review found three substantive issues, all corrected:

1. Positional-record property annotations caused HTTP 500 before MVC actions. Reproduced,
   moved validation to constructor parameters, retained direct service validation and reran HTTP
   checks. Valid disabled-mobile requests return 503; invalid access DTOs return 400.
2. Access checks consumed unbound options, losing the configured timezone. The repository now
   consumes the shared bound settings instance; DI/timezone boundary checks passed.
3. Self-service permissions were advertised but unenforced. Added permission plus own-employee
   scope checks, repeated inside write transactions. A temporary permission-bypassed control
   failed all 36 denial cases; production code passed the complete service suite.

Current-session logout deliberately remains available without a management grant. Further
integration corrections include effective employment-history checks for administrators,
clearing pre-transaction tracked snapshots, shared SQL locking, explicit initial-access
selection, safe web-credential-precondition errors and bounded encrypted-payload storage.

### Follow-Up Implementation Checks

The continuation added direct service-level provisioning checks. Three negative cases first
failed: an existing account could be enrolled without role-delegation management, without
the actor owning the rights being enabled, or with only a Self grant where All was required.
First enrollment now enforces those delegation checks before changing the account/binding.
All 17 provisioning scenarios pass, including retained web credentials and mandatory MFA.
The fixture uses real domain transitions and test-only repository substitutes; it does not
prove SQL transaction/rollback behavior or actual HR approval API integration.

An independent correctness review reproduced permanent login blocking after the original
active-account assignment expired or was revoked despite authorized replacement access.
Both cases failed in the parent reproduction. Ongoing eligibility now uses a currently valid
account/employee assignment and its own scope, without changing the historical initial pair.
Both cases and six related negative/first-activation checks pass; the full 117-scenario
service regression copy is green. Original invitation activation/recovery remains unchanged.

Device revocation originally searched only the newest 100 devices and returned not-found for
an older owned device. A direct account-and-device lookup now resolves revocation inside the
existing transaction. The GET route also supports validated `page`/`pageSize` query parameters:
its pre-fix second page repeated the first, while the corrected checks discover all 205 fixture
devices, including 68 revoked records, with stable ties and no foreign-account entries.
The array response shape is unchanged. Session listing was not changed.

An independent HTTP harness exercised real JWT bearer validation, token issuance, controllers,
current-state services and exception handling with synthetic repository dependencies. The
parent reran its final 171 checks/301 requests successfully: invalid/tampered/expired JWTs,
current account/employment/phone/device/role changes, client/MFA-claim escalation denial,
paired Self permissions, transaction-entry revocation, replacement access and pagination.
Its 183 captured source files matched the live checkout before and after the run. All local
hosts stopped, with zero provider calls. This is not SQL concurrency, live SMS or full
production-startup acceptance. The service regression copies intentionally overlap; do not
sum their counts as unique test cases.

Follow-up fixes changed no schema. The prepared authentication migration remains unapplied;
all eight original migration/designer files were verified byte-identical through Git hashing.

## Migration

New migration:
`src/Cosmix.Infrastructure/Database/Migrations/20261006144249_AddSharedMobileAuthentication.cs`

Incremental script:
`docs/sql/20261006144249_AddSharedMobileAuthentication-from-CorrectReviewedModelLifecycles.sql`

Exact range:
`20261006105213_CorrectReviewedModelLifecycles` -> `20261006144249_AddSharedMobileAuthentication`.

- Four new auth tables: `LoginPhoneBindings`, `MobileOtpChallenges`, `OtpDeliveries`, `AuthRateBuckets`.
- Forty columns in those tables; seven additive fields on existing tables.
- Ten indexes, four restrictive FKs, four primary keys and one alternate key.
- Seventeen CHECK definitions, including replacement of the password-only active-account check.
- No forward table/column drops, business-data updates, seeds or automatic channel enablement.
- Script exactly matches fresh EF generation for this range; its only INSERT records EF history.
- `Down` is destructive and can fail to restore the old password constraint when active mobile-only
  accounts exist. It is not an operational backup/recovery plan.

**This migration was not applied during the initial authentication implementation, which made no database connections or changes.**
Earlier local development database setup was separate. Before applying, confirm the named target,
actual history and migration-owner approval; retain a recoverable backup where needed, then run
real constraint/concurrency/integration checks.

### Database Integration Preflight - 6 October 2026

The continuation inspected the exact proposed development target, with no database writes:

- Branch: `codex/shared-mobile-authentication`; HEAD `34e8e077283039a830cff7258bb37c2514f73d57`.
- SQL instance: `(localdb)\MSSQLLocalDB`, running under `COSMIXRND\Huma.Khan`.
- Server identity: `Software-PC1\LOCALDB#6AB26312`; `IsLocalDB = 1`.
- Opening `Cosmix_Development` failed. A read-only system-catalog query from `master`
  returned null for `DB_ID(N'Cosmix_Development')` and no visible databases named `Cosmix%`.
- The development user-secret connection also targets `Cosmix_Development`; no environment
  connection override was present. Secret values were not logged or copied into Git.
- The earlier recorded database baseline could not be confirmed on this live instance.
  No database was created, attached, restored, migrated, dropped or reset. No application
  tables were written to `master`. Creating the named development database awaits confirmation.

Prepared `docs/sql/20261006144249_AddSharedMobileAuthentication.from-zero.sql` for an
authorized empty target. Its range is `0` -> `20261006144249_AddSharedMobileAuthentication`.
Temporary offline checks passed 307 assertions: the five-migration chain creates 121 business
tables plus EF history, all created/added columns match the current relational model, the
snapshot matches, and all 272 foreign-key operations avoid cascading deletes. No business
seeds or forward table/column drops are present; existing preflight conversion guards remain.
The script has five history inserts and five per-migration transactions. A connection
interceptor recorded zero connection-open attempts during generation and offline validation.
The existing incremental migration review also passed 55/55 assertions without connections.

This was offline evidence, not proof of SQL Server execution, constraints or authentication
concurrency. The 7 October follow-up above supersedes this preflight's target/application status.

## Reproduction on This Workstation

No test project or test provider was added to the repository. Temporary checks are outside Git;
these paths are workstation-local, not a committed CI regression suite.

```powershell
dotnet build Cosmix.Backend.sln -c Release
dotnet run --file "$env:TEMP/cosmix-mobile-domain-checks/Checks.cs"
dotnet run --file "$env:TEMP/cosmix-mobile-domain-checks/MappingChecks.cs"
dotnet run --file "$env:TEMP/cosmix-mobile-domain-checks/MigrationReview.cs" -- --final
dotnet run --file "$env:TEMP/cosmix-mobile-domain-checks/FullChainReview.cs" -c Release
dotnet run --file "$env:TEMP/cosmix-provider-offline/checks.cs"
dotnet run --file "$env:TEMP/cosmix-service-offline/checks.cs"
dotnet run --file "$env:TEMP/cosmix-provisioning-offline/Checks.cs"
dotnet run --file "$env:TEMP/cosmix-active-access-checks-20261006/checks.cs"
dotnet run --file "$env:TEMP/cosmix-device-revocation-checks-20261006/checks.cs" -c Release
dotnet run --file "$env:TEMP/cosmix-access-checks-20261006/AccessChecks.cs"
dotnet run --file "$env:TEMP/cosmix-access-checks-20261006/AccessHttpChecks.cs"
& "$env:TEMP/cosmix-auth-http-check.ps1"
& "$env:TEMP/cosmix-jwt-http-20261006-b714f3/Run.ps1" -ReuseSnapshot
```

`cosmix-auth-http-check.ps1` starts/stops a loopback API on port 5389 and refuses an occupied
port. It does not call database readiness or enable OTP. The JWT runner uses ephemeral
loopback ports and a captured source snapshot; it rejects source drift. To test later source
changes deliberately, inspect the harness and run with `-RefreshSnapshot` instead.

## Current Remaining Gates

- SMS provider, approved limits and real delivery/outage/idempotency checks.
- Actual owner initialization is deferred by the PM; the local command and web password/SMS policy are implemented.
- Real HR approval-service integration; authentication recovery services are implemented separately.
- Remaining SQL races, provisioning/dispatch integration, multi-process/load tests and commit failure recovery; bounded checks and migration application are recorded above.
- Mobile secure storage/device acceptance and end-to-end UAT.
- Production TLS, trusted proxies, monitoring, backup/key rotation and retention.

No UI, test project, permanent account, production mock provider, commit or push was created.
