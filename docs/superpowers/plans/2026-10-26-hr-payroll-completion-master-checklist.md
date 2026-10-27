# HR and Payroll Completion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans when implementation is requested. Steps use checkbox (`- [ ]`) syntax for tracking. This document is the requested master work list; it does not authorize payments, invent company policies, or claim implementation is complete.

**Goal:** Complete and verify the employee/attendance-to-salary-to-payment workflow against the CEO documents and the user's existing mobile requirements.

**Architecture:** Preserve the current .NET application/domain/infrastructure boundaries, React web structure and React Native mobile services. Build one authoritative, versioned payroll run from approved compensation and attendance inputs; Finance and employee history consume that same run. Reuse existing employee, leave, correction, approval, loan and handoff functionality.

**Tech Stack:** Current .NET 10 / EF Core / SQL Server backend; React/TypeScript web; React Native/TypeScript mobile.

**Spec:** [CEO document and source gap review](C:/Users/Huma.Khan/.codex/artifacts/cosmix-ceo-review-20261026/HR-Payroll-CEO-Gap-Review-2026-10-26.md). Primary PDFs: [Implementer Booklet](C:/Users/Huma.Khan/Downloads/Cosmix_Hub_Implementer_Booklet_v2.0.pdf), physical pp20–23,44–46,75–76; [Build-Out Recommendations](C:/Users/Huma.Khan/Downloads/Cosmix_Hub_BuildOut_Recommendations.pdf), pp10,15–16,22,26,32–33,53–54,58,62–63.

**Global constraints**

- This is planning based on the 26 October review. Recheck affected code before implementation because other chats are editing it; mark findings resolved when supported by new evidence.
- The PDFs describe a different repository. Preserve this workspace's architecture; use the documents for requirements, not assumptions about current code status.
- Real screens and APIs must never substitute sample employee amounts or turn a failed operation into success.
- Salary and statutory amounts come from approved records. No invented tax/contribution rates, equal allocation of scheme totals, bank account defaults or automatic approval of SAMPLE policies.
- Enforce permissions and employee scope on the server. Financial changes retain actor, source, version and audit evidence.
- Locking and posting must be concurrency-safe and idempotent. Preserve existing evidence; posted corrections use approved adjustments/reversals.
- Follow repository instructions and existing test/build patterns. Use meaningful permission, calculation, state-transition and integration checks; do not fabricate passing results or recreate a test framework unnecessarily.
- One owner coordinates shared payroll contracts and database migrations. HR and Finance may work independently only where their file and contract boundaries are clear.
- The approved attendance mode must reconcile manual camera check-in/out with the booklet's automatic fence/ping proposal before background attendance is introduced.
- Completion requires actual acceptance evidence, not merely screens, source files or a successful build.

**Review focus**

1. API/network failure must preserve authoritative state and show a recoverable error: checked by tasks 02–03 and 28.
2. Another employee or an unauthorized logged-in user must not read/export/pay salaries or publish policy: checked by tasks 04,25 and 28.
3. A mid-month salary/site/leave/status change must use approved effective-date policy and never silently invent an amount: checked by tasks 10–15 and 29.
4. Repeated or concurrent lock, import, posting and payment requests must not duplicate effects or accept stale evidence: checked by tasks 03,13,16,20–24 and 28.
5. Consent failures, location loss, device replacement and face/camera failure must produce explicit recoverable outcomes without fabricated attendance: checked by tasks 05–09 and 28.

**Ownership and boundaries**

| Owner | Responsibility |
|---|---|
| HR | Employee/leave/compensation rules, attendance review and closure, salary calculation, payslips and employee contribution inputs. |
| Finance | Approved accounts/banks/periods, accrual and liabilities, statutory payable posting, bank output, settlement/reconciliation and reversals. |
| Mobile/shared integration | Consent, device/session rule, chosen attendance behavior, personal history and device acceptance. |
| Shared contract/migration owner | Run schema and state transitions, immutable totals, source identifiers, API versions, audit and migration coordination. |
| Company HR/Finance/Management owners | Policy/amount inputs, named approvers, real payroll data and final acceptance. These are business decisions, not values the developer should guess. |

**Code boundaries for subsequent implementation tasks**

Existing anchors are linked below. The implementing task must read the applicable AGENTS.md and current contracts before changing them. New salary-run files belong alongside existing HR feature folders, with exact interfaces agreed in task 01.

- Web payroll behavior: [PayrollHandoffPage.tsx](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_frotned/src/modules/hr/pages/PayrollHandoffPage.tsx).
- Approved input projection and batch versions: [PayrollHandoffService.cs](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Application/HR/PayrollHandoff/PayrollHandoffService.cs), [PayrollHandoffContracts.cs](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Application/HR/PayrollHandoff/PayrollHandoffContracts.cs).
- Compensation: [EmployeeCompensationService.cs](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Application/HR/Compensation/EmployeeCompensationService.cs).
- Finance connection: [PayrollAccountingService.cs](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Application/HR/PayrollAccounting/PayrollAccountingService.cs), [PayrollAccountingController.cs](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Api/Controllers/HR/PayrollAccountingController.cs), [PayrollAccountingPage.tsx](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_frotned/src/modules/hr/pages/PayrollAccountingPage.tsx).
- Contributions: [StatutoryContributionService.cs](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Application/HR/StatutoryContributions/StatutoryContributionService.cs), [StatutoryContributionsController.cs](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Api/Controllers/HR/StatutoryContributionsController.cs).
- Consent and GPS: [locationConsentService.ts](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_mobile/src/services/consent/locationConsentService.ts), [AttendanceScreen.tsx](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_mobile/src/screens/AttendanceScreen.tsx), [CurrentLocation.ts](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_mobile/src/services/location/CurrentLocation.ts), [LocationConsentController.cs](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Api/Controllers/Attendance/LocationConsentController.cs).
- Device/session management: [MobileAuthenticationService.cs](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Application/Authentication/MobileAuthenticationService.cs).
- Employee salary history: [EmployeePayrollHistoryReader.cs](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Infrastructure/Repositories/HR/EmployeePayrollHistoryReader.cs).

**Phase 1 — truthful behavior, access and shared contract**

- [ ] **01. Establish one payroll contract and migration owner — Shared.** Recheck the review against current work. Record run/employee/component identifiers, source and policy versions, decimal/currency rules, approval/lock/post/payment states and correction links. Map each later task to an owner and affected files. **Done when:** HR and Finance have one written contract and no competing salary totals or overlapping migration ownership. Enables tasks 12–26.
- [ ] **02. Remove false payroll success and synthetic fallbacks — Web/HR.** Remove automatic sample preview/batches and browser-storage approval/processing paths from the real workflow. **Done when:** rejected or unavailable API operations show failure and never change the displayed authoritative payroll state or fabricate records. Covers review issue 1.
- [ ] **03. Repair versioned locking and real exports — Web/HR.** Send the required current batch version, retain preparer identity and load actual batch detail/server exports. **Done when:** a stale lock is rejected, an authorized fresh lock persists, and CSV rows/amounts match server records. Depends on current contract review in 01; covers review issue 2.
- [ ] **04. Enforce sensitive API permissions, scope and audit — Shared/security.** Apply established HR/Finance authorization patterns to accounting, statutory records/exports and policy administration, including configured MFA requirements. **Done when:** ordinary employee, wrong-scope and self-approval requests are denied; permitted views/exports/actions retain audit evidence. Covers HR-04 and SEC-16.
- [ ] **05. Fix mobile consent service and error behavior — Mobile.** Use the actual request-capable session service, remove error-to-NotRequired handling, and align requests/responses with the backend. **Done when:** required consent is displayed; denied/network/session errors stay visible; failed acceptance never unlocks a gated action. Covers the observed integration defect.

**Phase 2 — attendance and HR source readiness**

- [ ] **06. Record approved operating rules — HR/Management.** Obtain shift/workweek, timezone, full/half-day, grace/OT, leave, paid/unpaid treatment, proration, rounding, salary components and named approval rules from approved records. **Done when:** versioned rules have owners/effective dates, and missing required values explicitly block dependent calculations. No SAMPLE value becomes policy automatically.
- [ ] **07. Resolve manual versus automatic attendance — Product/HR/mobile.** Decide how camera check-in/out relates to automatic fence events, shift pings and no-signal review; specify which evidence can create a payable attendance event. **Done when:** one approved flow defines duplicates, foreground/background operation, exits, missed signals and HR review. Then implement only that agreed flow. B pp75–76; user mobile requirements.
- [ ] **08. Enforce the approved single-device/recovery flow — Authentication/mobile.** Implement atomic active-device restriction and authorized replacement/recovery without breaking account access. **Done when:** concurrent second-device login cannot create two active allowed devices; replacement invalidates old access and retains audit. B p75.
- [ ] **09. Complete attendance exceptions and month closure — HR/mobile.** Validate geofences, mock-location/travel checks, camera/liveness failures, duplicate/retried punches, missing check-out, cross-midnight shifts and any approved no-signal flow. **Done when:** unresolved pay-affecting cases block closure; corrections require independent review; no-signal is not automatically absence. Depends on 06–08.
- [ ] **10. Verify employee, leave and lifecycle integration — HR.** Reuse existing services; check leave entitlement/usage/cancellation, compensation changes, joining/leaving dates, scope transfers and access revocation. **Done when:** approved effective dates and leave treatment correctly reach the payroll input snapshot, with employee-visible balances/history. Depends on 06.
- [ ] **11. Complete location notice and retention operation — HR/mobile/backend.** Publish an approved versioned notice, collect acceptance during onboarding/first login, and configure approved GPS retention while preserving required summaries/audit. **Done when:** consent changes, refusal, failed requests and cleanup are verified; unauthorized users cannot change policy. Depends on 04–07; SEC-12/Q50.

**Phase 3 — complete salary run and payslips**

- [ ] **12. Complete effective-dated salary inputs — HR.** Validate base compensation and approved earning/deduction components; define the source precedence between master data and imported sheets. **Done when:** every included employee has a traceable approved salary source, or a visible blocking issue. Depends on 01,06,10.
- [ ] **13. Build salary-sheet CSV/XLSX import — HR/web.** Support column mapping, validation preview, duplicate protection, source retention and authorized application. **Done when:** invalid/unknown/duplicate employee rows and missing amounts are reported before mutation; accepted rows reconcile to the source file; retries do not double-apply amounts. Depends on 01,12; B p46.
- [ ] **14. Build full per-employee gross/net calculation — HR/backend.** Include base salary, approved OT/allowances, attendance deductions, loan recoveries and applicable explicitly entered deductions/contributions. **Done when:** every amount has a source; totals/rounding/currency are consistent; unsupported or missing policy inputs block finalization. Depends on 06,09,12–13 and 18 where applicable. Handoff adjustments alone must not be labeled complete salary.
- [ ] **15. Complete effective-date and exception calculations — HR.** Apply approved rules to mid-month joiners/leavers, salary changes, unpaid leave, attendance corrections and adjustments after closure. **Done when:** agreed reference cases reconcile employee-by-employee; the same component or loan installment cannot be counted twice. Depends on 10,14.
- [ ] **16. Complete approval, lock and correction lifecycle — HR/Management/shared.** Enforce approved HR approval, Management lock and Finance handoff boundaries, separate actors, current source versions and completeness gates. **Done when:** incomplete payslips/unresolved required evidence cannot be approved; locked results are immutable; post-lock/posting corrections follow the appropriate approved adjustment or reversal route. Depends on 04,14–15; HR-04.
- [ ] **17. Produce employee payslips and controlled exports — HR/web/mobile.** Generate salary breakdown PDF and required Excel exports from the approved run. **Done when:** employee and HR views agree with the same run version; personal access is scoped; gross/net and deductions reconcile; payroll status and payment status are distinct. Depends on 14–16.

**Phase 4 — contributions and Finance posting**

- [ ] **18. Store actual employee/employer contributions — HR.** Obtain applicable registrations and authorized monthly values; replace aggregate equal allocation with employee-level eligible entries. **Done when:** no amounts are invented; enrollment/month applicability is respected; employee totals exactly reconcile to scheme totals. Depends on 01,06 and Q26 input; HR-03.
- [ ] **19. Complete statutory approval, payable posting and schedule — HR/Finance.** Produce monthly schedules from actual lines and connect posting to the Finance ledger. **Done when:** Posted means a real linked journal exists; duplicate posting is prevented and reported values reconcile. Depends on 04,18,20; HR-03.
- [ ] **20. Bind accounting to authoritative locked payroll — Finance/shared.** Derive all accounting amounts from the approved run instead of caller gross/net totals. Use approved chart accounts, financial periods and required project/cost allocations. **Done when:** unlocked, stale, mismatched and already-posted requests are refused; journal references identify the exact run. Depends on 01,04,16 and Q22–24 inputs.
- [ ] **21. Post complete salary and deduction accounting — Finance.** Split salary expense, net salary payable, employee deduction/contribution liabilities and applicable employer contribution expense/payables. **Done when:** balanced entries reconcile to employee totals and correct accounts; retry and closed-period cases behave correctly. Depends on 18–20; B pp20–23.

**Phase 5 — bank payment and employee history**

- [ ] **22. Complete approved bank details and payment export — Finance/HR.** Obtain the chosen salary payment method and actual bank bulk-file specification; protect employee banking data and audit export. **Done when:** the file uses locked net salary amounts and passes the bank-format acceptance check; no hardcoded bank defaults or generic adjustment-only file is presented as a payment file. Depends on 04,16,20–21 and Q24–25 inputs; HR-02.
- [ ] **23. Complete payment records and independent confirmation — Finance.** Bind each payment to employee entitlement/run and retain the Finance confirmation evidence/reference. **Done when:** confirmed, pending, failed and partial outcomes are accurate; retries cannot duplicate payment records; total settlement cannot exceed approved entitlement. Depends on 21–22.
- [ ] **24. Complete reconciliation, failure recovery and reversals — Finance.** Match payment outcomes to the payroll and liability ledger, support approved retry of failed items and compensating entries where needed. **Done when:** partial/rejected payments and reversals leave correct outstanding balances and an auditable history. Depends on 23.
- [ ] **25. Finish unified employee history — Mobile/HR.** Use one history area with categories for check-in/out times, leaves/balances, advances/loans, other supported requests, payslips and salary payment status. **Done when:** records come from backend, pagination/filters work, amounts agree with payroll and employees cannot access one another's records. Depends on existing self-service APIs, 10,17,23–24.
- [ ] **26. Complete operational reports — HR/Finance.** Provide payroll register, component/deduction totals, contribution schedule, paid/unpaid/partial salary and reconciliation reports. **Done when:** reports and exports reconcile with the same source run and ledger, obey permissions and record export audit. Depends on 17–25.

**Phase 6 — integration evidence and acceptance**

- [ ] **27. Verify migration/deployment readiness — Shared/IT.** Coordinate required migrations, review existing-data impact, rehearse deployment/recovery and verify configured background jobs, storage protection and production login delivery. **Done when:** the intended environment has compatible schema/configuration and retained deployment evidence; no development fallback is mistaken for production authentication. Depends on the implemented work above.
- [ ] **28. Run focused integration and device checks — All owners.** Exercise unauthorized access, wrong employee scope, stale versions, concurrency/retries, money rounding, import failures, consent/network/camera failures and the selected attendance model. Test at least three staff Android phones for the required pilot flow. **Done when:** failures are resolved, relevant builds/checks pass, and actual results are recorded. Depends on 02–27; MOB-01 and the five review-focus classes.
- [ ] **29. Reconcile a historical payroll month — HR/Finance.** Use approved actual salary, attendance, leave, loan and contribution data; compare each employee and accounting total to the real reference. **Done when:** every difference is explained/corrected and approved. Depends on 26–28 and Q27 data; HR-01.
- [ ] **30. Complete a parallel live month and signed handover — HR/Finance/Management.** Run the full cycle through salary payment evidence, employee payslips/history, month-end reconciliation and user training. **Done when:** named owners sign the acceptance checklist and the chosen pilot scope is approved for live operation. Depends on 29 and Q51/Q53 inputs; HR-01, GOV-02/04.

**Required business inputs and their dependent tasks**

| Input | Source | Needed by |
|---|---|---|
| Named approvers and separation rules | Q4/Q5 | 04,06,16,19,23 |
| Approved attendance, leave and salary policy | Booklet pp45,75–76 | 06–07,09–10,12–16 |
| Accounts, financial year and banks | Q22–24 | 20–22 |
| Payment method and bank format | Q25 | 22–24 |
| Applicable registrations and actual contributions | Q26 | 18–19 |
| Historical and parallel-month inputs | Q27 | 29–30 |
| Location notice and retention | Q50 | 07,11 |
| Pilot scope and accepting owners | Q51/Q53 | 28–30 |

**Completion rule:** Mark an item checked only when its stated result has supporting evidence. Coding, company configuration and real-data acceptance are separate deliverables. Independent tasks may proceed while business-dependent tasks await the specified inputs; elapsed time is never policy approval.

**Plan self-review:** The four HR recommendations, identified current defects, attendance/device/consent differences, user-requested unified history, shared Finance dependencies and real-month/device acceptance each map to the tasks above. No implementation changes or tests were performed while preparing this work list.
