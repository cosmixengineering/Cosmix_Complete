# HR and payroll web integration review — 28 October 2026

## Conclusion

The web application has substantial real HR and payroll API integration, but it is not fully connected or accepted end to end. A mounted route, passing build, or available controller is not proof that a workflow completes successfully.

This review preserves the existing checkout and does not apply migrations, seed business data, approve employees, change permissions, finalize payroll, or release payments.

## Evidence and coverage

| Area                                         | What is connected                                                                                                                       | Evidence/limit                                                                                                                                                                  |
| -------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Employee directory and hiring                | Directory, draft, submission, approval, and employee access screens call existing HR/authentication APIs.                               | Compared frontend methods, contracts, and permission gates with `EmployeesController`, `EmployeeHiringContracts`, and `AccessController`. Fresh write acceptance not performed. |
| Employee current sections and change history | Current-section reads and change-request workflows use `EmployeeUpdatesController`.                                                     | Corrected Active-only UI eligibility assumptions, permission requirements, and history prerequisites. Overview hiring snapshots are not authoritative current records.          |
| Compensation and employee status             | Existing request screens use compensation and lifecycle controllers.                                                                    | Source comparison; approval/write acceptance remains separate.                                                                                                                  |
| Organization and holiday calendars           | Organization masters and holiday revisions have API-backed screens.                                                                     | Source comparison against master and holiday controllers.                                                                                                                       |
| Attendance tracking                          | Monthly matrix, daily facts, employee search and pagination use real directory, calendar and session reads.                             | Earlier live authenticated checks passed; see `Attendance-Tracking.md`. Missing locked history remains explicitly unavailable.                                                  |
| Shift list and schedules                     | Paged/filterable shift list and effective schedule details use `/hr/shifts` and `/hr/shifts/{id}`. Existing create/manage forms remain. | Removed fabricated fallback shifts, worker/site allocations and invented policy amounts. The screenshot is a layout reference, not business data.                               |
| Attendance payroll inputs                    | Preview, saved revisions, revision detail and operational-activation requests/reviews are wired.                                        | Compared source schemas and paths. Saved calculations remain distinct from posted/settled deductions.                                                                           |
| Month finalization                           | Preview, retained lock and guarded finalize form are wired.                                                                             | Source inspection only in this pass; no month was finalized.                                                                                                                    |
| Deduction attestations                       | Saved-revision attestation history is displayed.                                                                                        | **Missing:** frontend write form for the existing attestation POST operation. Batch handoff is a different workflow.                                                            |
| Financial adjustments                        | Backend exposes preview/history, approval, posting and cancellation APIs.                                                               | **Missing:** no frontend caller for `financial-adjustments` was found. Do not claim this workflow is connected.                                                                 |
| Legacy prepared workspaces                   | Some legacy workspace IDs redirect to actual operational screens; remaining prepared workspaces explicitly retain local drafts only.    | A local draft is not a saved server transaction. These are not counted as completed integration.                                                                                |

## Runtime inspection

Payroll contract fixes in this pass cover exact salary-accrual source selection, own-salary decision restrictions, batch cancellation permission, older payment response compatibility without invented actor metadata, read-only salary-import access, nullable statutory entries and paginated statutory obligation selection. These are frontend changes, not proof that payments or settlement were executed.

Additional concrete gaps found:

- The first runtime OpenAPI snapshot did not contain routes for 13 operations present in local source: eight bank-format/export operations, three payroll-settlement reads/exports and two payment-reversal actions. A later successful snapshot had 595 paths, including all six bank paths, three settlement paths and two reversal paths. Registration has therefore changed during this review; this does not prove successful authenticated payment/export execution.
- Confirmed loan-posting GET/POST operations exist but have no frontend caller.
- Employee banking verification, revoke, history and evidence endpoints have no frontend caller.
- Bank-format configuration is nested behind obligation/payment access; a configuration-only user does not yet have an independent entry point.
- Request search currently filters loaded request pages with names from only the first 100 directory records. This is not a complete organization-wide search.

The running API's `/openapi/v1.json` was readable earlier during this review: 582 paths, including 58 paths containing payroll, salary or statutory operations. This confirms registration at that moment, not successful business execution.

The API at `https://localhost:7240` was intermittent during review: connection refusal, a timeout, then successful reads, then another refusal. The prior owned host exited; its log contains SQL connection failures. A subsequent direct read-only SQL connection verified the same `Software-PC1\LOCALDB#36E7B148` / `Cosmix_Testing` database ONLINE, the retained family identity and the same MDF/LDF paths. It now had 31 already-applied migrations; this review applied none. No database-loss or definitive outage-root-cause claim is made.

During the successful window the updated Shifts screen displayed five real records. Cards/table switching, a server search for `SH-SITE` returning two records, inactive filtering returning a genuine empty result, and opening the retained night-shift detail were checked. Day/night timings, breaks and weekday chips rendered from saved schedules. Desktop and narrow-screen layout were inspected. These are read-flow checks; no shift was created or edited.

The API Release build passed with zero warnings/errors. No second API host was started over the listener that became available during review.

The previous successful Attendance live checks must not be presented as fresh successful payroll writes. No replacement database, attachment, reset, or migration was attempted to hide this outage.

## Executed verification

- Full frontend `npm run verify` passed: ESLint, Prettier, TypeScript and production Vite build. Main JavaScript chunk remains about 1.46 MB minified; bundle optimization is still open.
- Backend API Release build passed with zero warnings/errors; no migration applied.
- Payroll agent executed 17 in-memory contract/lifecycle assertions; shift agent executed eight seeded component-render assertions. These are offline checks, not SQL business acceptance.
- Live Shift reads: five records; search returned two site shifts; inactive filter returned zero; night-shift Manage opened saved version details; Cards/Table switching passed.
- Responsive inspection at 1440px and 390px passed for the new workspace. Its measured narrow-screen content width was 343px without internal horizontal overflow. Mobile status-label clipping was fixed.
- Create form opened with blank operating times/weekdays and required audit reason; no schedule was submitted. Removed invented new-shift defaults, misleading net-hours subtraction and unsupported compliance claims.
- Browser automation stopped responding after the last form-navigation check. The attempted viewport reset could not be confirmed. This does not invalidate the earlier observed results, but no final post-interruption screenshot or fresh payroll mutation acceptance is claimed.

## Remaining work order

1. Verify the current API/database binding and rerun the updated read screens with real responses.
2. Finish the missing deduction-attestation and financial-adjustment user flows using exact backend permissions, evidence, concurrency and retry contracts.
3. Replace remaining hiring-snapshot overview data with permitted authoritative current-section data; remove or redirect obsolete prepared workspaces deliberately.
4. Exercise each authorized business mutation with bounded synthetic fixtures, including denied access, stale versions, duplicate requests and persistence evidence.
5. Treat real SMS, device/face acceptance and legal activation as their own approvals and acceptance boundaries.
