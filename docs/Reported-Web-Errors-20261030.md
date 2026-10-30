# Reported web errors — 30 October 2026

## Latest live recheck

Rechecked after the user's follow-up quoting the earlier unverified status. The existing fixes are active in the running server:

- The exact previously failing `EmployeeDetailPage.tsx?t=1793344259696` URL now returns HTTP 200, 264,672 characters and the named export. HR index, dashboard, approvals and app-route transforms also return nonempty HTTP 200 JavaScript. This verifies the served modules; it does not claim to have inspected the user's Chrome console or cookies.
- Normal synthetic test login: 202; guarded test OTP verification: 200; authenticated permissions: 200; refresh: 200; logout: 204. No credentials, OTPs or cookies were printed. Existing expired/anonymous browser sessions still need a real sign-in.
- September payroll preview: 409 `payroll_handoff_month_not_locked`, confirmed from the actual response. This is an unfinished month-close prerequisite, not a module-loading failure.
- Selected employee enrollment devices: 409 `face_employee_ineligible`. A read-only check against the running local `Cosmix_Testing` configuration confirmed one employee row and **zero corresponding user-account rows** for the reported employee. Account setup/activation is required; no account or approval was fabricated. The employee-profile read also returned 404, so it must not be assumed this record has a complete HR profile just because its base employee row exists.
- October attendance preview: 200 with `not_ready`, 392 issues, approximately 6.7 seconds. It no longer returns the reported 503.

New evidence: `D:/CosmixReleases/product-readiness-20261030/reported-errors-recheck.log`. All diagnostic sessions were logged out. No further source-code defect was reproduced in these reported endpoints, and no additional code change was necessary for this recheck. The development URL is `https://localhost:5173`; the React DevTools suggestion is informational.

## Reproduction and fixes

- The exact failing Vite URL for `EmployeeDetailPage.tsx?t=1793344259696` returned HTTP 200 but only 183 characters of source-map content, with no export. The source file contained the named export. Added file-write stabilization to the development watcher; its config restart cleared the stale transform. That same URL now returns the complete module and named export. The HR barrel, Admin dashboard, Admin approvals and route modules also return nonempty transformed JavaScript with HTTP 200.
- October month-close preview initially returned HTTP 503 `staff_taxonomy_category_not_configured`. Missing staff mapping is now retained as a blocking, employee/date-specific readiness issue in both daily and financial inspection. Only explicitly recognized setup failures are caught; denied access, invalid evidence, ambiguous mappings and infrastructure errors still propagate.
- A subsequent live test exposed HTTP 409 `policy_calculation_not_ready`: an unclassified saved summary was being projected into a lock entry requiring a policy ID. Such rows now remain excluded from the lockable rows while their readiness issues are retained. No salary or policy is fabricated.
- The payroll screen previously treated every 409 as an unlocked month. It now checks the exact `payroll_handoff_month_not_locked` code.
- Face registration now uses the structured API error messages rather than searching the generic Error string for status/code. Camera eligibility also requires a successful, completed device query, so stale device data cannot enable capture after a failed refresh.
- Attendance error rendering preserves actionable configuration messages and support references instead of replacing all server errors with a generic connectivity warning. Month-close setup issues have readable descriptions.

## Live results

Verified through the trusted HTTPS Vite proxy at `https://localhost:5173`, using the existing synthetic HR account, normal password/challenge/OTP/session endpoints and the existing guarded local OTP inbox. Credentials, OTPs and cookies were not logged. The diagnostic session was logged out.

| Check | Result |
| --- | --- |
| Login / OTP verification | 202 / 200 |
| Authenticated permissions | 200 |
| Session refresh | 200 |
| Anonymous permissions | Correctly denied, 401 |
| October month-close preview | 200, `not_ready`, `ready: false`, 392 issues, approximately 10.9 seconds |
| September payroll preview | Correctly blocked, 409 `payroll_handoff_month_not_locked` |
| Selected test employee's enrollment devices | Correctly blocked, 409 `face_employee_ineligible` |
| Logout | 204 |

The two remaining 409 responses are actual workflow prerequisites. This work did not approve employment, activate an account/device, publish company policies, finalize an attendance month or change salary records. The initial 401 sequence can occur when restoring an anonymous/expired session; a valid login and refresh were verified. No claim is made that the user's old browser cookies were repaired.

## Verification evidence

- Backend Release build succeeded; targeted month-close tests: 40 passed.
- Backend non-SQL regression suite: 642 passed, zero failed/skipped. SQL and full acceptance suites were excluded from this run.
- Evidence-record JavaScript tests: 4 passed.
- Frontend `npm run verify` passed: ESLint, Prettier, TypeScript and production build. The existing large-bundle warning remains; it is not a failed build.
- Logs and TRX results: `D:/CosmixReleases/product-readiness-20261030/` (`reported-errors-live.log`, `reported-errors-regression.trx`, `month-close-setup.trx`, `reported-errors-web-final.log`).
- Updated API restarted locally on port 7240 using the existing launch profile in Release configuration. No schema migration was needed.

This verification addresses the reported errors, not production/company payroll acceptance. Previously generated IIS archives do not include these new fixes until rebuilt.
