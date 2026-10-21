# C02 Ordinary Time Checkpoint

13 October 2026. C02 explanation baseline was frozen after 71 actual passing standalone checks, freshly rerun at 17:46 UTC; Domain ownership was released and the parent has since extended the source. This worker made no further Domain edits. C07 Finance award projection and supported-formula guard are now APPLIED, following verified D: CreateNew backups and the C: reserve check. C07 executable RED/GREEN remains held until the parent grants the serial slot after C05. No C07 pass count or integrated release is claimed. Part D has not started.

## Recovery and Current Changes

After the disk-full incident, the parent restored the exact ordinary Domain source. Its pre-change, 13,351-byte backup has SHA256:
`F8B8AD06716B69ACB6936B6F7CD1B4F4341107BEC3D3A2C6078F2777AA29EFD5`.
After ownership release, a fresh nonempty CreateNew backup/hash check and the C: reserve check, this worker applied only the bounded Domain explanation delta. The root-owned producer was restored by its owner and remains outside this worker's edits.

Exact changed checkout paths, relative to `C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/`:

- `src/Cosmix.Domain/Attendance/Calculation/AttendanceDailyTimeCalculation.cs`: existing Domain explanation delta; frozen below.
- `src/Cosmix.Application/Attendance/Overtime/OvertimeContracts.cs`: optional null-omitted restricted Finance award and typed authority/evidence contracts.
- `src/Cosmix.Application/Attendance/Overtime/OvertimeService.cs`: only Finance projection and supported-formula check before pricing; root guards preserved.
- `src/Cosmix.Application/Attendance/Overtime/OvertimeFinancialAwards.cs`: new feature-local archived award mapping, fingerprint and nine-hour/rate guard.
- `src/Cosmix.Application/Attendance/Overtime/OvertimeQueriesAndBulk.cs`: two narrow named-bulk work-allocation replay hooks; existing taxonomy calls retained.
- `docs/attendance-gap-c/C02VerifyOrdinaryTime.cs`: focused standalone Domain verification. The first missing-trace assertion remains a required guard. This is not a new test project.
- `docs/attendance-gap-c/C02-Ordinary-Handoff-20261013.md`: this corrected source/ownership checkpoint.
- `docs/attendance-completion/VerifyOvertimeService.cs`: added C07 supported-formula, immutable finance award, named bulk/retry and authority checks; not executed yet.
- `docs/attendance-completion/VerifyOvertimeRules.cs`: added C07 fractional-tick, split-gap, open-session and non-nine-hour duration checks; not executed yet.

Only the four OT production files above were changed during C07, including the explicitly coordinated bulk partial-service hooks for the bidirectional guard. No ordinary producer/DTO, OfficeSourceReader, roster, TaxonomyCurrentReader, policy codec/resolver, correction/journal, shared DI/catalog, model/configuration or migration file was edited by this worker. Existing test guards were preserved. Static comparison retains the root taxonomy invocation arguments and original/current resource-scope lines; new work checks run after authorization, not in place of those guards. Unowned OT sources/eligibility/taxonomy/rules retain their root manifest hashes; the existing formula/duration helper retains its earlier observed hash.

All new recovery/backup/build/cache/TEMP artifacts live under the unique root:
`D:/CosmixVerification/part-c-ordinary-76e5ea760c4e46ee8f8c1b2efe466671/`.
Nonempty existing owned files were backed up using FileMode.CreateNew and matching SHA256. Earlier recovery files, checks, RED logs and metadata were preserved without deletes. This document's original was also backed up before this correction.

## Bounded Functionality

The applied Domain-only change fills the observed C02 explanation gap:

| Output line | Meaning |
| --- | --- |
| `configured_scheduled_time` | Exact ticks from the supplied configured scheduled minutes |
| `configured_scheduled_paid_capacity` | The same scheduled capacity already used for half-day capping, with the explicitly chosen daily break treatment |
| `configured_payable_time` on holiday/rest branches | Exact existing interpreted credit, including explicit no-credit treatment |
| `ordinary_payable_shortfall` | max(0, scheduled paid capacity minus interpreted payable ticks); zero on rest/holiday days |

The change moves the existing half-day capacity expression into the shared explanation scope and reuses it. It introduces no second calculator, policy default, new financial engine, placeholder producer or ordinary monetary effect. Existing numerical rules, late flags and `attendance-configured-time-v1/v2` numerical interpretation versions are unchanged. No reviewed-work field has been added to the Domain input.

Exact applied delta against the verified F8B8AD original:

1. Move the existing three-line `scheduled`/`capacity` expression from the half-credit branch to immediately after creation of `lines`, without changing the expression.
2. Append `configured_scheduled_time` and `configured_scheduled_paid_capacity` there.
3. Append `configured_payable_time = value` and `ordinary_payable_shortfall = 0` on the holiday/rest return path.
4. Append `ordinary_payable_shortfall = Math.Max(0, capacity - payable)` immediately after the ordinary `configured_payable_time` line.

The exact already-applied patch is retained as `c07-resume-20261013-01/C02-applied-explanation-delta.patch.txt` under the D: root. Do not apply it again or compile the old whole-file candidate as another source. A read-only `fc.exe /N` comparison against the nonempty CreateNew backup confirmed these four differences and no additional Domain delta.

## Source Gap Matrix

| Requirement | Current source and verified/prepared coverage | Remaining boundary |
| --- | --- | --- |
| C02.1 time explanation | `Domain/Attendance/Calculation/AttendanceDailyTimeCalculation.cs`; meaningful missing-trace RED, applied delta, 71-check GREEN | Integrated producer/summary acceptance belongs to the parent; this is not a full Release build. |
| C02.1 office/site sources | Existing producer binds raw office member/branch/roster/shift/calendar/boundary evidence and retained corrections, including Manual5 authority. Root reports taxonomy source freeze/composition release | Parent owns shared producer/roster/office/taxonomy wiring. Preserve B pins/raw/holiday/leave/manual semantics; do not rebuild those sources or infer a site source for office work. |
| C02.1 leave/holiday | Passed pure exact paid/unpaid leave bounds, adjusted required arrival, overlaps, explicit rest/holiday credit and unavailable calendar | D01 exact allocation producer and approved company leave/holiday instructions remain dependencies; these checks are not live leave or HR approval acceptance. |
| C02.2 BASIC/actual-calendar divisor | Unchanged Domain payroll rules passed literal October and February 28/29 amounts, distinct BASIC/gross values and final two-decimal midpoint | Parent reruns financial producer/service and source reconciliation guards. No posting/payment claim. |
| C02.2 larger-only overlap | Passed three ordinary 55.56 candidates versus a 1000 late candidate (833.32 excess), higher ordinary 1500 suppressing late, and incomplete occurrence threshold | Preserve existing late reset/counting, legacy rounding and prior-month reconciliation in integrated checks. |
| C02.3 arrivals | Passed configurable 10:00/10:15 equality and one tick/minute after, 11:00 half and 12:59 hourly boundaries, inclusive/strict late comparison, independent late-count flags and alternate parameters | Synthetic typed examples are not signed company policy. Actual approved bands/activation instructions are not fabricated. Half/hourly comparisons retain the existing strictly-after convention. |
| C02.3 schedule/breaks | Passed temporary 11:00 start, six explicitly supplied dates, nine-hour/alternate capacity, overnight duty, fractional split gaps and daily break once | Roster publication, office transfer and taxonomy acceptance remain parent-owned. No built-in six-day calendar, default nine-hour ordinary policy or early-credit exemption was introduced. |
| C02.T1 missing OUT | Existing producer has the actual open-session guard; pure zero-duration closed interval check passed | This pure check does not prove the nullable/open-session producer case. Parent reruns the existing producer verifier. |
| C02.T2 immutable locked result | Existing summary service returns protected frozen evidence for locked days and rejects recalculation | Parent reruns frozen-summary/source/correction cases. New unlocked explanation lines must not rewrite retained locked evidence or historical protected schemas1/2. |
| C02/C04 KPI shortage consumer | Current `KpiSupplementalSourceReader` reads authorized saved days and snapshots; checks saved revision, fingerprint, rules-derived version and numeric match; computes the approved shortage choice with exact ticks | Not a missing trace-only or minute-floor-counter implementation. Parent owns new reviewed-input version wiring and its integrated checks. Reuse the engine's clamped capacity rather than a divergent unclamped scheduled-minus-break expression. |
| C03 reviewed early/study/continuous | Domain was released to parent. Current readonly inspection finds parent `CalculationVersionFor(input)`, typed ReviewedWork input, actual reviewed reader and `FinancialUnverified` checks | Parent owns and verifies these extensions and shared consumers. C02's 71 checks certify only the earlier frozen explanation baseline, not later parent source. Study/continuous cash requires an approved versioned formula. |
| C02/C06 company travel | Approved deployment/trip evidence and company-versus-personal interpretation are still required | No travel producer, ordinary credit, OT credit or company instruction is fabricated. C01/C06 owner supplies the approved contract/examples. |
| C07 eligibility/formula | Root taxonomy guards are preserved. Existing formula uses BASIC, source month 31/28/29, exact ticks, confirmed nine-hour/no-break treatment and final whole-rupee rounding. Added boundary checks are prepared, not run | C07 must run meaningful RED/GREEN after the serial Application slot is released. No broader category mapping or fallback nine-hour pricing for a non-nine-hour schedule is approved. |
| C07 immutable award delivery | APPLIED feature-local Finance projection retains archived source/compensation/policy/eligibility IDs, site/job/session, exact intervals, actual request/decision/reviewer authority and deterministic fingerprint. Checks include named bulk/retry/authority and digest sensitivity to actual identities/amount | Executable C07 checks are still unrun under the build hold. Finance posting/consumption and at-most-once consumption keyed by actual award identity remain F integration, not proven here. |

## Actual Verification

Before the earlier build hold, the check compiled and reproduced the required failure:
`FAIL C02: successful calculation explains the actual configured scheduled ticks`.
The native exit was `-532462766`; source-before/after hashes were stable. The earlier new-check compilation error was corrected before that meaningful RED run, and both logs remain on D:.

After the Domain-only serial slot release, the actual strict Release standalone command was:

```powershell
& 'D:\CosmixVerification\part-c-ordinary-76e5ea760c4e46ee8f8c1b2efe466671\prepared-20261013-01\Run-C02-AfterRelease.ps1' -RunName 'c02-green-handoff-02' -ParentBuildsReleased
```

Fresh actual result: exit 0; `PASS C02 ordinary time checks: 71; pure Domain synthetic policies, no signed company or integration acceptance.` Source-before/after hashes were stable. Completed at `2026-10-13T17:46:30.6383330Z`. No Application or solution build was run. C: reserve immediately before the run was 462,348,288 bytes, above 350 MB; all build/cache/TEMP/TMP outputs were on D: and process environment values were restored.
Evidence: `prepared-20261013-01/c02-green-handoff-02/result.json`, `verification.log`, `source-before.json`, `source-after.json` beneath the D: root above. Earlier meaningful RED and the 17:10 UTC GREEN in `c02-green-serial-01` remain preserved.

The earlier 17:10 UTC SDK first-run log reported an automatically installed ASP.NET development certificate. No host was launched, trust command run or certificate removed. This fresh run explicitly set `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`, `DOTNET_CLI_TELEMETRY_OPTOUT=1` and `DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1`, and restored their prior process values. Its log did not report certificate installation. No SQL/provider/device acceptance, full solution Release, C07 passing count or financial consumption is claimed.

All future builds must wait for explicit parent release and use D: build/restore/cache/TEMP/TMP outputs. Recheck C: reserve >=350 MB before any checkout writes. Back up nonempty existing owned source with CreateNew and verify its hash before an authorized edit.

## Exact C02 Source Freeze

Freeze manifest: `c07-resume-20261013-01/C02-source-freeze.json` under the D: root above.

| Frozen file | Bytes | SHA256 |
| --- | --- | --- |
| `src/Cosmix.Domain/Attendance/Calculation/AttendanceDailyTimeCalculation.cs` | 13685 | `B34C67DFC5FFD896BB215593813B559EC25B7D5903DB2C88D45388DED3F3B819` |
| `docs/attendance-gap-c/C02VerifyOrdinaryTime.cs` | 17593 | `64CFBFC45D194D8068049E0800487AFFBA8E515B0F521DDE00145CDA1A48D9EE` |

Frozen C02 public method/input boundary at handoff (not the later parent v3 source):

```csharp
AttendanceTimeInput(AttendancePolicyRules Rules, DateTimeOffset? ShiftStartsAtUtc,
    DateTimeOffset? ShiftEndsAtUtc, int ScheduledMinutes, int UnpaidBreakMinutes,
    bool IsRestDay, bool? IsHoliday, IReadOnlyList<AttendanceTimeInterval> Duty,
    IReadOnlyList<AttendanceLeaveTime> Leave,
    IReadOnlyList<AttendanceTimeInterval>? ApprovedOvertime = null)

AttendanceDailyTimeCalculation.VersionFor(AttendancePolicyRules rules)
AttendanceDailyTimeCalculation.Calculate(AttendanceTimeInput input)
```

Domain ownership is released to the parent at the frozen hash above after the actual 17:46 UTC GREEN. This worker will not add fields or modify that source concurrently. The 71 checks certify the frozen C02 explanation baseline, not any later parent-reviewed-work extension.

## Actual C07 Helper Freeze

The earlier pre-apply observations are preserved in `c07-resume-20261013-01/C07-actual-helper-freeze-1746.json` under the D: root; they are historical, not the applied Service/Contracts hashes. The existing helper is `OvertimeFormula.WholeRupees(decimal monthlyBase, DateOnly sourceDate, long approvedTicks)` and `ConfirmedOvertimeDurationPolicy.EvaluateAsync(...)` in `OvertimeFormula.cs`, still SHA256 `93A2FEE7EFBF07FE98578D9513D0438F7707BBEB5046EB52D21D964505861527`.

Current applied C07 hashes: `OvertimeService.cs` = `9C19EB5945E442469CEB5FDF34B6D5C7CF01F6A84BC825EAE1E1634C640C3693`; `OvertimeContracts.cs` = `530C51644929C1303B4455FD9B521716BE367D16DBE9452AAE8EC2C63B996AD9`; `OvertimeFinancialAwards.cs` = `5FF193DF192642DB626CC1E127074426B638E21B38125FE138863DA6DE577AFA`; `OvertimeQueriesAndBulk.cs` = `61627D3DCA366A454E1AD48F3EAFE0C54402473BDE2FCE7041D68FCA8198C9E8`. All are nonempty. Exact verified CreateNew backups and current source-ready freeze are under `D:/CosmixVerification/part-c-ordinary-76e5ea760c4e46ee8f8c1b2efe466671/c07-work-allocation-40dfd80d348a4d60a56f24edb9ad73b4/`. The added C07 verifiers have not been run. Parent owns the next integrated build after Missing source is ready. These are source observations, not an assertion that C07 is complete.

The bidirectional guard is APPLIED. Optional `IWorkTreatmentCreditReader? workCredits = null` preserves legacy standalone constructors; current real `AddApplication` plus registered `AddWorkTreatments` resolves the actual reader without a shared-DI edit. The guard reads actual `ReadAsync(employee, date, date)` proof inside the existing authorization transaction and rejects physical overlap regardless of session key or credit role. It protects the full requested span as well as the exact approved monetary intervals, checks before amount calculation and after private submission/decision IO, and covers preview plus single/named-bulk submission and approved-decision replay. It creates no WorkCreditRange, fake work source identity or payment. Historical Finance reads remain archived rather than repriced from current work claims. Runtime SQL concurrency/isolation acceptance is not claimed.

## ReviewedWork Handoff

Exact proposed typed shape, not runtime code: `c07-resume-20261013-01/ReviewedWorkInput.Contract-v3.cs.txt` under the D: root. It supersedes the earlier proposal's method-name/version comments; older artifacts remain preserved. Mapping, exclusion and financial gaps: `c07-resume-20261013-01/ReviewedWork-Contract-Proposal.md`.

Append a null-omitted `AttendanceReviewedWork? ReviewedWork = null` after `ApprovedOvertime`. The root value carries schema, employee/date, source fingerprint and lists of typed early credits, study excuses and continuous requirements. Each item has proof containing actual request/approval/decision/reviewer IDs, retained policy-version IDs, policy fingerprint and source fingerprint. Early credits also carry instruction, canonical session/revision and exact interval; study excuses carry exact interval, deadline/makeup ticks, approved hourly/TDA/KPI choices and written document proof; continuous requirements carry explicit applies-on date, expected ticks, reviewed cycle and contributing sessions.

Add input-aware `CalculationVersionFor(AttendanceTimeInput input)`: nonnull `ReviewedWork` selects `attendance-configured-time-v3`; null calls the unchanged `VersionFor(input.Rules)` and retains v1/v2 numerical behavior. Invalid nonnull proof must block calculation, not silently fall back to v1/v2. `Calculate(AttendanceTimeInput input)` must actually apply accepted early duty/approved study excuse/reviewed continuous expected time in v3, retain actual duty/roster separately and return exact expected/credited ticks for KPI. Parent updates all snapshot/calculator/KPI version call sites together. Historical protected schemas1/2 must never be enriched with current reader results.

Adapter dependencies discovered in the actual C03 contracts:

- Use actual `IWorkTreatmentCreditReader.ReadAsync` and `StudyAsync`, not injected proof IDs. The live reader exposes distinct `EarlyCredit`, `MakeUp` and `NormalWorkTravel` ranges; only an `EarlyCredit` range is admitted as early credit. Do not relabel a makeup/travel reservation or grant another allocation. Credit evidence currently has a policy fingerprint but no policy-version ID; bind retained submission source policies or coordinate a typed source-proof extension. Never invent an ID.
- Bind early intervals to actual canonical snapshot session/revision; the credit range has session IDs but no revision. Interpret an existing EarlyCredit reservation once; do not create a second OrdinaryPay reservation for the same interval. Reject overlap with OT/makeup/travel/recovery claims using the existing exclusive-allocation boundary, including the symmetric OT writer check.
- The reader currently returns reservations, not reviewed continuous cycles/explicit applies-on dates. Expose authoritative reviewed cycle evidence before calculating a continuous requirement; do not infer a 4.5-hour or other default from an interval/date.
- Actual `WorkStudyEvidence` carries the excused start/end, obligation/deadline, written proof and retained `Rules.HourlyTreatment`, `Rules.TdaExcluded`, `Rules.KpiTreatment`. Preserve these explicit choices without synthesizing a default. Study hourly/TDA/KPI choices and continuous expected-time rules are not a cash formula. Parent's reviewed-work source `FinancialUnverified` flag must block special study/continuous cash when no approved versioned formula exists, rather than return zero or price ordinary nine-hour rules.
- Preserve the current resolved roster and its source provenance. The adapter must not choose an individual/default schedule, rewrite a roster or introduce new precedence. Continuous expected ticks are a separate reviewed requirement, not a substituted schedule.

## Parent Integration

No new DI, catalog, DTO, EF, schema or policy-codec hook is needed for the applied explanation delta: the existing configured calculator forwards Domain result lines. The parent owns integrated Release, shared consumer tests, ReviewedWork adapter/producer hook, and version-aware snapshot/calculator/KPI/payroll updates. Approved signed company instructions and travel taxonomy remain explicit dependencies.

The reviewed C07 delta from `c07-resume-20261013-01/C07-Proposed-Financial-Award.patch.txt` is now applied, not awaiting application. Its scope is only `OvertimeContracts.cs`, the Finance projection/supported-formula boundary in `OvertimeService.cs`, and new `OvertimeFinancialAwards.cs`. Root taxonomy guards and sources are not replacement candidates. Tests were written first, but executable RED/GREEN is deferred by the parent's build hold; no full TDD cycle is claimed yet. The gated D: `c07-apply-0d5c4fea55f14e7fb39ea16e3dc634ab/Run-C07-AfterSlot.ps1` compiles archived pre-change Service/Contracts through a verification-only MSBuild overlay for meaningful RED, then runs actual current-source service/rule GREEN phases. It never rewrites/reverts checkout source or creates a test project. Parent performs integrated composition and release. The earlier full Domain candidate is historical evidence, not a second source to compile.

Earlier pre-apply and pre-work-guard packets are preserved as history. Current source-ready status is `c07-work-allocation-40dfd80d348a4d60a56f24edb9ad73b4/C07-SourceReady.md` under the D: root. The symmetric work-allocation guard is implemented, with test-first cases prepared for actual runtime verification. The new D:-only `C07VerifyWorkAllocationComposition.cs` verifies real AddApplication/AddWorkTreatments scoped injection and lazy annual-reader construction without business/provider IO; it is also unrun. Parent builds the frozen source once after Missing is ready, then coordinates runtime checks. Finance consumption/posting and provider concurrency acceptance remain dependencies. No C07 completion or cash-consumption acceptance is implied by source readiness.
