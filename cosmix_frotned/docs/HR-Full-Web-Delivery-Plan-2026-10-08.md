# Complete HR and Attendance Web Delivery Plan

Date: 8 October 2026. Approved by the PM's instruction to complete SRS-based React
frontend work while the backend team delivers the remaining business modules.

## Goal and boundaries

Extend the existing React HR workspace to cover the web responsibilities in SRS v1.2:
employee administration, site/access setup, shifts/rosters, attendance review, policy
configuration, leave, overtime, operational zones/site reports, claims/vehicles,
advances/loans, monthly closure, reporting, notifications and employee self-service.
Mobile live-face Check In/Out, camera/GPS/background tracking and React Native remain
separate. Preserve the latest approved phone/SMS authentication and HR/Admin flow.

Use existing React/TypeScript/router/query/Zod/Lucide architecture. Reuse actual delivered
APIs for employee, organization, site and access operations. The current OpenAPI/backend
exposes no operational shift, face, attendance, leave, zone, claim, payroll/report or
notification contracts. SRS endpoint examples are not permission to invent deployed APIs.

For those future services, build complete field groups, repeatable entries, filters,
read/review layouts, client validation and transient draft review. Mark service availability
honestly; keep business save/approve/calculate/export actions unavailable. Do not insert
sample rows, fabricate totals, send unsupported HTTP calls, calculate pay in the browser,
or claim persistence. UI draft schemas are frontend view models, not final backend DTOs.
Private drafts/files remain in memory and are discarded on logout/employee/workspace change.

## Tasks

1. **Verify the present scope:** read the SRS and 7 October plan, compare the latest source
   and actual API/controller/permission catalogue. Record current integration versus new
   screen coverage; the old backend plan is a historical baseline.
2. **Complete existing-contract pages:** integrate site CRUD/status/history, employee site
   assignment/history and permitted approved-employee access setup. Keep fixed-role and
   delegation checks on the server. Add own session/device controls using delivered APIs.
3. **Complete pending-service web screens:** use one focused shared form/review surface,
   with separate domain definitions for attendance setup/review/face administration,
   policy/zone/site-report work, leave/overtime, claims/vehicles, financial services and
   monthly/report/audit work. Fields derive from SRS sections 5-15; unresolved management
   settings remain empty or explicitly awaiting approved configuration.
4. **Employee web self-service:** use only the authenticated employee identity. Do not
   route own-data views through All-scope HR APIs. Prepare personal attendance/roster,
   leave/correction/overtime/financial views and safe requests until own-data APIs exist.
5. **Existing hiring acceptance:** configure a genuine local ClamAV scanner and verify
   clean/private document paths. The testing host permits exactly three initial fixture
   accounts at startup; run normal new-account hiring acceptance in an isolated LocalDB
   copy also named Cosmix_Testing. Do not change that guard, original data or backend modules.
   Use normal APIs for hiring decisions, assignment-backed transfer and sensitive changes.
6. **Verification and handover:** run lint/format/type/build checks and real API/browser
   checks. Inspect desktop/mobile layouts and pending-service validation; prove no unsupported
   writes or fake success. Record positive/negative results and backend contract needs.

No commit/push, production data, database schema changes, new backend module or real SMS
integration is authorized by this work. Local scanner/test configuration is the specifically
approved exception. No payroll formula, geofence value, face threshold, approval matrix,
retention period or bank payment is guessed.

## Review focus

- Unauthorized viewers must not fetch confidential HR records or prepared financial views.
- Unsupported services must never appear saved, approved, calculated or exported.
- Dirty and repeated drafts must retain stable rows, clear hidden conditional values and
  never survive an authentication/workspace change in persistent storage.
- Source punches, zone events and payroll calculations remain separate read-only records.
- Positive scanner/hiring tests require real engine evidence and normal business approvals.
