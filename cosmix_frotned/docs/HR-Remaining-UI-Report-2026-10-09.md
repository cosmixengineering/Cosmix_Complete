# HR Remaining Web UI Delivery

Date: 9 October 2026, Asia Karachi. Continuation of the PM-approved SRS frontend scope.

## Changes

- Leave team calendar: Monday-first month view, period navigation, current period,
  valid leap dates, list view and accessible date-detail dialog.
- Employee attendance calendar and weekly shift view use the same presentation controls;
  they do not retrieve All-scope HR employee records.
- Employee roster: weekly dates, week navigation and list view.
- Attendance monthly summary and monthly attendance report: employee-by-day register
  headers, horizontally contained table and explicit unavailable record/totals state.
- Operational zones: real draft circle outline and Polygon coordinate preview. Closed
  ring/coordinate validation, outer-ring vertex addition/removal, minimum three vertices
  and preservation of interior rings. Geometry never leaves the browser in this editor.
- Circle centre/radius and Polygon boundary are required when their shape is selected;
  zero-radius circles fail client draft validation. Shape changes still clear hidden values.
- Reports: selectable XLSX/PDF/CSV format and explicit disabled export/print commands until
  an authorized report job/download exists. No local counterfeit report is generated.
- Existing eighteen workspaces and thirteen own-employee views remain in place. Remaining
  claims/leave/overtime/policy/request field groups keep their earlier transient draft review.

This increment changes React only. Backend, migrations, React Native, scanner/SMS settings
and Git history were not edited. Original local testing settings were preserved. No commits
or pushes. No business request, approval, employee account or HR record was created.

## Verification

- `npm run verify`: lint, formatting, TypeScript and production build passed during this
  increment; a final fresh rerun is recorded at handover. Bundle size remains a warning.
- Offline date checks passed Pakistan day rollover, February 2026/2028, year transitions,
  Monday-first month/week construction and invalid date/month rejection.
- Offline geometry checks passed coordinate ranges, closed rings, vertex add/remove,
  triangle minimum and interior-ring preservation.
- Isolated browser checks passed at 1440, 390 and 320 pixels with the exact application
  styles: period navigation, leap-day detail dialog/list, seven-day roster, register columns,
  Polygon edits, invalid-geometry state and disabled exports. Canvas pixels were checked
  nonblank. No browser exceptions or actual API calls occurred in the component probe.
- These are real component checks, NOT authenticated integration tests or complete SRS UAT.
  Temporary test pages/scripts/screenshots remain outside the repository.

## Current Blockers

The existing testing API could not start because LocalDB reported SQL Server process
startup failure. Its older named-pipe address was also stale. A private temporary runtime
copy attempted the existing `Cosmix_Testing` database via stable LocalDB instance name;
the instance startup still failed. No database recovery, attach, migration, reset, account
seed, security bypass or original settings edit was performed. Fresh live frontend/API
regression is blocked until the testing database service is available.

Operational attendance/leave/zone/claim/payroll/report/notification and own-data contracts
are still absent from the current backend. Calendars/registers therefore show unavailable
records, not invented attendance, leave balances, holidays, totals or approvals. Save,
approve, calculate and export remain disabled. The coordinate outline is not a geographic
map or authoritative geofence evaluator; approved basemap/provider configuration is pending.

Mobile camera/liveness, live Check In, Check Out, GPS and offline sync remain separate
React Native/backend work. Fixed Admin/SuperAdmin boundaries and actual backend authority
remain unchanged. This is not a claim of complete production-ready HR/Attendance delivery.

## Files

New presentation code:

- `src/modules/hr/utils/calendar.ts`
- `src/modules/hr/utils/boundary.ts`
- `src/modules/hr/components/workspaces/ScheduleView.tsx`
- `src/modules/hr/components/workspaces/BoundaryEditor.tsx`
- `src/modules/hr/components/workspaces/ReportActions.tsx`

Updated integration/presentation code:

- PreparedWorkspace, PreparedForm and PreparedField in `components/workspaces/`.
- `types/prepared-workspace.ts`, `utils/prepared-draft.ts` and `styles/hr.css`.
- Attendance, people-services, operations, reporting and self-service view definitions.
- README/module documentation and the dated implementation plan/report.

Main frontend: `https://localhost:5173`. It is running with the existing trusted localhost
certificate. Login still needs the testing API on `https://localhost:7240`; do not replace
that requirement with frontend mock authentication. Use `npm run dev` for normal startup.

## Next Integration Order

Restore the existing testing API without losing history, then rerun authenticated layout,
permission, session and navigation regression. Backend team should deliver real shift/policy
contracts first, then attendance/review/overtime, leave, zones/site reports, claims/financial
services and reports/month close. Map each SRS view model to those reviewed DTOs, wire
actual data/decisions/downloads, enforce supplied scope/error/version rules and run positive
and negative workflow acceptance before marking a module complete.
