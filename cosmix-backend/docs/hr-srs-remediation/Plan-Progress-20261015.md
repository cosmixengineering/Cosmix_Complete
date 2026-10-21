# HR SRS Plan: Current Progress

## 19 October Continuation

The table below is the retained 15 October checkpoint. Use
[Continuation-20261019.md](Continuation-20261019.md) for the latest fixes, fresh
build/test results and remaining gates. Financial adjustments and deployment
workflows now have bounded implemented and standalone-verified slices; they are
no longer wholly unimplemented. Full G1/G3, financial edge cases and authenticated
SQL acceptance remain open. No database migration was applied on 19 October.

Date: 15 October 2026. Controlling status for the attached 19-task remediation
plan. Earlier "Repaired" shorthand in Status.md is historical, not acceptance.

## Direct Answer

Attendance is not 100% accepted. There is substantial implemented and standalone
verified code, but remaining implementation is not just testing or legal paperwork.
No current authenticated SQL acceptance or owner sign-off closes the plan.
Do not calculate a completion percentage from routes, files, or passing assertions.

The attached plan has 19 tasks. The table below separates available repairs from
the remaining exit gates; **none is being marked fully Accepted by this report**.
That is not a claim that no code has been written.

## Task-by-Task Status

| Task | Implemented / evidenced | Still required to close |
| --- | --- | --- |
| A1 baseline | Current source reviewed; frozen Release builds and preserved test artifacts; previous concurrent edits retained | Full remaining-finding reconciliation and shared final accepted baseline |
| A2 test environment | Frozen A02 packet, offline 24-migration SQL, 1,140 bound inputs and standalone guards; bounded setup/auth approved, but both read-only attempts stopped before setup (runtime mismatch, then invocation-policy error); zero SQL/private writes | Exact stopped-state evidence in A02-Execution-20261015.md; no automatic retry. Actual login/MFA/CSRF, revocation and independent SQL smoke are not executed |
| B1 tracking | Unsafe write retired; scoped Application read; standalone authorization regressions | Real allowed/denied HTTP + SQL verification |
| B2 reports access | All 14 report variants require explicit report/source/export authority and scope; standalone boundary/query regressions | Real authenticated SQL exports and revocation scenarios |
| C1 encashment workflow | Self ownership, fixed Admin decisions, expected version, durable decision/audit/receipt design; 30 workflow checks | Actual SQL persistence, restart and concurrent decision acceptance |
| C2 encashment values | Configured policy, retained KPI/salary/leave evidence, reservation/consume/release; 80 valuation checks | Later financial adjustments/settlement and real reservation races |
| D employee self-service | Authoritative leave/policy fractions and fiscal context, dated employment and per-day Attendance calendar integrated; 37 consistency + 21 financial checks pass | Authenticated self API/SQL, storage and client contract acceptance |
| E1 handoff vs posting | Separate exact confirmed-loan-posting API and durable evidence read; 60 checks pass. Fully repaid installments excluded before preparation; stale balances block locking/export. Expanded Finance projection suite: 102 checks | Real Accounts receipt acceptance, authenticated SQL persistence/rollback, independent-connection races |
| E2 loan retry identity | Route-bound retries, actual role, foreign schedule/duplicate reversal denial; interest-free principal-only approval/schedules and overpayment protection; 31 checks pass | Live separate-connection repayment/reversal races; broader historical/source compatibility acceptance |
| F1 legal gates | Existing versioned evidence and fail-closed legal readiness preserved | Complete applicability/evidence verification; formally appointed authority and signed rule-specific evidence. No assumed SuperAdmin approval |
| F2 finalization/OT | Exact schema-3 retained OT and payroll revision membership; Finance consumes those sources; no-charge/stale/missing-source guards; standalone checks | Complete legal/financial readiness integration and real correction/finalization races |
| F3 financial carry-forward | Existing confirmed-posting reconciliation reader and factual carry-forward remain available | Financial credit/recovery write workflow, first eligible target, later-correction reconciliation and duplicate prevention are NOT complete |
| G1 benefits/recovery | Configurable policies and source-bound recovery commitments; cancelled/missing/damaged parent and beneficiary replay bypass repaired, 16 checks pass | Actual KPI benefit award/exclusivity, leave credit and recovery fulfilment transactions; commitment is not completed work |
| G2 outstation | Existing allowance policy/claim/review/handoff framework | Deployment and return-home continuity evidence; replace deployment_workflow_unavailable only with the full supported workflow |
| G3 vehicle | Dated custody, historical policy, retained approved fuel distance, authoritative as-of service mileage, self-review and incident validation repaired; 19 service scenarios + 21 repository checks pass | Full staff eligibility/map valuation/workshop authority/mileage-cycle/incident outcome workflows and authenticated acceptance; no wholesale completion claim |
| H1 report values | All 14 projections repaired; 942 report checks pass including the final same-employee guard, independent expected results/CSV, retained OT, signed leave minutes and pagination | Authenticated SQL exports, XLSX parsing and load acceptance |
| H2 reminders | Bounded transactional outbox pages, producer lock and durable identity; synthetic tests cover 602/source, failures/restart/replay and competing producers/dispatchers | Actual SQL locks/concurrency, encrypted-storage restart and deployment durable-worker configuration acceptance |
| I schema/contracts | Current Release and generated OpenAPI verified offline; this continuation needs no migration | Remaining workflow schema needs, approved incremental SQL and complete client-owner handoff |
| J full acceptance | Affected real application logic tested with synthetic collaborators | Authenticated HTTP/SQL, actual encrypted-storage restart/failure, independent connections, workers, load/restore and owner UAT |

Earlier suite counts describe their own retained builds; they are not added
together as current whole-system coverage. This continuation's exact evidence is
in Verification.md, Confirmed-Loan-Posting-20261015.md, ESS-Consistency-20261015.md,
Reports-Remaining-20261015.md, Document-Expiry-Reliability-20261015.md and
G3-Vehicle-Source-Integrity-20261015.md.

## Current Fixes

- A handoff processing reference cannot reduce loan principal.
- Reusing the same request/key against another loan/request conflicts.
- A second key cannot reverse the same original repayment again.
- A schedule from a different agreement is rejected.
- Loan decision audit uses the actual assigned role.
- Generic auth.self.read cannot disclose the salary overview.
- Financial reads revalidate current session and grants, and require audit persistence.
- Future/expired salary is excluded; local effective end dates remain exclusive.
- Negative repayments/reversals are summed once across the whole ledger.
- No new interest, markup, fee or extra loan charge; installments sum exactly to principal.
- A separate authorized posting receipt has one exact principal-ledger effect.
- Fully paid installments do not enter a new payroll handoff; changed balances
  block stale frozen lock/export without silently changing the original lines.
- ESS uses the authoritative leave policies/ledger and Attendance calendar.
- Cancelled or damaged leave parents cannot replay a successful recovery promise.

Historical records were not deleted, reset or automatically reversed. Existing
unproved deductions, if any, need an explicitly evidenced reconciliation; this
patch does not retroactively certify them.

## Next Implementation Order

1. Final combined Release is clean (zero warnings/errors); generated OpenAPI has
   469 paths and 89 checks pass. A02 setup/auth was approved but read-only preflight
   stopped on execution errors; follow the user's no-automatic-retry instruction.
   A later explicitly resumed execution must revalidate all original guards. Then review and exercise the actual
   D/E/H scenario fixtures; the initial A02 login packet does not authorize those
   extra writes or close A2/J.
2. Complete F3's financial adjustment workflow. E1's loan posting now has a separate
   exact-source action; it is not F3's ordinary/late deduction adjustment ledger. A
   confirmed PKR 200 deduction corrected to PKR 100 yields one PKR 100 credit;
   calculated-only money cannot create a refund. Target selection must be shown
   before approval and rechecked against locks at commit.
3. Finish G's actual benefit/deployment/vehicle transactions, then H's remaining
   SQL/export/load and worker acceptance. H's report-value/outbox fixes have passed
   standalone checks; they are not waiting for a duplicate implementation.
   Do not label a policy/model as a workflow.
4. A2/J run each finished slice through real approved authenticated SQL/storage
   acceptance, not only a final build. No existing ambiguous database may be adopted
   or modified without resolving its ownership and synthetic-data provenance.

External dependencies: formal legal activation authority and signed applicable
evidence; actual company policies/assignments where not configured; exact SQL test
target authorization. These do not prevent independent backend implementation.
Real face/model/device and external OTP delivery are excluded by current scope;
no mock success substitutes for them. React/React Native and salary release are
not implemented or accepted by this backend report. Clone integration is deferred.
