# HR business SQL continuation - 19 October 2026

## Approved boundary

The user approved a bounded first business SQL batch after readonly inspection.
This approval is broader than completed C02 authentication testing, but is NOT
permission to migrate, seed employees/policies, change permissions or execute
vehicle/Finance transactions. C01/C02 guards and private evidence are preserved.

- Existing `Software-PC1\LOCALDB#F9CAED57`, PID 29836, catalog `Cosmix_Testing`.
- Owner/operator `COSMIXRND\Huma.Khan`; family
  `D9C34627-18A2-41CF-A804-821E39516303`; ONLINE, 25 applied migrations.
- Exact existing MDF/LDF files under `C:/Users/Huma.Khan/` stay attached and
  unchanged in configuration. No reset, restore, detach, attach or migration.
- Explicit testing-only local named-pipe transport exception; no production TLS
  claim and no external SQL connection. HTTPS API is only `localhost:7481`.
- Private output root:
  `C:/Users/Huma.Khan/AppData/Local/Cosmix/HrAcceptance/Huma20261019C03/`.
  It must be absent before the first write. Fresh keys/settings/evidence are
  confined there; no permanent appsettings/environment modification or cleanup.
- Only known synthetic `hr@example.invalid` and `admin@example.invalid` logins.
  Normal authentication, local OTP delivery, MFA, CSRF and authorization remain.
- At most 160 HTTP requests. Business writes are one marked
  `TEST-C03-20261019` department, its winning update and inactive status, with
  corresponding organization history, audit and retry receipts.
- Read the bounded Admin leave queue with allowed/denied identities. No leave
  request, employee, role, policy, vehicle or financial record is changed.
- Real face/OTP providers and legal activation remain explicitly skipped.

## Verification approach

Use separate HTTP clients/connections for simultaneous same-key creates and
competing updates. Verify changed-payload conflict, stale version, fresh read,
exact history, inactive retained test row and three atomic audit/history/receipt
links using an independent SQL connection. Hash all other business rows before
and after, plus retained C01/C02 evidence. Repeat preservation checks after the
owned host has stopped. No automatic partial-run reset or retry.

Preparation reads found no C03 department/receipts, no pending OTP work, no enabled
non-system trigger/synonym/external dependency, absent private root and free port.
An initial readonly identity query needed an explicit sql_variant conversion;
the corrected query confirmed identity without any database write.

The separate profile and runner must pass offline guard tests and a frozen
Release build before execution. A recorded packet hash binds runtime and sources.
Execution evidence, failures and remaining boundaries are recorded below once
actually observed; preparation alone is not a successful business test.

## Still outside this batch

Business SQL acceptance for benefit grant/correction/waiver, recovery fulfilment,
vehicle history/maintenance/fuel corrections, outstation and durable export workers
requires bounded fixtures for those specific flows. The department concurrency
test does not establish their transactional behavior or full HR acceptance.

## Executed C03 outcome

Frozen Release `D:/CosmixVerification/hr-backend-20261019/hr-business-c03-build02`
built with zero warnings/errors. Offline profile 119, boundary 1246, composition
11 and generated MVC/OpenAPI 310 checks passed; runner checks 38 passed.
The boundary regression first exposed uncaught `InvalidDataException` during
bounded response capture; the explicit catch now fails closed and clears success
headers. These offline checks are not SQL acceptance.

Packet: `EA9BBB469301DC521F4EAA3462D3C9CC5DD0771808FA4D6602EB68BC26A23A9E`.
Execution made 21 HTTP requests: 11 checks passed, one failed at expected Admin
leave queue success. Both synthetic password/MFA/permissions flows passed, and
HR was denied the Admin queue. The runner stopped before creating any department.
The original runner did not retain the failing HTTP status/body, so its status
must not be invented. Subsequent readonly inspection found a concrete code/data
mismatch: system roles are stored as `ADMIN`, while `LeaveReviewAuthority` only
accepted title-case `Admin`. No permission or role data was altered.

Post-host independent inspection: 155 protected snapshots and 15 C01/C02 files
unchanged; C03 department count zero. Accounts remained 3 and migrations 25.
Sessions 122 -> 124; refresh tokens 183 -> 185; challenges and deliveries each
128 -> 130; rate buckets 45 -> 57; audit events 2530 -> 2540. New audit types were
two web OTP requests, two accepted local deliveries, two created sessions and
four permissions reads. No automatic logout/revocation repair was attempted
after the stop. C03 sessions were not claimed as logged out.

Owned host PID 26756 exited. Existing SQL was not stopped. No reset, cleanup,
migration or business mutation occurred. Private failure/start/preservation/host
evidence remains under the approved C03 root. Department concurrency and full HR
business SQL acceptance did NOT pass in this run.

## Approved continuation

The user separately approved bounded C04 after the failure: same SQL identity,
port and two synthetic identities; one `TEST-C04-20261019` department and at most
160 requests. Only the new private root `Huma20261019C04` may be used. C01 through
C03 private files and historical records are preserved. A separate runner now
retains sanitized HTTP statuses/error codes without response bodies or cookies.
It also verifies the exact existing fixed Admin leave grant before writes.
Execution requires the role fix, regressions, frozen build and all target guards
to pass. This approval still excludes other HR business fixture families.

## 20 October connectivity boundary

On resume the approved SQL PID 29836 no longer existed. `sqllocaldb info
MSSQLLocalDB` reported Stopped and C04 private output remained absent. The user
then explicitly approved starting only this existing instance and readonly checks.

`sqllocaldb start MSSQLLocalDB` succeeded. The resulting named pipe was
`np:\\.\pipe\LOCALDB#D8AD654D\tsql\query`, server
`Software-PC1\LOCALDB#D8AD654D`, PID 18860, Windows operator
`COSMIXRND\Huma.Khan`. Readonly queries of `sys.databases`,
`sys.database_recovery_status` and `sys.master_files` on this exact instance
returned no registration for `Cosmix_Testing`. Consequently its current family,
file ownership and migration history could NOT be verified.

This does not establish deletion of the database or its files. Database work
stopped immediately: no replacement instance/database, attach, detach, restore,
reset, migrations or fixture writes. No C04 host or private artifacts were
created. The original approved target constants were not repinned to a different
database. C04 SQL acceptance remains unexecuted. Before a later run the owner
must make the existing intended database available and its exact identity must
be confirmed; any recovery/attachment or changed binding needs separate approval.
