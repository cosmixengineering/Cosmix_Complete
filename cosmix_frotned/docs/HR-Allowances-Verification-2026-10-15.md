# Allowances UI and request validation

## Scope

Local frontend changes only. No database, account, permission, migration, backend,
or permanent environment changes were made by this work.

## Implemented

- Restrained Allowances layout with compact controls, neutral sections, one action
  accent, accessible focus states, and responsive form/table layouts.
- Eligibility runs on an explicit check for a validated business date instead of
  automatically requesting it on mount and every date edit.
- Allowances uses the existing finance error-code messages. Missing effective
  employment and overlapping history are no longer described as a stale edit.
  The live 409 response code still needs confirmation; a status alone does not
  establish which business prerequisite is missing.
- Claim submission requires the selected date/kind's successful server eligibility,
  published policy reference, amount/currency/key, no existing claim, and valid
  reason. Missing data is never converted into an allowance amount.
- Date changes invalidate eligibility presentation. In-flight checks disable submit.
- Finance queries wait for an authenticated session. Review filters reject partial
  employee IDs and invalid date ranges locally; disabled queries do not show an
  indefinite loading state for those invalid filters.
- Attendance Processing now supplies the required leave queue date bounds and no
  longer requests shifts solely because a session exists without the read permission.

## Executed checks

- `npm run build`: passed, including TypeScript. Existing large-chunk warning remains.
- `npm run verify`: lint passed; formatting stopped on existing unrelated changes in
  `AdminLayout.tsx`, `EmployeeHiringPage.tsx`, `HiringApprovalsPage.tsx`, and
  `admin-dashboard.css`. These files were not reformatted by this work.
- 19 standalone in-memory helper checks passed: valid/invalid dates, missing source,
  wrong date/kind, missing policy/amount/currency/key, duplicate/ineligible claims,
  and Pakistan business-date midnight boundaries. No test files or fixtures written.
- Actual authenticated browser acceptance and desktop/mobile screenshots remain
  unverified: the browser is at sign-in with a connection error.

## Backend/debugger observations

- User supplied cancellations from `ValidateSessionCoreAsync`, at session lookup
  and account lookup. `AuthenticationExtensions` passes `RequestAborted` through
  to these EF queries. Navigation/disconnect or the client's 30-second request
  timeout can cancel them; the stack trace alone does not identify the trigger.
- Read-only liveness probe to `https://localhost:7240/health/live` timed out at
  eight seconds with HTTP 000. This endpoint excludes database health checks.
  Port 7240 was listening under `Cosmix.Api`, PID 26132.
- User confirmed Visual Studio stops again on this exception after Continue.
  Resume the debugger after reviewing the specific TaskCanceledException break
  setting, then recheck liveness and normal authenticated requests. Changing a
  debugger break setting is not proof that an underlying timeout is fixed.
- No cancellation catch, auth bypass, timeout increase, SQL reset, or fake success
  response was added.

## Remaining verification

- Confirm healthy responsive API, then exercise normal login/MFA and session restore.
- Check Allowances initial/check/error/eligible/duplicate states at desktop and mobile
  sizes, including dates and review filters. Confirm the live 409 business code.
- Inspect denied hiring/leave review with the correct authorized account. Backend
  review operations enforce Admin authority as well as permissions; do not grant
  extra access merely to remove a 403 from the console.
- Investigate the reported employee 404 against its actual authorized record. No
  synthetic employee fallback or successful response should replace a missing record.
- These changes do not establish full Attendance, payroll, provider, or SQL acceptance.
