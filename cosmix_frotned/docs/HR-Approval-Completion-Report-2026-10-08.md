# HR approval extension and acceptance

Date: 8 October 2026. Frontend: existing cosmix_frotned project.

## Delivered

The PM extended the earlier HR-only scope to include Admin approval UI and remaining
acceptance. Hiring approvals now has a real server queue, name search, status filtering,
paging and frozen submission review. Submitted financial and medical sections are
loaded separately under their actual permissions when selected. File actions use
private APIs; standard document versions are compared with the frozen submission.
Medical supporting files can be inspected under matching permissions using current
metadata. The API does not expose frozen medical file metadata; backend validation
of that immutable evidence remains authoritative.

Pending hiring supports Approve, Return for correction and Reject, reason validation,
explicit confirmation, self-review prevention and optional initial Site assignment.
Office staff cannot receive a site through this form. Current employee rowversion,
submission revision and document versions remain separate. Approved hiring shows
the existing access-setup state where permitted; it does not provision roles or passwords.

Existing change, salary and employment-status request details now support authorized
Pending decisions. The requester/target cannot review their own request. Current
employee and request versions are submitted together. Definitive HTTP rejections
unlock correction/reload; uncertain results retain exact payload/key for explicit retry.
Successful decisions refresh current/scheduled details and history. Closing a dirty
decision requires discard confirmation, and pending decisions prevent dialog dismissal.

Returned correction links now close their old dialog before opening the correction
form. This fixes a reproduced issue where the route changed but the modal covered
the new form. Existing HR cancellation, private sections and login remain in place.

## Actual verification

All business writes were synthetic records in the backend-confirmed Cosmix_Testing
database. Existing dummy accounts were used. No accounts, roles or permissions were
created, no scanner/SMS configuration changed and no backend/mobile/schema edits made.

| Check                                                                 | Actual result                                                                                        |
| --------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------- |
| New hiring route before implementation                                | Failed as expected: missing Hiring approvals heading                                                 |
| Actual Admin queue/navigation and 1440/390/320px                      | Passed after implementation; current queue is empty                                                  |
| Salary Return, Reject and future-dated Approve                        | Passed using actual UI and APIs                                                                      |
| Employment-status Return, Reject and future-dated Approve             | Passed using actual UI and APIs                                                                      |
| Returned salary/status correction links                               | Reproduced modal issue, fixed, then passed                                                           |
| Self-decision and blank Return/Reject reasons                         | Backend 403 and UI required-field protection passed                                                  |
| Dirty decision dialog, retained reason, closed-state controls/history | Passed in the live decision flows                                                                    |
| Real stale request-version 409                                        | Passed: rejection unlocked fields; corrected decision was accepted                                   |
| Lost response after real decision commit                              | Passed: exact body/key retry returned success with one decision only                                 |
| Current/scheduled salary refresh without page reload                  | Passed after decision; current value remained unchanged                                              |
| Limited HR approval route/navigation/API denial                       | Passed: no forbidden queue prefetch; backend returned 403                                            |
| Actual returned detail at desktop and 320px, Escape and screenshots   | Passed; screenshots visually inspected; no page errors                                               |
| Fresh synthetic-document rescan                                       | Actual ScanStatus=3 (Failed), IsAvailable=false; clean success remains blocked                       |
| npm run verify                                                        | Passed: ESLint, formatting, TypeScript and Vite build; approximately 616 kB JS chunk warning remains |

Temporary verification files remain outside Git in the task TEMP directory. The combined
approval-workflows check reached six passed salary/status transitions, then stopped on
a bank-request 409. That combined command is not labelled a complete passing suite.
The initial helper status/visibility checks were corrected before reproducing the actual
returned-dialog bug. Dedicated recovery/replay/refresh/denial checks passed separately.

## Testing database changes and limits

Synthetic TEST ONLY HR has approved future salary and suspended-status records effective
1 January 2029. Its current status remains Active. TEST ONLY Admin has a future salary
record effective 1 January 2030. Reasons identify all these records as testing. No current
employment status was changed and no new login account was provisioned.

The scheduled lifecycle record correctly prevents further post-hire profile proposals
for that HR fixture. The subsequent bank acceptance probe was rejected with 409 under
the existing active-unscheduled-employment rule. A separate fresh approved synthetic
employee is required for bank/medical/transfer acceptance; history was not rewritten
or deleted to force a pass. The earlier approved login fixture also lacks a current
employment assignment, so assignment-backed transfer success remains unverified.

Clean scanning, private verified-document success, complete successful hiring submission,
live hiring Approve/Return/Reject and initial assignment creation still require a real
available scanner. The fresh rescan remains Failed; Docker exists but its local engine
is not running. No simulated Clean status or database evidence override was used.
Real local scanner setup is awaiting the PM's exception to the earlier configuration
restriction. Synthetic hiring approval would create a dormant account, so that test also
awaits the requested exception to the earlier account-creation restriction.

The new hiring review screen has no valid submitted fixture in the current queue, so
its successful decision/file-review paths are implemented but not labelled live-tested.
Identity/document/medical evidence success and every change-decision combination are
not claimed as exhaustive acceptance. Full role/password/mobile provisioning UI remains
separate. SMS stays in the existing guarded test mode; real delivery is excluded for now.

## Files in this increment

- src/modules/hr/schemas/approvals.ts
- src/modules/hr/pages/HiringApprovalsPage.tsx
- src/modules/hr/components/approvals/HiringSubmissionReview.tsx
- src/modules/hr/components/approvals/SubmissionDocuments.tsx
- src/modules/hr/components/requests/RequestDecisionForm.tsx
- src/modules/hr/components/requests/ChangeRequests.tsx and WorkflowRequests.tsx
- src/modules/hr/components/requests/EmployeeWorkflows.tsx
- src/modules/hr/pages/EmployeeDetailPage.tsx
- src/modules/hr/components/ReferenceField.tsx
- src/modules/hr/hooks/use-hr-queries.ts
- src/modules/hr/index.ts, src/app/router/AppRoutes.tsx and src/layouts/AppLayout.tsx
- Current architecture, module and verification documentation

The existing dirty tree includes earlier authentication and HR work. This list identifies
the new increment rather than claiming ownership of every uncommitted file.

## Local use

Frontend: https://localhost:5173. API: https://localhost:7240.
Use the existing test login. Authorized reviewers open /hr/approvals for hiring or
/hr/requests for change/salary/status decisions. Other users retain their HR workflows.
Start the frontend with npm run dev; rerun checks with npm run verify.

All changes remain local. No commit, push or deployment was performed. Successful
available checks do not establish production readiness while the listed gates remain open.
