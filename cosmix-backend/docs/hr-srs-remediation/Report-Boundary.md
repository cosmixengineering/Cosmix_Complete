# HR Report Access Contract

15 October 2026. B2 security repair; not report-value or production acceptance.

## Client Contract

Existing routes remain `GET /api/v1/hr/reports/{reportType}` and
`POST /api/v1/hr/reports/{reportType}/export`. The static `GET .../types`
index lists report identifiers only; listing a type does not grant data access.

Both data operations require a current persisted Web session with MFA.
Mobile management access is denied. Current, effective permissions are resolved
server-side, filtered through the existing session evaluator, and rechecked after
query/render/private-storage IO. Role names, organization-management and a JWT
alone do not grant access. No implicit SuperAdmin access or automatic permission
assignment is introduced.

Every preview requires `hr.reports.read`. Every export requires BOTH
`hr.reports.read` and `hr.reports.export`. Each also requires the source grants
listed below. Each individual required permission must independently cover the
selected employee's current, historical and retained source scopes. A broad
report-read scope cannot enlarge a narrower financial or identity scope.

| Report | Additional source permission definitions |
| --- | --- |
| daily-attendance, monthly-attendance-register | AttendanceReportPermissions.Read |
| punch-audit | AttendanceAuditPermissions.Read |
| operational-zones | ZonePermissions.Review |
| site-reporting | SiteReportPermissions.Read |
| late-and-kpi | KpiPermissions.Read, AttendancePayrollInputPermissions.Read, HrReportPermissions.DisciplinaryRead |
| overtime | OvertimePermissions.Review, OvertimePermissions.FinancialRead |
| meal-allowance | AllowancePermissions.Review, AllowancePermissions.Settlement |
| leave | LeavePermissions.Review |
| employee-master | EmployeePermissions.Read, EmployeeUpdatePermissions.IdentityRead |
| document-expiry | EmployeePermissions.Read, EmployeeUpdatePermissions.IdentityRead, EmployeePermissions.DocumentsRead, EmployeePermissions.MedicalRead, EmployeeRecordPermissions.CertificationsRead |
| fuel-and-mileage | VehicleFuelPermissions.Fuel |
| payroll-handoff | PayrollHandoffPermissions.Read |
| audit | HrReportPermissions.AuditRead |

The executable matrix is `HrReportAuthorization.RequiredPermissions`.
New sensitive, management-only catalog entries are `hr.reports.audit.read` and
`hr.reports.disciplinary.read`. Existing authorized role-management must assign
them explicitly. No migration or seed grants them to anyone. Employee-master and
document-expiry contain restricted identity fields; OT and allowance rows contain
financial details. The existing DTOs are therefore protected as a whole, not
silently returned with guessed/redacted financial values.

Document-expiry also includes medical document filenames/dates. Consequently the
whole existing report requires scoped `hr.employees.medical.read` before any row
is loaded, both for preview and export. A standard-documents-only report is not
silently substituted. This preserves full-report meaning without leaking medical
metadata to a user holding only general document/identity permissions.

## Filters And Evidence

- Employee/department/site/branch selectors narrow server-selected scope. An
  omitted selector never means unrestricted authority. All 14 repository reads
  require a server-produced selection, including exports and document branches.
- Unscoped audit records require All scope for every required permission and no
  employee/department/site/branch selector. The current generic audit DTO also
  exposes target/actor metadata not proven by EmployeeScopeId, so the entire
  audit variant requires All scope for both report-read and audit-read. A scoped
  audit view needs a separately designed protected projection; it is not silently
  enabled here.
- `Page >= 1`, `PageSize` 1-1000; overflowing page offsets and empty GUIDs are
  invalid. Ranges are inclusive and at most 366 days. Tracking uses its separate
  exclusive-To contract; do not reuse tracking date semantics for these reports.
- Dates normalize once before authorization and query. Daily uses `Date`;
  monthly/payroll use the selected calendar month; leave/KPI use the selected
  calendar year. Master/document authorization uses current scope, while document
  `Date` retains the expiry horizon. Unspecified dates and current authorization
  use the existing `RosterSchedule.Today` business-date contract, including the
  hours after local midnight but before UTC midnight. Fiscal/report-display-time
  correctness remains H1; no new timezone policy is invented.
- Export `Reason` is required, nonblank, at most 1000 characters and contains no
  control characters. CSV/XLSX use the requested filter/page, not an unbounded
  whole-company dump. Existing web CSRF and response no-store behavior remain.
- Read/export audit records retain the real assignment role, session/correlation
  context and protected filter/selection/reason evidence. Selection retains the
  contributing read-role assignment IDs; the recorded role comes from those
  grants, not the first unrelated role. Empty selections record query authority
  without claiming data was returned. Failed audit/storage or
  changed/revoked authority prevents response disclosure. Reports create no
  business mutation or idempotency receipt.

## Status And Limits

Standalone verification exercises actual service and evaluator logic with
synthetic collaborators. Repository query tests are separately documented in
`Report-Scope.md`; SQL translation is not SQL persistence acceptance.
Live HTTP/SQL coverage is pending A2's exact reusable target and bounded profile.
No real employee data, production face/OTP providers or client implementation
are part of this repair.

H1 remains open for numerical/source correctness, fabricated report metadata,
payable/absent/leave totals and requested-versus-authoritative financial values.
Securing a report does not certify its values. See `Verification.md` for executed
results rather than treating this contract or registered routes as acceptance.
