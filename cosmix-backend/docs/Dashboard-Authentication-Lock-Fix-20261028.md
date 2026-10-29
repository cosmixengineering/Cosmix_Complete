# Dashboard authentication and locked-summary fixes

The reported `access_temporarily_busy` is the bounded SQL application-lock failure (10 seconds). The HR dashboard launches nine reads concurrently. Previously each acquired the same exclusive authentication lock, including attendance calculations and salary-history aggregation. A subsequent screenshot also showed the debugger stopped inside a dashboard attendance calculation on `locked_attendance_summary_unavailable`; a paused transaction can prolong blocking of other requests.

## Changes

- Explicitly selected dashboard directory, submission queue, sites, leave queue, payroll summary, current-permission and notification reads now share the existing transaction-owned authentication lock.
- Attendance dashboard reports use an Update application lock: reports that read audit ranges serialize with one another while remaining compatible with ordinary shared reads.
- Writes, revocations, face enrollment/capture and all unselected operations retain the exclusive boundary. Serializable isolation, permission checks, scoped queries and audit persistence remain. Shared/Update read transactions reject tracked business mutations at every save and before commit; only new audit rows may be written. No timeout increase or automatic replay of business actions.
- Dashboard attendance reports classify missing **or ambiguous** locked summary membership as unavailable/review-required. They do not substitute mutable attendance or infer absence. Available and unavailable populations are counted separately. Other authorization and integrity error codes propagate. The repository exposes a non-throwing coverage lookup for this dashboard path; its strict `LatestAsync` still rejects unavailable locked evidence.
- Web schema validates partial coverage; the turnout card and CSV show review-required coverage and no longer substitute an employee count or claim live coverage when attendance cannot load.

## Verification

- Before the concurrency fix, the SQL parallel-read regression failed while waiting behind an exclusive read lock.
- Five SQL tests cover overlapping reads, exclusive writer blocking, serialized audit-range reports using the real WorkTreatmentRepository, rollback after audit save, intermediate business-save rejection, unsaved mutation rejection, and transaction cleanup (some tests cover multiple behaviors).
- Before the availability fix, its regression reproduced the exact `locked_attendance_summary_unavailable` exception.
- Five dashboard cases cover partial/unavailable evidence, mixed available/missing records and propagation of other authorization/integrity failures.
- Final backend regression: **494 passed, 0 failed, 0 skipped**. Tests used explicitly selected disposable `Cosmix_Payroll_20261028`, not the business database. Legacy acceptance execution and the already-populated synthetic month scenario were excluded.
- Release compilation succeeded with zero warnings/errors. Full Release dependency build preceded the final application-only availability change; affected application and test projects were rebuilt afterward.
- Evidence logs: `D:/CosmixPayrollVerification20261028/access-red.log`, `access-final.log`, `dashboard-red.log`, `dashboard-final-build.log`, `dashboard-final-tests.log`.
- Web lint and five executable schema checks passed (partial/all-unavailable coverage, invalid totals, misleading complete coverage and old complete-response compatibility). Changed-file formatting passed. Whole-repository verification stops on pre-existing formatting differences in nine unrelated files. Separate production web build is recorded in `dashboard-web-build.log`.

## Runtime limits

### Follow-up: debugger pause after the first fix

The first dashboard fix caught `locked_attendance_summary_unavailable` after the repository had thrown it. A subsequent SQL regression with a FirstChanceException observer reproduced three exception notifications on the real repository-to-dashboard path even though the dashboard returned partial coverage. That was insufficient to eliminate a debugger stop at the throw site.

The dashboard now reads `AttendanceSummaryLookup` and receives a nullable day result for unavailable locked evidence. It no longer calls the throwing strict lookup or catches this exception as expected control flow. Valid frozen records continue through the existing identity/fingerprint checks; missing private proof also returns unavailable. Strict consumers continue rejecting incomplete locked data. `DashboardSummarySqlTests` exercises a real SQL locked period without employee membership, observes exceptions during the complete dashboard read, then independently verifies the strict reader still rejects it. The fixture is rolled back.

Follow-up evidence: `D:/CosmixPayrollVerification20261028/dashboard-no-throw-red.log` and `dashboard-no-throw-green.log`.

Follow-up Release build and regression run passed: **495 passed, 0 failed, 0 skipped**. The SQL test verified zero first-chance missing-summary exceptions on the dashboard path while the strict reader still rejected the same missing membership. This is executable SQL/service verification, not a claim of a completed interactive Visual Studio login retest.

The user's Visual Studio debugger must restart/rebuild the API to load the changes. No business records, locked periods, employee roles, credentials or database migrations were changed. Actual missing/ambiguous frozen records still require HR review; these changes make their dashboard coverage explicit, not repaired or approved. Long reports/writers can still reach the unchanged bounded lock timeout under contention. The original live SQL lock holder was not captured, so no claim is made to identify its exact transaction.
