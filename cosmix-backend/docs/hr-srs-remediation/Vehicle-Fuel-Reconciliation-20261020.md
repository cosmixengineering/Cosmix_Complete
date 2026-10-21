# HR fuel reconciliation after approved attribution correction

## Approved boundary

The user approved a separate HR proposal and independent Fuel review of the exact
dated-policy difference. The original approved claim, trip, mileage, approval,
receipt and amount remain immutable. No posting, settlement, refund, debt,
salary deduction or payment is created. No schema or migration is required.

This flow applies to an approved schema-1 legacy fuel claim whose original
evidence and approved dated trip correction are both retained. Schema-2 dated
claims and schema-3 corrected claims are not silently converted into this legacy
workflow. A missing original calculation or decision is an explicit conflict,
not an amount of zero. Existing pending-claim historical closure remains separate.

## Client contract

All routes use `/api/v1/hr/vehicles/fuel-claims/{id}` and existing
`attendance.vehicle.fuel.manage` permission, All scope and current web MFA.
Writes additionally require CSRF and `Idempotency-Key`.

1. GET `/reconciliation-context`: returns original claim/decision/correction
   identities, source fingerprint, latest request ID, original/corrected amounts,
   signed difference, currency and dated policy line breakdown.
2. POST `/reconciliations`: creates an immutable Pending proposal. Body:

```json
{
  "expectedLatestRequestId": null,
  "expectedSourceFingerprint": "<exact context fingerprint>",
  "evidenceReference": "<reviewed evidence reference>",
  "reason": "Review retained fuel calculation after approved trip correction"
}
```

3. POST `/reconciliations/{requestId}/review`: another authorized Fuel reviewer
   sends the existing `ReviewVehicleAdjustmentRequest` contract with the exact
   `expectedVersion`, `decision`, `evidenceReference` and `reason`. Approved,
   Returned and Rejected are supported. Reviewer cannot be the beneficiary,
   original claimant or proposal author.
4. GET `/reconciliations`: reads retained proposal/decision history.

One Pending or Approved reconciliation is permitted for the retained correction.
Returned/Rejected requests remain in history and permit a source-bound subsequent
revision. Same-key retries preserve the recorded resource and decision; changed
payloads, stale versions and conflicting predecessors are rejected.

The calculation uses the existing dated fuel policy for each segment, including
its retained rate and rounding. Callers cannot supply the difference or a payable
amount. For example, original PKR 550 and corrected PKR 700 gives a **PKR +150 HR
comparison**. A negative difference is likewise only a comparison.
`SettlementState = NotRecorded` means this workflow records no settlement; it is
not proof that Accounts never paid the original claim.

## Persistence and evidence

The existing module repository, generic approval tables, serializable
authorization transaction, encrypted private storage, audit and idempotency
journal are reused. Review loads a tracked approval aggregate for mutation;
history-list projections remain untracked. Source and current authorization are
checked after private IO, before commit and on receipt replay. Original evidence
is never rewritten to make a correction pass.

Legacy claim retry validation now checks the retained claim fields, original
mileage, policy, currency, arithmetic and approved decision. Previously, schema
1/2 replay only recognized the schema and could return a cached response after
stored claim values changed.

## Executed verification

Initial RED against frozen C04 build02: **2/19**. Four altered legacy monetary/
policy cases were incorrectly accepted, and the new reconciliation methods were
absent. RED log:
`D:/CosmixVerification/part-c-parent-20261013/managed-d775ef8e28094a8e8ed125918a90e93c/`.

Build03 initially had an anonymous-field naming compile error, then built with
zero warnings/errors. Its first reconciliation run was **28/34**: the new receipt
reader used `RequestId` instead of the operation envelope's `ResourceId`, and
several test keys were shorter than the existing required eight characters.
The invalid keys were corrected, not the production key guard.

Independent review additionally found mutation of a detached approval header.
Regression against build03 with detached history projections reproduced the
issue for Approved/Returned/Rejected; that run was **33/38**:
`D:/CosmixVerification/part-c-parent-20261013/managed-7dfe85253699406691a0557df90ab59b/`.

After tracked-header and receipt fixes, frozen
`D:/CosmixVerification/hr-backend-20261020/hr-vehicle-close-build04/bin/Cosmix.Api/release`
passed **38/38** reconciliation scenarios. Covers positive/negative differences,
immutable originals, exact retries, changed payloads, duplicate prevention, stale
versions, independent review, source loss, session/grant expiry during protected
IO, returned/rejected revisions, detached projections and strict request binding.
The same frozen output passed MVC/OpenAPI **328/328** (529 paths), vehicle
integrity repository **218/218** and vehicle repository **64/64** checks.
Both repository suites asserted zero SQL connection attempts. Logs and frozen
input hashes:
`D:/CosmixVerification/part-c-parent-20261013/managed-f9bb913c02904017b874a8ebbdb4dcf9/`.

These are real service calls with synthetic in-memory repositories/storage,
including a detached-history fixture; they are **not SQL execution**. Existing
database-target restrictions remain in force.

The standalone verifier reuses the already compiled, reviewed `DatedFixture`
without copying it. Set process-only `COSMIX_VEHICLE_FIXTURE_ASSEMBLY` and
`COSMIX_VEHICLE_FIXTURE_SHA256` before invoking `VerifyManaged.ps1`. The verified
fixture for the above run was:

- `D:/CosmixVerification/part-c-parent-20261013/managed-9a4212a10a4448fca2e57219e8b573ea/VerifyVehicleDatedAttribution.dll`
- SHA256 `5A39372F3290038F3F838290B6763F3C18417233F9E88B820F4A42344283DFAB`

The verifier rejects paths outside the verification root or a mismatching hash.
These are verification inputs, never application configuration.
# Final frozen output verification

Build06 Release passed with zero warnings/errors. Expanded fuel suite:
**42/42 PASS**, including detached history projections, tracked review writes,
receipt replay binding, original-amount drift at the final protected read,
strict JSON source binding, authority expiry and no-self-review.
Generated MVC/OpenAPI: **328/328**, 529 paths. Vehicle repository checks:
**218/218 + 64/64**, zero SQL attempts. Policy publication: **71/71**.
C04 offline guards: **256/256**; C04 itself did not execute.

Frozen output:
`D:/CosmixVerification/hr-backend-20261020/hr-vehicle-close-build06/bin/Cosmix.Api/release`.
Exact final logs and inputs:
`D:/CosmixVerification/part-c-parent-20261013/managed-a71f4319943449efb35c4c26d435fb24/`.
Runner exit zero. OpenAPI standalone compiler had two CS1701 reference warnings.
No migration, host, SQL writes, Accounts posting or external provider calls.

The first build05 policy-publication fixture run was 70/71 because its fake
repository lacked `FindVehicleAdjustmentDecisionAsync`. The fixture was extended
to return its actual retained decision; product integrity checks were preserved.
Corrected build05 and final build06 both passed 71/71. This fixture failure is
not represented as a passing run.

