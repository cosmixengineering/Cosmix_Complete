# Benefit/recovery: finite next batch, 20 October 2026

## Decision packet

Propose **BR1: one actual paid leave approval and one recovery commitment**.
This is not benefit-award acceptance and not recovery-work fulfilment.
It can run before the work date, using real service-generated leave/decision/
commitment evidence and just one explicitly synthetic initial-employment row.
No annual KPI history, verified attendance, face result or historical approval
is seeded. This document does not authorize execution.

P1 is already provisioned. Add **zero permissions, roles, grants, accounts or
employees**. Its delegated assignments expire **2026-10-21T11:09:40Z**.
Do not execute the superseded extra-role proposal in
[the earlier scope](Benefit-Recovery-Live-Fixture-Scope-20261020.md).
Preserve P1/Retry01 and all earlier evidence. Parent owns target binding, approval,
manifest preparation and live execution.

## Exact scope and dates

Execute on 20 October, or before **2026-10-21T11:00:00Z**, without a fake clock.
If missed, stop and obtain revised dates/authority; never silently renew P1.
All dates below are test-only, not company policy defaults.

- Beneficiary/requester: existing HR employee
  `35d19922-70a8-4c80-8578-e1029360e9fc`, account
  `8475187f-bf3b-4f9c-a3ed-4bac32253005`.
- Independent reviewer/commitment recorder: existing Admin employee
  `7c505086-87ea-4fd2-980e-823ecccbe36a`, account
  `94a83b62-5b56-4450-9175-2a7a9e3261bf`.
- Leave: **2026-10-22, 09:00-10:00 Asia/Karachi**,
  `2026-10-22T04:00:00Z` through `05:00:00Z`, **60 minutes**.
- Commitment: **2026-10-21, 60 minutes, BeforeLeave=true**.
  This is a promise, not a record of work performed.
- Support interval: **[2026-10-21, 2026-10-23)**.
- Keep HR's reported joining date **2026-01-15** unchanged.

### Prerequisites: seven new source rows

| Source | Count | Exact fields / provenance |
| --- | ---: | --- |
| Initial EmployeeAssignment | 1 | Explicit **test seed**, effective support interval; beneficiary above; one existing active department, designation, employment type, OFFICE staff category and isolated branch; SiteId and ReportingManagerId null. Constructor-produced, no invented hiring decision or assignment-change approval. |
| HolidayCalendar | 1 | Actual calendar creation service; code/name `TEST-BR1-20261020-CAL`; support interval; notification reference `TEST ONLY BR1 no holidays`; zero holiday rows. |
| Shift and initial ShiftVersion | 2 | Actual shift creation service; code/name `TEST-BR1-20261020-SHIFT`; effective 2026-10-21; 09:00-17:00 Karachi; end-day offset 0; 480 scheduled minutes; paid/unpaid breaks 0; weekdays mask 127; no reminder. |
| OfficeRosterAssignment | 1 | Actual office-roster service; same branch and support interval; returned shift/calendar IDs and expected timeline. No individual/site roster or ending row. |
| Leave Policy and PolicyVersion | 2 | Actual leave-settings publication service; code/name `TEST-BR1-20261020-RECOVERY`; DirectHr, support interval, schema `leave-settings-v4`; settings below. PublishedAt is real execution time, not backdated. |

New masters, leave types, office boundaries, policy-applicability assignments,
holidays and taxonomy publications: **zero**. Reuse exactly one existing active
Casual leave type with RequiresEvidence=false, RequiresAdvanceApproval=true and
ReducesRegularBalance=true. Bind its ID and the five existing master IDs in the
parent's manifest; do not guess them or change their flags. The branch must have
no overlapping roster or other affected employee in this interval. Confirm that
the current staff taxonomy maps the chosen category to office scheduling.
If those exact references are unavailable, this seven-row packet is not executable;
do not silently create masters or switch to a site path.

The employment row is the only proposed direct fixture preparation. Current
post-hire update APIs require an existing assignment; they cannot create this
employee's missing first row. A bounded, separately approved seed must use real
domain construction, explicit test provenance, fresh IDs and overlap/count guards.
It is **not** evidence that hiring or transfer was tested. No employee/account
identity or joining-date rewrite is authorized.

The four source-service commands are calendar create, shift create, office roster
assign and leave policy publish. Retain their real IDs, versions, audit/receipt
references and exact request JSON; they are setup operations, not annual history.
The shift version is open-ended where the API has no end field; the bounded roster
and employment prevent its use here outside the stated interval. Retain it, do
not delete it as cleanup.

### Exact leave settings

```text
SettingsVersion=4; YearStartMonth=7; YearStartDay=1
AnnualDays=1; SickDays=0; CasualDays=1
MinimumServiceMonths=0; StandardDayMinutes=480
EligibleStaffCategoryIds=[the one pinned OFFICE category]
HolidayCalendarId=the newly returned calendar ID; HolidayTreatment=Exclude (1)
PaidKinds=[Casual]
Recovery.EligibleKinds=[Casual]; Recovery.EligibleTreatments=[Paid]
Recovery.MaximumMinutesPerLeave=60; Recovery.MaximumMinutesPerDay=60
Recovery.MaximumDaysBeforeLeave=1; Recovery.MaximumDaysAfterLeave=0
Benefits=null; Encashment=null; RecoveryFulfilment=null
```

The service serializes the actual enum values and rules JSON; do not hand-author a
protected reference. Policy supplies a 480-minute quota; approving the leave uses
60 minutes. No synthetic entitlement credit is necessary. This policy is selected
explicitly, but is not intrinsically employee-isolated: another employee in its
eligible category could select it within the date interval. Disclose and approve
that persistence; a TEST code is not an isolation mechanism.

## Actual business operations and expected state

Use `L=/api/v1/leaves`, real local MFA/CSRF, fresh versions and `TEST-BR1-*`
idempotency keys. Maximum **two new sessions**, **60 HTTP requests**, **seven
distinct successful write commands** including the four setup commands above.
Only HR and Admin are needed if their existing setup permissions pass preflight.
No extra grants or SuperAdmin login is implicit.

1. HR submits `POST L/mine`: pinned leave type, one day/interval above, reason
   `TEST ONLY BR1 recovery commitment`, no attachment, reliever or previous request;
   use a synthetic contact such as `BR1 fixture only` and no handover requirement.
2. Admin approves `POST L/admin/{leaveId}/decisions`: Approved, Paid, exact returned
   policy version and ExpectedApprovalVersion; same test reason.
3. **Admin**, not HR, submits `POST L/{leaveId}/recovery-commitments`: date/minutes/
   BeforeLeave above. `CreateRecoveryCommitmentAsync` uses Review plus
   requireSystemRole=true and explicitly denies the beneficiary/requester.
4. HR lists commitments and reads
   `L/mine/{leaveId}/recovery-commitments/{commitmentId}/progress`.
   Expect committed=60, fulfilled=0, remaining=60, status=`commitment_only`.

Expected business additions: **one LeaveRequest, one LeaveRequestDay, one
ApprovalRequest, one ApprovalDecision, one utilization LeaveLedgerEntry (-60),
one LeaveRecoveryCommitment**. The commitment has no second approval request.
No recovery claim/allocation, correction/session/summary, payroll deduction,
benefit award/reversal/waiver, KPI request/finalization or cancellation is created.
Normal audit, idempotency and encrypted storage are additional evidence, not
business fixture rows. Exactly seven successful-command receipts are expected
if these four setup paths and three business writes are used; read/replay audits
are separate. Freeze auxiliary evidence counts from the chosen services in the
runner before execution, rather than claiming all rows total thirteen.

Test exact-key retries of all writes; no duplicate business/source rows. Test a
changed commitment payload under its original key (409), a fresh duplicate key
(409), and HR attempting commitment creation (403). For concurrency, issue two
Admin commitment requests on separate connections with distinct keys: exactly
one commitment may succeed; the other must conflict. Perform this as the first
commitment attempt, not by adding a second obligation. A stale approval version
must not append another decision. All denied writes retain zero new business rows.
No mutating historical-proof tampering test is authorized.

## Authority and evidence boundaries

HR requires existing `attendance.leaves.self`. Admin requires genuine fixed
reviewer authority and `attendance.leaves.review`; the commitment's permission
must be on the same fixed assignment. P1's custom benefit-review grant is neither
a substitute nor needed for this recovery commitment. Setup requires existing
`hr.holidays.read/manage`, `attendance.shifts.read/manage`,
`attendance.rosters.read/manage`, and `attendance.leaves.configure`.
Check current grants/scopes, active employment, unlocked dates and independent
identities before setup writes. Do not modify fixed roles.

The actual leave services produce the submission snapshot and decision proof.
The commitment service re-reads them and retains its schema-1 audit with
`attendanceFulfilled=false` and `salaryRecovered=false`. Do not seed those proofs,
flip Verified flags or transplant the standalone verifier's mock approval graph.
No office GPS boundary is needed for this commitment-only path. Full recovery-work
acceptance would additionally need genuine approved session/correction provenance,
an explicit fulfilment policy and an elapsed work interval; it is not this batch.

## Benefit acceptance remains separate

Reuse P1's existing benefit grant; **do not provision it again**. An optional Admin
benefit-list read and matching HR self-list can confirm empty state before the P1
expiry, but are not an award workflow. No successful benefit award is proposed.

The service requires a completed annual KPI, matching finalization, complete saved
source verification, current policy/assignment eligibility and an award window.
The existing HR employee joined 2026-01-15, so no complete post-join annual period
has ended by 2026-10-20. Seeded score/finalization flags or 365 fabricated verified
days are not an honest workaround. Keep positive award/correction/waiver coverage
offline until separately approved historical provenance can be produced honestly.

## Source basis

- [Leave submit/approval](../../src/Cosmix.Application/HR/Leaves/LeaveService.cs):
  roster required; independent approval; real proof and utilization.
- [Commitment creation](../../src/Cosmix.Application/HR/Leaves/LeaveEncashmentService.cs)
  and [retained evidence checks](../../src/Cosmix.Application/HR/Leaves/LeaveRecoveryEvidence.cs).
- [Progress](../../src/Cosmix.Application/HR/Leaves/LeaveRecoveryWorkService.cs)
  verifies parent evidence without inventing fulfilment when there are no claims.
- [Office roster](../../src/Cosmix.Application/Attendance/Rosters/OfficeRosterResolver.cs)
  does not require a GPS boundary for resolving leave duty.
- [Benefit eligibility](../../src/Cosmix.Application/HR/Leaves/LeaveBenefitService.cs).
- Existing offline `VerifyLeaveRecoveryReplay.cs` tests the real commitment service
  with synthetic ports; its constructed parent evidence must not be used as a live
  historical seed. No new standalone run was performed for this packet before the
  user redirected work to the approved O1 fixture.

This is source-only feasibility and a finite approval proposal, not live acceptance
or permission to prepare the seed. SQL/HTTP/host/private paths were not accessed.
