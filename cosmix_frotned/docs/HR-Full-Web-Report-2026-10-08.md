# Cosmix HR Web Delivery and Backend Handover

Date: 8 October 2026. Source requirements: HR Attendance Employee Mobile SRS v1.2,
the 7 October progress plan, and the PM's later approved workflows.

## Delivery Boundary

This report supersedes the earlier HR-only and approval-extension reports for current
frontend scope. Those reports remain historical evidence. Work is local in the existing
React project; no commit or push. Backend source and schema, React Native and production
data are outside this delivery. Real SMS remains deferred, with the existing local test
sender unchanged.

The delivered employee/organization/hiring/request/site/access APIs are integrated. The
remaining SRS web workspaces have their fields, filters, repeatable rows, review layouts
and client validation prepared. They are NOT operational modules yet: their backend
contracts are absent. Save, approval, calculation and export remain disabled. There are
no mock business rows, invented endpoints, manufactured totals or simulated successful
submissions. Prepared values and selected files stay in memory only.

## Work Completed

1. Preserved shared email/password then SMS OTP web login, cookies, CSRF, refresh,
   session cancellation and the existing local test OTP screen.
2. Preserved the integrated directory, organization masters/holidays, full hiring form,
   standard/medical document handling, readiness, submission and employee profile screens.
3. Added hiring approval review, protected snapshots, Return/Reject/Approve decisions and
   approved-employee links to access setup. Existing post-hire change, salary and status
   decisions remain integrated with current/scheduled/history views.
4. Added actual site create/edit/status/history pages and employee site assignment/history.
   HR category selection still does not assign a site; Admin does. Current site APIs lack
   geofence fields, so coordinates are not added to their payloads.
5. Added approved-employee access setup using existing roles, versioned assignments,
   approved-phone mobile provisioning and first web credentials. Additional scoped roles
   are explicit, not an automatic All-scope promotion. Backend authority remains final.
6. Added own session/device lists and controlled revocation through delivered endpoints.
7. Added eighteen future-service workspaces and a separate thirteen-view own-employee
   workspace. Own views never use global HR lookups to retrieve personal records.

## Prepared SRS Workspaces

| Workspace           | Included views                                                                                          |
| ------------------- | ------------------------------------------------------------------------------------------------------- |
| Shifts and rosters  | Versions, dated population assignments, roster and temporary opening changes                            |
| Face administration | Authorized enrollment proposals, attempts and exceptions; no browser face-matching engine               |
| Attendance review   | Daily/monthly registers, raw punches, sessions, exceptions, corrections, decisions and import proposals |
| Overtime            | Requests, bulk review and server-calculated payable summary                                             |
| Zones               | Circle/Polygon boundaries, zone types, restrictions, Amber exceptions, events and review                |
| Site reports        | Queue and report review                                                                                 |
| Attendance policies | Versioned office/site settings, assignments, early-reporting declarations and activation                |
| Policy impacts      | Waiting, late/KPI, disciplinary results and authorized waiting decisions                                |
| Month closure       | Lock, controlled reopen, Accounts handoff, adjustments and decisions                                    |
| Employee services   | Post-hire certificates, SIM, asset custody and archive views                                            |
| Leave               | Balances, requests, calendar, cancellation, encashment and recovery commitments                         |
| Meals and travel    | Meal evidence, outstation deployment/travel claims and allowance ledger                                 |
| Vehicles            | Trips/fuel, mileage, workshop, tuning, overhaul, custody and accident/insurance evidence                |
| Financial services  | Advances, loans, repayment and payslip views                                                            |
| Approvals           | Matrices, explicit delegation and decision history                                                      |
| Notifications       | Inbox and reminders                                                                                     |
| Reports             | Fourteen SRS report types, period/scope filters and export job views                                    |
| Audit               | Business history and security/access records                                                            |

Own employee views cover profile, documents, attendance, shifts, corrections, leave,
overtime, salary, advances, claims, site reports, zones and notifications. Attendance
capture, live face Check In, Check Out, GPS/background tracking and offline mobile sync
still belong to the separate mobile/backend work. Self-service is not an Admin panel.

## Existing API Integration

Paths below are delivered contracts, relative to `/api/v1`. Each write uses the existing
CSRF/secure-cookie transport. Supported business writes retain exact retry keys and
versions; credential setup is not blindly retried.

- Shared authentication, session and recovery endpoints already used by the login work.
- `/hr/{master}` lists, details, create/edit/status/history; delivered holiday calendar APIs.
- `/hr/employees` directory/drafts and employee profile, financial and medical sections.
- Employee documents: upload/list/rescan/review/verification/download, with medical privacy.
- Employee readiness/submit and hiring decisions; `/hr/employees/submissions` queue,
  protected submission snapshots and submission decision endpoint.
- Employee current sections, direct basic edits, sensitive/employment change requests,
  salary/status requests, cancellation, decisions and history.
- `/hr/sites`, `/hr/sites/{id}`, `/status`, `/history`.
- `/hr/employees/{id}/assignments`, `/assignments/history`, `/assignments/site`.
- `/hr/employees/{id}/access-setup`, `/mobile-provisioning`, `/web-credentials`.
- `/access/permissions`, `/access/roles`, `/access/accounts/{id}/assignments`.
- `/me/sessions`, `/me/devices` and their supported DELETE operations.

No attendance, leave, zone, claim, payroll, report, notification or own-employee service
HTTP paths are invented. SRS example paths alone are not deployable API contracts.

## Verification Evidence

- Fresh `npm run verify` passed lint, formatting, TypeScript and production build during
  implementation. Final rerun and browser coverage results will be appended below.
- Offline prepared-form checks passed required/range/UUID/date ordering, valid dates,
  conditional clearing, closed Polygon/range validation, file constraints, private
  filename-only review and unique definition/section/field keys.
- Genuine local ClamAV 1.5.4 and official signature databases are installed privately.
  Actual rescan changed the earlier failed scan to Clean; availability stayed false
  until authorized verification. Clean upload, private review, verification and exact-byte
  download passed on synthetic identity/photo/medical evidence.
- Complete office hiring passed actual UI submission, Return, correction/resubmission,
  Approve, immutable two-decision history and initial office assignment.
- Actual initial Self-role assignment and approved-phone mobile provisioning passed.
- A separate complete hiring rejection passed; the rejected hiring state remained locked.
- Actual site UI create/edit/deactivate/reactivate passed with preserved history.
- Complete site-category hiring passed actual submission and Admin initial site selection.
  Actual future-dated site reassignment passed; both assignment records remained in history.

Hiring acceptance uses an isolated LocalDB instance with a copied `Cosmix_Testing`
database. Normal hiring APIs create the synthetic account; no direct SQL seed or backend
testing guard is changed. The original three testing accounts remain separate. The
existing local sender allows only those fixed three identities; new synthetic employee
SMS login is therefore not claimed tested. The isolated host cannot restart with its
extra hiring accounts under that existing guard, so it is stopped after acceptance.

## Backend Team Handover

Deliver one module at a time in this order: shifts/policies/assignments; face and attendance;
attendance corrections/overtime/month close; leave; zones/site reports; claims/vehicles;
advances/loans; reports/Accounts/audit/notifications. This is integration order, not a
promise of dates or a substitute for approved business rules.

For each module provide actual OpenAPI DTOs, enums/status transitions, supported grants
and record scopes, lookup contracts, pagination/filter rules, concurrency versions,
idempotency/retry behavior and business error codes. Include isolated positive/negative
fixtures, effective dates/timezone rules, audit and document access requirements. Own
employee endpoints must derive identity from the authenticated account, not accept an
arbitrary global employee identifier as authority.

Approved policy rates, overtime/leave eligibility, rounding, face-provider thresholds,
zone/geofence rules, retention, matrices and Accounts handoff behavior still require
explicit business/backend decisions. Frontend drafts neither decide these policies nor
calculate payroll or enforce authorization. Backend integration must map the prepared
view models to actual reviewed DTOs before enabling each action.

## Run Locally

Main frontend: `https://localhost:5173`. Main testing API: `https://localhost:7240`.
From the existing React project run `npm install` when dependencies are absent, then
`npm run dev`; development certificates and private API settings remain local. If the
port is occupied, the existing running service should be used or a new port configured
with its actual allowed backend origin. Do not weaken TLS or CSRF for testing.

The scanner uses `127.0.0.1:13310`; its binaries, signatures and private test configuration
are outside Git. It is a running local verification setup, not a production scanner
service or a promise of automatic startup/signature updates. Production operational
configuration and monitoring remain a deployment task.
