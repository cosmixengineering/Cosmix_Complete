# Part B Integration File Map

This map identifies the delivered B ownership, not every dirty file in the shared
checkout. Part A's HR/access changes must remain intact. No commits, Git sync or
automatic cherry-picking are performed here. Applied B24 must never be rewritten.

## B01 Office Source And Shared Capture

- `src/Cosmix.Application/Attendance/Offices/OfficeBoundaryContracts.cs`
- `src/Cosmix.Application/Attendance/Offices/OfficeBoundaryService.cs`
- `src/Cosmix.Application/Attendance/Offices/OfficeAttendanceSourceReader.cs`
- `src/Cosmix.Domain/Attendance/Offices/OfficeGeofenceVersion.cs`
- `src/Cosmix.Infrastructure/Repositories/Attendance/OfficeBoundaryRepository.cs`
- `src/Cosmix.Infrastructure/Database/Configurations/Attendance/OfficeGeofenceVersionConfiguration.cs`
- `src/Cosmix.Api/Controllers/Attendance/OfficeBoundariesController.cs`
- `src/Cosmix.Application/Attendance/Punches/AttendanceCaptureService.cs`
- `src/Cosmix.Application/Attendance/Punches/AttendanceCaptureContracts.cs`
- `src/Cosmix.Domain/Attendance/Punches/RawPunchEvent.cs`
- `src/Cosmix.Domain/Attendance/WorkSessions/WorkSession.cs`
- `src/Cosmix.Domain/Attendance/Sites/SiteGeofenceEvaluator.cs`
- `src/Cosmix.Infrastructure/Database/Configurations/Attendance/RawPunchEventConfiguration.cs`
- `src/Cosmix.Infrastructure/Database/Configurations/Attendance/AttendanceCalculationConfigurations.cs`
- `src/Cosmix.Application/Attendance/Reminders/MissingCheckoutReminderRules.cs`
- `src/Cosmix.Infrastructure/Repositories/Attendance/MissingCheckoutReminderRepository.cs`

Exact calculation/facts/OT/payroll-source changes and standalone checks are listed
in [the consumer handoff](../attendance-policies/Office-Calculation-Payroll-Consumers-20261013.md).
They extend existing sources; there is no duplicate payroll module or Office OT.

## B02-B07 Feature-Owned Lists

- B02: `Application/Attendance/Punches/CaptureValidation`, its two Domain entities,
  corresponding Infrastructure configurations/repositories, capture-policy controller
  and existing protected attendance audit reader. Exact list:
  [B02Verification.md](B02Verification.md).
- B03: existing correction/carry-forward services, dedicated Admin alternative
  policy/contracts/repository, office evidence and nine alternative API actions.
  Exact source/controller/check list:
  [B03 handoff](B03-Office-Corrections-Handoff-20261013.md).
  The final authority journal/repository supplements the selected calculation
  repository without adding EF fields. Additional exact files:
  `src/Cosmix.Application/Attendance/Corrections/AlternativeCorrectionApprovalContracts.cs`,
  `src/Cosmix.Application/Attendance/Corrections/AlternativeCorrectionApprovalJournal.cs`,
  `src/Cosmix.Infrastructure/Repositories/Attendance/AlternativeCorrectionApprovalRepository.cs`,
  and the existing calculation repository, calculation producer, CorrectionReviewService
  and both AttendanceCorrectionRegistration extensions. The selected repository
  query/DI check verifies that writer and reader share the same scoped journal.
  Parent additionally changed CarryForwardService and its existing verifier for
  special beneficiary denial on all decisions and receipt replay; no EF delta.
- B04: retain the existing Development-only self-hosted adapter and face challenge,
  enrollment and Check-In contracts. No production provider/model or deployment
  configuration is created by this source delivery.
- B05: face service/capture-evidence retirement hooks, durable repository, worker,
  journal/service/processor/disposal boundaries and protected store use controls.
  Exact list and disposal limits:
  [B05 handoff](../attendance-capture/PartB05-Durable-Retirement-20261013.md).
- B06: exact 25 runtime/doc/check files, including the frozen two EF changes,
  offline-observation controller/service/repository and expiring encrypted claim
  store/cleanup worker: [B06 handoff](B06ConsumerContract.md).
- B07: exact report contracts/rules/service/repository/controller plus runnable
  examples and recovery checks: [B07 handoff](B07ConsumerContract.md).
- B08: scope closure only; no source import module exists or is required.

## Coordinated Shared Files

- `src/Cosmix.Application/Attendance/Common/AttendanceOperation.cs`: capture-only
  final receipt replay session/device/authority-time revalidation.
- `src/Cosmix.Application/Authentication/Access/AccessContracts.cs` and
  `src/Cosmix.Infrastructure/Authentication/Access/AccountAccessReader.cs`:
  internal JSON-ignored grant timing and exact SQL interval projection; no EF change.
- `src/Cosmix.Application/DependencyInjection.cs`: Office and independent capture
  policy services, exact concrete Site/Office jurisdiction reader composition;
  preserve separately requested Part A feature registrations.
- `src/Cosmix.Infrastructure/DependencyInjection.cs`: Office and capture-policy
  repositories; preserve all Part A descriptors.
- `src/Cosmix.Application/Authentication/Access/AccessPermissionCatalog.cs`:
  explicit B02/B03 permission definitions; no automatic grants.
- `src/Cosmix.Application/Attendance/Punches/AttendanceCaptureRegistration.cs` and
  `src/Cosmix.Infrastructure/Repositories/Attendance/AttendanceCaptureRegistration.cs`:
  existing feature registrations and B05 retirement integration.
- `src/Cosmix.Api/Program.cs`: registered default-disabled retirement worker;
  no production cleanup/provider activation.
- `src/Cosmix.Api/OpenApi/OrganizationOperationTransformer.cs`: office boundary
  created-version Location metadata using existing company authority.
- `src/Cosmix.Infrastructure/Database/CosmixDbContext.cs`: B entities only within
  the agreed post-A23 model window; no second DbContext/database.

## Frozen Schema

- `src/Cosmix.Infrastructure/Database/Migrations/20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims.cs`
- Matching `.Designer.cs` and current `CosmixDbContextModelSnapshot.cs`.
- `docs/sql/20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims.sql`.

See [frozen hashes and SQL-owner boundary](../integration/Part-B24-Source-Handoff-20261013.md).
Later additions require a later separate reviewed migration, not an applied-file edit.

## Parent Checks And Records

- `docs/attendance-gap-b/B01VerifyOffice{Source,Boundary,Model,Persistence}.cs`
- `docs/attendance-gap-b/VerifyCaptureAuthorityTime.cs`
- `docs/attendance-gap-b/VerifyRegisteredContracts.cs`
- `docs/attendance-gap-b/VerifyBMigration.cs`
- `docs/attendance-gap-b/ScaffoldBMigration.cs` (offline guarded generator; not rerunnable after B24)
- `docs/attendance-gap-b/VerifyIntegratedSource.ps1`
- Existing capture/composition/reminder checks and `VerifyOvertimeService.cs` fixture
  updated for the current real constructor/authority contracts; no test project.
- `docs/attendance-gap-b/Continuation-Plan-20261013.md`, `Implementation-Ledger-20261013.md`,
  `Delivery-Status-20261013.md`, `B01OfficeCapture-Contract.md` and this map.
- Latest Part B addenda in `docs/SRS-Module-Map.md`,
  `docs/attendance-completion/Verification.md` and `Attendance-UAT-Guide.md`.

Private database backups, keys, credentials, model/provider settings and local
acceptance manifests are deliberately not part of this file map or a source package.
