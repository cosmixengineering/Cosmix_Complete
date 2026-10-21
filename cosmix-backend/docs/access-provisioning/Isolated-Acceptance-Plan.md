# Scheduling and geofence isolated acceptance

## Approved execution — 9 October 2026

The user explicitly approved: **“Haan, isolated test setup aur tests karo”**, in response
to the concrete request covering `Cosmix_Testing` migrations, synthetic role/staff fixtures,
private test keys/files and authenticated scheduling/geofence checks. This supersedes the
approval-pending and existing-database-only assumptions in the historical proposal below.

An exact Windows-authenticated read on `(localdb)\MSSQLLocalDB` found no `Cosmix_Testing`
catalog. The guarded `InitializeAcceptance.ps1` refused any existing database/populated
private directory, then created a fresh catalog using all 13 prepared migrations and the
existing `LocalTestingSetup` command's three synthetic identities. The original user-secrets
and development database were not used as a target or changed. Private settings, keys,
accounts and evidence are under
`C:\Users\Affan.Ahmed\AppData\Local\Cosmix\AccessAcceptance\20261009`.

The new database's account/migration counts were checked. A COPY_ONLY backup with CHECKSUM
was created and `RESTORE VERIFYONLY ... WITH CHECKSUM` passed before attendance fixtures.
The owned hidden host serves only `https://localhost:7249`, with real authentication and
the existing local-only synthetic SMS inbox. `/health/ready` returned 200. This establishes
setup readiness, not completion of the acceptance checks below. Execution results belong
in `Verification.md` and the private run evidence, without credentials or tokens.

## Historical proposal (superseded where described above)

The user's current instruction requires fresh approval before database access, fixture writes or new private-storage fixtures. Historical approval in Local-Testing.md is not being reused for this task. This plan does not authorize its execution. Fixed Admin/SuperAdmin web role configuration was confirmed on 9 October; only SuperAdmin may change protected Admin/SuperAdmin access. That rule approval is not permission to change any existing account or execute this plan.

## Concrete target

- SQL instance: intended `(localdb)\MSSQLLocalDB` on this computer. The installed SqlLocalDB tool was observed in the 9 October offline inventory; instance liveness and pipe mapping were not queried. No database connection or data inspection was made for this plan.
- Database: **Cosmix_Testing**, the existing synthetic test catalog described in [Local-Testing.md](../Local-Testing.md). Never use Cosmix_Development or a different catalog. Verify the actual database and local instance from the existing private testing configuration before any write. A named LocalDB pipe must resolve to the confirmed local instance.
- Existing synthetic accounts only: `superadmin@example.invalid`, `admin@example.invalid`, `hr@example.invalid`, with the exact names/phones required by the existing LocalTesting startup guard. These must be the only accounts. Do not create additional user accounts, alter their identity/password/phone/channel configuration, or treat them as real organizational authority.
- New fixture storage: `C:\Users\Affan.Ahmed\AppData\Local\Cosmix\AccessAcceptance\20261009`, outside every repository. Refuse to reuse a populated directory. Proposed children are `objects`, `keys`, `settings`, `backups` and `evidence`. Store the new private-file key only under `keys`, never under `objects`; use a new reference such as `access-acceptance-20261009`.
- Keep existing `%LOCALAPPDATA%\Cosmix\LocalTesting` passwords/settings/keys unchanged. Generate a separate private settings overlay for the acceptance process. Never print or save credentials, OTPs or key material in workspace artifacts or chat.
- Use the existing Development-only local testing mode with real password hashing, random OTP proof, browser binding, signed tokens, current SQL sessions, cookies, MFA and CSRF. The existing in-memory SMS inbox prevents carrier delivery; this does not verify a real SMS provider. Use an unused localhost HTTPS port and stop only the process created for this run.

The SQL catalog is shared with the existing synthetic login setup, not a new empty database. Approval must explicitly cover adding attendance fixtures and changing **synthetic account role assignments** in that catalog. If this would interfere with another active local test session, arrange isolation first; do not reset it or interrupt an unrelated host.

The 9 October [offline inventory](Readiness-Checklist.md) did not find the documented LocalTesting directory/settings/account file and observed sandbox `LOCALAPPDATA` redirection. Recover the original private configuration location with the operator; do not interpret missing files as permission to initialize/reset the catalog. No alternative fixture directory is selected by this plan. Site-roster plus temporary-amendment schema requirements must be reviewed together; schema application is separate authorization and is not included below.

## Before the first fixture write

1. Read the private connection without outputting it; verify local Windows-authenticated instance and exact catalog. Read-only inspection must confirm the three synthetic accounts, expected schema and no pending model/migration difference. Abort on unexpected identities, incompatible schema, unresolved target or non-synthetic data in intended fixture records.
2. Do not run `Start-LocalTesting.ps1 -Initialize`, `setup-local-testing`, database migrations, EnsureCreated, reset/drop commands or production bootstrap. This plan uses the already-created compatible schema; a missing/outdated database is a blocker.
3. Create the approved new private directory with restrictive ACLs for the current Windows user, SYSTEM and Administrators. Generate separate random test-only cryptographic keys and private process settings. Validate using the actual private-path/storage guards.
4. Create and verify a backup of Cosmix_Testing inside the approved backup directory before mutating fixtures. If backup verification fails, stop before fixture writes. Restoration is a separate deliberate recovery operation, never an automatic cleanup step.
5. Exercise the confirmed existing web role/permission/assignment APIs with genuine current fixed Admin/SuperAdmin authority. Direct SQL grants do not prove that flow works. Record initial synthetic account versions/assignments without credentials. Admin may modify ordinary synthetic employee access; only the synthetic SuperAdmin may modify protected Admin/SuperAdmin access. No separate owner-command flow is used.

## Fixture envelope requested

Create only clearly prefixed synthetic attendance masters, approved synthetic employees without new user accounts, site/office assignments, teams, dated membership transfers, shifts, future geofence versions, roster assignments and the audit/receipt records required by the real workflows. Use the existing HR synthetic account for self tests. Scope all harness queries to the generated fixture IDs; never edit an unrelated fixture to force a result.

Use fresh custom feature roles rather than changing immutable fixed or previously assigned role permissions. Any synthetic account assignment change must be explicit, audited, version-checked and recorded in the run manifest. Existing passwords, MFA proof requirements and frozen bootstrap grants stay unchanged. Retain immutable evidence; do not delete audit/receipt/history rows for cleanup. End/revoke only this run's temporary grants using supported operations where possible, and report retained fixtures and security-version changes.

## Acceptance checks

- Real web password/random-OTP login, current MFA, HTTPS cookies and CSRF through the current host. Verify valid writes and missing/wrong tokens, wrong browser, non-MFA and revoked sessions. The existing OTP viewer supports WebLogin only: mobile acceptance needs an explicitly reviewed harness path for synthetic OTP delivery; it must not expose mobile OTPs through a new production route or claim successful mobile testing prematurely.
- Bootstrap-supported geofence authority: fixed Admin/SuperAdmin writes/history, appropriately delegated reads and employee self-only assigned-site configuration. A custom role named Admin must not obtain fixed-role authority.
- Explicit scheduling role creation, catalog permission addition and employee assignment through the confirmed web flow; verify that neither personal module ownership nor automatic startup grants are needed. Check fixed Admin/SuperAdmin identity, custom-role denial, protected-target hierarchy, expected versions, duplicate/stale writes, self-denial and revocation. Use separate Shift-All, Roster-scoped and MyShift-Self roles so one assignment does not widen unrelated scopes.
- Allowed and denied employee/site/team scopes, own-versus-another identity, historical intervals, overnight transfer across occupied dates and exact-midnight exclusion.
- For scheduling/geofence writes that support idempotency keys: competing HTTP writes with same key/body, changed payload/key, distinct keys with stale expected versions and overlapping assignments. For access writes: review current GET state, submit required reasons and expected versions, verify protected grant/assignment evidence, and exercise stale-write conflicts plus lost-response reconciliation from subsequent reads. Verify actual persisted outcomes from independent contexts, not only response status. Access APIs use [normal web review and conservative retry semantics](Verification.md#ui-review-reason-and-retry-semantics); they do not provide a separate preview digest or idempotency-key replay contract.
- Failure injected in a standalone verification harness around transaction boundaries: verify business changes, audit and receipt roll back together. No failure hook or authentication bypass is added to production.
- Actual encrypted private-file storage: write/read with separate protected key, frozen retry result after process restart, wrong purpose/key/tamper refusal and unavailable storage fail-closed. Corrupt only this run's dedicated test objects, never existing LocalTesting objects or keys. SQL rollback may leave encrypted orphan objects; report them rather than claiming cross-store atomic deletion.

## Approval boundary

Required approval covers the exact synthetic database writes, new private directory/keys/settings/backup, temporary synthetic account grants, and localhost test process described above. It does not authorize any production account grant, real employee/biometric data, migration, external SMS, GitHub action or existing-file reset. Until granted, run only standalone in-memory/component/model and anonymous HTTP checks and label actual SQL/authenticated/encrypted-disk acceptance outstanding.
