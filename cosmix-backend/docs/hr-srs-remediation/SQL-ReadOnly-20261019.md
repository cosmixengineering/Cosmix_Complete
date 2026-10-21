# SQL and Private Storage Prerequisite Audit - 19 October 2026

Read-only inventory completed around 15:12 Asia/Karachi in the existing backend checkout.

**Result: no already isolated, wholly synthetic, current-schema database is verified available for real auth/SQL acceptance.** A current-user LocalDB process and diagnostic configuration exist, but catalog ownership/provenance, physical database files, schema and fixture population are unresolved. SQL connection probing stopped at that ambiguity. This is not a claim that no compatible database exists anywhere.

The user's **NO migration apply** instruction remains binding. Historical approvals are stale. No migration, setup, auth or acceptance executor was executed or dot-sourced. This audit used file reads, allowlisted configuration fields, LocalDB `info`, process/listener metadata and ACL/file-count inspection. It made zero SQL connections and HTTP requests; started no API host or SQL instance; created no fixtures, accounts, keys or configuration; performed no build, reset, cleanup or Git operation. Only this report was written. No password, token, OTP, connection string containing credentials, key content or personnel row was printed. Credential/key files were inspected only through filesystem metadata.

## Available Now

| Item | Verified observation | Limit |
| --- | --- | --- |
| Operator | `COSMIXRND\Huma.Khan`, machine `SOFTWARE-PC1` | OS ownership does not prove database contents are synthetic. |
| SQL tools | LocalDB 17.0.4025.3 and sqlcmd are installed | Neither was used to connect. |
| Named LocalDB inventory | `MSSQLLocalDB`, `CosmixUat_d893653346`, `CosmixAttendanceHuma20261013A01` each report **Stopped**, no pipe | No alias connection or start attempted. |
| Current LocalDB process | PID 24152, owned by the operator, started 2026-10-19 13:27:48 +05:00; SQL internal argument `LOCALDB#2E40033B` | Conflicts with the named-instance state above. |
| Process system-file binding | PID 24152 uses `master.mdf`, `mastlog.ldf`, `error.log` under `C:\Users\Huma.Khan\AppData\Local\Microsoft\Microsoft SQL Server Local DB\Instances\MSSQLLocalDB` | These are system files, **not** the physical files of `Cosmix_Testing`. |
| Existing API process | PID 26872, operator-owned; started 14:37:04 +05:00; `src\Cosmix.Api\bin\Debug\net10.0\Cosmix.Api.exe` in this checkout | No listener attributed to this PID in the sample; its effective environment/configuration and binary/source agreement were not established. Not adopted or contacted. |
| Other SQL process | PID 17836; owner/path unavailable; listens on loopback TCP 1434 | Not identified as an acceptance target; not contacted. |
| Candidate HTTPS port | No listener on 7481 in the sample | Availability and trusted certificate must be rechecked for any later authorized host. |

### Private Paths and Counts

Existing root `R = C:\Users\Huma.Khan\AppData\Local\Cosmix\LocalTesting`:

| Path under R | Read-only finding |
| --- | --- |
| `settings.json` | LocalTesting enabled; integrated auth; catalog `Cosmix_Testing`; historical pipe `np:\\.\pipe\LOCALDB#EE16F2FB\tsql\query`; no owned profile marker. |
| `Attendance-Host-20261013-B01.json` | Profile `HumaExistingAttendance20261013B01`; integrated auth; `Cosmix_Testing`; historical pipe `np:\\.\pipe\LOCALDB#842DD4FB\tsql\query`; URL `https://localhost:7481`. |
| `Dashboard-20261019-Diagnostic.json` | Updated today; integrated auth; `Cosmix_Testing`; candidate pipe `np:\\.\pipe\LOCALDB#2E40033B\tsql\query`; LocalTesting enabled, but neither fresh nor existing owned profile is declared in this file. |
| `files` | 132 direct files, zero subdirectories. Their contents, SQL references and synthetic provenance were not read or established. |
| `keys\files.key` | Exists, 32 bytes. Bytes were not read; usability/decryption is untested here. |
| `keys\attendance-dp-20261013-b01` | One direct file, zero subdirectories. Key activation, expiry, application discriminator and decryptability are unverified. |
| `Test-Accounts.json` | Exists, 514 bytes. Contents/passwords were not read. Existence does not establish working credentials. |

All three settings files name `R\files` and `R\keys\files.key`, select SMS provider `LocalTesting`, and have no nonempty SMS Endpoint/BearerToken in the inspected fields. This does not prove the running API loaded any of these files or exclude other inherited provider settings.

The inspected root, files/keys directories, file key, B01 keyring directory and credentials file are operator-owned, not reparse points, and expose only the operator's Allow FullControl ACE. The root protects its ACL; children inherit that ACE. Ancestor-wide and individual-object validation was not performed. These findings establish existing storage material, not permission to repurpose 132 objects.

## Retained Database Evidence

Evidence root `E = C:\Users\Huma.Khan\AppData\Local\Cosmix\PartAContinuation\Database20261013`:

| Evidence | Historical facts, not current SQL observations |
| --- | --- |
| `target-binding.json` | Selected `CosmixUat_d893653346`, `Cosmix_Testing`, pipe suffix `D3CA674F`. |
| `CosmixUat_d893653346-inventory.json` | Later **UnselectedReadOnlyInventory**, suffix `673B4355`: 12 migrations, 5 accounts, 14 employees. |
| `original-upgrade-f90875ab8719494484783114825f0331\result.json` | 23 migrations; 3 known fixture matches among 5 accounts/14 employees. |
| `b24-original-86e3870eda664c97bf82adf5575f3e0c\result.json` | `Cosmix_Testing`, 24 migrations, 5 accounts, 14 employees, 135 idempotency receipts; 0 leave ledger entries, leave types, policy versions, policy assignments and delivered notifications. |
| `b24-clone-6c709a6d194f4bbc8aaac6814220c643\result.json` | `Cosmix_PartA_Acceptance_20261013`, 24 migrations; same listed account/employee/ledger/receipt/notification counts. Wrong catalog for existing LocalTesting guards. |

B24 original intent records family `d9c34627-18a2-41cf-a804-821e39516303`, but does not bind current instance plus exact MDF/LDF paths. A family shared by a restore does not distinguish original from clone. Current SQL counts are **unknown**. The 2 extra accounts and 11 employees beyond the three matching fixtures are not fully classified as synthetic by these receipts.

The original B24 auth population was already nonempty: 82 OTP challenges, 82 deliveries, 78 sessions, 88 refresh tokens and 33 rate buckets. Their current state, including queued deliveries, is unknown. An empty A02-style auth baseline cannot be assumed.

### A01 and A02

- A01's private root `C:\Users\Huma.Khan\AppData\Local\Cosmix\AttendanceAcceptance\Huma20261013A01` exists, with four direct manifest files, but zero direct files in config/sql/keys/files/results. Its named instance reports stopped. Retained setup describes a partial failed attempt, not a usable seeded catalog. No resume was attempted.
- A02 instance `CosmixAttendanceHuma20261015A02` is absent from LocalDB inventory. Its private root `C:\Users\Huma.Khan\AppData\Local\Cosmix\AttendanceAcceptance\Huma20261015A02` and corresponding LocalDB system directory are absent. `A02-Execution-20261015.md` records that execution stopped before setup/auth. Its proposed `sql\Cosmix_Testing.mdf` / `sql\Cosmix_Testing_log.ldf` paths are **not existing verified database files**.
- The A02 review's 3 accounts, 3 employees, 39 permissions, 98 role-permission pairs and bootstrap files are a planned seed budget, not available rows/files. Creating them would violate this task and the no-migration constraint.

## Schema and Profile Blockers

Current source contains **25 designer-backed migrations**. Comparing their ordered IDs to the retained B24 history found exactly one source-only migration:

`20261015144424_AddFinancialAdjustmentsAndOutstationDeployments`

It creates `attendance.AttendancePayrollFinancialAdjustments`, `attendance.AttendancePayrollFinancialResolutions`, `hr.OutstationDeployments` and `hr.DeploymentHomeBreakRequests`. This comparison establishes a difference from the retained receipt, **not** a live pending-migration finding. No migration may be applied to resolve it.

- `LocalTesting.VerifyDatabaseAsync` rejects pending model changes, any pending migrations and any population other than its three matching synthetic accounts. Retained 5-account evidence cannot pass that guard unchanged.
- `LocalTestingA02Database.cs` explicitly requires `expected.Length == 24`, ending `20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims`. It is incompatible with a current 25-migration build even if some database already has all 25. The old A02 executor is not a reuse solution.
- B01 additionally pins historical pipe `842DD4FB` and historical server identity. Today's diagnostic pipe does not satisfy it.
- The earlier worker blocker was fixed in source: owned profiles now remove all six business workers, including DocumentExpiryReminderWorker and FaceRetirementWorker. Generic diagnostic settings do not establish that this owned-profile removal ran. Only OTP delivery should remain for the proposed auth slice; existing pending deliveries must first be proven outside its reach or absent.
- Source startup no longer synchronizes permissions. A02's 39-code list lacks `attendance.leaves.benefits.review` and `attendance.leaves.self`. Role labels do not establish current grants. Benefit writes require current web MFA and an active **Admin** system-role assignment with explicit All scope for `attendance.leaves.benefits.review`; they disallow self-award. An HR login alone cannot prove that workflow.

## Storage Evidence Already Available

`D:\CosmixVerification\hr-backend-20261019\storage-tests-4bbc7452506a4da3ad136e0589e7bbb1\results.log` exists and records 20 scenarios, 152 parent assertions, zero failures; restart child lines record 2 plus 5 additional checks. This supports the prior report's **159 assertions**, including real Windows storage and separate-process reread. It was not rerun here and used the earlier frozen fixed-build.

This is useful existing evidence for encryption, tamper, key handling and ACL failure behavior. It does not establish authenticated HR evidence creation, live SQL transaction behavior, current host storage binding or synthetic provenance of `R\files`.

## Smallest Conditional Test Plan

This is a concrete scope for subsequent review, **not execution permission**. There is no runnable target-bound command today. No thousand-file hash packet is needed: a short target record, a selected final build identifier and the exact cases/write budget below are sufficient once the missing facts are supplied.

1. **Resolve one existing target, then metadata only.** The strongest current candidate is the diagnostic configuration's explicit `np:\\.\pipe\LOCALDB#2E40033B\tsql\query`, catalog `Cosmix_Testing`, operator above. Do not adopt it until the LocalDB state contradiction and original/clone identity are resolved. Obtain exact catalog owner, family and MDF/LDF paths plus synthetic provenance. Once unambiguous, use one integrated-auth connection to that explicit pipe, no alias/fallback, to read identity/files, history and relevant schema metadata. Require the final build's complete schema, including the 25th migration, and no relevant cross-database dependencies/synonyms/unreviewed triggers. If it lacks schema, **stop under the existing no-migration instruction**. Select another already compatible synthetic target only with explicit identity/provenance; do not create, restore, attach or migrate one.
2. **One real Admin auth session.** After a bounded profile is prepared by its owner and separately authorized, use one owned HTTPS localhost:7481 host, an existing synthetic Admin credential and local OTP. Check pre-MFA denial, one successful MFA, permissions, missing-CSRF logout denial, valid logout and retained-cookie 401. Allow at most one challenge/verification/session, 30 local OTP polls, no resend/refresh/retry. For auth-only, send zero business requests. Validate an existing usable Data Protection keyring before launch: the proposal grants zero key writes. If key generation is necessary, stop and present its exact separate path/write scope.
3. **Only if existing benefit prerequisites pass, reuse that same session before logout.** Pin one distinct synthetic subject, existing active leave type, published `leave-settings-v4` policy/assignment, finalized completed-year KPI with unchanged authoritative sources and resolved disputes, an open award window and unlocked target year. Require no prior award for that source year and no consumption/reservation/encashment that would block reversal. Record exact IDs, dates, minutes and expected fingerprints privately. These fixtures/grants are **not verified available**; retained zero leave-type/policy rows cannot support a positive case. Missing fixtures are not permission to seed or publish policy.
4. **Five business requests:** one award POST, one identical same-key replay, one list GET proving persisted award, one revoke POST with the returned fingerprint, and one list GET proving immutable reversal. Paths are `/api/v1/leaves/admin/employees/{subjectId}/benefits`, its `?yearFrom=<target-year-start>` GET and `/{awardId}/revoke` POST. Verify aggregate/allowlisted SQL deltas and private-reference readability after each stage. The replay must add no ledger, receipt, outbox or object. This is a persistence smoke test; concurrency, injected failure/rollback and full HR UAT remain additional tests, not implied coverage.

### Proposed Write Budget

Fix all unknown IDs/baselines before requesting execution; these are prospective deltas, not today's permissions.

| Surface | Auth-only ceiling | Additional five-request benefit slice |
| --- | --- | --- |
| `auth.MobileOtpChallenges`, `auth.OtpDeliveries`, `auth.UserSessions`, `auth.RefreshTokens` | At most 1 new row each; only the test challenge/delivery/session/refresh lifecycle updates | No additional login/session |
| `auth.UserAccounts`, `auth.LoginPhoneBindings` | Only the selected actor's login-counter/phone-verification effects; no account/grant creation | No business update to either |
| `auth.AuthRateBuckets` | At most 20 affected buckets for the selected flow; current code must confirm this inherited one-session ceiling | Reconfirm any authentication middleware effect before enabling business requests |
| `audit.AuditEvents` | At most 12 new auth/security events, inherited from the retained one-session budget and subject to final source verification | 7 success events: 2 provenance, 2 operation, 1 replay, 2 list-read audits |
| `hr.LeaveLedgerEntries` | 0 | Exactly 2 appends: one entitlement and one linked negative reversal; net minutes zero, history retained |
| `workflow.IdempotencyReceipts` | 0 | Exactly 2 new completed receipts |
| `jobs.OutboxMessages` | 0 | Exactly 2 staged notification messages if the pinned subject has one active account, which this proposed case requires |
| `communication.Notifications` | 0 | 0 with the outbox worker disabled; delivery is not tested |
| Encrypted private objects | 0 | Exactly 8 successful-path new `<guid>.enc` files: award/reversal proof, notification payload, protected result and operation-audit evidence per write |
| Keys, credentials, migrations, fixtures, roles/grants, policy/KPI sources, other business tables | 0 | 0 |

The eight-object count follows `LeaveBenefitService.Store/Notify`, `NotificationOutbox.StageAsync` and `AttendanceOperation.WriteAsync`. Files are written outside the SQL commit's physical rollback capability; a failed request can leave new private objects even if SQL rolls back. Retain them, stop, and report deltas; do not delete, reset or automatically retry. The revoke above is an explicitly tested business operation, not cleanup.

Existing candidate object/key paths are exactly `R\files` and `R\keys\files.key`; existing keyring candidate is `R\keys\attendance-dp-20261013-b01`. None is approved for reuse merely by this inventory. A profile must bind the selected database and authorize only enumerated existing evidence references plus the eight new objects. Do not point a new empty store at retained SQL references.

Proposed new private run-receipt directory, observed absent: `D:\CosmixVerification\hr-backend-20261019\sql-acceptance-20261019-r01`. A later bounded run would create only `target.json`, `owned-host.json`, `before.json`, `after.json`, `result.json` or `failed.json` there, with operator-only ACL and sanitized counts/statuses. A new private runtime-settings path, if required, is **not selected or approved**; the parent must present its exact path/configuration separately. No existing receipt is overwritten.

## Exact Missing Access or Decision

- **Identity/provenance, not broad permission:** an owner-supplied binding from the current candidate to one existing catalog, exact two physical files, family, and a fully synthetic population. Reconcile `MSSQLLocalDB` stopped metadata versus PID 24152 before further SQL probing. Do not access the unidentified SQL service or adopt the running Debug API.
- **Read access after identity resolution:** integrated SQL CONNECT, visibility of database owner/files/family/dependencies/schema and SELECT on `dbo.__EFMigrationsHistory`; then aggregate counts and narrowly allowlisted actor/grant/fixture metadata. An owner can supply these sanitized metadata results instead. No broad personnel export or new SQL grants are requested/performed here.
- **Required existing state:** current schema, matching private encryption keys/evidence, reusable Data Protection keyring, valid existing synthetic credential, appropriate Admin permission/All scope, and the exact benefit fixture above. Current account IDs, subject/policy/type/KPI IDs, dates, live counts, physical catalog files and pending OTP state are missing. Absence of these is a technical blocker that a generic approval cannot cure.
- **Parent-owned implementation:** a current target-bound profile and reviewed final build with verified worker removal and real auth intact. Establish exclusive use of the selected catalog/storage during measured tests; the current Debug process is not evidence of exclusivity and must not be stopped by inference. Preserve the three-account and schema safety checks unless the parent explicitly designs and reviews a new precise synthetic population binding; do not relax them just to fit historical counts. No product files belong to this audit.
- **Fresh limited execution authorization, only after those facts exist:** one owned host, use of the selected private credential/keyring, the auth SQL effects, selected receipt path, and optionally the five-request benefit SQL/eight-object budget. If the selected instance is actually stopped, starting that exact existing instance requires separate explicit operational scope; no start is inferred here. Vehicle testing belongs to the separate agent. No migration, new fixture/account/key, provider call or old packet execution is proposed.

Until then, source/offline verification and the retained standalone storage evidence are available; real auth/SQL/storage acceptance remains unexecuted and unproven.
