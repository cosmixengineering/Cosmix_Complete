# G3 Vehicle Source Integrity

Later same-day continuation: [bounded vehicle workflows](G3-Vehicle-Workflows-20261015.md)
adds configured eligibility/workshops, retained maintenance cycles and factual
incident review outcomes. The 19/21 results below remain the earlier checkpoint;
the continuation records its own current scoped verification and remaining limits.

15 October 2026. A bounded repair of existing vehicle workflows, not full G3
acceptance. No database, provider, migration, payroll or client was changed by
this verification.

## Implemented

- Trips require dated custody covering their full interval. Returned historical
  custody remains usable for its own dates; a transfer during a trip is rejected.
- Service requests require dated custody. Approved mileage is read as of the
  service date, excluding future mileage and ignoring the client's claimed total.
- Historical policy dates and the exact policy version are retained. A configured
  maintenance milestone is required; no default amount or threshold is invented.
- Fuel claims consume retained approved trip mileage, not a new calculation using
  current policy. Missing approved mileage, configured fuel price or required
  commute/map evidence blocks the relevant action with an explicit reason.
- Decisions record the actual authorized reviewer role. An employee cannot
  approve their own trip/fuel/service through another account.
- Future incident dates and liability amount/currency mismatches are rejected.
  Recording proposed incident liability is not approving a deduction or settling it.
- New mileage, fuel and service evidence uses existing protected storage and
  operation/audit/retry infrastructure. Existing historical data is not rewritten.

## Verification

The existing standalone runner executes actual services, authorization and
repository query expressions with synthetic repositories/storage/transactions.
The baseline service run passed only 5 of 19 scenarios. The final current Release
passes all 19 service scenarios and 21 repository checks.

Final retained runner:
`D:\CosmixVerification\part-c-parent-20261013\managed-94b94e35e8ba4fec812cb8461ac710f2`.

Sources: `VerifyVehicleEntitlements.cs` and `VerifyVehicleRepository.cs` in this
directory. Source copies, assembly hashes, compiler inputs and individual logs
are retained in that runner directory. This is not authenticated SQL execution,
real encrypted-file persistence, independent-connection concurrency or device UAT.

## Still Open

Full G3 requires verified staff eligibility and assignment precedence, approved
map-versus-odometer valuation, workshop authority, maintenance amounts/caps and
cycle/reset/reuse rules, incident review/outcomes and auditable later settlement.
Do not count an API/model, proposed liability or policy definition as these
completed transactions. Real HTTP/SQL and private-storage acceptance remains open.

Changed product files, below `src`:

- `Cosmix.Application/HR/VehicleFuel/VehicleFuelService.cs`
- `Cosmix.Application/HR/VehicleFuel/IVehicleFuelRepository.cs`
- `Cosmix.Infrastructure/Repositories/HR/VehicleFuelRepository.cs`

Unrelated and pre-existing changes in these files were preserved.
