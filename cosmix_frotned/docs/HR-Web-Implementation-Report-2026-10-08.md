# Cosmix HR Web Implementation Report

Date: 8 October 2026. Scope: approved HR-only React web workspace.

This records the original delivery. The later PM-approved Admin review extension and
fresh decision acceptance are documented in HR-Approval-Completion-Report-2026-10-08.md.

## Executive Status

All six implementation tasks have been worked through in sequence. All supported screens
and frontend integrations are implemented locally. Available live checks passed, including
actual hiring saves, post-hire basic edits and bank/salary/status requests with cancellation.
Full workflow acceptance is NOT claimed: scanner, full employment fixtures and recorded
approval-state fixtures still limit some end-to-end tests.

Work remains in `cosmix_frotned`, branch `codex/frontend-foundation`, based on frontend
HEAD `426f2b9` and read-only backend `70adaf7`. Existing uncommitted global-authentication
work was preserved. No commit, push, backend/mobile edit, schema change, account creation,
provider configuration change or authorization upgrade was made.

## Six-Task Status

| Task                             | Implementation status               | Actual acceptance status                                                                                                                                                             |
| -------------------------------- | ----------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 1. Foundation and directory      | Implemented                         | Real list/search/filter, access gates, responsive frame, existing login/session regression passed                                                                                    |
| 2. Organization references       | Implemented                         | Reads, city create/edit/history, duplicate/stale protection, holiday creation/revision and dirty navigation passed; exhaustive combinations not claimed                              |
| 3. Hiring, documents, submission | Implemented                         | Actual draft/profile/financial/medical saves, stable repeated IDs, Office/Site clearing, readiness and failure handling passed; clean-scanner/complete submission acceptance blocked |
| 4. Current employee and changes  | Implemented                         | Current reads, direct basic save/history, refreshed values, bank proposal/detail/cancellation and denied sections passed; transfer/evidence-dependent success needs fixtures         |
| 5. Salary and status requests    | Implemented                         | Actual future-dated proposals, details, unchanged current values and own cancellation passed; Admin decisions intentionally excluded                                                 |
| 6. Verification and handover     | Completed for available environment | Build checks, browser regression, responsive/security-focused review and docs completed; remaining UAT limits explicitly listed below                                                |

## Screens and Features

1. **Shared HR frame:** permission-filtered Employees, Organization and HR Requests navigation;
   account link, existing logout, real Cosmix logo, keyboard-accessible mobile navigation.
2. **Employee directory:** server pagination, name/number search, onboarding-state filter,
   draft/current routing, explicit New Employee action, loading/empty/error/denied states.
3. **Organization:** departments, designations, cities, offices, staff categories and employment
   types; authorized add/edit/activate/deactivate/history; immutable codes and audit reasons.
   Holiday calendars support dated creation, revision, holiday rows and history. Sites are read-only.
4. **Hiring profile:** legal identity, contact details, both addresses, emergency contact,
   employment references, joining/probation, proposed mobile access/SIM details, previous
   employment, certifications, asset evidence and selected document manifest. Incomplete
   drafts are allowed. Pending/rejected forms are not silently unlocked; returned drafts can edit.
5. **Office/Site rules:** Office staff select an office; changing category clears the previous
   office. Site staff have no HR office/site selector. Admin assignment is outside this UI.
6. **Private sections:** financial salary/bank/prior-salary and medical details/authorization
   are fetched only under matching grants. Previous-salary rows retain the corresponding
   previous-employment entry IDs. Each save uses the latest section/employee rowversion.
7. **Documents:** separate standard/medical lists, PDF/PNG/JPEG up to 10 MiB, private upload,
   scan/verification/availability state, authorized rescan/review/verification/download.
   No public links, file deletion, face templates or scanner bypass.
8. **Submission:** actual backend missing-field links, document readiness and manifest,
   confirmation, submission endpoint and recorded hiring decisions. Submission is not activation.
9. **Approved employee:** current basic/identity/employment/bank/medical/document sections,
   scheduled versus current values, protected history, direct basic edit with reason, and
   approval requests for sensitive/organizational changes. No login-phone/password/role edits.
10. **HR requests:** separate change/salary/status lists and details, protected before/proposed
    values, recorded decisions, requester-only pending cancellation and new corrected requests
    for returned post-hire cases. There are no Admin Approve/Return/Reject controls.

## Integrated Backend Endpoints

All paths below are relative to `/api/v1/hr`. Integration means implemented consumption;
it does not mean every provider-dependent endpoint passed a successful live test.

| Area                  | Methods and paths                                                                                                                                                         |
| --------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Master lists/details  | `GET /{master}`, `GET /{master}/{id}`                                                                                                                                     |
| Master writes/history | `POST /{master}`, `PUT /{master}/{id}`, `POST /{master}/{id}/status`, `GET /{master}/{id}/history`                                                                        |
| Holidays              | `GET /holiday-calendars`, `GET /holiday-calendars/{id}`, `POST /holiday-calendars`, `POST /holiday-calendars/{id}/revisions`, `GET /holiday-calendars/{id}/history`       |
| Sites                 | `GET /sites`; read-only detail from the returned site record                                                                                                              |
| Employees             | `GET /employees`, `POST /employees`, `GET /employees/{id}`                                                                                                                |
| Hiring sections       | `PUT /employees/{id}/profile`; `GET` and `PUT /employees/{id}/financial` and `/medical`                                                                                   |
| Documents             | `GET` and `POST /employees/{id}/documents`; optional `medical=true` privacy selector                                                                                      |
| Document operations   | `POST /employees/{id}/documents/{documentId}/rescan` and `/verification`; `GET .../review` and `.../download`                                                             |
| Submission            | `GET /employees/{id}/submission-validation`, `POST /employees/{id}/submit`, `GET /employees/{id}/decisions`                                                               |
| Current details       | `GET /employees/{id}/current/{section}`, `PUT /employees/{id}/current/basic`                                                                                              |
| Change requests       | `POST /employees/{id}/change-requests`, `GET /employees/change-requests/{section}`                                                                                        |
| Change records        | `GET /employees/{id}/changes/{section}/requests`, `GET .../requests/{requestId}`, `POST .../requests/{requestId}/cancel`, `GET .../history`, `GET .../history/{changeId}` |
| Compensation          | `GET /employees/{id}/compensation`, `GET .../compensation/history`, `GET /employees/compensation-requests`                                                                |
| Salary requests       | `GET` and `POST /employees/{id}/compensation/requests`, `GET .../requests/{requestId}`, `POST .../requests/{requestId}/cancel`                                            |
| Employment status     | `GET /employees/{id}/lifecycle`, `GET .../lifecycle/history`, `GET /employees/lifecycle-requests`                                                                         |
| Status requests       | `GET` and `POST /employees/{id}/lifecycle/requests`, `GET .../requests/{requestId}`, `POST .../requests/{requestId}/cancel`                                               |

`master` is departments, designations, cities, branches, staff-categories or employment-types.
`section` is basic, identity, employment, bank, medical or documents; basic has direct editing
rather than an approval-request form. Existing global cookie/CSRF/auth endpoints were reused.

## Verification Results

**Passed: build and code checks.** `npm run verify` completed with ESLint, formatting,
TypeScript and Vite production build. Vite reported an approximately 599 kB combined JS
chunk; route splitting remains a performance improvement, not a build error. No new dependencies.

**Passed: actual API/browser work.** Existing synthetic accounts only; the local-testing
endpoint confirmed `enabled=true` and `database=Cosmix_Testing` before each write suite.
Synthetic drafts, organization records and requests remain in that testing database for inspection.
No production/development employee data was edited and no accounts were created.

| Verification group                                                                    | Result                                        |
| ------------------------------------------------------------------------------------- | --------------------------------------------- |
| Email/password then single SMS-code field, actual local-test OTP verification         | Passed; this is not real carrier SMS delivery |
| Directory, organization/site/holiday reads and limited-HR manage controls             | Passed                                        |
| Synthetic city save/edit/history, exact idempotent replay and stale version rejection | Passed                                        |
| Duplicate organization code and holiday creation/revision                             | Passed                                        |
| Explicit draft creation and profile save/reload                                       | Passed                                        |
| Financial/medical saves and stable previous-employment/salary linkage                 | Passed                                        |
| Office-to-Site change clears hidden office; no HR site selector                       | Passed                                        |
| Incomplete submission blocked using backend readiness                                 | Passed                                        |
| Invalid file type and oversized file blocked before upload                            | Passed                                        |
| Actual document upload with failed scan, unavailable evidence and disabled download   | Passed fail-closed path; clean path blocked   |
| Current basic edit, audit-history rendering, fresh current value and reopened editor  | Passed                                        |
| Bank and medical proposal/detail and cancellation                                     | Passed; current values unchanged              |
| Organization deactivation and active lookup exclusion                                 | Passed                                        |
| Readiness link reaches the matching address section                                   | Passed                                        |
| Salary/status proposal/detail and cancellation, no optimistic current-value change    | Passed                                        |
| Sensitive deep-link denial; approved hiring link does not first fetch identity        | Passed                                        |
| Competing real write returns 409 without overwriting newer data                       | Passed                                        |
| Explicit retry after network abort retains the same body and key                      | Passed                                        |
| Dirty navigation and reconnect preserve in-memory edits                               | Passed                                        |
| Real 404, network failure and manual retry                                            | Passed                                        |
| Access-cookie expiry/refresh; cancelled read during real refresh                      | Passed                                        |
| Delayed successful financial response after cross-tab logout                          | Passed: no late HR read or cache repopulation |
| Cross-tab logout, cleared query cache, no HR data in local/session storage            | Passed                                        |
| 1440/390/320px, loaded logo, screenshot inspection, mobile keyboard/Escape/focus      | Passed; wide tables scroll internally         |

**Failures encountered and resolved:** absent feature routes during initial red/green work,
organization dirty-navigation loss, stale basic current data, session-lifetime callbacks,
reconnect remounting dirty forms, approved hiring deep-link gate, readiness anchors and
business-error mappings. A bounded reviewer recheck found a cancelled-refresh session-outage
race; the transport was corrected and the real 401/refresh cancellation check passed.
Temporary test selectors and missing basic fixture details were corrected before rerunning.
No known failing frontend check is being labelled passed.

**Review scope:** one fresh, read-only whole-module review plus a targeted fix verification.
This is not a penetration test, security certification or exhaustive browser/device certification.

## Remaining Limits and Acceptance Work

- Scanner is unavailable: clean scan, successful verification/private download, complete hiring
  submission and approved identity/document changes need authorized clean evidence. They were
  implemented but not claimed as live-success tests. Do not alter scanner settings to bypass this.
- Existing synthetic HR fixture has no current employment assignment. Transfer form success
  needs an approved assignment-backed fixture. The UI blocks a proposal when current values are absent.
- Returned/rejected hiring/request decisions, scheduled values after approval and final Admin
  approval/application need controlled UAT fixtures and the later Admin UI or approved API testing.
- Document-review success and every sensitive-section combination have not received exhaustive
  live acceptance. Medical draft saving, protected proposals/detail/cancellation and permission
  boundaries passed; the medical proposal left current approved values unchanged.
- Actual Jazz/SMS-provider delivery, production accounts, hosting, deployment and production UAT remain external.
- No approved shift-list contract exists: an existing proposed shift is retained read-only.
  Asset-return recipient currently accepts a validated account UUID; no account search endpoint was invented.
- Automated checks are temporary files outside Git, as requested. A permanent regression suite
  and cross-browser/accessibility audit are not included in this increment.
- Admin review, role management, site assignment, attendance, leave, claims and other ERP
  modules are intentionally outside the approved HR frontend scope, not hidden completed work.

## Frontend Files Delivered

Paths are relative to `cosmix_frotned`. Existing authentication files were not rebuilt or
replaced by a second authentication system. The wider dirty tree includes pre-existing login work.

| Folder                     | Files created or changed for this increment                                                                                                     |
| -------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| `src/app/`                 | `providers/AppProviders.tsx`, `router/AppRoutes.tsx`                                                                                            |
| Shared integration         | `src/layouts/AppLayout.tsx`, `src/pages/AccountPage.tsx`, `src/main.tsx`, `src/services/api/http-client.ts`, `src/styles/hr.css`                |
| `src/modules/hr/`          | `index.ts`, `README.md`                                                                                                                         |
| HR `api/`                  | `hr-api.ts`                                                                                                                                     |
| HR `hooks/`                | `use-hr-access.ts`, `use-hr-queries.ts`, `use-hr-lifetime.ts`, `use-dirty-form.ts`                                                              |
| HR `pages/`                | `EmployeesPage.tsx`, `OrganizationPage.tsx`, `EmployeeHiringPage.tsx`, `EmployeeDetailPage.tsx`, `HrRequestsPage.tsx`                           |
| HR `components/`           | `Feedback.tsx`, `RecordView.tsx`, `ReferenceField.tsx`                                                                                          |
| `components/organization/` | `MasterEditor.tsx`, `HolidayCalendars.tsx`, `OrganizationHistory.tsx`                                                                           |
| `components/hiring/`       | `FormFields.tsx`, `ProfileForm.tsx`, `SensitiveForms.tsx`, `SubmissionReview.tsx`                                                               |
| `components/documents/`    | `EmployeeDocuments.tsx`                                                                                                                         |
| `components/employee/`     | `ChangeForm.tsx`, `ChangeHistory.tsx`                                                                                                           |
| `components/requests/`     | `ChangeRequests.tsx`, `WorkflowRequests.tsx`, `EmployeeWorkflows.tsx`, `SalaryRequestForm.tsx`, `StatusRequestForm.tsx`, `CompensationView.tsx` |
| HR `schemas/`              | `contracts.ts`, `organization.ts`, `hiring.ts`, `updates.ts`, `requests.ts`                                                                     |
| HR `types/`                | `contracts.ts`, `hiring.ts`, `updates.ts`, `requests.ts`                                                                                        |
| HR `utils/`                | `messages.ts`                                                                                                                                   |
| Documentation              | `AGENTS.md`, `README.md`, `docs/Architecture.md`, `docs/Backend-Integration.md`, `docs/Verification.md`, `docs/HR-Web-Progress.md`, this report |

## Local Use and Next Step

Frontend is running at **https://localhost:5173**. Sign in normally and use Open HR workspace,
or open **https://localhost:5173/hr/employees**. API is **https://localhost:7240**.
Do not use `127.0.0.1` in place of `localhost` for this cookie/CORS setup.

To start the frontend when it is not already running, from `cosmix_frotned`:

```powershell
npm ci
npm run dev
```

To rerun static verification:

```powershell
npm run verify
```

The launcher requires the existing trusted localhost development certificate. The backend
must run separately with its authorized testing configuration. If another port is selected,
its exact HTTPS origin must already be approved by the API; do not weaken CORS or TLS.

Next delivery gate: PM walkthrough of the HR screens, then scanner/full-fixture acceptance
and separately authorized Admin review UI. No production-readiness or full-ERP completion claim.
