# A02 Local Acceptance Packet

Prepared 15 October 2026. LOCAL PREPARATION ONLY. Neither executor has been run
in execution mode. Approval of this packet is separate from code preparation.
The historical A01 instance, its partial records and all existing databases remain
untouched. No business-default, legal activation, provider or payroll-posting
fixture is included.

## Exact Target

- Checkout: `C:\Users\Huma.Khan\Downloads\Cosmix_Erp_software\cosmix-backend`.
- New instance: `CosmixAttendanceHuma20261015A02`, SQL LocalDB 17.0.
- Catalog: `Cosmix_Testing`; Windows operator/owner: `COSMIXRND\Huma.Khan`.
- Private root: `C:\Users\Huma.Khan\AppData\Local\Cosmix\AttendanceAcceptance\Huma20261015A02`.
- Managed system directory: `C:\Users\Huma.Khan\AppData\Local\Microsoft\Microsoft SQL Server Local DB\Instances\CosmixAttendanceHuma20261015A02`.
- Explicit database files under the private root: `sql\Cosmix_Testing.mdf` and
  `sql\Cosmix_Testing_log.ldf`.
- Settings: private root + `config\LocalTesting.json`; credentials: private root
  + `Test-Accounts.json`.
- Private keys: `keys\files.key`, `keys\snapshot.key`, and later
  `keys\data-protection\key-<guid>.xml` under the private root.
- Other private folders: `manifest`, `results`, `snapshots`, `files`, `metadata`,
  `receipts`, `exports`, `temp`. No existing parent's ACL is altered.
- Verified preparation Release: `D:\CosmixVerification\hr-backend-continuation-20261015\posting-build\bin\Cosmix.Api\release`.
  Finalization takes the parent's explicit final Release directory under the same
  verification root. Regenerate the SQL from that build before the final manifest.
- Owned host: `https://localhost:7481/`, loopback only, Development,
  `HumaAttendanceIsolatedA02`. Existing trusted HTTPS development certificate is
  required; no certificate creation, installation, trust change or TLS bypass.

The human-readable LocalDB alias is not a connection fallback. After approved
creation/start, the executor resolves ONLY the named A02 instance's explicit pipe
and verifies server/operator, then physical files and owner before further work.
Unexpected presence, state, identity, files, rows or hashes stops execution.

## Setup Writes

`Invoke-A02Setup.ps1` defaults to an in-memory review. Execution creates only the
new protected private root/subfolders, the named instance and its system files,
the catalog and its two explicit files. It applies the generated from-zero
24-migration `A02-FullSchema.sql`, ending
`20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims`.
This is initialization of a new disposable target, not a new feature migration.
Existing migration source, snapshot and DbContext are unchanged by this packet.

Five independent 32-byte cryptographic values are generated only during approved
execution: JWT, OTP verifier, OTP encryption, files and snapshot. The first three
stay in private settings; the latter two have private key files. The existing
setup command generates three fresh random passwords privately. Nothing is
copied from another database, credential file or keyring.

Initial SQL row budget (every unlisted business table must remain zero):

| Table | Rows |
| --- | ---: |
| hr.Employees | 3 |
| hr.EmployeeSubmissions | 3 |
| hr.EmployeeOnboardingDecisions | 3 |
| auth.UserAccounts | 3 |
| auth.Roles | 3 |
| auth.Permissions | 39 |
| auth.RolePermissions | 98 |
| auth.UserRoleAssignments | 3 |
| auth.UserAccessScopes | 3 |
| auth.LoginPhoneBindings | 3 |
| audit.AuditEvents | 1 |

The three existing synthetic aliases are superadmin/admin/hr at example.invalid.
The source-bound manifest enumerates all 39 permission codes and the 20 explicit
HR codes; Admin and SuperAdmin each receive those same 39 codes. Role-code and
permission-code pairs are checked, not just counts. This is not all current
catalog authority. No permission synchronization occurs on startup or restart.
Missing/revoked permissions remain missing/revoked. Later scenario grants require
their own explicit packet; no attendance/report/loan/finance fixture is added here.

Private seed objects: one encrypted bootstrap snapshot, two encrypted submission
files, one credentials file; zero Data Protection keys during setup. Every setup
stage writes a CreateNew private receipt. Input files remain read-locked and
hash-checked. Failure retains state; no resume, reset, drop, deletion or cleanup.

## One-Session Auth Writes

`Invoke-A02Auth.ps1` defaults to a read-only summary. Execution uses the same
combined setup-plus-auth decision and exact review-file hash, completed matching setup
receipts, unchanged private material, the same still-running pipe, empty auth
tables/keyring, exact seed/grants and an unused port. It starts one owned host
with a cleared child environment and no application arguments. Normal password,
local OTP, persisted session and CSRF handling remain active.

Only HR logs in, once. The existing helper performs one local-mode probe, two
CSRF GETs, one password challenge, one pre-MFA permissions denial, at most 30
browser-bound local OTP polls (500ms interval), one OTP verification and one
authenticated permissions read. Then the runner sends one missing-CSRF logout,
one still-active permissions read, one valid logout and one retained-cookie
replay that must return 401. No resend, refresh, retry, role mutation or business
request is sent.

Allowed persistent effects: one MobileOtpChallenge, one OtpDelivery, one
UserSession and one RefreshToken; local delivery claim/submission and OTP
consumption; HR phone verification; existing account login-counter reset;
session revocation; up to 20 auth-rate buckets; at most 12 additional audit
events. The normal successful path has four additional auth audits. SQL requires
one revoked HR session tied to its consumed WebLogin challenge. All immutable
business/grant tables are compared using independent count/digest reads.
Phone/account counts remain three. One DPAPI-protected Data Protection key may be
created in the A02 keyring. Only local OTP dispatch and framework key initialization
remain; all six business/background workers are removed for the isolated profile.

Private auth files: `results\auth-attempt.json`, `results\owned-host.json`, and
either `results\auth-result.json` or `results\auth-failed.json`. No credential,
cookie, OTP, settings content, raw provider/host output or response body is logged.
The owned API process is stopped in finally; instance/database/evidence are
retained. A failed run is never automatically retried. Real SMS/face providers,
mobile devices, legal approval and positive financial workflows are excluded.

## Review And Execution

Run from the exact checkout. Default preparation creates only the non-secret
`docs\attendance-a02\A02-Review.draft.json`, containing exact source/tool/runtime/SQL
hashes, ordered migration IDs, paths and fixture grants. It does not create the
private root or connect to SQL. Use Windows PowerShell 5.1 for stable serialization:

```powershell
& 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe' -NoProfile -ExecutionPolicy Bypass -File .\docs\attendance-a02\Prepare-A02Review.ps1
```

Parent reviews the complete SQL, source changes, generated manifest and the two
operation budgets together before obtaining ONE explicit authorization. A hash binds
bytes; it is not consent. Source/binary/script drift invalidates the packet.
The parent must preserve the frozen Release while inspecting/running copied suites.

The retained draft is historical preparation evidence and is not executable.
After the shared final build, regenerate the SQL without a product rebuild:

```powershell
& .\docs\attendance-gap-c\VerifyManaged.ps1 -ReleaseDirectory <FINAL_RELEASE_DIRECTORY> -Sources @('docs/attendance-a02/GenerateA02Sql.cs')
& 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe' -NoProfile -ExecutionPolicy Bypass -File .\docs\attendance-a02\Prepare-A02Review.ps1 -Finalize -FrozenReleaseDirectory <FINAL_RELEASE_DIRECTORY>
```

The final review includes both `FixtureBudget` and `AuthBudget` explicitly.
Do not ask for execution approval against a draft or before the shared freeze.
The execution-policy argument applies only to that child process; no machine/user
execution policy or permanent configuration is changed.

Only AFTER the combined setup-plus-auth decision, substitute the final packet's
PacketSha256 and exact Release directory. Execute these stages sequentially:

```powershell
& 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe' -NoProfile -ExecutionPolicy Bypass -File .\docs\attendance-a02\Invoke-A02Setup.ps1 -Execute -ReleaseDirectory <FINAL_RELEASE_DIRECTORY> -ApprovedPacketSha256 <PACKET_SHA256> -SqlApproval 'APPROVE A02 SETUP AND ONE HR WEB MFA ON 7481 ONLY'
```

After successful setup, the SAME approval covers host/key/auth writes. Use
PowerShell 7 and the SHA256 of the exact retained A02-Review.json (not PacketSha256):

```powershell
& .\docs\attendance-a02\Invoke-A02Auth.ps1 -Execute -ApprovedManifestSha256 <REVIEW_FILE_SHA256> -AuthApproval 'APPROVE A02 SETUP AND ONE HR WEB MFA ON 7481 ONLY'
```

There is no permission to execute either command in the current preparation.
The later user decision is owned by the parent. Live SQL, key persistence,
auth/session and storage effects remain unverified until the approved execution.

## Offline Verification

`VerifyStartupPermissions.cs` first failed against the retained Release: compiled
startup could reach permission Add, SaveChangesAsync and role-grant INSERT. The
fixed startup passes the executable call-graph invariant. No in-memory test is
represented as persisted SQL grant-revocation acceptance.

`VerifyA02Guard.cs` and `VerifyA02Runtime.cs` exercise real profile/configuration,
wrong-target-before-connection, DI/worker and keyring bindings offline. The existing
owned-profile runtime suite remains applicable. `VerifyA02Packet.ps1` exercises
actual executor guards with synthetic inputs and refuses mutation helpers without
their owned context. `GenerateA02Sql.cs` generates SQL through the actual reviewed
EF migrations with a connection-denial interceptor; it applies nothing.

No new test project, Program/DI change or global progress edit is included.

Latest executed preparation checks against the parent posting-build: compiled
startup invariant (17 methods), A02 pure guard (194), actual A02 runtime (24),
existing-profile runtime (6), and offline 24-migration SQL generation passed.
Retained output: `D:\CosmixVerification\part-c-parent-20261013\managed-86bce5315e2448a0ab0ddcec9608e265`.
An earlier isolated full build hit a concurrent missing SiteReportResponses
reference; the subsequent isolated Release passed with zero warnings/errors.
No full build was started after the parent reserved its build slot.
The final PowerShell packet checks pass 52 checks on both PowerShell 7 and
Windows PowerShell 5.1. No final executable review manifest exists during preparation.
