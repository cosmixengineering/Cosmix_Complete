# HR Web Workspace

Implemented scope: the approved HR design and six-task plan in `docs/`, plus the
subsequently authorized Admin hiring and post-hire review extension.
Global authentication stays in `src/auth`; no separate HR identity layer exists.

## Routes

| Route                      | Screen                                               |
| -------------------------- | ---------------------------------------------------- |
| `/hr/employees`            | Server-paged directory, search and onboarding filter |
| `/hr/employees/new`        | Explicit draft creation                              |
| `/hr/employees/:id/hiring` | Profile, private sections, documents and submission  |
| `/hr/employees/:id`        | Current details/history and post-hire request forms  |
| `/hr/organization`         | Masters, holiday revisions and read-only sites       |
| `/hr/requests`             | Change, salary and employment-status requests        |
| `/hr/approvals`            | Admin hiring queue, frozen review and decisions      |

Query parameters select sections, filters and edit mode; they do not grant access.

## Ownership

- `pages/`: route composition only.
- `components/organization/`: master and calendar editing/history.
- `components/hiring/`: full form fields, financial/medical sections and readiness.
- `components/documents/`: private upload, scan/review/verification and download controls.
- `components/employee/`: current-data edit/proposal forms and protected history.
- `components/requests/`: lists, details, salary/status proposals, requester cancellation and permitted Admin decisions.
- `components/approvals/`: frozen hiring review, protected file actions and hiring decisions.
- `api/hr-api.ts`: typed shared transport; no duplicate authentication or per-entity wrapper boilerplate.
- `hooks/`: All-scope checks, validated queries, mutation keys, session cancellation and dirty navigation.
- `schemas/` and `types/`: actual backend contracts, separated by workflow.
- `utils/messages.ts`: safe business-error labels and Pakistan display timestamps.
- `index.ts`: public module boundary. Internal imports remain relative.

## Rules

Office staff can select an office. Site staff select neither office nor site; changing
category clears the old office. Admin may select an initial site in hiring approval;
subsequent site management and reassignment remain separate.

Employee designation is not an access role. Phone/password/role recovery and account
provisioning do not belong in these forms. Profile images are not face templates.

Do not replace current post-hire values with hiring snapshots, auto-apply proposals,
invent scan results, mutate returned requests, reuse stale versions or persist private
forms in browser storage. Preserve reasons, effective dates and history from the API.

## Not included

Full site management/role assignment, attendance, face enrollment, leave,
allowances, loans, payroll and reports remain outside this approved frontend increment.
Read the implementation report for provider/fixture-blocked acceptance checks.

## Subsequent approved continuation

The sections above describe the original HR-only increment. Subsequent PM approval added
site CRUD/assignment, approved-employee access setup, own session/device controls and
prepared SRS workspaces under `/hr/workspaces/:workspaceId` and `/self-service`.
The prepared workspaces use frontend view definitions rather than guessed API DTOs.

`components/workspaces/` contains field/review controls, date-oriented calendars/rosters,
monthly registers, local circle/Polygon outlines and unavailable report actions.
`workspaces/` owns SRS field definitions; `utils/calendar.ts` and `utils/boundary.ts` own
pure presentation helpers. Geographic basemap, server calculations and operational
business writes are not implemented by these controls. They require approved contracts.

Read `docs/HR-Remaining-UI-Report-2026-10-09.md` for current verification and limitations.

## Access contract continuation (13 October 2026)

Approved employee initial and additional role assignment now require an explicit reason of 1–1000
characters without control characters. The client trims surrounding whitespace and the server retains
its required reason validation and protected evidence. Both requests send Idempotency-Key. On an
uncertain assignment response, the exact payload and key stay in component memory for an explicit
identical retry; changing dates, versions or reason requires a new intent after current-state review.
Successful/replayed responses reload current access. Credentials are never retained for replay.
Keys/payloads are discarded on refresh (with the dirty-form confirmation), employee/session change
or unmount. The backend rechecks current MFA, authority, employment, scope and protected targets.

See backend docs/Authentication-Access.md for the 24-hour receipt contract. Real SMS/MFA, owner setup,
SQL rollback/races and browser acceptance against approved accounts remain separate config/acceptance
gates. No permanent automated web test file was added.

## Policy Administration Continuation (13 October 2026)

The protected HR shell now mounts the actual Foundation Fields/Lifecycle/Records policy
workspaces under `/hr/foundation-policies` and Staff Taxonomy under `/hr/staff-taxonomy`.
Each feature owns its typed contracts, structured proposal controls, private reads,
current/effective history, request decisions and exact uncertain retries. The public
HR index is the composition boundary; existing authentication/transport is reused.

Navigation requires each feature's exact All-scoped read permission and an authenticated
web session. Company/Head Office authority is not borrowed by either feature. Fixed-role
decision authority remains server-authoritative because the current session DTO exposes
grants rather than role codes. Missing deployment authority or downstream capabilities
remain unavailable; the UI does not activate policies or calculate cash effects locally.

The PM cancelled the new bank-verification UI during this continuation. Only its newly
created, unmounted frontend files were removed; bank-detail forms and historical data
were preserved. Do not reintroduce the cancelled feature from an older checklist.

See each feature README and `docs/HR-Gap-Continuation-Report-2026-10-13.md` for exact
endpoints, verification and live acceptance limitations.

## Admin hiring connection (15 October 2026)

`/admin/approvals?type=hiring` now composes the same server-backed hiring queue as
`/hr/approvals`. Fabricated Admin requests and local-only approve/reject handlers were
removed. The queue exposes explicit review details and approved employee links.
Review uses the retained submission, protected documents/sections, and current
rowversion. Department/designation/category/office labels resolve from organization
references. Effective taxonomy controls custom-category site placement; the existing
OFFICE/SITE baseline remains available without taxonomy-read permission.

Approval refreshes the queue after closing the review. From the Approved filter,
Admin can continue to the existing site assignment and role/software access workspaces. Roles and scopes are
explicit selections, not inferred grants from a job title. No backend, database,
authentication or permission catalogue changes were made. See
`docs/HR-Admin-Onboarding-2026-10-15.md` for checks and live-data limitations.

## HR dashboard source summaries (19 October 2026)

The dashboard reads `/hr/dashboard/attendance` and `/hr/dashboard/payroll` through
the existing authenticated transport. Backend summaries select verified active
employment and the current Karachi date. Turnout counts accepted session evidence;
attendance classification coverage remains separate. Salary shows effective monthly
gross by currency, missing/nonmonthly coverage and excluded future versions. Frozen
handoff components for active employees are shown separately from base salary.

These are read-only summaries behind existing All-scope source permissions. No local
salary formula, fabricated attendance rate or payroll preparation runs in the browser.
Each source has independent loading, error/retry and restricted states. CSV exports
the same source dates, coverage and state as the visible dashboard.
