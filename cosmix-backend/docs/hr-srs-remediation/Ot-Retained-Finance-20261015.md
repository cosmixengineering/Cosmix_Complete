# OT Retained Month Lock and Finance

Date: 2026-10-15. Local backend continuation; no database or production activation.

## Fixed Behavior

1. September 30 duty approved October 2 belongs to September, not October.
   The old repository query selected the approval month. Its boundary regression
   changed from 17/19 to 19/19 passing checks. Production Finance now reads the
   protected lock manifest instead of using that period query for money.
2. `OvertimeService` implements the month-lock award reader. It reuses the actual
   submission/decision validator, retained formula, approved intervals and original
   salary evidence; checks current and retained financial scopes; matches session
   revision/times/ownership; and checks competing work credits. It does not call a
   nested Attendance operation or recalculate awards using current salary.
3. New private month-lock schema 3 retains OT awards and exact private references
   alongside existing summary, carry-forward and payroll revision membership.
   Completeness, order, duplicates, member identity and fingerprints are checked.
   Storage IO is followed by the existing complete readiness reinspection.
4. Finance projects the validated retained awards and loads the precise retained
   payroll revision using `FindAsync(employee, revisionId)`. A newer revision cannot
   replace the frozen amount. Missing/corrupt/unauthorized financial sources stop
   preparation; they are not silently skipped. Verified empty deduction lines remain
   zero deductions, not evidence of settlement.
5. Prepare rechecks the projection after private line writes and validates the
   retained lock before receipt replay. Same-key retry returns one batch; changed
   payloads, another prepare key, source races and revoked dependencies are rejected.
6. Salary-bearing OT evidence remains private. Existing public month detail does
   not acquire the award object or private file references. Schema 1/2 historical
   readers remain supported; Finance refuses old manifests without exact OT
   membership. No old lock is rewritten or automatically upgraded.

## Access and Client Contract

Existing month/Finance permissions and web MFA continue to apply. Reading approved
OT dependencies also requires `attendance.overtime.financial.read` across retained
and current employee scopes. Existing payroll-input read permissions still apply.
No new role, automatic grant or approval authority is created.

Client sequence remains preview -> prepare with `ExpectedPreviewFingerprint` and
`Idempotency-Key`. Refresh a stale preview; do not retry a changed payload under
the same key. A source/readiness conflict needs its evidence repaired, not an
amount override. Preparing is not posting, settlement or salary payment.

New explicit dependency/conflict codes include:
`month_lock_overtime_sources_unavailable`, `month_lock_overtime_reader_unavailable`,
`month_lock_overtime_dependencies_unavailable`, `month_lock_overtime_source_changed`,
`month_lock_overtime_contract_invalid`, `month_lock_overtime_membership_conflict`,
`payroll_handoff_lock_reader_unavailable`, and `payroll_handoff_retained_membership_unavailable`.

## Verification

Release build command:

```powershell
dotnet build Cosmix.Backend.sln -c Release --artifacts-path D:\CosmixVerification\hr-remediation-ot-20261015\reviewed --property:RestoreLockedMode=true --property:UseSharedCompilation=false -m:1 -nr:false
```

Exit 0; 0 warnings, 0 errors; 58.35 seconds. Frozen Release directory:
`D:\CosmixVerification\hr-remediation-ot-20261015\reviewed\bin\Cosmix.Api\release`.

Existing `docs/attendance-gap-c/VerifyManaged.ps1` compiled standalone verifiers
against that frozen Release, without project recreation or implicit host/SQL execution.

| Suite | Passing checks |
| --- | ---: |
| VerifyOtFinalizationBoundary | 19 |
| VerifyOvertimeService | 154 |
| VerifyMonthLocks | 70 |
| VerifyPayrollInputService (including Finance continuation) | 95 |
| VerifyMonthLockQueries | 42 |
| VerifyOvertimeQueries | 46 |
| VerifyPayrollAttestations | 195 |
| VerifyPayrollReconciliation | 48 |
| VerifyAttendanceComposition | 46 |
| VerifyRemediationContracts | 51 |
| VerifyPayrollInputModel | 22 |
| VerifyPayrollInputs | 59 |
| VerifyOvertimeRules | 31 |
| Total | 978 |

Exact test sources, assembly hashes, compiler inputs and output logs:

- First eight suites: `D:\CosmixVerification\part-c-parent-20261013\managed-574a9cba168d46fcace84d47c4cc18b6`.
- Last five suites: `D:\CosmixVerification\part-c-parent-20261013\managed-9ac42df527ed4e05a3c1e655241b32ad`.
- Pre-fix reproduction against prior reviewed Release:
  `D:\CosmixVerification\part-c-parent-20261013\managed-46d745321ce848be9c9119eb693d3944`.

The standalone OpenAPI compiler emitted two CS1701 compatibility warnings about
Microsoft.OpenApi's .NET 8 references against .NET 10. Runtime checks passed across
467 registered paths. Product Release itself had zero warnings.

Finance positive tests use an explicitly synthetic lock/legal collaborator; real
production legal verification remains unavailable. No test creates legal clearance.
The tests verify service boundaries separately, not a live complete OT-to-salary run.

Bounded independent read-only review of the changed OT/month-lock/Finance services
reported no concrete new P1/P2 findings. `git diff --check` passed. Neither result
substitutes for real database, encrypted-storage or end-to-end acceptance.

## Changed Files

Under `src/Cosmix.Application/Attendance/MonthLocks/`:
`MonthLockContracts.cs`, `MonthLockEvidence.cs`, `MonthLockService.cs`, `MonthLockRegistration.cs`.

Under `src/Cosmix.Application/Attendance/Overtime/`:
new `OvertimeMonthLockReader.cs`, `OvertimeRegistration.cs`.

Other production files:
`src/Cosmix.Application/HR/PayrollHandoff/PayrollHandoffService.cs`,
`src/Cosmix.Infrastructure/Repositories/Attendance/MonthLockRepository.cs`,
`src/Cosmix.Infrastructure/Repositories/HR/PayrollHandoffRepository.cs`.

Extended existing standalone checks:
`VerifyOvertimeService.cs`, `VerifyMonthLocks.cs`, `VerifyPayrollInputService.cs`,
`VerifyAttendanceComposition.cs` in `docs/attendance-completion/`.
Status, verification and SRS mapping documentation updated. No schema migration.
Preexisting loan/allowance/HR and other working-tree changes were preserved.

## Still Not Complete

- Formal legal activation authority and jurisdiction/rule clearance remain pending.
  Existing unavailable legal readiness is preserved; month finalization is NOT
  represented as production-ready. Approval of a company formula is not legal clearance.
- Prior financial handoff supersession, confirmed posting/settlement reconciliation,
  financial carry-forward and later adjustments remain separate outstanding work.
- Real isolated authenticated HTTP/SQL, multi-connection races, storage restart/failure,
  notifications/workers and complete end-to-end acceptance have not been executed here.
- Remaining HR/Attendance backlog in Status.md is not closed by these fixes.
- Face/model/device, external OTP delivery and separate web/mobile clients were not tested.
