# Web role entry and payroll comparison — 30 October 2026

This increment uses existing backend permissions and routes. It does not provision
accounts, assign roles, change company configuration, create source evidence, or
extend the employee mobile application with an Admin panel. The API remains
authoritative for every permission, scope and transition.

## Entry and navigation

| Account capability                               | Landing                             | Presentation boundary                                                                  |
| ------------------------------------------------ | ----------------------------------- | -------------------------------------------------------------------------------------- |
| Existing Admin/SuperAdmin access capability      | `/admin/dashboard`                  | Existing `isSystemAdmin` grant classification; fixed server roles remain authoritative |
| HR employee read                                 | `/hr/dashboard`                     | All `hr.employees.read`                                                                |
| Finance accounting read/configuration/accrual    | `/hr/payroll-accounting`            | At least one existing All grant matching an available page tab                         |
| Payroll salary/handoff read                      | `/hr/payroll-handoff`               | All `attendance.payroll-salary.read` or `attendance.payroll-handoff.read`              |
| Scoped attendance summary/exception access       | `/hr/attendance`                    | Matching Employee/Department/Team/Site/Project target or All grant                     |
| Scoped overtime review                           | `/hr/overtime/review`               | Matching `attendance.overtime.review` scope                                            |
| Scoped correction review                         | `/hr/attendance/corrections/review` | Matching `attendance.corrections.review` scope                                         |
| Scoped site reports                              | `/hr/attendance/site-reports`       | Matching `attendance.site-reports.read` scope                                          |
| Only Self grants/no matching operational landing | `/account`                          | Account security/session controls; personal attendance/leave remains mobile            |

Where multiple capabilities coexist, Admin, HR, accounting, payroll, then operational
entry takes precedence. The sidebar exposes other available destinations. `/self-service`
opens Account & Security; the old prepared employee workspace is not mounted.
Non-admin `/admin/*` deep links now return to the account's permitted landing.

The shell's employee search and quick shortcuts require their destination access.
Operational navigation evaluates each permission after excluding Self grants, so
an unrelated management grant cannot expose personal workflows. Server scopes
remain authoritative. The exact static destination map is in
`src/auth/workspace-access.ts`; existing policy and attendance navigation definitions
provide their respective additional links.

## Payroll comparison

Route: `/hr/payroll-reconciliation`. API prefix: `/hr/payroll/reconciliation`.
All permissions below require All scope for this workspace.

| UI action                           | Existing permission                    | API                                                      |
| ----------------------------------- | -------------------------------------- | -------------------------------------------------------- |
| Saved monthly list, detail, history | `attendance.payroll-salary.read`       | GET `parallel`, `parallel/{id}`, `parallel/{id}/history` |
| Historical sheet comparison         | `attendance.payroll-salary.review`     | POST `historical`                                        |
| Prepare parallel run                | `attendance.payroll-salary.request`    | POST `parallel/initiate`                                 |
| HR explanation/decision             | `attendance.payroll-salary.review`     | POST `parallel/hr-review`                                |
| Finance decision                    | `payroll.accounting.configure.approve` | POST `parallel/finance-review`                           |
| Management decision                 | `attendance.payroll-handoff.lock`      | POST `parallel/management-approval`                      |

The read permission is required to enter the page. Preparation accepts a real CSV
or XLSX source up to 4 MiB, a selected frozen batch and explicit mappings for every
required source column. Deductions excludes employee EOBI/social security/income
tax; these are separate explicit fields. The UI shows all component comparisons,
including individual EOBI, SESSI/PESSI and income-tax amounts, even if aggregate
statutory differences offset.

Saved reports are validated against the run's period, batch and fingerprints.
HR submissions preserve server employee/component/amount fields exactly and edit
only root cause and evidence. Initial Unclassified rows require explicit review;
nonzero differences cannot become Exact match. Review decisions bind the current
version. The preparer and previous reviewer accounts/employees cannot perform
later stages. Reconciliation reviews do not approve a new salary, release funds,
or create bank instructions.

Sensitive forms use memory-only state, current-session cancellation, dirty-navigation
protection and reviewed idempotent submission. After an uncertain response the
same immutable action is retried. Failed or missing reads remain unavailable;
they are never replaced with fabricated successful comparison data.

## Other corrections and verification boundary

The advances/loans page no longer describes static labels as live payroll sync or
approved policy terms. Count cards distinguish loading/errors from observed zero.

Final verification passed: `node --test scripts/*.test.mjs` reported **15/15 passed,
zero skipped** (six existing evidence/readiness checks, five entry/scope checks and
four comparison-contract checks). `npm run verify` passed full ESLint, whole-tree
Prettier, TypeScript and production Vite build. The existing large-chunk warning
remains; the main JavaScript bundle is approximately 1.69 MB before gzip.

An intermediate lint run found two test-only global-identifier issues, corrected
without runtime changes, and an unused import during concurrent editing; that
import was already used again in the latest source and was preserved. The next
format check identified five concurrently modified files, formatted with no semantic
changes before the final successful verification. Existing EMP-code edits remain.

Source/contract checks are not browser acceptance. Parent audit work owns the live
browser record; no browser was driven by this frontend subtask. Actual company
historical/HBL evidence and named parallel sign-off remain deferred.
