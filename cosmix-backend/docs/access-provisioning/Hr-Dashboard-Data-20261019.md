# HR dashboard attendance and payroll

The requested dashboard reads attendance and salary data through two current-web-session,
audited endpoints. Neither endpoint creates business records or changes access grants.

- `GET /api/v1/hr/dashboard/attendance`: All-scope `attendance.reports.read`.
  The denominator is approved, nonarchived, joined, effectively active employment.
  Accepted attendance evidence means checked in; missing evidence does not mean absent.
  Current daily policy classification and saved-summary coverage remain separate.
- `GET /api/v1/hr/dashboard/payroll`: All-scope
  `hr.employees.compensation.read` and `attendance.payroll-handoff.read`.
  Effective monthly gross compensation is grouped by currency. Missing, nonmonthly
  and future compensation are reported separately. Frozen handoff components for
  the exact current month are separate from base salary and never prove payment.

Both responses capture the server's Karachi business date and the same approved-active
population rules. Existing compensation and attendance calculations remain authoritative.
No migrations, currency conversions, salary proration, new permissions or sample payroll
data are required. Reads fail explicitly when authorization or source evidence fails.

Implementation is split between the two backend summaries and typed HR frontend queries.
Verification uses standalone behavior checks and the existing synthetic HR account in
real Chrome, including cookie/CSRF login, OTP, dashboard API responses, refresh, exports,
failure/retry and logout. Credentials and OTP values stay out of artifacts and output.

Baseline: authenticated HR directory reads returned 24 employee records, 12 approved
onboarding profiles, 12 drafts and 5 sites. Both new summary routes returned HTTP 404
before implementation. Current persisted attendance sessions and monthly handoff batches
were empty; the two recorded salary versions start in 2029 and 2030, so they must not
inflate the October 2026 salary total.

Live testing also found `employee_assignment_unavailable` for approved profiles without
effective attendance assignments. A dashboard-specific trusted daily reader preserves
validated accepted-session facts and leaves classification explicitly unconfigured for
that exact missing-assignment case. Existing strict daily reads, locked protected evidence,
other assignment conflicts and source-integrity failures retain their previous behavior.

Executed checks:

- `VerifyHrDashboardAttendance.cs`: 36 standalone behavior checks passed, including
  accepted open/closed evidence, missing assignments, strict existing reads, locked
  missing evidence, lifecycle/approval provenance, authorization and cancellation.
- `VerifyHrDashboardPayroll.cs`: 28 standalone behavior/authorization checks passed,
  including currency groups, missing/future/zero salaries, salary gaps, nonmonthly
  wages, signed frozen handoff amounts and denied access before source discovery.
- Final solution Release build: 0 warnings, 0 errors, using isolated outputs under
  ignored `artifacts/dashboard-live-build` to avoid concurrent checkout build locks.
- Authenticated HTTP against normal `https://localhost:7240`: 52 live checks passed.
  Both summaries returned HTTP 200 (attendance 266 ms, payroll 181 ms in that run).
  Verified guest denial, wrong OTP, successful HR MFA login, current permissions,
  cookie protections, refresh, source counts, business dates, no-store and logout.
- Frontend full ESLint, production type/build, scoped Prettier and 13 temporary
  contract/state/export checks passed. `npm run verify` stopped at the existing
  formatting issues in `AdminLayout.tsx`, `EmployeeHiringPage.tsx`,
  `HiringApprovalsPage.tsx` and `admin-dashboard.css`. Those unrelated changes were
  preserved. The existing large-bundle advisory remains.
- The direct source verification observed 24 employees, 2 salary versions, 0 handoff
  batches/lines, 0 accepted sessions and 5 access-scope rows. A subsequent new draft
  brought the live directory to 25 employees (12 approved, 13 drafts). Browser checks
  compare the visible counts against fresh API responses rather than a fixed fixture
  count. No business data, grants or migrations were changed by this dashboard addition.

The real dataset therefore shows 0 checked in out of 12 verified active employees,
12 awaiting attendance classification, 12 missing current salaries, 2 scheduled
future salary versions, and no current active-employee handoff components. Positive
attendance and monetary behavior is covered by standalone synthetic checks; no live
employee attendance or salary records were fabricated to make a positive dashboard.

Final real Chrome verification passed directly against `https://localhost:5173`
and `https://localhost:7240`, without redirecting requests to a diagnostic backend:

- HR password + issued local OTP login; actual attendance/payroll JSON agrees with UI.
- Authenticated reload fetched fresh summaries; directory counts matched fresh API reads.
- Ready CSV contains actual dates, 0/12 turnout, salary coverage and 2 future exclusions.
- Attendance-only HTTP 503 in the isolated test browser leaves directory/payroll intact;
  error CSV does not invent attendance zeros; Retry restores the actual 0/12.
- Payroll-only HTTP 503 leaves directory/attendance intact; Retry restores salary coverage.
- No JavaScript application errors; verification session signed out successfully.

Ignored local evidence: `artifacts/browser-qa/dashboard-real-verified.png`,
`dashboard-real-api.json`, `dashboard-real-summary.csv`, and the failure-state CSV.
The normal local backend was restarted with the verified Release build and remains
listening on HTTPS port 7240. The extra diagnostic port 7241 is stopped. HR's existing
Admin-only hiring/leave queue restrictions remain enforced; those counters do not
become fabricated zero approvals.
