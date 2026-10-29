# Web fixes and verification — 2026-10-28

## Delivered changes

- Local testing OTP uses the exact browser-bound challenge. Removed latest-challenge substitution and brute-force recovery in the development backend helper. The web helper fetches once per action, handles expiry/cancellation and malformed responses, and fills the normal MFA form. No automatic authentication bypass or embedded-password sign-in.
- Session-refresh uncertainty blocks repeated refresh in development as well as production. Manual refetch also enforces authentication and permission gates.
- HR/Admin dashboard uses actual employee counts and explicitly unavailable attendance/salary results. KPI cards retain a compact 78px height; detailed missing-attendance notices appear below the row.
- Attendance and Leave management entry screens use existing backend workflows instead of fabricated summaries, staff balances or successful mock actions. Employee selection uses the directory API when authorized and retains the selected name.
- HR daily/calendar reads support explicit missing locked history. Valid dates remain visible, unavailable totals are not shown as zero, and missing days cannot trigger recalculation/history reads through the UI.
- Allowance summary respects the API's `hasMore` contract instead of inventing a total count. Vehicle refresh respects permissions; hardcoded fuel rates are no longer mounted. Site maps require actual boundary data instead of invented coordinates.
- Admin module navigation no longer presents disconnected ERP mock records as completed workflows. Notification routing reaches the existing page.

## Executed checks

| Check                                                       | Result and limit                                                                                                                    |
| ----------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| TypeScript + Vite production build after Attendance changes | Passed. Main bundle remains large (about 1.45 MB minified); optimization is still open.                                             |
| Targeted ESLint for changed Attendance files                | Passed. Earlier targeted OTP/dashboard/allowance lint also passed.                                                                  |
| Auth transport, in-memory execution of actual source        | Passed: refresh uncertainty, no write replay, abort handling, origin checks.                                                        |
| OTP helper, in-memory component harness                     | Passed: six digits including leading zero, one fetch, expiry/abort, invalid data, development-only visibility.                      |
| Attendance read schema and date helpers                     | Passed: missing/null contract, rejection of invented values, 31-day bound, calendar-month boundaries.                               |
| Backend Release build                                       | Passed, zero warnings/errors.                                                                                                       |
| Existing backend Web Authentication standalone verifier     | 93 checks passed; synthetic adapters, not SQL acceptance.                                                                           |
| Existing backend Attendance Summary standalone verifier     | 70 checks passed; synthetic adapters, not SQL acceptance.                                                                           |
| Live local OTP and normal HR MFA login                      | Passed; HR dashboard reached through normal Verify and sign in. No secrets retained here.                                           |
| Live HR dashboard and directory                             | Actual counts loaded; employee search returned API results.                                                                         |
| Live dashboard card dimensions                              | All four 78px during loading and API failure. Final loaded-data transition recheck remained interrupted by the API/debugger outage. |

## Attendance live check

The user-authorized backend was started on HTTPS localhost:7240 against the same verified existing `Cosmix_Testing` database. A separate private process settings copy references the current LocalDB pipe; the original settings file was preserved. The existing 30 applied migrations were inspected, not applied by this work.

Live authenticated recheck of the originally failing employee's October calendar succeeded: all 31 dates rendered with the explicit missing-locked-summary warning. No invented totals, unlocked history or recalculation was introduced.

The Attendance register now has a compact employee picker, month toolbar, readable table and daily facts. A separate dynamic monthly matrix/daily-log page is available under **Attendance Tracking** in the sidebar. Live directory pagination (10 + 2 approved staff), name search, date selection and day-detail display passed. Its actual-source harness passed 25 checks; see [Attendance tracking](Attendance-Tracking.md) for contracts and limits.

The full frontend `npm run verify` passed after integration. The main bundle warning remains (approximately 1.49 MB minified).

## Attendance employee selection and assignment setup follow-up

This bounded frontend change excludes unapproved hiring drafts from attendance
selection while preserving approved former and archived staff for historical
review. The backend's `EmployeeListRequest.Status` is `OnboardingStatus`:
`status=2` means Approved, not employment status Active. Directory queries use
`status=2&includeArchived=true`; they impose no Active-only restriction.

Exactly seven attendance source files were changed in this follow-up:

| File under `src/modules/attendance/`           | Change                                                                                                                                                                                                                                                                                                                                                         |
| ---------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `operations/review/ReviewUi.tsx`               | Opt-in approved directory lookup for attendance, guarded manual GUID selection and initial deep-link selection using existing grants. Other workspaces retain their existing selection policy.                                                                                                                                                                 |
| `schemas/employee-selection.ts` (new)          | Named approved directory filter, response validation requiring onboarding approval, and detection of missing assignment/schedule setup.                                                                                                                                                                                                                        |
| `components/AttendanceEmployeeSetup.tsx` (new) | Checks the existing `current/basic` endpoint before attendance requests when HR directory permission is available. That endpoint validates retained approval and submission evidence without requiring identity permission or current Active status. Attendance-only scoped users retain server-authorized access. Provides hiring and employment setup links. |
| `operations/review/AttendancePages.tsx`        | Register/calendar/daily setup labels and actionable notices; recalculation is disabled for a response requiring assignment/schedule setup.                                                                                                                                                                                                                     |
| `pages/EmployeeAttendanceDetailPage.tsx`       | Approved selection guard, monthly setup notice and explicit setup labels.                                                                                                                                                                                                                                                                                      |
| `pages/AttendanceTrackingPage.tsx`             | Approved historical directory filter, setup legend/notices and exclusion of setup dates from present/late classification counts.                                                                                                                                                                                                                               |
| `api/tracking-api.ts`                          | Validates approved directory rows, retains relevant backend error codes and labels setup dates `SET` rather than present or absent.                                                                                                                                                                                                                            |

Unapproved selections link to the existing employee hiring route. Missing setup
links to the employee's Employment & Org section, adding `edit=1` to open the
existing change form when the user has `hr.employees.changes.request`. No identity,
approval, assignment or attendance data is fabricated or automatically saved.

The existing attendance JSON schemas already accept
`policy.status='schedule_configuration_required'`,
`roster.status='employee_assignment_unavailable'`,
`factualStatus='no_accepted_session_evidence'` and null policy amounts/day status.
No schema change or new endpoint was needed for that response. Session facts
remain factual; missing configuration does not become absence, presence or zero
pay. Existing frozen missing-summary handling is preserved.

### Follow-up verification results

| Check                                                    | Exact result and scope                                                                                                                                                                                                                                                                                                                                                                                                                          |
| -------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Final `npm run verify`                                   | Exit 0: repository-wide ESLint with `--max-warnings 0`, Prettier check, TypeScript build and Vite production build passed. Vite transformed 2,491 modules. Main JavaScript bundle: 1,473.64 kB minified / 369.27 kB gzip. Existing warning for chunks over 500 kB remains.                                                                                                                                                                      |
| Targeted ESLint on all seven source files                | Exit 0 with `--max-warnings 0`.                                                                                                                                                                                                                                                                                                                                                                                                                 |
| Formatting                                               | Final repository-wide Prettier check passed; explicit check of `AttendanceEmployeeSetup.tsx` also exited 0.                                                                                                                                                                                                                                                                                                                                     |
| `git diff --check -- src/modules/attendance`             | Exit 0.                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| In-memory execution of actual schemas and status helpers | Exit 0. Rejected Draft, PendingApproval, Returned and Rejected onboarding states; accepted approved Active, Suspended, Resigned, Terminated and Retired records, including archived records. Verified day/calendar setup response acceptance; empty, open and worked session evidence remained setup; null payable time displayed Unavailable. Calculated present/late/absent, future dates and frozen missing-summary behavior were preserved. |

An intermediate verification run found an optional-scope initializer TypeScript
error. It was corrected to `Scope | undefined`; the final full verification above
passed. No automated test files were added, so these in-memory checks do not add
persistent regression coverage.

Browser acceptance of this follow-up remains unverified. The attempted visit to
`https://localhost:5173/hr/attendance` initially returned
`net::ERR_CONNECTION_REFUSED`. The parent subsequently reported starting the
frontend on port 5173 and seeing an HR biometrics tab in CUA inventory, but binding
that tab failed with `browser.user unavailable`. That later limitation is
parent-reported; no further browser retry was performed, as requested. Earlier
live checks elsewhere in this report do not establish acceptance of these new
selection/setup changes.

This follow-up made no backend, authentication, database or Git writes. The
backend missing-assignment handling and separately labelled demo integration are
owned by other agents and are not claimed as verified by this frontend pass.

## Limits

This is a record of specific fixes, not certification that all web screens or the whole HR backend are production-ready. All approval/write forms, exports, roles and mobile clients have not been exercised end-to-end in this frontend pass. Real SMS, biometric/device acceptance and legal activation are outside this verification. No migrations, business fixtures or historical repairs were applied.
