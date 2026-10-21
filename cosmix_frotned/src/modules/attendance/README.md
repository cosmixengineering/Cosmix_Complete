# Attendance

This module owns Daily attendance, Sites, Shifts and rosters, Attendance corrections, Face enrollment, Operational zones, Overtime, Month lock, Attendance reports.

## Operational scheduling package

The 13 October 2026 package implements real shift management, site roster history and
assignment/replacement/ending, temporary site preview/create/cancel/supersede, circular
site geofence versions, and office roster history/assignment/replacement/ending.
These screens use the existing authenticated request client and actual backend DTOs.
They do not use the old prepared-workspace data or mock endpoints.

Routes start at `/hr/attendance/shifts`, `/hr/attendance/sites`,
`/hr/attendance/geofences` and `/hr/attendance/offices`. Known site and office routes
are resource-specific. Offices use the existing Branch master; they are not fake sites.

Office administration and global reference lists require matching All-scoped grants.
Only supported site-roster actions accept the matching Site scope. Each permission
is paired with its own scope; UI visibility never replaces server authorization.
Queries and forms are isolated by the authenticated subject and grants, and discarded
when that context changes. Private query retention is zero after the last observer.
Mutation retries retain the exact original method, path, payload and idempotency key
in memory. There is no automatic write retry or browser persistence of private bodies.
Conflicts require reload and a new review of server state.

Source implementation and isolated browser checks are not live SQL or production
acceptance. Office APIs require the reviewed `AddOfficeRosterHistory` migration in
the selected database. This package does not apply it or change runtime providers.
Missing schema or authority is unavailable, not an empty successful schedule.

Daily/register, corrections, OT, policies, ordinary leave, zones/site reporting,
notifications, month closure and reports remain separate operational web packages.
Existing HR prepared controls are not proof that those flows are connected.
Office physical capture/calculations and real face/model/device acceptance are not
completed by adding an office roster. React Native remains outside this package.

- `pages/`: route-level screens.
- `components/`: UI used only by this module.
- `api/`: this module's endpoint functions; use the shared HTTP client.
- `hooks/`: query and feature hooks.
- `schemas/`: client-side validation and server-response schemas.
- `types/`: frontend DTOs and view models, not EF entities.
- `utils/`: pure helpers specific to this module.
- `index.ts`: public entry for app composition. Internal imports stay relative.

Reuse `src/components` for general controls. Do not deep-import another module. Agree cross-module contracts with the owner. Server authorization and policy calculations remain authoritative.

## Leave web boundary - 19 October 2026

The owner confirmed that personal leave requests and personal balances belong in
the employee mobile application, including for employees who work in HR. The HR
web Leave section is for employee management, not a second personal leave portal.

`/hr/leaves` and `/hr/leave-management` now use the scoped employee calendar when
`attendance.leaves.team-calendar.read` is assigned. The existing employee/scope
selector supplies an explicit employee; the server rechecks current and historical
scope. The API returns only recorded dates and statuses, not balances, reasons,
leave types or inferred absence. The inclusive date range is limited to 31 days.
Missing access and failed/empty reads remain distinct, with no sample records.

Authorized Admin review uses the existing `/hr/attendance/leaves` workflow and
`attendance.leaves.review`; settings use `attendance.leaves.configure`. The change
does not grant HR approval authority or bypass fixed-role server checks. It removes
the mixed personal balance cards, self-request form, fabricated requests/calendar,
hardcoded quota calculations and optimistic fake approval actions from the old page.
The personal `/account/leaves` routes and navigation item are no longer registered;
the leave workspace shortcut points to `/hr/leaves`. Other modules' web self-service
surfaces are outside this focused change. Backend/mobile APIs are unchanged.

Verification: production TypeScript/Vite build passed with its large-chunk warning;
changed-file ESLint, Prettier and diff checks passed. Full `npm run verify` stopped
on an existing conditional `useEffect` in the already-modified
`src/modules/hr/pages/EmployeeHiringPage.tsx:240`. The repository-wide diff check
also found an unrelated extra EOF blank line in `src/styles/onboarding-stepper.css`.
Neither unrelated file was changed. Authenticated visual/API acceptance remains
unverified: the running site redirected to sign-in and showed a connection error.
No mock session, API host, database migration or real employee write was introduced.
