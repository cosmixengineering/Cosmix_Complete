# G3 bounded vehicle workflow continuation

15 October 2026. This continuation preserves the earlier custody, historical
policy, retained fuel mileage, independent-review and incident-input repairs.
It does not mark G3 or the wider HR/Attendance plan accepted.

## Implemented behavior

- Trips and maintenance require explicit, dated employee duty evidence in the
  authorized vehicle policy. Only ComplaintResolution and FieldVisits duties are
  recognized. Missing, expired, overlapping, foreign-employee or incomplete-date
  coverage fails closed. No role, staff category, job title or custody record is
  treated as proof of these duties. No population-assignment precedence is guessed.
- Independent site-to-site trip approval retains the submitted map distance and
  route evidence as the approved distance, separately from odometer observations.
  Other work purposes require an explicit distance basis. No map provider is
  called and no distance is fabricated. Fuel currency must match the configured
  price currency; approval rechecks the retained mileage and calculated amount.
- Maintenance allows only Tuning and EngineOverhaul. It requires a configured
  vehicle type, dated workshop authority, fixed amount or maximum, currency,
  milestone and cycle mode. Other costs are refused. Service odometer readings
  must fit the retained custody bounds. Future trips/services are rejected.
- A maintenance request retains the exact approved mileage membership fingerprint,
  cumulative mileage, prior approved same-kind service, consumed mileage boundary,
  next threshold, configured rule and staff evidence in protected storage. The
  caller's mileage is retained as a claim, never used as authoritative mileage.
- Approval re-reads those sources and compares the retained calculation. Another
  approval consuming the cycle makes the pending request stale. EveryThreshold
  consumes one configured threshold and preserves surplus; Once prevents a second
  approved service. These modes must be explicitly configured. Different service
  kinds have separate consumption histories. No monetary payment is recorded.
- Reusing pending/approved/returned service evidence for the same vehicle is
  refused. An ambiguous ordering of approved service timestamps fails closed.
  Unknown legacy cycle evidence, changed thresholds/cycle modes, nonzero monthly
  adjustments and excessive mileage-source volume cannot silently reset mileage.
- Incident review records one immutable outcome: ReturnedForEvidence, Rejected,
  NoLiabilityProposed, ProposedLiabilityRecorded or InsuranceReviewRequired.
  Review requires the existing incident permission, independent reviewer and an
  explicitly configured reviewer role/authority reference. Exact retries recheck
  that authority and return the same retained decision. A second decision is
  refused. Reading the outcome validates its binding to the incident and decision.
- Incident outcomes always return EmployeeDeductionAuthorized=false and
  InsuranceSettled=false. Neither an Approved review state nor a proposed amount
  authorizes employee collection, salary deduction, insurer settlement or payment.

Cycle inspection, decisions, service rows, audit and retry receipts reuse the
existing AttendanceOperation transaction. AccessManagementRepository serializes
these writes with the existing exclusive authentication application lock and a
serializable transaction. The standalone checks exercise sequential competing
proposals, not real independent SQL connections.

## Stable integration contract

No new entity, DbContext member, mapping, migration, model snapshot, permission,
service registration or startup change is needed. VehicleFuelService stays on
its existing registration and IVehicleFuelRepository stays implemented by
VehicleFuelRepository. Parent-owned shared files were not edited.

New controller routes, under the existing authorized controller:

- POST /api/v1/hr/vehicles/incidents/{id}/review
- GET /api/v1/hr/vehicles/incidents/{id}/review

POST requires Idempotency-Key and ReviewVehicleIncidentRequest, including
ExpectedApprovalRequestId, Outcome, EvidenceReference and Reason. Liability amount
and currency are accepted only for ProposedLiabilityRecorded. InsuranceReviewRequired
requires an insurance claim reference. Approval-request identity is bound into
the retry payload along with the route incident ID.

The existing authorized PolicyVersion.RulesJson retains its existing
StandardFuelKmPerLiter, ClaimFuelPricePerLiter, TuningKm and OverhaulKm properties.
It now accepts the following case-sensitive Workflow object, represented by
VehicleWorkflowPolicy. Nothing is seeded or enabled by this change.

| Workflow property | Required meaning |
| --- | --- |
| SchemaVersion | 1; unknown versions fail closed |
| StaffAssignments | EmployeeId, EffectiveFrom, exclusive EffectiveTo, Duties and EvidenceReference; maximum 1,000 entries |
| Workshops | Reference, EffectiveFrom, exclusive EffectiveTo, ServiceKinds and AuthorityReference; maximum 500 entries |
| Maintenance | At most one rule per kind, with Kind, CycleMode, AmountMode, Amount, Currency and VehicleTypes |
| OtherWorkDistanceBasis | ApprovedMapDistance or Odometer; required for non-commute/non-site-to-site work |
| IncidentReview | Exact RoleCodes and AuthorityReference; required for incident decisions |
| FuelCurrency | Uppercase three-letter configured price currency; required for fuel claims |

Maintenance Kind uses the existing numeric enum values: Tuning=1 and
EngineOverhaul=2. CycleMode is EveryThreshold or Once; AmountMode is Fixed or
Maximum. Staff Duties uses ComplaintResolution and/or FieldVisits. Unknown
Workflow properties and duplicate JSON properties are rejected. Missing sections
block only their dependent actions. The tests' financial values and identities
are synthetic; they are not proposed company settings.

Operational dependency for the policy owner: publish/provision actual authorized
vehicle policy versions containing these facts through an approved configuration
workflow. This slice does not add a policy publisher or authorize fixture/database
insertion. Existing policy versions without Workflow cannot grant new vehicle
entitlements. Historical values and records are not backfilled or rewritten.

## Executed verification

Command, from the existing checkout:

```powershell
& ./docs/hr-srs-remediation/VerifyG3Scoped.ps1
```

Final result: 46/46 service scenarios and 35/35 repository checks passed, zero SQL
connection attempts. Actual changed Application, repository and controller sources
were compiled into standalone executables against copies of existing dependency
assemblies. The original 19 service scenarios remain present and pass. Checks cover
dated eligibility, workshop authority, amount bounds, source changes, cycle reuse,
surplus, Once mode, policy transitions, historical evidence, map distance, currency,
independent incident review, outcomes, authority replay and query translation.

Retained output, relative to this document:
`artifacts/g3-scoped-aff551c8e74a4acab12533102ad32135`.
It contains exact source/dependency copies, SHA256 input manifest, compiler response
files and logs. Compiler shadow-type warning CS0436 is suppressed only by this
scoped harness because its source types replace corresponding baseline types.
This is not a fresh integrated product build. No shared bin/obj was written.

Earlier harness-only failures are retained: the first lacked the web SDK's implicit
namespace import; the second selected the generic SqlClient assembly and failed
offline SQL translation. The final runner selects the Windows runtime dependency,
compiles the controller as well, and passes both suites.

Parent's exact commands after the parent-owned integrated Release build:

```powershell
& ./docs/attendance-gap-c/VerifyManaged.ps1 -ReleaseDirectory $ParentReleaseDirectory -Sources @(
  'docs/hr-srs-remediation/VerifyVehicleEntitlements.cs',
  'docs/hr-srs-remediation/VerifyVehicleRepository.cs'
)
```

ParentReleaseDirectory must be the absolute, frozen API Release directory on D
produced by the parent's build, as required by the existing runner. The scoped
runner also accepts an explicit -ReleaseDirectory for checking against that build.

## Remaining boundaries

- Full solution build, integrated endpoint/OpenAPI verification, authenticated HTTP,
  SQL persistence/rollback, independent-connection races and real protected-storage
  failure/restart checks remain for the parent and explicitly authorized acceptance.
- Real duty evidence, workshop authority, configured company amounts/currency and
  incident-review authority are required. Recording references is not external
  verification of their authenticity or legal authorization.
- Population assignment precedence, monthly nearer-assignment adjustments, legacy
  cycle reconciliation, transitions to different mileage thresholds/modes, amended
  or resubmitted incident reviews and financial settlement remain unimplemented.
  Completed incident reviews are immutable; non-pending review attempts return 409.
- The source freeze was obeyed with no edits or builds until released. Subsequent
  G3 edits invalidate historical A02 source hashes as expected. The A02 packet was
  not regenerated. No A02 execution, SQL/private-store writes, host or real provider
  call was performed by this scope.

## Changed paths

- src/Cosmix.Application/HR/VehicleFuel/IVehicleFuelRepository.cs
- src/Cosmix.Application/HR/VehicleFuel/VehicleFuelService.cs
- src/Cosmix.Application/HR/VehicleFuel/VehicleFuelWorkflow.cs (new)
- src/Cosmix.Application/HR/VehicleFuel/VehicleWorkflowPolicy.cs (new)
- src/Cosmix.Application/HR/VehicleFuel/VehicleWorkflowContracts.cs (new)
- src/Cosmix.Infrastructure/Repositories/HR/VehicleFuelRepository.cs
- src/Cosmix.Api/Controllers/HR/VehicleFuelControllers.cs
- docs/hr-srs-remediation/VerifyVehicleEntitlements.cs
- docs/hr-srs-remediation/VerifyVehicleRepository.cs
- docs/hr-srs-remediation/VerifyG3Scoped.ps1 (new)
- docs/hr-srs-remediation/G3-Vehicle-Workflows-20261015.md (new)
- docs/hr-srs-remediation/G3-Vehicle-Source-Integrity-20261015.md (continuation link)

All earlier dirty changes were preserved. No Git write, branch, worktree or agent
was created. Generated standalone artifacts remain in this scope's ignored
artifacts directory.
