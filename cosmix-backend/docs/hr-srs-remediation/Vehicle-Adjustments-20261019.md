# Bounded HR Vehicle Adjustments

## Scope and Source

This slice implements HR SRS VEH-003 nearer-assignment monthly evidence, its
explicitly configured maintenance effect, retained maintenance-cycle transitions,
and VEH-008 amended HR incident decisions. It does not implement Accounts posting,
settlement, payment, insurer settlement, employee deduction or legal certification.
It preserves the existing module repositories, AttendanceOperation authorization
transaction, private evidence store and generic approval request/decision storage.

No company rates, financial values, reviewer grants or policy versions were seeded.
Synthetic test values are fixtures only. No product build, host, SQL connection,
database operation, migration application, provider call or Git operation was run
by this worker. Product builds are parent-owned.

## Implemented Monthly Workflow

- Preview a completed Pakistan-local calendar month for one employee and vehicle.
  One retained vehicle policy must cover the entire month. Every selected approved
  trip must use that exact policy and retain schema-3 mileage evidence. The exact
  commute revision referenced by the original approval is used, never today's
  replacement commute baseline.
- For configured eligible purposes, calculate `max(0, baseline - approved km)`.
  Exactly one eligible trip per date is supported. More than one fails closed;
  ordering by timestamp or GUID never chooses a business precedence.
- Sum all approved eligible work kilometres for the same employee/vehicle/month.
  Explicit `CapAtMonthlyEligible` caps the negative monthly adjustment at that sum;
  explicit `RetainSignedDeficit` retains the full signed deficit. No option is a
  default. A month without approved mileage is unavailable, not an entitlement.
- Submit with exact preview fingerprint, nullable-but-required latest request ID,
  evidence and reason. A pending revision blocks another submission. Review needs
  the exact rowversion, configured reviewer role, and an actor distinct from both
  requester and employee beneficiary. Approved/Returned/Rejected are supported.
- Approval rechecks the exact source fingerprint. Late-approved mileage blocks
  stale approval. Return/reject can close a stale request; a new explicit revision
  can then be submitted. Older requests, decisions and calculations are retained.
- The latest approved revision replaces its approved predecessor. It is not an
  additional deduction. Revision ordering and protected decision evidence are
  validated before history is used. Limits fail closed at 1,000 revisions per
  vehicle or incident and 10,000 approved vehicle mileage rows per calculation.

### Actual Consumers

`GET .../monthly-adjustments/entitlement` is the explicit HR distance handoff:

| State | Usable `AdjustedEligibleKm` |
| --- | --- |
| `Approved` | Latest approved revision's exact total, only while sources match |
| `PendingReview` | null; a preview or pending proposal is not approved |
| `ConfigurationPending` | null; absent monthly rules/policy do not imply zero |
| `SourcesChanged` | null; old approved evidence is retained but not consumable |
| `SourceUnavailable` | null; missing/invalid evidence blocks use |

The response retains the latest request ID and exact previous approved view when
one exists. It contains kilometres only, not a payable value or Accounts command.

Maintenance submission and review are real internal consumers. If a retained
monthly rule explicitly selects `ReduceOutstation`, they add only the latest
approved revision's signed adjustment to approved vehicle mileage. `None` leaves
maintenance mileage unchanged. Source changes block maintenance until an explicit
replacement monthly revision is independently reviewed. A pending service whose
eligibility changes cannot be approved from stale evidence. Negative adjusted
maintenance totals fail closed. A month affects a service only after month-end.

The original mileage rows' `MonthlyAdjustmentKm` remain zero intentionally. New
monthly evidence is independent of trip rows, avoiding duplicated trips in fuel
or maintenance queries. Fuel claims remain their exact retained per-trip amounts;
this slice neither edits them nor silently redistributes monthly differences
across them. Consumers needing a monthly HR result must use the entitlement read,
not sum fuel claims and assume that sum includes a monthly adjustment.

## Policy Configuration

Existing vehicle schema 1 remains accepted with unchanged canonical JSON shape.
Schema 2 requires an explicit `Adjustments` property, which can be null to leave
these capabilities unconfigured. Its object requires all four properties:

- `SchemaVersion`: structural version 1.
- `Monthly`: null or an explicit rule containing `AssignmentSelection`,
  `OffsetScope`, `ExcessTreatment`, `MaintenanceEffect`, `EligiblePurposes`,
  `ReviewerRoleCodes`, and `AuthorityReference`.
- `MaintenanceTransitions`: explicit array of `Kind`, `PreviousPolicyVersionId`,
  `Mode`, `AuthorityReference`. Supported mode is `CarryConsumedKilometers`.
- `IncidentAmendments`: null or `AllowedPriorOutcomes`, `ReviewerRoleCodes`,
  `AuthorityReference`. Amendment roles must also be configured incident reviewers.

Supported monthly selection is `SingleEligibleTripPerDay`; supported offset scope
is `SameEmployeeVehicleMonthAllApprovedWork`. Eligible purposes must explicitly
select ComplaintVisit, FieldVisit and/or Outstation enum values. Null, missing,
unknown, duplicate, undefined enum and incomplete required fields fail validation
where configuration is required. No missing decimal, date or enum is accepted as
an approved zero/default. Policy roles must exist and be active; their names do
not grant permissions.

Changed maintenance thresholds/cycle modes require the new version to name the
exact previous policy and carry previously consumed kilometres. The preceding
approved service remains unchanged. No transition means fail closed. This is not
an implementation of automatic closure/succession of existing open-ended policies.

## Incident Amendment Workflow

An existing independently reviewed incident can receive an evidenced proposal
only when the currently effective policy explicitly allows its current outcome.
The proposal names the current decision ID. Only one pending proposal is allowed.
Its independently authorized reviewer uses the retained proposal policy, both
incident and amendment role rules, expected rowversion and predecessor identity.

Approving a proposal changes the current HR interpretation, not the original
report, initial approval, prior proposed liability or decision. Returned/rejected
proposals remain in history without changing the current interpretation. Additional
proposals can follow. Current review reads project the latest approved amendment;
history exposes original, current and every amendment. All responses keep
`EmployeeDeductionAuthorized=false` and `InsuranceSettled=false`.

Incident list filtering, totals and displayed liability now use the same effective
review projection. An approved amendment with a rejected outcome filters as a
rejected incident, not as an approved incident. Pending, returned or rejected
amendment proposals do not replace the last approved interpretation. Original
report evidence and third-party fields stay original; current insurer reference,
liability, currency and approval request identity come from the retained decision.
Projection precedes filtering and paging, with deterministic date/ID ordering.
More than 1,000 candidate incidents fails with `vehicle_incident_projection_limit`;
clients must narrow by custody ID. Partial candidate reads also fail explicitly,
never returning misleading totals.

## Contracts and Authorization

All routes below are under `/api/v1/hr/vehicles`:

| Method | Route |
| --- | --- |
| POST | `/{vehicleId}/monthly-adjustments/preview` |
| POST | `/{vehicleId}/monthly-adjustments` |
| POST | `/{vehicleId}/monthly-adjustments/{requestId}/review` |
| GET | `/{vehicleId}/monthly-adjustments?employeeId=...&monthStart=...` |
| GET | `/{vehicleId}/monthly-adjustments/entitlement?employeeId=...&monthStart=...` |
| POST | `/incidents/{id}/amendments` |
| POST | `/incidents/{id}/amendments/{requestId}/review` |
| GET | `/incidents/{id}/review-history` |

Monthly methods require existing `attendance.vehicle.fuel.manage`; incident
methods require existing `attendance.vehicle.incident.manage`. Both require a
current Web/MFA session and All scope. No employee-scoped fallback is inferred.
Writes require Idempotency-Key and reason; reads and writes use existing auditing.
Review replay rechecks independent actor and configured role authority. All request
DTO fields are JsonRequired, including explicit nullable fields. Public request
boundaries reject null. Global policy overlap and adjustment history queries run
inside the existing serializable authorization operation, with rowversion checks
on reviews and protected result/audit receipts for retries.

No new DI registration, permission definition, role grant, DbSet or migration is
required. `VehicleFuelService` now also consumes existing `ICurrentAuthentication`,
`IAccountAccessReader`, `IAccessManagementRepository`, `ISchedulingJournalRepository`
and `TimeProvider` registrations. Parent owns the shared AttendanceOperation
`beforeCommit` callback and replay reauthorization changes. The vehicle helper
rechecks the live session, Web/MFA identity, effective All-scope grant and same
configured reviewer role after private decision evidence IO, before business save.
Review callbacks repeat the retained policy/independent-reviewer checks and fresh
authority after result/audit IO and protected replay reads. Submit/propose writes
also check current authority after their private snapshot and before commit.
No nested transaction or shared-file edit was introduced by this vehicle fix.
Parent already registered the earlier vehicle publication permission.
With explicit parent authorization, the legacy `VerifyVehicleEntitlements.cs`
and `VerifyVehiclePolicyPublication.cs` fixtures now return an empty approval list
from `ListVehicleAdjustmentRequestsAsync` for their no-adjustment scenarios.
Publication's unsupported-schema case now uses 99, and schema 2 requires an
explicit Adjustments choice. `VerifyVehicleRepository.cs` adds exact history,
request/decision and retained commute query checks. These legacy fixtures also
provide explicit grant timing and resolve the service constructor across frozen
builds; they do not fabricate adjustment history.

## Verification

Standalone suite: `docs/hr-srs-remediation/VerifyVehicleAdjustments.cs`.

```powershell
& ./docs/attendance-gap-c/VerifyManaged.ps1 `
  -ReleaseDirectory 'D:\CosmixVerification\hr-backend-20261019\hr-benefits-build03\bin\Cosmix.Api\release' `
  -Sources @('docs/hr-srs-remediation/VerifyVehicleAdjustments.cs')
```

The command above reproduces the review-fix RED cases against parent build 03.
Initial expanded RED run against `hr-scope-build`
compiled successfully: 1/16 scenarios passed, with the source-evidence fixture
passing and missing workflow/schema/transition behavior failing. Evidence:
`D:\CosmixVerification\part-c-parent-20261013\managed-e8f19aa831644c82801850b9fe3b92aa`.

Parent `hr-benefits-build02` passed 16/16 scenarios before the final entitlement
read and validation guards were added. Evidence:
`D:\CosmixVerification\part-c-parent-20261013\managed-9bdf1b4984f947378a9fa06f4cfaaa86`.
These prove real application services with synthetic repository, private storage,
authentication and clock collaborators. They include observed 60 km from 70 - 10,
180 km after replacing a prior approved revision (not double-counted), unchanged
fuel claim evidence, stale-source maintenance blocking, retry/RBAC/concurrency
guards, and retained incident history. This is not HTTP/SQL acceptance.

The final suite adds explicit approved entitlement/read-state and decision-history
integrity tests. It compiled and ran against build 02: 17/20 passed. The three
expected failures were the two entitlement-read scenarios (method absent in that
snapshot) and the added base-incident-authority configuration guard. Evidence:
`D:\CosmixVerification\part-c-parent-20261013\managed-757fc163f12842b088a23d838e0a3ade`.
That build-02 result was not a passing result for the final source. No new test
project was created.

### Pre-Review Build-03 Verification

After parent confirmation, all four suites ran against the frozen
`D:\CosmixVerification\hr-backend-20261019\hr-benefits-build03\bin\Cosmix.Api\release`
and the verification command exited 0:

| Suite | Result |
| --- | --- |
| VerifyVehicleAdjustments | 20/20 scenarios |
| VerifyVehicleEntitlements | 46/46 scenarios |
| VerifyVehicleRepository | 53/53 checks; zero SQL connection attempts |
| VerifyVehiclePolicyPublication | 71/71 scenarios |

Exact evidence directory:
`D:\CosmixVerification\part-c-parent-20261013\managed-45d9088d315f485b91ab3d5138ca8e1e`.
It retains each suite's source, compiler response/compile log, execution log and
`inputs.json` dependency hashes. The runner confirmed source/input stability.
The repository suite executes synthetic queries and offline SQL translation,
not live database commands. No product source change or product build was made
during this run. Exact combined invocation:

```powershell
& ./docs/attendance-gap-c/VerifyManaged.ps1 `
  -ReleaseDirectory 'D:\CosmixVerification\hr-backend-20261019\hr-benefits-build03\bin\Cosmix.Api\release' `
  -Sources @('docs/hr-srs-remediation/VerifyVehicleAdjustments.cs',
    'docs/hr-srs-remediation/VerifyVehicleEntitlements.cs',
    'docs/hr-srs-remediation/VerifyVehicleRepository.cs',
    'docs/hr-srs-remediation/VerifyVehiclePolicyPublication.cs')
```

### Review-Fix RED and Build-04 Verification

Reviewer findings were reproduced against frozen build 03 before changing product
code. The first expansion ran 37 scenarios: original 20 passed, 17 new scenarios
failed for expected expiry/projection behavior, not compilation or fixture errors.
Evidence: `D:\CosmixVerification\part-c-parent-20261013\managed-1852b9d1e53e4987a422826d48553820`.

The final expanded suite has 45 scenarios. It compiles against build 03 and runs
20/45: 12 grant/session expiry cases inside decision/result/audit storage, two
session-expiry replay cases, eight role-change storage/replay cases and three
effective incident-list cases fail as expected. Evidence:
`D:\CosmixVerification\part-c-parent-20261013\managed-8fdf508a55f24f61a0b1e4cf2650fe01`.
The harness models transaction rollback of business, receipt and audit records;
it also checks decision-evidence expiry is denied before the business save.
External protected files can be orphaned on a denied transaction; these are not
committed HR decisions or successful receipts.

Product source was frozen for parent build 04. All four suites subsequently passed
against its frozen assemblies: Adjustments 45/45, Entitlements 46/46, Repository
53/53 and PolicyPublication 71/71, with command exit 0. All 25 new expiry, role-change
and effective-incident-projection regressions passed. Exact evidence:
`D:\CosmixVerification\part-c-parent-20261013\managed-ffa3f417d4e84729b80700d12458260d`.
No product build was run here.

After the constructor/timing fixture changes, the three legacy suites were rerun
separately against frozen build 03 and exited 0: Entitlements 46/46, Repository
53/53 (zero SQL connection attempts), PolicyPublication 71/71. Evidence:
`D:\CosmixVerification\part-c-parent-20261013\managed-e347fdc959eb4faa9175fc078f6fe0c7`.
The combined command stops at the intentionally RED adjustment suite; this
separate run is the evidence for the updated legacy fixtures.

No additional product wiring is requested beyond the already supplied shared
`beforeCommit` API and the five existing constructor dependencies noted above.

### Final Build-05 Verification

Parent supplied build 05 after a narrow shared session-check ordering fix. Vehicle
product and test source stayed unchanged. All four suites compiled and ran against
the parent-confirmed frozen assemblies; the combined command exited 0:

| Suite | Result |
| --- | --- |
| VerifyVehicleAdjustments | 45/45 adjustment scenarios |
| VerifyVehicleEntitlements | 46/46 legacy assertions/scenarios |
| VerifyVehicleRepository | 53/53 legacy checks; zero SQL connection attempts |
| VerifyVehiclePolicyPublication | 71/71 legacy assertions/scenarios |

Report the adjustment suite's **45 scenarios** separately from the **170 legacy
assertions/checks** (46 + 53 + 71). These counts have different granularity; they
are not a uniform total of 215 checks or 215 adjustment scenarios.

Exact evidence directory:
`D:\CosmixVerification\part-c-parent-20261013\managed-1b825ff952bd4a66a1ae3e3d5e0e4b00`.
It retains frozen suite sources, compile responses/logs, execution logs and
`inputs.json` dependency hashes. The runner confirmed source/input stability.
Observed approved monthly adjusted kilometres, historical replacement, stale-source
blocking, all 25 review-fix regressions and legacy policy/entitlement behavior pass.
This is synthetic collaborator/offline translation verification, not live SQL or
authenticated HTTP acceptance. No host, provider, migration, Git operation or
product rebuild was run. Only this handoff document changed after the final run;
vehicle product source remains frozen.

Exact final invocation:

```powershell
& ./docs/attendance-gap-c/VerifyManaged.ps1 `
  -ReleaseDirectory 'D:\CosmixVerification\hr-backend-20261019\hr-benefits-build05\bin\Cosmix.Api\release' `
  -Sources @('docs/hr-srs-remediation/VerifyVehicleAdjustments.cs',
    'docs/hr-srs-remediation/VerifyVehicleEntitlements.cs',
    'docs/hr-srs-remediation/VerifyVehicleRepository.cs',
    'docs/hr-srs-remediation/VerifyVehiclePolicyPublication.cs')
```

### Superseding Build-06 Verification

The parent reran all four vehicle suites in the final 24-suite batch against
`hr-benefits-build06`, including the final shared read-authority check. Command
exit 0; adjustments 45/45, entitlements 46/46, repository 53/53 with zero SQL
connections, policy publication 71/71. Exact frozen evidence:
`D:\CosmixVerification\part-c-parent-20261013\managed-fa0e8bddaeb341d2a1239e699e0d8d8c`.
This supersedes build05 as the latest verified product assembly; the scope and
remaining limitations below are unchanged. No product/test source changed
during this final run.

## Bounded Gaps and Configuration Pending

Unconfigured approved company choices are configuration pending, not absent code:
monthly cap/deficit, maintenance effect, eligible purposes, reviewer roles and
evidence; maintenance thresholds/rates/workshops and transitions; allowed prior
incident outcomes. There are no production defaults for them.

Remaining genuine unsupported flows: multiple eligible assignments on a day,
cross-midnight/multi-day monthly trip attribution, policy changes within a month,
retroactive monthly rules for old policy versions lacking configuration, and
immutable succession of existing open-ended policy versions. No guessed precedence
or backdated policy mutation was introduced. HR monthly distance is not a general
monthly fuel-payment calculation. Authenticated HTTP/SQL transaction, competing
request/deadlock behavior, client UAT and production authority acceptance remain
separately unexecuted and unauthorized here.

## Changed Files in This Slice

New application files in `src/Cosmix.Application/HR/VehicleFuel/`:
`VehicleAdjustmentPolicy.cs`, `VehicleAdjustmentContracts.cs`,
`VehicleMonthlyAdjustments.cs`, `VehicleIncidentAmendments.cs`,
`VehicleAdjustmentAuthority.cs`.

Modified in that same directory: `VehiclePolicyContracts.cs`,
`VehiclePolicyRulesCodec.cs`, `VehiclePolicyPublication.cs`,
`IVehicleFuelRepository.cs`, `VehicleFuelWorkflow.cs`, `VehicleFuelService.cs`.

Also modified: `src/Cosmix.Infrastructure/Repositories/HR/VehicleFuelRepository.cs`
and `src/Cosmix.Api/Controllers/HR/VehicleFuelControllers.cs`.

New verification/documentation: `docs/hr-srs-remediation/VerifyVehicleAdjustments.cs`
and this document. Subsequently authorized legacy test-only edits:
`VerifyVehicleEntitlements.cs`, `VerifyVehicleRepository.cs`,
`VerifyVehiclePolicyPublication.cs`, all in `docs/hr-srs-remediation/`.
No domain, shared registration, permission catalogue, DbContext, migration or
unrelated dirty file was edited.
