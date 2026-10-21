# Frontend API Test Report

Date: 13 October 2026. Existing React frontend only; source changes, backend configuration, databases, accounts and mobile implementation were not part of this test run.

## Conclusion

The frontend runs at `https://localhost:5173`. Its actual HTTP client sends API calls to `https://localhost:7240/api/v1`. The backend API is not listening on port 7240 or 5240 in the observed environment. All eleven live browser API probes failed at the network boundary, without an HTTP response. This is not evidence that eleven endpoint implementations are defective, nor evidence of working integration.

The saved private testing configuration was inspected without displaying secrets. A SELECT-only probe of its exact `Cosmix_Testing` connection also failed. No database was created, attached, restored, migrated, seeded or changed. The earlier discovered existing testing catalogue on another LocalDB instance was not substituted automatically.

## Live Browser API Probes

These requests used the actual `src/services/api/http-client.ts` through the running Vite application, with trusted HTTPS, cookies enabled and no request interception, fabricated responses or TLS bypass. They used a fresh anonymous browser context, not another user's stored session.

| Method | Path relative to `/api/v1`                                       | Actual result                         |
| ------ | ---------------------------------------------------------------- | ------------------------------------- |
| GET    | `/auth/web/csrf`                                                 | Network unavailable; no HTTP response |
| GET    | `/me/permissions`                                                | Network unavailable; no HTTP response |
| GET    | `/hr/employees?page=1&pageSize=1`                                | Network unavailable; no HTTP response |
| GET    | `/hr/departments?page=1&pageSize=1`                              | Network unavailable; no HTTP response |
| GET    | `/hr/branches?page=1&pageSize=1`                                 | Network unavailable; no HTTP response |
| GET    | `/hr/staff-categories?page=1&pageSize=1`                         | Network unavailable; no HTTP response |
| GET    | `/hr/shifts?page=1&pageSize=1`                                   | Network unavailable; no HTTP response |
| GET    | `/hr/employees/submissions?page=1&pageSize=1`                    | Network unavailable; no HTTP response |
| GET    | `/hr/employees/record-requests/Asset?page=1&pageSize=1&state=0`  | Network unavailable; no HTTP response |
| GET    | `/hr/staff-taxonomy/effective?categoryCode=OFFICE&on=2026-10-13` | Network unavailable; no HTTP response |
| GET    | `/hr/foundation-policies/Fields`                                 | Network unavailable; no HTTP response |

Query dates and categories here are probe filters, not newly published policy or evidence that a mapping exists. No protected employee response was returned.

## Passed Frontend Checks

- Fresh `npm run verify`: lint, formatting, TypeScript and production build passed. The existing 975.18 kB initial-chunk advisory remains; production performance is not certified.
- Twenty-six actual-browser UI assertions passed: login email/password rendering; seven protected routes redirecting to sign-in without exposing employee data; desktop/mobile page width; no uncaught page errors.
- Protected routes checked: Employees, Organization, Hiring Approvals, Document Expiry, Staff Taxonomy, My Attendance Calendar and My Workspace.
- Desktop/mobile login screenshots were captured outside the repository; the mobile screen was visually inspected. Existing error feedback remains visible rather than claiming a restored session.
- Offline testing-readiness inventory ran successfully, but its metadata-only observations do not constitute SQL or authentication acceptance.

## Blocked Acceptance

Password/SMS-OTP login, authenticated permission enforcement, employee read/save/reload, clean documents, hiring submission/decision/access, transfers, sensitive changes, assets/SIM writes and policy decisions were not run against a live API in this test. No pass is claimed for these workflows. Real carrier SMS remains outside scope and testing SMS configuration was not changed.

Before those tests: restore the correct approved testing-host binding and reviewed fixture manifest, start the backend on its approved HTTPS origin, and confirm that it connects only to the existing authorized synthetic `Cosmix_Testing` catalogue. Current source's strict three-account test-host guard must not be weakened or additional accounts deleted merely to obtain a passing startup. No new account, database or provider setup was performed here.

The backend must be reachable before CORS, cookies, CSRF, endpoint payloads, grants and business outcomes can be accepted. A failed TCP connection does not establish a CORS or TLS defect.

Temporary runner and sanitized results: `D:/CodexTemp/CosmixHrFrontend20261013/live-api-checks.mjs` and `live-api-results.json`. The runner prints paths/status categories only, not credentials, cookies, OTPs or response bodies. All repository work stays local; no commit/push.
