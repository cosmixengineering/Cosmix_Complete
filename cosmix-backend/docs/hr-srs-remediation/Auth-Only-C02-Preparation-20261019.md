# Auth-only C02 preparation - 19 October 2026

## Later transport decision in HR closure continuation

The original blocked checkpoint below is retained as historical evidence.
An actual Microsoft.Data.SqlClient 6.1.6 / .NET 10.0.12 read-only probe reached
the exact existing family and process, and SQL reported `Named pipe|FALSE` for
the connection despite `Encrypt=True`. This is not a TLS certificate failure
or evidence that the database is missing. Probe output is retained at
`D:/CosmixVerification/part-c-parent-20261013/managed-08762cc82ef2426b8aa5b71cd55f3a94`.

The user subsequently explicitly approved a testing-only exception for this
exact local Windows-protected pipe, the two synthetic accounts, owned HTTPS
7481 host, bounded authentication writes and existing C02 private paths.
Production connection settings and encryption requirements remain unchanged.
The profile's new explicit `LocalTesting:AllowUnencryptedLocalPipe` opt-in is
separate from its server-reported transport validation; TCP, unknown transport,
unknown encryption state and an unapproved unencrypted session are rejected.
The System.Data.SqlClient metadata reader uses the approved local exception.
The API still requests encryption; its observed local-pipe exception is now
explicitly checked, rather than inferred from the connection-string flag.

Read-only preflight subsequently verified 25 migrations, two approved accounts,
148 protected-table snapshots, all seven retained C01 files, no C02 root and
free HTTPS port 7481. A preflight bug treating a legitimate empty C01 stdout
log as an invalid credential was reproduced and fixed: only retained stdout/
stderr logs allow empty contents; source settings, keys and credentials still
require nonempty private files. Both log hashes and ACL checks remain required.
No existing C01 file was changed. Execution results are recorded separately in
[the closure ledger](HR-Closure-20261019.md); preflight alone is not acceptance.

Related primary driver discussion:
[Microsoft.Data.SqlClient LocalDB named-pipe issue](https://github.com/dotnet/SqlClient/issues/1395).
The local observation above, not that historical issue, establishes this target's
actual transport state.

## Scope and status

The user approved local preparation, read-only target checks and offline tests
only. No live host, login/MFA request, private C02 directory/key/settings write,
schema change, migration, database repair or cleanup has been executed.

**Implementation prepared; encrypted-connection preflight BLOCKED. Execution is
not approved or ready. Do not run Execute or weaken the encryption guard.**

## Read-only observations

Existing instance process arguments identify the user's MSSQLLocalDB instance:

- Actual pipe: `np:\\.\pipe\LOCALDB#F9CAED57\tsql\query`.
- Server: `Software-PC1\LOCALDB#F9CAED57`, process 29836.
- Catalog: `Cosmix_Testing`, ONLINE; owner/operator `COSMIXRND\Huma.Khan`.
- Family GUID: `D9C34627-18A2-41CF-A804-821E39516303`.
- Files: `C:\Users\Huma.Khan\Cosmix_Testing.mdf` and
  `C:\Users\Huma.Khan\Cosmix_Testing_log.ldf`, both ONLINE.
- Applied migration count: 25; no migration applied.
- Exact HR/Admin email, known synthetic phone and fixture-name checks matched.
  Both accounts have Active status (3) and web access enabled. Credentials were
  not printed; successful login/password validity is not established.
- Zero enabled non-system triggers, synonyms/external dependencies and pending
  status 0/4 OTP deliveries in the executed metadata checks.

These metadata queries used `sqlcmd -C` without mandatory encryption. They do not
establish that the application's required encrypted connection will succeed.

The full new preflight first failed because sqlcmd does not permit `-h -1` with
`-y 0`. The reader was replaced with a typed System.Data.SqlClient connection,
preserving `Encrypt=True`, exact pipe/catalog and Windows authentication.
It then stopped at connection Open with:

> The instance of SQL Server you attempted to connect to does not support encryption.

A separate bounded read-only `sqlcmd -N -C` probe against the same exact pipe
also failed: ODBC Driver 17 reported encryption not supported on SQL Server.
No encryption downgrade or server/configuration change was made. The exact
underlying TLS/server configuration cause is not established. This is not a
claim that the catalog is missing, nor an instruction to recreate/attach it.
The API's Microsoft.Data.SqlClient path has not been exercised live.

The new full protected-table digest, exact migration-ID comparison, private
source ACL and packet-generation stages were NOT reached. Therefore no final
execution packet is represented as fully verified.

## Prepared isolation

- New opt-in profile `HumaHrAcceptance20261019C02`, independent of C01.
- Proposed private root:
  `C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261019C02`.
  It is still absent; no credentials or keys were generated for it.
- Exact existing database family, process, files, owner, connection and schema
  guards; nonterminal OTP work rejects startup.
- Empty configuration builder, no inherited appsettings/user-secret providers;
  real SMS/face/scanner and business background workers disabled.
- Only `https://localhost:7481`; loopback origin, no alternate listener.
- Additional pre-authentication request boundary permits only CSRF, two-account
  login, local OTP retrieval, OTP verification, permissions and logout.
- One login attempt per approved HR/Admin email and at most 80 requests.
  SuperAdmin login, mobile, refresh/resend/logout-all and every business route
  are unavailable on this test host. Existing authentication/MFA/CSRF still run.
- Local OTP sending additionally rejects phone numbers outside the two approved
  synthetic account bindings when C02 is selected.
- Runner defaults to Inspect. Execute requires a matching reviewed SHA256 of
  frozen runtime inputs, scoped source and private source hashes. It never
  resets, repairs or resumes partial runs automatically.
- Planned before/after protected-table hashes include roles, permissions,
  employees, policies, financial tables and unapproved accounts. No business
  rows are exported. Actual final auth record counts are retained privately.
- C01 files/settings/results are never rewritten. All seven original private
  files matched their initial SHA256 values at the end of this preparation.

## Executed offline verification

Release: `D:/CosmixVerification/hr-backend-20261019/hr-authonly-build01/bin/Cosmix.Api/release`.
Build PASS, zero warnings/errors. The first `--no-restore` attempt failed because
the new artifact directory had no assets file; normal restore/build then passed.
No test project or database migration was created.

| Verification | Result |
| --- | --- |
| Configuration, SQL-identity and schema rejection cases | 102 PASS |
| Synthetic HTTP boundary cases, route/account/host/query/budget denial | 143 PASS |
| Actual service-registration/worker restrictions and ordinary-host preservation | 11 PASS |
| PowerShell parser, synthetic transport allowlist, request budget, snapshot checks | 30 PASS |

**286 offline checks PASS.** These are not authenticated HTTP/SQL acceptance.
The profile test was observed failing against the previous frozen API because
the new guard did not exist, then passing after implementation.

Logs:

- `D:/CosmixVerification/part-c-parent-20261013/managed-d8ead115d6f645a9bac4f1cbc193991f` (expected RED).
- `D:/CosmixVerification/part-c-parent-20261013/managed-57cff2ee40264d40a90627d62b13c223` (guard/boundary PASS).
- `D:/CosmixVerification/part-c-parent-20261013/managed-34171f1478864fd49f8fa1c0760ef856` (composition PASS).
- `VerifyAuthOnlyRunner.ps1`: 30 PASS, synthetic transport only.

End state: C02 root absent, no listener on port 7481, existing SQL instance not
stopped/restarted/modified, zero live auth requests. No full HR acceptance claim.

Independent scoped static review found no concrete code blocker or authentication
bypass in the C02 changes. Two evidence limitations remain: protected-table
assertions currently run before owned-host termination on successful execution,
and are not run on failure; the PowerShell offline suite verifies selected helpers
and parsing, not an instrumented execution of the full top-level script. These
are not claims of completed live preservation/transaction acceptance. The review
did not execute SQL, the host, private reads or repeat the reported tests.

## Integration files

- `src/Cosmix.Api/Development/AuthOnlyTestingGuard.cs`
- `src/Cosmix.Api/Development/AuthOnlyTestingHost.cs`
- `src/Cosmix.Api/Development/AuthOnlyTestingDatabase.cs`
- `src/Cosmix.Api/Development/AuthOnlyTestingBoundary.cs`
- Narrow C02 wiring in existing `LocalTesting.cs` and `Program.cs`; preserve the
  other pre-existing changes in those dirty files.
- `Invoke-HrAuthOnlyC02.ps1` and four `VerifyAuthOnly*.cs/.ps1` verification files
  in this directory (profile, boundary, composition and runner).

Next gate: resolve the required encrypted connection without unauthorized SQL
configuration changes, then rerun the complete read-only preflight. Only after
all checks pass should a final exact execution packet be presented for approval.
