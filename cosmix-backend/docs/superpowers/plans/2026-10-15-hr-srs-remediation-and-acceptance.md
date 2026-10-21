# HR SRS Remediation and Acceptance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax. Execution method and shared ownership must be agreed before dispatch; this plan does not authorize new chats, Git operations or database writes.

**Goal:** Repair the audited HR/Attendance defects, finish approved incomplete backend workflows, and produce current authenticated SQL acceptance evidence without claiming that external provider or client acceptance has occurred.

**Architecture:** Extend the existing .NET 10 Clean Architecture and module repositories. Reuse the existing authorization operations, immutable calculation revisions, approvals, audit/outbox, private storage and financial attestations. No replacement HR system, duplicate payroll engine, identity system or generic repository framework.

**Tech Stack:** ASP.NET Core 10, C#, EF Core, SQL Server, existing standalone C# and PowerShell verifiers. React and React Native remain separate clients.

**Spec:** `C:/Users/Huma.Khan/Downloads/HR_Attendance_Employee_Mobile_SRS_v1.2.docx`, repository `docs/requirements/HR_Attendance_Employee_Mobile_SRS_v1.2.docx`, the user's confirmed amendments, and `C:/Users/Huma.Khan/Desktop/Cosmix_HR_SRS_Backend_Audit_2026_10_15.docx`.

**Planning date:** 15 October 2026. This is a proposed execution plan, not evidence that the repairs have been made.

## Global Constraints

- Work only in the existing local checkout; preserve all current changes.
- No fetch/pull/push, commits, merges, PRs, branches, worktrees or automations. This overrides skill examples that suggest commits.
- Do not recreate test projects. Extend the existing standalone verification approach.
- Preserve applied migrations, current authentication, encrypted storage, custom-role permissions and unrelated HR features.
- One agreed owner for permissions, DI, DbContext, model snapshot and each migration window.
- Face verification remains Check-In only and fail-closed. Real face/model/device acceptance is explicitly excluded from this workstream.
- External OTP delivery/provider acceptance is excluded. Test real authentication/session/MFA/CSRF behavior through the existing isolated synthetic delivery mechanism, never a production authentication bypass.
- Mobile punches plus approved HR corrections only; attendance file import is out of scope.
- Site-assigned staff inherit historical site schedules. No individual-over-site precedence or new site-shift approval stage.
- One authorized HR reviewer handles ordinary corrections and OT; authorized Admin handles leave and approved special medical/accessibility/boundary alternatives. Preserve self-review restrictions.
- Admin policy configuration follows existing permissions. SuperAdmin company settings do not imply legal deduction approval authority.
- Legal activation authority remains proposed, not approved. No fabricated legal clearance or automatic financial activation.
- Locked months never reopen. Approved later adjustments use the first eligible unlocked month, visible before approval.
- Schema-3 ordinary and late deductions use two decimals and larger-only overlap handling; preserve schema-1/2 behavior.
- OT is BASIC / actual source-month calendar days / 9 * approved hours, separately rounded to whole rupees. Office staff remain ineligible; no break subtraction from this approved formula.
- A saved calculation, no-charge handoff, bank advice or processing reference is not proof of posting, settlement or payment. No salary payment release.
- Company schedules, KPI values, entitlements, rates and eligibility must be explicit approved policies, not guessed fallback values.
- No production/development business data or real employee credentials in tests. Database writes require an identified approved target and bounded write scope; a planning document is not that approval.

## Review Focus

1. Revoked, transferred, mobile or non-MFA actors replay a previously valid request: current authorization and historical/current scope rules must still hold. Tasks B1/B2 and J.
2. Missing or inaccessible financial source versus a genuinely complete zero-deduction source: incomplete coverage must block, never become zero. Tasks E1/F2.
3. A second correction or simultaneous finalization races a source revision: exact immutable membership and first-eligible-target selection must remain valid. Tasks F2/F3 and J.
4. Future-dated compensation, partial leave and signed reversals cross a fiscal/month boundary: every view must use the same authoritative result. Tasks C2/D/E2.
5. More than 500 reminders, process restart or private-storage failure occurs after partial progress: no starvation, duplicate notification, partial financial write or lost evidence. Tasks H/I/J.

## 1. Honest Baseline

The audit tested revision `686d5027e79d1dfa0c2c7df2aadd7d3975e79253`: Release build passed, 45 existing suites passed, one Leave suite failed against an obsolete permission expectation. Seven targeted defect scenarios were reproduced using actual services with synthetic collaborators. This was not fresh authenticated HTTP/SQL acceptance.

Six files already have concurrent local edits: PayrollHandoff controller/contracts/service/repository and VehicleFuel service/repository. They must be compared with the audit before repair. Do not overwrite or call these edits verified merely because they exist. The test suite count is a baseline, not a permanent acceptance quota.

## 2. Delivery Order and Ownership

| Phase | Deliverable | Entry dependency | Exit gate |
|---|---|---|---|
| A | Current baseline and reusable test target | Approved plan | Changed files mapped; current build frozen; exact test scope known |
| B | Close tracking and HR report access gaps | A | Allowed and denied HTTP scopes pass; no bypass write path |
| C | Repair encashment and recovery | B security contract | Correct self scope, real evidence, durable approval and retry checks |
| D | Reconcile employee self-service | C source contracts; can start independent reads earlier | Leave/salary/loan views match authoritative records |
| E | Repair loans and Finance handoff | B and current concurrent-code review | Handoff cannot post deductions; incomplete sources block |
| F | Reconciliation, legal gates and month finalization | E; legal activation conditional | Exact revisions; no dependency cycle; approved legal evidence required |
| G | Finish benefits, outstation and vehicle branches | C/E evidence contracts | Complete configured transactions, not models/placeholders |
| H | Report correctness and notifications | B plus owning-module outputs | Correct scoped totals/formats and restart-safe delivery |
| I | Schema and client contract consistency | Each affected slice, not only at the end | Additive migration/schema checks; actual OpenAPI matches |
| J | Integrated acceptance and owner handoff | Each slice feeds evidence; full run after integration | Current release, HTTP, SQL, storage, race and recovery evidence |

Recommended use of a three-person team, respecting `docs/Work-Ownership.md`:

- **Backend owner 1:** HR leaves/ESS/reports/allowances/vehicle/loans; preserves existing Developer 1 ownership.
- **Backend owner 2:** Attendance corrections/calculations/KPI/month locks/financial source producer/workers; preserves existing Developer 2 ownership.
- **Third person / PM:** API/client contract review, synthetic acceptance scenarios and UAT coordination; do not assume this is a third full-time backend engineer if they own web/mobile.
- One of the two backend owners is designated integration/migration owner per working window. Do not let both edit shared financial contracts or snapshots simultaneously.
- Small parallel slices are appropriate after interfaces are agreed. Finalization and financial handoff are a coordinated integration task, not competing independent rewrites.

No agent has been dispatched and no new ownership assignment communicated by creating this plan.

## 3. Common Implementation and Test Cycle

For every task below:

1. Capture the relevant current diff and reproduce the defect on the current frozen binaries.
2. Add a standalone regression asserting the correct behavior; verify it fails for the actual reason.
3. Implement the smallest scoped correction in the owning service/repository.
4. Run the affected standalone checks and a clean Release build with retained output hashes.
5. On the approved isolated host, execute the corresponding authenticated API scenario and inspect independent SQL/audit/receipt/outbox results. Use separate requests/connections for races.
6. Record source revision and dirty-file hashes, commands, pass/fail, SQL target and remaining limits. Review before moving the task to Accepted.

Do not mark a financial task Accepted after only steps 1-4. Do not delay all real integration checks until Phase J. Phase A establishes a reusable environment; later tasks reuse its approved bounded scope without repeatedly redesigning it.

### A1. Reconcile the Current Baseline

**Files:** Read `AGENTS.md`, `README.md`, `docs/Architecture.md`, `docs/SRS-Module-Map.md`, `docs/Work-Ownership.md`, audit F01-F14, and the six concurrently edited files. Create `docs/hr-srs-remediation/Status.md` and `docs/hr-srs-remediation/Verification.md` during execution.

**Interfaces:** Consume the existing controller/service contracts. Produce a task ownership matrix and a file-hash manifest, not new application interfaces.

- [ ] Confirm which F01-F14 still reproduce after the concurrent edits; retain evidence for any already repaired finding.
- [ ] Record the exact HEAD plus dirty-file hashes and freeze a new Release output.
- [ ] Add each finding and missing SRS branch to Status with one owner, dependencies and an evidence link.
- [ ] Preserve current tests and the failed Leave test until C1 verifies the approved expectation; do not reduce permission checks to make it pass.
- [ ] Agree an integration window for PayrollHandoff/VehicleFuel before modifying them.

**Accepted when:** Everyone is testing the same identified version; no duplicate repair or overwritten local work.

### A2. Establish One Reusable Isolated Acceptance Setup

**Files:** Reuse `docs/attendance-completion/Huma-Acceptance-Preflight.ps1`, `Huma-Acceptance-Common.ps1`, `Huma-Acceptance-Auth.ps1`, existing acceptance manifest examples, `docs/access-provisioning/RunAuthenticatedAcceptance.ps1`, and the existing guard/start/stop scripts. Inspect each script before execution; some retain source-laptop paths or old migration ranges.

**Interfaces:** Existing guarded manifest -> actual local SQL target, private storage and owned API process. No application connection-string changes committed or permanently saved.

- [ ] Read-only verify the actual instance/catalog/owner, migration history, data origin and approved manifest. Do not infer state from old docs.
- [ ] Reuse the already approved synthetic environment only if identity and scope still match. If ambiguous, stop DB writes and present one exact replacement proposal; independent code/test work continues.
- [ ] Review every pending migration and seed operation. If upgrading an existing approved test DB, back up and verify before applying only the pending range.
- [ ] Bound synthetic accounts, dates, sites, policies, files and enabled workers. Isolate keys, credentials and encrypted evidence; deny real SMS, face and payment calls.
- [ ] Run real synthetic-user login, MFA, CSRF, session revocation and read-only DB smoke checks before module scenarios.
- [ ] Keep this host/profile for subsequent tasks within approved scope. Any cleanup must be a separately reviewed operation limited to owned test resources.

**Accepted when:** One real authenticated request and independent SQL verification succeed on the exact approved target; all other databases/configuration remain untouched.

### B1. Close the Tracking Write/Read Bypass (F01)

**Files:** `src/Cosmix.Api/Controllers/HR/EmployeeTrackingController.cs`; reuse existing Attendance capture, corrections and scoped reads. Test: create `docs/hr-srs-remediation/VerifyTrackingBoundary.cs`.

**Interfaces:** Existing `Get(Guid id, CosmixDbContext db, CancellationToken ct)` and `RecordCheckIn(...)` are the audited unsafe boundary. Preserve legitimate client reads through an authorized Application service; a controller must not directly manufacture RawPunchEvents.

- [ ] Add regressions: foreign employee, wrong site/department, mobile management caller, missing MFA and revoked role cannot read or write; denial produces no punch/audit-success/receipt side effects.
- [ ] Retire the bypass `record-checkin` action with an explicit unavailable/deprecated contract, and route client attendance writes to the existing capture/correction flows. Do not merely add an HR role and retain the bypass.
- [ ] Replace tracking direct DbContext reads with the owning module's server-scoped service/repository and protected DTOs.
- [ ] Remove fabricated verification defaults, random server idempotency identity and fake device provenance from the unsupported path.
- [ ] Test authorized scoped reads and the documented alternative correction path; real provider Check-In remains excluded and fail-closed.

**Accepted when:** No alternate route can insert purported verified raw attendance; legitimate scoped history remains usable.

### B2. Repair Report Authorization (F02)

**Files:** `Application/HR/Reports/HrReportService.cs`, `IHrReportRepository.cs`, `Infrastructure/Repositories/HR/HrReportRepository.cs`, `Api/Controllers/HR/HrReportsController.cs` under `src/Cosmix.*`. Test: `docs/hr-srs-remediation/VerifyHrReportBoundary.cs`.

**Interfaces:** Preserve `GetReportDataAsync(string reportType, HrReportFilter filter, AuthRequestContext context, CancellationToken ct)` and `ExportReportAsync(string reportType, HrReportExportRequest request, AuthRequestContext context, CancellationToken ct)`. Add the existing validated actor/scope contract to all repository reads; caller filters must intersect scope, never define authority.

- [ ] Turn audit R01-R03 into regressions: mobile/no-MFA access denied; report-read cannot export; organization-management alone cannot read payroll reports.
- [ ] Use the established operation/session authorization and revalidation. Remove broad organization/role-management fallback grants.
- [ ] Enforce per-report financial/medical/bank permissions and separate export permission before loading rows.
- [ ] Apply server scope before filtering, totals and pagination in every report repository query; audit protected reads/exports.
- [ ] Verify self/team/site/department/all and revoked historical receipt behavior, including zero unauthorized rows in SQL-backed exports.

**Accepted when:** All 14 report variants obey the same documented permission/scope matrix. Correct report values are separately tested in H1.

### C1. Fix Encashment Access and Approval Persistence (F03/F05)

**Files:** `src/Cosmix.Application/HR/Leaves/LeaveEncashmentService.cs`, `LeaveEncashmentContracts.cs`, `src/Cosmix.Infrastructure/Repositories/HR/LeaveEncashmentRepository.cs`; existing approval entities/mappings. Tests: new `docs/hr-srs-remediation/VerifyEncashmentWorkflow.cs`, existing `docs/attendance-completion/VerifyLeaves.cs`.

**Interfaces:** Existing SubmitAsync, MineAsync, QueueAsync, DecideAsync, CreateRecoveryCommitmentAsync and GetRecoveryCommitmentsAsync stay the workflow entry points. Use `FindAsync(Guid id, Guid? employeeId, CancellationToken ct)` for ownership. `ExpectedApprovalVersion` must be enforced.

- [ ] Reproduce own Submit/Mine/Recovery authorization failures and a foreign employee recovery read before changing the self-service flag.
- [ ] Correct self-service operation mode and bind identity to current employee. Validate recovery parent ownership before querying commitments.
- [ ] Persist ApprovalDecision and request state atomically with audit/receipt; enforce current scope, expected version and self-review restrictions.
- [ ] Bind recovery evidence to the affected employee and real policy/approval records. Remove random policy/approval-ID fallbacks.
- [ ] Define ApprovedAmount behavior explicitly: compare with the authoritative policy valuation; reject an unsupported override rather than silently ignore it or permit arbitrary money.
- [ ] Correct only the obsolete VerifyLeaves expectation: non-Admin publication remains denied, authorized Admin publication succeeds. Keep all other assertions.
- [ ] Run positive/negative/retry/concurrent-decision tests and independent SQL persistence checks.

**Accepted when:** The authorized employee can submit/read their request, reviewers cannot approve themselves, exact decisions survive restart, and no foreign recovery data is disclosed.

### C2. Replace Encashment Fallback Values With Evidence (F04)

**Files:** C1 service/repository plus existing `src/Cosmix.Application/HR/Leaves/LeaveSelfReads.cs`, `src/Cosmix.Application/Attendance/Kpi/FinalizedKpiReader.cs` and compensation/policy readers. Test: `docs/hr-srs-remediation/VerifyEncashmentValuation.cs`.

**Interfaces:** Replace repository methods that imply a numeric answer when evidence is absent with explicit available/pending results carrying retained source IDs. Reuse finalized KPI and authoritative balance readers; do not add a second KPI or leave calculator.

- [ ] Regressions: no KPI, unfinalized/disputed KPI, no salary, no approved policy, exhausted balance, wrong fiscal year and future-only salary all block with a precise reason.
- [ ] Remove default KPI=100, fixed 18*9*60 quota, fixed /30/9/60 valuation and fallback arbitrary policy version.
- [ ] Evaluate approved eligibility/rate/divisor/rounding at the correct effective date and record KPI, leave balance, salary and policy revisions.
- [ ] Reserve/consume eligible leave through the existing immutable ledger so two concurrent requests cannot cash out the same minutes.
- [ ] Rejected/cancelled requests reverse only their own reservation; later corrections append an adjustment, never overwrite settled evidence.
- [ ] Use an explicitly synthetic policy to test boundary eligibility, partial entitlement and amount precision. Do not activate SRS example values as company defaults.

**Accepted when:** Every amount can be reconstructed from retained approved inputs; absent inputs never produce invented money.

### D. Reconcile Employee Self-Service (F06/F07)

**Files:** `src/Cosmix.Application/HR/EmployeeSelfService/EmployeeSelfService.cs`, `IEmployeeSelfServiceRepository.cs`, `EmployeeSelfServiceContracts.cs`, `src/Cosmix.Infrastructure/Repositories/HR/EmployeeSelfServiceRepository.cs`. Test: `docs/hr-srs-remediation/VerifyEssConsistency.cs`.

**Interfaces:** Existing GetProfileAsync, GetAttendanceCalendarAsync, GetLeaveOverviewAsync, GetFinancialOverviewAsync and GetRequestsSummaryAsync. Consume the authoritative leave/loan/compensation results; do not duplicate their formulas in ESS.

- [ ] Regressions: leave fiscal rollover, fractional day, configured shift length, cancelled leave and multiple historical years return the same balance as LeaveSelfReads.
- [ ] Remove hardcoded 480-minute conversions and AL14/CL10/SL8 fallback entitlements. Return explicit unconfigured/pending state where required.
- [ ] Use effective-as-of compensation and assignment records, excluding future versions.
- [ ] Apply signed loan ledger entries once; a negative reversal must not be negated a second time.
- [ ] Require the proper financial self permission and protected-read audit, not generic auth.self.read as a fallback.
- [ ] Test two employees, revoked permission and date boundaries through authenticated self APIs.

**Accepted when:** ESS and owning modules agree exactly; a payroll handoff is never presented as a paid payslip.

### E1. Separate Handoff From Posting (F08)

**Files:** `src/Cosmix.Application/HR/PayrollHandoff/PayrollHandoffService.cs`, `PayrollHandoffContracts.cs`, `src/Cosmix.Infrastructure/Repositories/HR/PayrollHandoffRepository.cs`; reuse `src/Cosmix.Application/Attendance/PayrollInputs/AttendancePayrollAttestationService.cs` and loan ledger. Test: `docs/hr-srs-remediation/VerifyHandoffPostingBoundary.cs`.

**Interfaces:** Existing PreviewAsync/PrepareAsync/LockAsync/ProcessingAsync and the attestation CreateAsync/ReadConfirmedPostingAsync paths. Agree their exact cross-module source references before both owners edit consumers.

- [ ] Regressions: preparing/locking/abandoning a handoff leaves the actual loan balance unchanged; a processing reference or bank advice is not posting evidence.
- [ ] Remove premature PayrollDeduction creation from handoff locking. Keep any scheduled/reserved state distinct from a posted repayment.
- [ ] Require an authorized Accounts/Finance posting record with exact source revision, employee, month, line IDs, amounts, status, timestamp and actor before a financial balance changes.
- [ ] Preserve manual attestation functionality where already approved; do not fabricate an external payroll integration or receipt.
- [ ] Same posting twice creates one ledger effect; changed amount/source conflicts; reversals bind to one confirmed posting.
- [ ] Recheck concurrent bank-advice edits without assuming generation of an advice document is salary payment.

**Accepted when:** Proposed, handed-off, posted/settled and paid are never conflated. Payment release stays outside scope.

### E2. Fix Loan Retry Identity (F13)

**Files:** `src/Cosmix.Application/HR/AdvancesLoans/AdvanceLoanService.cs`, `AdvanceLoanContracts.cs`, existing operation receipts. Test: `docs/hr-srs-remediation/VerifyLoanRetryIdentity.cs`.

**Interfaces:** Existing ApproveAsync, RecordRepaymentAsync and ReverseRepaymentAsync must bind route request/agreement/entry IDs and affected employee to the operation identity.

- [ ] Same key/body on a different route ID must conflict rather than return another agreement's receipt.
- [ ] Same key/same exact request replays one result; changed payload, stale version or revoked scope must reject correctly.
- [ ] Record the actual authorized actor/role context instead of a hardcoded HR label.
- [ ] Race two repayments/reversals with separate HTTP connections and inspect ledger, approval, audit and receipts.

**Accepted when:** No request can replay a result belonging to a different resource or double-apply a repayment.

### F1. Implement Legal Review Dependencies Without Inventing Authority (F09)

**Files:** `src/Cosmix.Application/Attendance/LegalReview/AttendanceLegalReviewService.cs`, `AttendanceLegalReviewContracts.cs`, `src/Cosmix.Application/Attendance/PayrollInputs/AttendanceMonthLockFinancialReader.cs`, existing protected legal evidence/repositories.

**Interfaces:** Extend the existing versioned legal evidence workflow. A recorded dossier or selected province is not an active legal decision. Production activation authority is a pending company decision, not a new assumed role.

- [ ] Trace current rules to jurisdiction, worker/establishment applicability, rule version, effective dates and signed evidence; research official primary sources during implementation.
- [ ] Implement/reuse review-evidence validity, supersession, expiry and exact-version matching; material rule changes must not inherit old clearance.
- [ ] Test absent, expired, wrong-jurisdiction, wrong-worker and superseded clearance as unavailable for active payroll.
- [ ] Keep production activation blocked until the company formally confirms authority and supplies the actual required legal evidence.
- [ ] Use clearly synthetic legal dependencies only for isolated service/contract tests; do not insert fabricated real legal approvals or call them production acceptance.

**Accepted when:** Review/evidence checks are implemented and tested; production activation is separately labelled pending until external approval actually exists.

### F2. Resolve Readiness, Exact Revisions and OT Ordering (F09/F10)

**Files:** `AttendanceMonthLockFinancialReader.cs`, `AttendancePayrollInputService.cs`, `IAttendancePayrollInputLedger.cs` under `src/Cosmix.Application/Attendance/PayrollInputs`; `src/Cosmix.Application/Attendance/MonthLocks/MonthLockService.cs`; `src/Cosmix.Infrastructure/Repositories/Attendance/MonthLockRepository.cs`; E1 handoff repository/service.

**Interfaces:** Preserve `InspectAsync(DateOnly start, DateOnly end, IReadOnlyList<Guid> employeeIds, AttendanceActor actor, CancellationToken ct)` and VerifyRetainedAsync. Use exact lock source revision IDs, not the ledger's newest revision.

- [ ] Write a dependency/state table with both module owners: source calculation and pre-lock review obligations; immutable lock; downstream Finance handoff; later confirmed posting; subsequent adjustments.
- [ ] Eliminate the circular requirement that current-month OT must be settled before the lock while its handoff requires that lock. Preserve legitimate historical posting requirements for corrections. Review any changed workflow contract before implementing it.
- [ ] Replace unconditional readiness blockers only with verified dependency readers; do not set PayrollReady=true or bypass missing legal authority.
- [ ] Bind complete employee/component coverage and all missing/skipped reasons into the readiness fingerprint. A required missing/inaccessible source blocks preparation rather than silently excluding an employee or charge.
- [ ] Permit exact Lines=[] only for an authoritative saved, ready, issue-free calculation with the approved no-charge handoff; disallow empty posted/settled receipts.
- [ ] Lock retains exact source IDs, versions, evidence hashes and approval/attestation dependencies. Recheck after IO and at commit; reject concurrent changes.
- [ ] Later handoffs consume retained lock membership; never substitute ledger.Latest. Historical authorization/replay behavior remains explicit.
- [ ] Test no-charge, missing-source, mixed-employee failure, OT dependencies, stale evidence, simultaneous correction and competing finalization.

**Accepted when:** The dependency graph has no cycle and valid synthetic dependencies produce the expected lock in isolated tests; real legal activation still has its independent gate.

### F3. Complete Financial Carry-Forward and Subsequent Corrections

**Files:** Existing `AttendancePayrollReconciliationSourceReader.cs`, `AttendancePayrollReconciliationContracts.cs`, `AttendancePayrollInputProducer.cs`, attestation services and correction/carry-forward services; extend existing ledgers rather than introduce payroll duplication. Tests: extend `docs/attendance-completion/VerifyPayrollReconciliation.cs` and add `docs/hr-srs-remediation/VerifyFinancialCarryForward.cs`.

**Interfaces:** Original financial revision + confirmed posting evidence + corrected valuation -> immutable credit/recovery adjustment with source and first-eligible target identities.

- [ ] Test the approved example: confirmed PKR 200 deduction corrected to PKR 100 yields PKR 100 credit; calculated-only PKR 200 yields no refund.
- [ ] Preserve original salary, policy, attendance and jurisdiction evidence and larger-only ordinary/late comparison. Keep OT and its rounding separate.
- [ ] Show target month before approval, then revalidate that it remains first eligible/unlocked at commit. If it changes, require renewed confirmation.
- [ ] Prevent duplicate adjustments across requests/months; a second correction uses prior confirmed adjustments rather than refunding the original amount twice.
- [ ] Absent settlement/valuation evidence remains a precise pending dependency, never an invented recovery or credit.
- [ ] Verify concurrent approval, target locking, receipt replay and subsequent correction against actual SQL persistence.

**Accepted when:** Auditable adjustments reconcile to confirmed money and never rewrite finalized history.

### G1. Finish KPI Benefits, Extra Leave and Recovery Fulfilment

**Files:** Existing HR Leaves policy/service/ledger files, Attendance KPI/FinalizedKpiReader and WorkTreatments services. Extend C1/C2 contracts only where an actual award/fulfilment workflow needs them; do not create another approval subsystem.

**Interfaces:** Approved versioned benefit policy + finalized KPI period + eligibility -> one authorized leave credit OR financial benefit with immutable source references. Recovery commitment -> approved actual fulfilment evidence -> remaining obligation.

- [ ] Define explicit configurable eligibility, period, valuation, authority, evidence and exclusivity fields. No default score, six-day entitlement or cash amount becomes active automatically.
- [ ] Implement submission/assessment, authorized decision, ledger credit/financial line and cancellation/reversal with expected versions and receipts.
- [ ] Prevent awarding both leave and equivalent cash for the same entitlement; prevent annual reruns or corrected KPI from duplicating a benefit.
- [ ] Link recovery fulfilment to genuine approved work/evidence before/after leave. A commitment alone is not worked time or salary recovery.
- [ ] Test absent policy, threshold boundaries, wrong period, self-review, parallel requests, revoked approval and subsequent KPI correction.

**Accepted when:** There is an actual configured award/fulfilment API and persisted result, not only policy fields. Unapproved company values remain pending.

### G2. Complete Outstation Allowance Workflow

**Files:** `src/Cosmix.Application/HR/Allowances/AllowanceService.cs`, `AllowanceRules.cs`, `AllowancePolicyResolver.cs`, `AllowanceContracts.cs`, `src/Cosmix.Infrastructure/Repositories/HR/AllowanceRepository.cs`, existing claim/settlement entities. Test: `docs/hr-srs-remediation/VerifyOutstationAllowances.cs`.

**Interfaces:** Authorized deployment dates/location/category/continuity evidence -> existing claim, approval and handoff/settlement records. The existing `deployment_workflow_unavailable` branch becomes supported only after these inputs exist.

- [ ] Record and review actual deployment start/end and return-home breaks; resolve effective employee/category policy.
- [ ] Calculate only configured entitlements from approved continuous duty/deployment evidence; block unsupported categories/rates.
- [ ] Prevent duplicate meals and a second dinner claim for the same entitlement period. Verified home visits close the old cycle.
- [ ] Persist exact calculation, payable date, policy/source revision, approval and Finance references; handle cancellation and later correction.
- [ ] Test midnight/month crossing, category change, partial interval, return/redeployment, duplicate key and competing claims.

**Accepted when:** A complete synthetic deployment can produce exactly the expected reviewed claim and Finance handoff, with no invented company amounts.

### G3. Complete Vehicle/Fuel/Maintenance/Incident Rules (F12)

**Files:** `src/Cosmix.Application/HR/VehicleFuel/VehicleFuelService.cs`, `VehicleFuelContracts.cs`, `IVehicleFuelRepository.cs`, `src/Cosmix.Infrastructure/Repositories/HR/VehicleFuelRepository.cs`, existing VehicleMileageEntry, VehicleServiceRecord, VehicleIncident and custody mappings. Test: `docs/hr-srs-remediation/VerifyVehicleEntitlements.cs`.

**Interfaces:** Existing SubmitTripAsync/ApproveTripAsync, SubmitFuelClaimAsync/ApproveFuelClaimAsync, SubmitServiceRecordAsync/ApproveServiceRecordAsync and ReportIncidentAsync. Respect current mileage-writer edits before extending these methods.

- [ ] Verify employee eligibility and custody on the trip/service date, not current profile or highest policy version.
- [ ] Require approved route/map-distance evidence where required; distinguish odometer observations, reimbursable distance and excluded commute distance.
- [ ] Derive approved outstation mileage and maintenance cycles from retained records. Enforce configured rate/cap/excluded-cost rules; client mileage/amount is a claim, not proof.
- [ ] Prevent reusing the same mileage cycle or claim in concurrent approvals; retain workshop/service evidence.
- [ ] Complete authorized incident review, liability/insurance decision and outcome records using approved authority. Do not automatically charge employees or mark insurers settled.
- [ ] Test backdated policy, custody transfer, missing map proof, maintenance boundaries, duplicate cycle, invalid workshop and amended incident outcome.

**Accepted when:** Each entitlement is reconstructable and approved; a recorded incident is not mislabelled a completed claim settlement.

### H1. Correct Reports and Export Contracts (F11)

**Files:** B2 report files, HrReportCsvExporter.cs/HrReportXlsxExporter.cs, existing Attendance private export jobs. Test: `docs/hr-srs-remediation/VerifyHrReportResults.cs`.

**Interfaces:** Existing report methods plus B2 server scope. Reuse existing private CSV/XLSX/PDF machinery where suitable instead of building a second job platform.

- [ ] Select the authoritative retained/latest permitted daily revision once; never sum all recalculation revisions.
- [ ] Do not turn absent data into absence or a fixed eight-hour day. Expose missing/unverified results and source provenance accurately.
- [ ] Remove unconditional verified-production metadata. Generated date, filters, policy/revision and lock state must reflect the actual query.
- [ ] Validate each report using a fixture with independently calculated expected rows/totals, scope restrictions and fractional values.
- [ ] Verify real CSV/XLSX/PDF parsing, export auditing, sensitive columns, pagination and bounded background processing.

**Accepted when:** All advertised report types have their own permission/value/format evidence; an older factual-report suite cannot substitute for this.

### H2. Repair Expiry Reminders and Worker Reliability (F14)

**Files:** `src/Cosmix.Api/BackgroundJobs/DocumentExpiryReminderWorker.cs`, existing notification repository/outbox/worker infrastructure. Test: `docs/hr-srs-remediation/VerifyExpirySweep.cs`.

**Interfaces:** Bounded eligible-expiry query -> atomic notification intent -> existing durable delivery. Use module repository boundaries rather than business logic embedded in direct controller/worker SQL access.

- [ ] Reproduce starvation with 501+ eligible records where the first page already has reminders.
- [ ] Apply eligibility/deduplication before the cap or maintain stable bounded pagination so all eligible records eventually progress.
- [ ] Ensure concurrent workers and restarts produce one logical reminder per event/version and do not leak sensitive data in notification text.
- [ ] Test document replacement, employee suspension, delivery retry, storage failure and rollback with SQL/outbox evidence.

**Accepted when:** The second page receives reminders and restart/parallel execution cannot duplicate or lose the intent.

### I. Protect Schema and Client Contracts Throughout

**Files:** Existing EF configurations/migrations/snapshot, changed controllers/contracts, `docs/SRS-Module-Map.md`, feature docs and UAT guide. Only the assigned migration owner edits shared schema files.

- [ ] For each task, decide whether existing records can represent the required immutable evidence. Add a migration only for a demonstrated schema need.
- [ ] Compare current model and snapshot; review fresh and incremental SQL, foreign keys, precision, uniqueness and indexes. Never rewrite an applied migration.
- [ ] Apply reviewed pending migrations only to the approved target and prove existing synthetic history remains readable.
- [ ] Verify actual registered OpenAPI and request/response examples, including forbidden/missing-policy/conflict statuses and expected versions.
- [ ] Publish breaking route/DTO changes, notably the unsafe tracking route retirement, to the client owner. No claim of React/React Native implementation/testing.
- [ ] Keep an owner-readable changed-file list, migration state and requirement-to-evidence mapping.

**Accepted when:** Schema, runtime API and documented client contracts agree on the same tested build.

### J. Run Integrated Acceptance and Close the Scope

**Files:** Existing guarded authenticated acceptance runners; create a reviewed `docs/hr-srs-remediation/RunAcceptance.ps1` orchestrator only if needed, reusing their guards rather than bypassing them. Update `docs/hr-srs-remediation/Verification.md` and audit status.

- [ ] Real synthetic-user web MFA/CSRF and mobile/self identity; allowed/denied scopes; current/retained historical scopes; revocation and replay.
- [ ] Hire -> approve -> provision -> transfer -> dated compensation -> leave/correction/OT -> calculation -> handoff, with each step's SQL records and audit checked.
- [ ] Overnight/holiday/fiscal/month boundaries and actual approved policy effective dates; compare ESS and reports with owning modules.
- [ ] Separate concurrent HTTP requests/SQL connections for approvals, same-key retries, altered payloads, stale versions, double benefits and finalization races.
- [ ] Encrypted file write/read, restart/replay, controlled storage failure and transaction rollback; verify no dangling successful record or notification.
- [ ] Notifications/export workers under duplicate delivery, restart and a population beyond one batch.
- [ ] Exercise financial reconciliation examples, immutable revision membership, ordinary/late overlap, separate OT rounding and first-unlocked-month targeting.
- [ ] Current Release build plus all affected existing and new standalone suites; retained logs and file hashes. Re-run after integration, not only before concurrent edits.
- [ ] Agree a synthetic load profile and recovery objective; run bounded load and restore tests on approved disposable resources. Do not claim an unspecified production capacity.
- [ ] Nontechnical owner UAT uses known fixture expected outputs and clear pending explanations. Document exact exclusions and approvals still required.

**Accepted when:** No open critical/high correctness or access defects in the included scope, no unexplained failed tests, each included SRS workflow has reproducible evidence, and every remaining exclusion is explicit. If legal authority/evidence is absent, label financial activation pending; do not report full production acceptance.

## 4. Verification Commands and Evidence Rules

Commands below are planned examples, not commands executed by creating this document. Run from the checkout. Choose a fresh run ID and do not overwrite the audit's frozen output.

```powershell
$run = 'D:\CosmixVerification\hr-srs-remediation-' + [Guid]::NewGuid().ToString('N')
dotnet build Cosmix.Backend.sln -c Release --artifacts-path "$run\release" --property:RestoreLockedMode=true --property:UseSharedCompilation=false -m:1 -nr:false
```

Expected: successful Release build; explain every warning rather than disabling it. Preserve lock files and existing outputs. A dependency/network restore failure is not permission to update package versions.

Existing standalone runner, after the named test files are implemented:

```powershell
$sources = @(
  'docs/hr-srs-remediation/VerifyTrackingBoundary.cs',
  'docs/hr-srs-remediation/VerifyHrReportBoundary.cs',
  'docs/attendance-completion/VerifyLeaves.cs',
  'docs/attendance-completion/VerifyPayrollAttestations.cs',
  'docs/attendance-completion/VerifyMonthLocks.cs'
)
& .\docs\attendance-gap-c\VerifyManaged.ps1 -ReleaseDirectory "$run\release\bin\Cosmix.Api\release" -Sources $sources
```

Expected: named suites pass against frozen binaries; hash checks remain intact. This runner is offline and does not itself prove SQL, HTTP, storage or deployment acceptance. Add each task's suite and affected existing suites to the final manifest.

Authenticated runner invocation must come from the reviewed target manifest and current script parameters. Do not copy an old executable SQL/seed command from this plan or assume a migration number. Record the exact approved command when A2 finishes preflight.

## 5. Decisions and External Dependencies

| Item | Already decided | Still needed | Work that continues meanwhile |
|---|---|---|---|
| Face/OTP | Real provider tests excluded now; fail-closed remains | Future deployment/model/license/device/provider acceptance | All non-provider repairs; real auth flow with isolated synthetic delivery |
| Legal activation | No formally approved authority; no SuperAdmin assumption | Named authority/process plus applicable signed legal evidence | Review/evidence versioning, denial tests and synthetic dependency tests |
| KPI/benefits/recovery | Configurable, not invented | Approved values, periods, evidence and authority assignments | Policy validation, workflows and clearly synthetic scenarios |
| Encashment | Must use approved policy and actual entitlement | Eligibility/valuation policy and source evidence | Access, persistence, balance and concurrency repair |
| Outstation/vehicle | SRS workflow must be represented | Approved rates/categories/routes/maintenance/incident authority | Evidence capture, effective policy binding and safe blocked states |
| Posting/settlement | Authorized Accounts/Finance, exact auditable receipt | Actual posting evidence/integration for real settlement | Handoff separation, receipt validation and synthetic reconciliation |
| Capacity/UAT | Backend/client acceptance are separate | Expected staff/site load and nominated owner reviewers | Security/functional fixtures and bounded measurement |

Ask these as one concise decision register when implementation reaches them; do not reopen already confirmed OT formula, ordinary review paths or database-scope approvals without a concrete change.

## 6. Progress Reporting and Completion Labels

Each task moves through: **Not started -> Reproduced -> Implemented -> Standalone verified -> Authenticated SQL verified -> Owner accepted**. A policy/provider-dependent item may be **Blocked with exact reason**; this is not a completed workflow.

At each agreed checkpoint, report only:

- Task IDs closed and evidence, not a vague completion percentage.
- Remaining defects and exact next task.
- Real tests executed versus offline/synthetic checks.
- Decisions/access needed and the independent work continuing.

Use three distinct release statements:

1. **Code repaired:** implementation/regressions pass; integration may remain.
2. **Included backend scope accepted:** current authenticated SQL/storage/concurrency acceptance passes for expressly included flows.
3. **Production ready:** adds real provider/device acceptance, approved legal/company policy activation, operations/security and owner sign-off. Provider exclusion or pending legal authority prevents this label.

Do not infer dates from suite counts. After A/B establish the current defect set and working integration environment, estimate remaining tasks from actual completed slices and team availability. Re-estimate after the first financial integration slice; legal/provider waiting time is separate from engineering effort.

## 7. Coverage and Plan Review

- F01 -> B1; F02 -> B2; F03/F05 -> C1; F04 -> C2; F06/F07 -> D.
- F08 -> E1; F13 -> E2; F09 -> F1/F2; F10 -> F2; financial carry-forward -> F3.
- Additional KPI/leave benefits -> G1; outstation branch -> G2; F12 -> G3; F11 -> H1; F14 -> H2.
- Schema/client contracts -> I; all included SRS acceptance, security, storage and operational evidence -> J.
- Preserve/test existing hiring, employee lifecycle, documents, scheduling, capture boundaries, zones, corrections, regular leave, OT and KPI through their affected suites and integrated scenarios; do not recreate them.
- Face and external OTP delivery remain deliberately excluded, with no success bypass. Legal authorization and company values remain explicit release dependencies.

**Recommended first executable batch after plan review:** A1/A2 in parallel with reproductions, then B1 and B2 with their negative/positive tests. Only after the access boundary is stable, proceed to C/D and E with coordinated financial contracts. Clone/module importing stays paused.
