# C04 authenticated business acceptance - 20 October 2026

## Actual outcome

**PASS: 28/28 checks, 35 actual HTTP requests.** Runner exit 0.
This is bounded authentication, leave-queue authorization and department
concurrency acceptance, not full HR/Attendance/vehicle acceptance.
No migrations, database recovery, cleanup, existing employee/role/policy changes,
real SMS, face calls, legal activation or payment operations were performed.

## Target diagnosis and authorization

The user's SSMS screenshot showed `Cosmix_Testing` on `(localdb)\MSSQLLocalDB`.
The Codex process's `sqllocaldb info MSSQLLocalDB` still reported Stopped, but
read-only process inspection found a separately running user-session SQL process
with the normal user's LocalDB master path. Direct named-pipe SQL verified the
original database, without starting or attaching anything.

This explains the different LocalDB views observed by the packaged Codex process
and SSMS. It does not establish deletion or explain an earlier transient startup
failure.

- SQL server: `Software-PC1\LOCALDB#9F3001F7`, PID `25768`.
- Pipe: `np:\\.\pipe\LOCALDB#9F3001F7\tsql\query`.
- Database: `Cosmix_Testing`, ONLINE, MULTI_USER, writable.
- Windows operator and database owner: `COSMIXRND\Huma.Khan`.
- Family: `D9C34627-18A2-41CF-A804-821E39516303`.
- MDF: `C:\Users\Huma.Khan\Cosmix_Testing.mdf`.
- LDF: `C:\Users\Huma.Khan\Cosmix_Testing_log.ldf`.
- 25 exact applied migrations; latest:
  `20261015144424_AddFinancialAdjustmentsAndOutstationDeployments`.
- Named-pipe transport reported `encrypt_option=FALSE`; the explicitly approved
  local-only exception applies to this bounded test, not production transport.

The user explicitly approved the same C04 scope on this verified new pipe.
Only the C04 runner and selected C04 host identity were rebound. C03's original
pipe/PID, historical records and files were preserved. No fallback or unpinned
connection was added.

## Preflight and frozen code

Release build07: zero warnings, zero errors.
`D:/CosmixVerification/hr-backend-20261020/hr-c04-rebind-build07/bin/Cosmix.Api/release`.

The new connection test first failed against old build06, proving the old host
refused the new target:
`D:/CosmixVerification/part-c-parent-20261013/managed-f519a52444eb496cb69a93fbaf296442/`.
After the approved pin update, C04 checks passed 268/268 and retained C03 checks
passed 119/119. Runner parser/allowlist/redaction checks passed 43/43.
These are offline tests, not part of the 28 HTTP/SQL checks.
Final offline manifest:
`D:/CosmixVerification/part-c-parent-20261013/managed-566fc788ac4c4d62ae58e2b64acd8e01/`.

Read-only Inspect and pre-write Execute checks both verified the exact target,
account bindings, schema, private-path absence, free port 7481, 155 protected
snapshots and 24 retained C01-C03 files.
Reviewed packet SHA256:
`5ADD3805A382AFEAFE4A31BFC2753AF698DC2E5F785E3B3A39D9B19964B92203`.

## Executed checks

- Synthetic HR and Admin: anonymous and pre-MFA access denied, actual local OTP
  challenge verified, authenticated permission reads succeeded.
- HR denied Admin leave queue; explicitly authorized Admin allowed.
- Missing-CSRF business request rejected.
- Two concurrent same-key HTTP creates returned the same department.
- Same key with changed payload rejected.
- Competing HTTP updates using the same version produced one success and one
  conflict; subsequent read returned the persisted winner.
- History contained only create and winning update before deactivation.
- `TEST-C04-20261019` retained inactive; SQL independently confirmed one row.
- Exactly three business changes had matching immutable history, audits and
  completed idempotency receipts.
- Both accounts: missing-CSRF logout denied, valid logout succeeded, retained
  revoked cookies denied.
- No migration applied.

## Record counts and preservation

| Record family | Before | After | Delta |
| --- | ---: | ---: | ---: |
| Accounts | 3 | 3 | 0 |
| Migrations | 25 | 25 | 0 |
| Sessions | 124 | 126 | 2 |
| Refresh tokens | 185 | 187 | 2 |
| OTP challenges | 130 | 132 | 2 |
| OTP deliveries | 130 | 132 | 2 |
| Rate buckets | 57 | 69 | 12 |
| Audit events | 2540 | 2558 | 18 |

One marked department, three organization history records and three associated
business receipts were retained. Existing business data was not adopted as a
fixture. All 155 protected snapshots and 24 prior private files matched both
before host shutdown and after shutdown. No reset or cleanup was used.

Owned HTTPS host PID `26136` exited. Port 7481 had no listener after completion.
The existing SQL process was not stopped. C04 evidence and fresh private keys
remain under the approved private directory:
`C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261019C04\`.
Redacted `results/acceptance.json`, `host-final.json` and
`post-host-preservation.json` record the outcome. Credentials/cookies/keys are
not copied into this report.

## Acceptance limit and next work

### Separate build07 HR regression run

Eleven existing standalone suites passed against the same frozen build07, with
zero suite failures. Nine counted suites total **2,264 checks/scenarios**:
benefit workflow 624, recovery fulfilment 38, recovery work claims 132,
outstation 71, report queries 250, report results 204, remaining reports 422,
XLSX 81 and durable exports 442. Export route-surface and PDF-layout suites also
passed without numeric assertion counts. All 168 input hashes remained unchanged.
Four existing PdfPig CS1701 standalone compilation warnings were retained; no
product warnings or guard weakening. These are not additional SQL tests.

Exact per-suite evidence:
`D:/CosmixVerification/hr-backend-20261020/hr-build07-offline-a656c161fedf41089c5c5ac6c21f0f32/REPORT.md`.

### Remaining live prerequisites

This run closes the previously blocked C04 test. It does not exercise benefit
award/use/correction, recovery fulfilment, vehicle/outstation financial effects,
report workers or their encrypted-storage/concurrent SQL transitions.
Those need separately bounded synthetic fixtures, actual authenticated
executions and independent persistence checks. C04 must not be rerun or reset.
Face, real OTP and legal/provider acceptance remain excluded by user instruction.

Additional read-only inspection of the same target found the supported-code
permission `attendance.leaves.benefits.review` absent from the database's
permission catalog. `LeaveBenefitService.RequireAdmin` requires that explicit
permission on a fixed ADMIN assignment. The normal role-grant API refuses
system-role mutation, so adding a custom role alone is not an equivalent
provisioning path. No permission/assignment was silently added, and the
authorization requirement was not weakened.

Row-count-only inspection found no existing leave requests/ledger/recovery,
outstation deployment, vehicle/custody/trip/mileage/service or incident fixtures
in the inspected tables. Five LeaveTypes exist; they were not adopted or
modified as synthetic policies. Positive workflow acceptance requires a reviewed
bounded synthetic graph and authorized test permission provisioning, not a retry
of the C04 department runner or use of arbitrary existing employees.
