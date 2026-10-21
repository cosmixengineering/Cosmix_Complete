# C2 Encashment and Recovery Continuation

Source ownership handed back to parent, 15 October 2026. No SQL, host, real providers, Git writes or financial settlement authorized or performed.

**Build coordination:** Production source and dedicated tests are stable; no further C2 source edits are outstanding. Parent reports final reviewed Release built with 0 warnings and 0 errors, with no source drift across 1052 inputs. Parent reports `VerifyEncashmentValuation` 80/80 and `VerifyEncashmentWorkflow` 30/30 passing against that Release. These are parent-run results; final retained runner details and remaining combined-suite results are pending parent closeout. This child did not duplicate the product build or final suites. Source changes since integrated Release are limited to the KPI projection in `LeaveEncashmentService.cs` and projection/timeliness handling in `LeaveEncashmentValuation.cs`.

## Integration Contract

- Preserve parent C1 authorization, version, receipt and decision persistence changes.
- Parent owns workflow fixture, controller metadata, DI and shared status. Child messaging to the native parent is unavailable through the exposed thread tool.
- Registration required: `AddScoped<LeaveEncashmentValuation>()`.
- New final service constructor dependency: `LeaveEncashmentValuation valuation`.
- Final valuation constructor: `AttendanceOperation`, `ILeaveRepository`, `ILeaveEncashmentRepository`, `IFinalizedKpiReader`, `IEmployeeCompensationRepository`, `IKpiDisputeRepository`, `KpiDisputeEvidenceReader`, `IKpiRepository`, `IKpiSourceReader`.
- Submit appends optional `Guid? LeaveTypeId` and `Guid? PolicyVersionId`; omission yields 409 `encashment_policy_selection_required`. Existing entry-point method signatures stay unchanged.
- Unsafe numeric repository readers are replaced with `LedgerAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct)`.
- `RecoveryOnDateAsync(Guid employeeId, DateOnly date, CancellationToken ct)` bounds commitments across that employee's leave requests on the proposed date.
- Legacy reject/return remain supported without a C2 valuation; they cannot approve unverified money. Fixture needs an empty ledger result for these paths.
- No schema/DbContext/migration/permission change planned. Existing nullable LeaveRequestId stays null on encashment ledger entries.

## Execution Record

- Read AGENTS, architecture, module map, ownership, C1/C2 plan and existing leave/KPI/compensation/transaction code.
- Ruling: use the existing exact policy-day calculator for quota and extend its ledger input, preserving v1/v2 policy semantics. C2 rules are opt-in schema 3 with every business value explicitly supplied.
- RED harness added before production edits. Baseline run and subsequent verification recorded below as executed.
- RED: supplied frozen baseline rejected explicit schema 3 with `leave_policy_unavailable`; v2 compatibility passed, 1/2. Retained run: `D:/CosmixVerification/part-c-parent-20261013/managed-9e64b7d43f8645acae382f8697466aa6`.
- Independent review identified fractional KPI entity precision and delayed-approval submission-window defects. Actual workflow regressions reproduced both against parent integrated Release: `assessedKpiPercent` argument-range exception and `encashment_submission_window_closed`. Run `D:/CosmixVerification/part-c-parent-20261013/managed-501ea0fd847049a886690fdf4581875f` passed 72/75; the remaining failure was a test retaining an obsolete policy ID after fixture republication, subsequently corrected.
- Corrected fixture Self permission grants to target the actor employee ID. No production authorization weakening was made.
- Review fixes: entity KPI is the existing four-decimal ToEven projection; protected exact KPI still determines money. Approval evaluates submission timeliness against immutable capture time while reading sources with current approval time. No migration or money-formula rounding change.
- Parent-reported reviewed GREEN: valuation 80/80 and workflow 30/30. Both reproduced review regressions are covered by the valuation harness. Final runner evidence remains with parent; no additional child tests or production changes are pending.

## Client Contract and Rules

All existing entry-point method signatures remain. Submit requires explicit `LeaveTypeId` and `PolicyVersionId` selections. `LeaveYearEnd` is exclusive and must match the selected policy's complete fiscal year. The submission window starts on that end date and is exclusive at `end + RequestWindowDaysAfterYearEnd`.

Schema 3 adds optional `Encashment` and `Recovery` objects; at least one must be supplied, and every field within a supplied object is required. Null objects are omitted from serialization, preserving retained v1/v2 JSON. Neither older schema gains an encashment or recovery entitlement.

- Encashment fields: `EligibleKinds`, `MinimumServiceMonths`, `MinimumKpiPercent`, `MaximumMinutesPerYear`, `SalaryDivisorDays`, `RateMultiplier`, `AmountDecimalPlaces`, `Rounding`, `Currency`, `ValuationDate`, `RequestWindowDaysAfterYearEnd`.
- `ValuationDate=1` means the final calendar date of the completed leave year. The matched monthly basic salary must be effective on that date. Other valuation bases are unsupported, not guessed.
- Amount is `monthly basic * requested minutes * configured multiplier * exact finalized KPI / (configured divisor days * policy standard day minutes * 100)`, rounded once using the supplied precision and midpoint rule (`0=ToEven`, `1=AwayFromZero`). The synthetic test values are not organization defaults.
- Annual and kind quotas reuse `LeaveTreatmentRules`; reservation, consumption and signed reversal entries are included by ordinary `LeaveRepository.PolicyAsync` and therefore by ordinary approval and self-balance reads.
- Submit appends a negative reservation. Approval appends its exact positive release plus negative consumption. Return/reject append only that reservation's release. All are staged inside the existing serializable authorization transaction, together with approval decision, audit and receipt. Stable keys bind entries to encashment IDs; `LeaveRequestId` remains null.
- An active legacy request without a verified reservation blocks further spending of that year's balance. Legacy return/reject remains possible without fabricating ledger entries. Approving an old unverified money snapshot is forbidden.
- `ApprovedAmount` can be omitted or equal the authoritative stored amount. An arbitrary override is rejected; equality alone does not bypass snapshot and current-source verification.
- Response `AssessedKpiPercent` is a four-decimal storage projection, not the input to money calculation. Exact finalized KPI remains in protected evidence. The submission window applies to original submission time; delayed Admin review still checks current source validity.
- Recovery fields: `EligibleKinds`, `EligibleTreatments`, `MaximumMinutesPerLeave`, `MaximumMinutesPerDay`, `MaximumDaysBeforeLeave`, `MaximumDaysAfterLeave`. Zero before/after days disables that side. Creation requires the actual approved leave's submission and decision evidence, original policy JSON, eligible treatment, future/current commitment date in the configured range, and unused per-leave/per-date capacity.
- Recovery creation is a commitment only. Its protected audit retains the original decision and policy evidence and explicitly records `attendanceFulfilled=false` and `salaryRecovered=false`.

Missing, future-only, ineligible, stale or unconfigured sources return explicit 409 reasons. Pending KPI preparations, unresolved disputes (including returned and correction-required outcomes), mismatched current source fingerprints and an absent exact annual final score prevent valuation or approval. A reviewed-no-change dispute can proceed after its retained evidence is verified.

## Acceptance Limits

Parent final reviewed-Release evidence: 80/80 valuation and 30/30 workflow checks,
part of the successful 17-suite run at
D:/CosmixVerification/part-c-parent-20261013/managed-96d7fc12a0004bc490000929fcf90324.
Exact inputs, source and compiler/runtime logs are retained there. Full results
and source digest are in Verification.md; this does not change the limits below.

No financial posting, settlement, payment, salary recovery, attendance fulfillment or G1 benefit fulfillment is implemented or claimed. There is no existing encashment cancellation endpoint; the supported terminal paths are approve/return/reject. An approved encashment cannot be rewritten or undone through these entry points.

The standalone harness uses actual workflow services, the existing KPI manifest verifier, exact leave calculator and actual EF query translation with a no-connection interceptor. Repositories/private storage are synthetic for service scenarios. It does not establish SQL commit/rollback durability, independent-connection races, HTTP authentication acceptance or actual encrypted-storage recovery. Those remain parent/integration acceptance work on an explicitly approved target.
