# HR Web Implementation Plan

> For agentic workers: use superpowers:subagent-driven-development or
> superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Deliver the HR-only React workspace using the implemented Cosmix backend APIs.
**Architecture:** Existing React/TypeScript module architecture, common authenticated frame,
TanStack Query for server reads and sectioned local forms. Reuse global authentication;
no duplicate identity layer, backend policy engine or Admin review interface.
**Tech Stack:** Existing React 19, TypeScript, React Router, TanStack Query, Zod, Lucide and CSS.
**Spec:** [Approved HR Web Design](HR-Web-Design.md).

## Global Constraints

- Backend baseline: `70adaf7`; preserve all existing frontend login changes.
- Work only in `cosmix_frotned`. Backend and React Native remain unchanged.
- No mock business screens, fabricated totals, unsupported endpoints or permission upgrades.
- No credentials, confidential records or documents in persistent browser storage/logs.
- Use `Cosmix_Testing` and synthetic records for authorized write checks. Do not touch the
  original database, reset data, create accounts, bypass scanning or change migrations.
- No repository test projects/artifact folders, new UI/state framework, commit or push.
- Verification scripts/screenshots belong in a task-specific TEMP directory.

## Review Focus

1. HR with limited grants must not issue financial, medical or identity reads before access.
2. A concurrent edit, stale rowversion or uncertain request must not cause silent overwrite.
3. Office/Site category changes must not carry a hidden office/site into a Site submission.
4. Route/account changes must not carry one employee's sensitive form state into another.
5. Scanner failure and minimal test fixtures must not be mistaken for successful onboarding.

## Task 1: HR Foundation and Real Employee Directory

Files: shared `src/services/api/http-client.ts`, `api-error.ts`; new HR `types/contracts.ts`,
`schemas/contracts.ts`, `api/hr-api.ts`, `hooks/use-hr-access.ts`, `hooks/use-hr-queries.ts`,
`pages/EmployeesPage.tsx`; existing `src/layouts/AppLayout.tsx`, router, module index;
new `src/styles/hr.css`. Split larger contracts by workflow as tasks grow.

Interfaces: `hasHrPermission(user, code)` checks the actual grant and All scope for HR
presentation; `hrGet(path, schema, signal)` validates a typed response through shared
transport; `hrWrite(path, body, method, key)` preserves explicit retry keys; hooks include
session identity/filter/employee in query keys. No helper may invent an authorization grant.

- [ ] Reproduce missing HR routes against the existing app and record baseline verify results.
- [ ] Add narrow idempotency/multipart/binary transport support; retain CSRF/origin/refresh guards.
- [ ] Build responsive shared frame and actual directory with server search/pagination/status.
- [ ] Add permission-aware navigation, deep-link denial and New Employee action.
- [ ] Verify HR list via actual API, anonymous/insufficient-scope denial, safe error state,
      401/refresh behavior, pending-action protection and account-switch cache isolation.

## Task 2: Organization References

Files: HR `api/organization-api.ts`, `schemas/organization.ts`, `pages/OrganizationPage.tsx`,
`components/organization/`, and necessary exports/routes. Reuse shared controls/styles.

Interfaces: consume Task 1 transport/access helpers. Master kinds are departments,
designations, cities, branches, staff-categories and employment-types; holiday calendars
use their separate revision DTOs. Site access is read-only, never an Admin write screen.

- [ ] Verify currently absent organization screens and capture actual DTOs/enums.
- [ ] Implement permission-based list/detail, authorized create/edit/status/history and holiday revisions.
- [ ] Keep codes immutable; use reasons/rowversions/idempotency; no DELETE or guessed master values.
- [ ] Verify actual permitted reads and writes, duplicate codes, inactive references, stale edit,
      empty/error states, and HR without manage rights. Use only synthetic testing records.

## Task 3: Hiring Form, Documents and HR Submission

Files: HR `types/hiring.ts`, `schemas/hiring.ts`, `api/employees-api.ts`,
`pages/EmployeeHiringPage.tsx`, `components/hiring/`, `components/documents/`,
`hooks/use-employee-draft.ts`. Keep repeated rows and sensitive sections in focused files.

Interfaces: DTO fields and limits exactly follow backend EmployeeFormContracts and
EmployeeHiringContracts. Section saves return EmployeeWriteResult; employee Version is
the next mutation's expectedVersion, including document mutations. Financial/medical reads
are separate and enabled only after matching permissions. Draft creation is explicit once.

- [ ] Capture missing hiring/form route baseline and submission-validation contract.
- [ ] Implement sectioned draft editing for all supported fields, repeated rows and stable IDs.
- [ ] Add valid active master/manager lookups; Office requires branch, Site clears branch and
      has no site selector. Do not fabricate unavailable shift/project/account lookup endpoints.
- [ ] Add independently authorized financial/medical sections and sequential saves.
- [ ] Implement permitted uploads, scan status, rescan, verification/review/download and
      selected document manifest; respect separate medical privacy and employee version.
- [ ] Add backend readiness review, safe field errors, submit/status and hiring decision history.
- [ ] Verify actual create/save/reload, incomplete draft vs blocked submit, Office/Site switching,
      duplicate actions, wrong/oversized files, scanner outage, dirty navigation and denied sections.
      Record provider-dependent success tests as blocked rather than simulating clean evidence.

## Task 4: Approved Employee Details and HR Change Requests

Files: HR `types/updates.ts`, `schemas/updates.ts`, `api/employee-updates-api.ts`,
`pages/EmployeeDetailPage.tsx`, `components/employee/`, `components/requests/`.

Interfaces: current/{section} supplies current and scheduled values; basic PUT takes
expectedVersion/data/reason. Non-basic POST change-requests contains exactly one matching
section, effectiveFrom where supported and a new operation key. No generic Employee PUT.

- [ ] Implement current section reads, history and direct basic edits under real grants.
- [ ] Implement identity/employment/bank/medical/document proposals and request detail/history.
- [ ] Show current vs scheduled state; do not reuse the original hiring snapshot as current data.
- [ ] Allow only supported cancellation/new corrected requests. No approval/return/reject controls.
- [ ] Verify permission denial, no sensitive prefetch, direct save vs pending request, stale
      versions, history rendering and employee-route state isolation; no login phone/role edits.

## Task 5: HR Salary and Employment Status Requests

Files: HR `types/requests.ts`, `schemas/requests.ts`, `api/requests-api.ts`,
`pages/HrRequestsPage.tsx`, `components/requests/SalaryRequestForm.tsx`,
`StatusRequestForm.tsx` and focused history/detail components.

Interfaces: use separate compensation-requests/lifecycle-requests/change-requests/{section}
endpoints and their real pagination/state DTOs. Request forms consume current employee/request
versions and correct salary/date/status enums. No synthetic universal approvals API.

- [ ] Build permission-aware request tabs, list/detail, protected proposal/history reads.
- [ ] Add new salary/status requests and allowed requester cancellation, not Admin decisions.
- [ ] Verify future business dates and explicit timestamps, no optimistic salary/status activation,
      denied salary reads, returned-request handling and pending/cancelled states.

## Task 6: Integrated Verification and Handover

Files: update module README, Architecture, Backend-Integration and Verification docs;
temporary checks outside Git. Do not rewrite unrelated documentation or login behavior.

- [ ] Run `npm run verify`; fix lint, formatting, TypeScript and build errors.
- [ ] Run browser/API acceptance in the testing environment at desktop and narrow widths.
      Check real navigation/forms, keyboard/focus, loading/errors, no overlaps, private cache
      clearing, no sensitive browser logs and direct protected-route access.
- [ ] Review the complete diff for scope, authorization presentation, replay/version behavior
      and missing fields; fix findings and rerun affected checks.
- [ ] Document verified flows separately from scanner/SMS, full-fixture, Admin UI and UAT gaps.
- [ ] Keep the working local frontend available, report its actual URL and local change summary.

No task is complete merely because its UI renders or its build passes. A provider/fixture gap
must remain visible in the handover; it does not authorize bypassing backend safeguards.
