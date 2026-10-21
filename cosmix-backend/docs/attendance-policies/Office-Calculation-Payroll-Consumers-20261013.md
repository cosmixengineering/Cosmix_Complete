# B01 Office Calculation and Payroll Source Consumers

Status: ordinary Office source checkpoint is ready for the parent's integrated suite. Parent Office jurisdiction wiring is present and verified; fresh serial solution Release build has zero warnings/errors. Parent owns actual authenticated acceptance. Calculation constructor/correction schema verification is held pending the separate stable B3 Alternative contract/checkpoint.

## Scope and Authority

- Existing fixed `OFFICE` category and dated Branch membership only. No StaffTaxonomy/HrFoundationPolicies integration or invented defaults.
- Application records, services, read-only repository projections, and standalone checks only. No Domain/EF entity, model, migration, snapshot, core DI, catalog, HR, capture, correction, or carry-forward edits.
- Office calculation requires the actual historical roster/member/shift/calendar and effective approved boundary from `IOfficeAttendanceSourceReader`. Every Office session and raw punch must match that exact source.
- Raw Office duty still requires the repository's accepted assessment and consumed passed/liveness/match face evidence. Existing device/challenge protection is unchanged. Manual Office corrections require schema-4 approved protected evidence, not fabricated raw punches or face success.
- Office membership must remain valid on every local date spanned by the session. Historical lifecycle eligibility and overlap guards remain in force.
- Office OT references are rejected, not converted into a silently zeroed award. OT policy/formula/source-engine implementation is unchanged.
- Optional Office fields are omitted when null. Site factual JSON/hash property order is preserved; Site session projections omit the new Office-only membership/calendar fields. Existing Site correction schema-3 behavior is retained; Office requires schema 4.
- Payroll manifests and line source IDs retain actual Branch, roster, member, shift, boundary, and calendar. The producer validates Office snapshots against the actual resolved roster and factual fingerprint before using them.
- Jurisdiction is dated Office boundary metadata, not a Head Office fallback, legal certification, payroll activation, or payment instruction. Missing Office proof/history remains unavailable; missing money remains null.

## Parent DI Hook

Existing `IOfficeAttendanceSourceReader` and `IOfficeBoundaryRepository` registrations are used; no duplicate Office source or storage is needed. The optional calculation constructor dependency resolves when the existing Office reader is registered, and otherwise reports `office_attendance_source_unavailable` for Office.

Parent has now wired the following concrete Site reader, Office factory, and interface alias in core DI. The isolated registration check below uses these actual `AddApplication()` descriptors, not a copied proposed factory:

```csharp
services.AddScoped<CompanyPayrollJurisdictionReader>();
services.AddScoped<OfficeAttendancePayrollJurisdictionReader>(provider =>
    new OfficeAttendancePayrollJurisdictionReader(
        provider.GetRequiredService<CompanyPayrollJurisdictionReader>(),
        provider.GetRequiredService<IOfficeBoundaryRepository>()));
services.AddScoped<IAttendancePayrollJurisdictionReader>(provider =>
    provider.GetRequiredService<OfficeAttendancePayrollJurisdictionReader>());
```

Namespaces: `Cosmix.Application.HR.CompanySettings`, `Cosmix.Application.Attendance.PayrollInputs`, and `Cosmix.Application.Attendance.Offices`. Parent owns the actual core DI edit and composition check.

Ready checkpoint names: `OfficeAttendancePayrollJurisdictionReader`, `CompanyPayrollJurisdictionReader`, `IAttendancePayrollJurisdictionReader`. Existing source dependencies are `IOfficeBoundaryRepository` and `IOfficeAttendanceSourceReader`.

Ready checkpoint test names:

- `docs/attendance-policies/VerifyOfficePayrollJurisdictionRegistration.cs` (new): 12 actual registration/factory/alias/scope checks, using read-only synthetic repositories and the parent descriptors. No host/model/SQL/taxonomy consumers are instantiated.
- `docs/attendance-policies/VerifyOfficePayrollSources.cs`: refreshed 38 source/history/legacy JSON/fingerprint checks.
- `docs/attendance-policies/VerifyPayrollInputs.cs`: refreshed 55 producer checks.
- `docs/attendance-completion/VerifyPayrollInputService.cs`: refreshed 73 service checks.

Fresh jurisdiction/payroll checkpoint total: 178 assertions. Existing calculation/Facts/query results below remain the ordinary Site-3/Office-4 baseline; calculation constructor/schema verification stays held for the separate B3 checkpoint. No `SourceSchema` API or schema mapping was invented.

The registration test initially emitted assembly-copy retry warnings while a concurrent build held `Cosmix.Application.dll`; it completed successfully. A concurrent solution build also hit `CS2012` on that lock. The final serialized solution build (`--maxcpucount:1`) succeeds with zero warnings/errors. The earlier transient `IZoneClaimRetentionStatus` resolution failure in the other owner's Zone source is no longer present in that final build. No unrelated source or warning guards were modified.

## Exact Source Files

- `src/Cosmix.Application/Attendance/DailySummaries/AttendanceFacts.cs`
- `src/Cosmix.Application/Attendance/Calculation/AttendanceCalculationContracts.cs`
- `src/Cosmix.Application/Attendance/Calculation/AttendanceCalculationSnapshotProducer.cs`
- `src/Cosmix.Application/Attendance/Overtime/OvertimeContracts.cs`
- `src/Cosmix.Application/Attendance/PayrollInputs/AttendancePayrollInputContracts.cs`
- `src/Cosmix.Application/Attendance/PayrollInputs/AttendancePayrollInputServiceContracts.cs`
- `src/Cosmix.Application/Attendance/PayrollInputs/AttendancePayrollInputProducer.cs`
- `src/Cosmix.Application/Attendance/PayrollInputs/AttendancePayrollInputService.cs`
- `src/Cosmix.Application/Attendance/PayrollInputs/OfficeAttendancePayrollJurisdictionReader.cs` (new)
- `src/Cosmix.Infrastructure/Repositories/Attendance/AttendanceCalculationRepository.cs`
- `src/Cosmix.Infrastructure/Repositories/Attendance/OvertimeRepository.cs` (read-only employment/session projections only)

`OvertimeSources.cs` is not part of the delivered changes. `VerifyCaptureService.cs` remains handed off to the parent; the parent's reported 135 capture checks are not counted as this consumer's verification.

## RED/GREEN Evidence

- `docs/attendance-completion/VerifyFacts.cs`: RED reproduced unchanged factual hash after Office boundary change; GREEN 17 checks, including exact legacy Site hash and independent Office source mutations.
- `docs/attendance-policies/VerifyDailyCalculation.cs`: RED reproduced rejection of real Office duty by the Site-only schedule gate. GREEN 90 checks, including exact raw/session context, overnight membership ending, schema-4 manual Office duty, tampered retained Office proof, and rejection of Office proof in schemas 1/2/3/5.
- `docs/attendance-policies/VerifyPayrollInputs.cs`: RED reproduced accepting an Office snapshot whose retained Branch disagreed with the resolved roster. GREEN 55 checks, including historical source IDs, changing boundary fingerprint, and missing Office proof.
- `docs/attendance-completion/VerifyPayrollInputService.cs`: RED reproduced dropping Branch from jurisdiction requests and later accepting configured jurisdiction without retained Office proof. GREEN 73 checks, including saved historical location and unavailable/null-money state.
- `docs/attendance-policies/VerifyOfficePayrollSources.cs` (new): RED reproduced returning unavailable instead of rejecting an invalid Office business date. GREEN 38 checks: exact legacy JSON/hash shapes, actual dated boundary selection, future/stale/misbound history, no HO fallback, missing proof/history, and fixed Office OT ineligibility. Schema 1/2/3 serialization shape checks do not imply that those schemas are valid Office correction evidence.
- `docs/attendance-policies/VerifyDailyCalculationQueries.cs`: adds exact Office raw projection coverage. GREEN 11 production translation checks after the unrelated Infrastructure syntax correction; zero SQL connections.
- Application-only Release build: zero warnings/errors.
- Solution Release build: initially blocked by three `CS1012` errors in `src/Cosmix.Infrastructure/Zones/EncryptedZoneClaimStore.cs:190` (columns 75/84/93). Its owner corrected that source; the fresh solution build passes with zero warnings/errors. That file was not edited by this consumer work.
- Unmodified `docs/attendance-completion/VerifyOvertimeRules.cs`: 26 regression checks passed; no OT formula or eligibility source-engine changes.
- Unmodified `docs/attendance-completion/VerifyPayrollReconciliation.cs`: 48 regression checks passed.
- Unmodified `docs/attendance-policies/VerifyPayrollInputQueries.cs`: 6 production translation checks passed; zero SQL connections.
- Unmodified `docs/attendance-completion/VerifyOvertimeService.cs`: attempted, but its authentication proxy does not implement the parent's new `IsCurrentSystemRoleAssignmentAsync` call and fails at the fixed-system-role check. No source guard was bypassed and that fixture was not edited outside this ownership.

Primary consumer GREEN total: 284 checks (17 + 90 + 55 + 73 + 38 + 11). Additional unchanged regressions: 80 checks (26 OT rules + 48 reconciliation + 6 payroll query translation). The failed unmodified OT service fixture is not included in either total. No warnings were suppressed and no guard tests were bypassed.

These ordinary correction/schema results precede the parent's Alternative contract hold. They do not constitute medical/accessibility/out-of-boundary Alternative duty acceptance. The new fixed-Admin, dedicated-permission special-duty path must bind its protected case/config evidence and discriminator explicitly once the correction owner supplies the stable checkpoint. No constructor/schema verification edits or reruns are made during that hold; ordinary HR manual duty is not treated as proof of special Admin duty.

Standalone command pattern:

```powershell
dotnet run --file docs/attendance-policies/VerifyDailyCalculation.cs --no-cache -c Release --property:RestoreSources="$PWD/src/Cosmix.Infrastructure" --property:NuGetAudit=false --property:UseSharedCompilation=false
```

## Remaining Limits

No SQL connections/writes, API host, live provider, credentials, real employee data, physical file deletion, or Git operations were used. Query checks only translate LINQ with a connection-denying interceptor. All workflow evidence is synthetic in-memory data.

Parent still owns final DI composition, schema/capture/correction integration, the frozen A23 migration, and genuine authenticated database/provider/device acceptance. Legal policy authority, payment release, and taxonomy integration remain outside this delivery.

Core Office jurisdiction wiring is now present and its actual isolated descriptor graph passes the 12-check fixture. Parent retains full integrated-suite ownership. Until the B3 Alternative checkpoint arrives, special-duty consumer integration is pending and not claimed complete; follow-up files are `AttendanceCalculationSnapshotProducer.cs` (`ValidateCorrection`) and `VerifyDailyCalculation.cs`.

B05 durable retirement's disposal/hold/backup/key authority and production cleanup acceptance remain pending and disabled by default; this Office consumer work does not mark B05 complete.

## Latest Special-Authority Consumer Freeze - 13 October 2026

The pending schema5/selected-repository statements above are superseded by this
checkpoint. Normal Site3 and Office4 behavior/legacy bytes remain; all three
Admin alternatives on Site/Office now bind their real approved decision, protected
source and retained authority. The producer does not accept schema5 merely by number.

The selected AttendanceCalculationRepository implements
IAttendanceAlternativeCorrectionApprovalReader through the existing feature registration.
CorrectionReviewService and that reader resolve the SAME scoped
AlternativeCorrectionApprovalJournal. Its explicit internal staging lease requires
the actual transaction identity and exact flushed decision/session. Missing outer
audit/summary alone cannot grant staged authority. Dedicated protected audit evidence
retains explicit grant timing (ordinary PermissionGrant JSON intentionally omits it),
fixed ADMIN assignment, authenticated web/MFA actor/session, source/policy/document
identities and original/current scopes. Initial calculation revalidates live evidence
and authority after private IO. Historical reads require the exact immutable authority
snapshot and outer correction-decision audit, without inferring authority from today's
role or rewriting old approval after revocation.

Special decision time and manual-session calculation time share the refreshed clock.
Ordinary HR timing is unchanged. All special beneficiary Approve/Return/Reject and
receipt replays return403 `correction_beneficiary_cannot_decide`, including supervisor
requests. Parent's matching factual carry-forward guard leaves locked source and
financial evidence unchanged.

Current parent-executed correction196, calculation181, actual selected-reader/query/DI22,
payroll producer55, payroll service73 and factual carry-forward87 passed with synthetic
IO or forbidden-connection translation. Actual Dev/Prod DI26 and registered API/JSON
checks passed. Final strict four-project Release has zero warnings/errors. No SQL,
real provider/device, payment or production authority acceptance follows from these checks.

New files are AlternativeCorrectionApprovalContracts.cs and
AlternativeCorrectionApprovalJournal.cs under Application/Attendance/Corrections,
and AlternativeCorrectionApprovalRepository.cs under Infrastructure/Repositories/Attendance.
Existing review, calculation producer/repository and feature registrations were extended.
No core/EF/migration delta is required or made. Parent independently verified the
owner's eight-source/three-check SHA256 freeze manifest and current check-file hashes.
See the final Part B delivery status and changed-file map for the complete integrated
record and the preserved test-host/provider/disposal blockers.
