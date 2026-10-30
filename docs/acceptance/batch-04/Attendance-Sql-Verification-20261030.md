# Attendance SQL verification — 30 October 2026

## Result and scope

**One complete SQL integration scenario passed; zero failures/skips.** Fresh isolated Release build passed with zero warnings/errors. The final test took approximately 3 minutes 7 seconds, including a fresh database, all migrations, synthetic provisioning, scenario execution and verified cleanup.

This extends the earlier 76 focused service checks and 677 broader non-SQL regressions with actual SQL persistence. It does **not** establish camera/liveness accuracy, production SMS login, HTTP middleware or real employee/physical-phone acceptance.

## Production components exercised

- `AttendanceCaptureService`, `LocationConsentService`, production `MobileAuthenticationService.ValidateSessionAsync`.
- Real authentication repository, role/scope reader, `AccessManagementRepository` transaction/application-lock boundary and scheduling journal.
- Runtime employee/site roster and staff-taxonomy resolution, SQL membership/shift/geofence repositories.
- Capture-policy reader with protected evidence, validation evaluator and SQL final evidence.
- Raw punch, assessment, work-session, history/pagination repositories; durable notification outbox.
- Actual AES-GCM private receipt/audit storage, exact retry replay across fresh DbContext instances.

Only initial hiring/account/device provisioning, fixed test time/current authenticated identity, and an explicitly synthetic preverified biometric proof were fixtures. No OTP was requested or delivered. The provider adapter refuses enrollment/verification calls, and no real biometric image/template was used.

## Scenario verified

1. Fresh, uniquely named LocalDB database upgraded through every current migration; no pending migrations/model changes.
2. Persisted account/device/session and scoped permissions pass production mobile session validation.
3. Missing GPS consent blocks camera preflight without attendance writes. Actual consent service saves one consent; exact reacceptance is idempotent.
4. Direct submission with an unavailable consent repository fails closed. Out-of-radius preflight also fails without attendance writes.
5. In-radius preflight permits camera guidance without creating attendance.
6. Fabricated proof submission is retained as rejected, creates no work session and does not consume the verified fixture proof. Its exception notification is staged in the SQL outbox.
7. Two simultaneous requests for the same valid check-in use independent DbContexts and produce identical responses with one accepted punch/session/receipt. A further exact retry also creates no duplicate.
8. Reusing that key with different coordinates returns `idempotency_key_reused` and retains the original rows.
9. Radius-only checkout closes the same work session. Exact retry creates no duplicate. Fresh history queries return the recorded punches with correct pagination and a 480-minute closed work session.
10. SQL counts reconcile: **3 raw punches, 3 assessments, 3 final validation records, 2 work-session revisions, 3 operation receipts**. One punch is the deliberately rejected proof, two are accepted check-in/out. Three submit audits are attributed to the authenticated employee/account; protected audit payloads exclude coordinates and the proof secret.
11. Publishing a new notice requires fresh employee consent before further GPS submission. Device revocation invalidates the persisted mobile session and attendance access.
12. The owned database was dropped and `DB_ID` verified absent. A separate master query confirmed zero remaining `Cosmix_Attendance_CI_%` databases after execution.

## Execution and evidence

Source:
- `cosmix-backend/tests/Cosmix.Tests/AttendanceSqlFixture.cs`
- `cosmix-backend/tests/Cosmix.Tests/AttendanceSqlIntegrationTests.cs`

Opt-in execution requires both variables. No external/business database is accepted:

```powershell
$env:COSMIX_ATTENDANCE_SQL_CONNECTION = 'Server=(localdb)\CosmixUat_d893653346;Database=master;Integrated Security=true;Encrypt=false'
$env:COSMIX_ATTENDANCE_SQL_WORKDIR = 'D:/CosmixReleases/product-readiness-20261030/attendance-sql'
dotnet test tests/Cosmix.Tests/Cosmix.Tests.csproj --configuration Release --filter FullyQualifiedName~AttendanceSqlIntegrationTests
```

The fixture requires integrated LocalDB/master, rejects `AttachDBFilename`, generates its own database name/files, and drops only its owned database. Private artifacts receive an operator-only ACL. Initial fixture attempts exposed artifact ACL provisioning issues before creating a database; the corrected fixture preserves the existing owner and sets only its newly owned directory's ACL.

Final evidence outside Git:
- `D:/CosmixReleases/product-readiness-20261030/attendance-sql.trx`
- `D:/CosmixReleases/product-readiness-20261030/attendance-sql.log`
- `D:/CosmixReleases/product-readiness-20261030/attendance-sql-build.log`
- `D:/CosmixReleases/product-readiness-20261030/attendance-sql/Cosmix_Attendance_CI_99fe8a862584413489e407c4d6ecf47b/result.json`
- Same owned directory: `owned-database-cleanup.json` confirms `OwnedDatabaseDropped: true`.

Private encrypted objects and their synthetic key remain in the restricted artifact directory for local inspection. Do not publish the private directory as a deployment or CI artifact. No migration was applied to the company database, and no company employee, attendance, payment or approval was created.
