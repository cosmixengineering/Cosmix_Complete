# V1 Vehicle/Custody Acceptance - 20 October 2026

## Actual Outcome

**The bounded run stopped at its final evidence assertion. It is not an overall
V1 PASS.** All 27 preceding checks passed over 36 actual authenticated HTTP
requests, including all 18 planned business requests. A subsequent read-only
diagnostic verified the persisted business state, three operation-linked
receipts/audits and six authenticated encrypted objects. No operational request
was repeated after the stop.

The stopped run did not reach logout/revoked-cookie tests. C04's earlier passing
logout tests remain separate evidence, not a substitute for completion of V1.
No migrations, role/grant changes, rates, policies, trips, fuel, payments, real
SMS or face calls were performed.

## Reviewed Target

- Server: `Software-PC1\LOCALDB#9F3001F7`, SQL PID `25768`.
- Database: `Cosmix_Testing`, ONLINE, MULTI_USER, same approved family
  `D9C34627-18A2-41CF-A804-821E39516303`.
- Owner/operator: `COSMIXRND\Huma.Khan`.
- Existing MDF/LDF: `C:\Users\Huma.Khan\Cosmix_Testing.mdf` and
  `C:\Users\Huma.Khan\Cosmix_Testing_log.ldf`.
- Exactly 25 applied migrations; none applied or changed.
- Existing synthetic HR/Admin identities only; no new accounts.
- Owned host: `https://localhost:7481`. The approved local-only named-pipe
  encryption exception was used; this is not production TLS acceptance.
- Private output:
  `C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261020V1\`.

Inspect and Execute independently matched packet
`A05A7750EAC6E2C0DB81E2E37B96FCD6CCC2301C2D2F586E03D9E4CF73BF921F`.
Fixture SHA256:
`F14B5874E45F1EF63D7E2C4622B57B5108F0ADDB13A2AA266350D3A9025F1A40`.
The new private root was absent and port 7481 free before execution.

## Passed Live Behavior

- Both synthetic actors: anonymous and pre-MFA denial, password challenge,
  actual local OTP/MFA session and authenticated permissions.
- Missing-CSRF vehicle create rejected.
- Two separate concurrent same-key create requests returned the same vehicle.
  A changed payload with that key returned conflict.
- One custody issue, exact retry without duplication, and conflicting new-key
  issue rejected.
- HR self history returned its own custody; Admin self history excluded it.
- One custody return, exact retry without duplication, and conflicting new-key
  return rejected.
- Final management and self history retained the same returned custody.
  The vehicle remained a retained active asset.

Retained synthetic identities:

| Item | Value |
| --- | --- |
| Registration | `TEST-HR-V1-20261020` |
| Vehicle ID | `c1f97788-0584-40aa-bec2-bce2c94e61bb` |
| Custody ID | `b9f26448-7b5f-4c5b-bc8c-916f0fec4dd2` |
| Issue / return | 2026-10-20 07:00 / 07:05 UTC |
| Odometer | 1000 at issue and return; no travel claimed |

Condition references are explicitly synthetic strings, not uploaded or verified
condition documents. The six encrypted objects below are operation result/audit
evidence, not condition attachments.

## Why the Runner Stopped

The runner's audit join incorrectly required
`a.CorrelationId LIKE 'HR-V1-%'`, assuming the request's arbitrary
`X-Correlation-ID` header was the persisted correlation value.
`VehicleFuelControllers.Context()` passes `HttpContext.TraceIdentifier` to the
application, and `AttendanceOperation.Audit()` persists that **server-generated**
identifier. The correct three audit records exist, linked to the exact actor,
operator session, operation and resource, but the incorrect prefix filter
selected zero records.

Read-only investigation also found a second reader defect that the stopped
runner had not yet reached: encrypted `AttendanceReceipt.Result` DTOs use
PascalCase .NET serialization, while the HTTP response assertions use camelCase.
PowerShell 7.6 JSON hashtables preserve case-sensitive keys, so `.id`/`.isActive`
do not read the stored `Id`/`IsActive` members. This is a test-reader mismatch,
not evidence that the persisted vehicle fields are missing.

The executed runner/helper were left unchanged. No filter was weakened and no
failed run was retroactively marked passed.

## Post-Stop Read-Only Verification

[Inspect-HrVehicleV1Evidence-20261020.ps1](Inspect-HrVehicleV1Evidence-20261020.ps1)
passed separately, with no HTTP, host startup, SQL mutations or private-file
writes. It rechecked the pinned server/database/owner/family/PID/files and
migration count, then verified:

- Exact vehicle/custody values, actors, dates, odometers and population.
- Exactly three completed operation receipts and three corresponding original
  audit records, bound to the actual operator session and resource identities.
- Distinct retained server trace identifiers, without treating caller headers
  as authoritative audit identity.
- Six distinct AES-GCM objects: key reference, purpose/AAD, object identity,
  envelope length, authentication tag and plaintext SHA256.
- Protected resource/scope identity, original request digest against the receipt,
  and equality of protected audit `After` with the protected replay result.
- PascalCase persisted DTO values, including immutable issue evidence and the
  returned-custody state.

This supports the business persistence/evidence claims above. It does not
provide missing logout coverage, host-restart acceptance, controlled storage
failure testing or broader vehicle/fuel/maintenance acceptance.

## Counts and Final State

| Record | Before | After |
| --- | ---: | ---: |
| Accounts | 3 | 3 |
| Migrations | 25 | 25 |
| Sessions | 126 | 128 |
| Refresh tokens | 187 | 189 |
| OTP challenges | 132 | 134 |
| OTP deliveries | 132 | 134 |
| Rate buckets | 69 | 81 |
| Audits | 2558 | 2582 |

Business delta: exactly one vehicle and one returned custody, with three
completed business receipts. **155 protected database snapshots and all 33
retained C01-C04 files remained unchanged**, including the post-host check.

Owned host PID `29672` exited; port 7481 had zero listeners. SQL remained ONLINE
and was not stopped. No cleanup, reset, deletion or repair was attempted.
The two V1 auth sessions were not revoked because the run stopped before
logout; read-only inspection found their recorded expiries at approximately
16:52:41-42 UTC on 20 October. No cookies, passwords or key bytes are exposed here.
Do not rerun the fresh-only V1 runner against its now-existing fixture/root.

Private redacted evidence remains in `results/start.json`, `failure.json`,
`host-final.json` and `post-host-preservation.json`. There is deliberately no
successful `acceptance.json`.

## Build and Scope Limits

Frozen build09 Release passed with zero warnings/errors and 1,157 unchanged
source hashes during compilation. V1 profile/boundary offline checks passed
320; runner parser/manifest/allowlist/budget/sequence checks passed 86.
These offline checks did not catch the two evidence-reader assumptions above.
A separate corrected pure assertion helper,
[HrVehicleV1EvidenceAssertions.ps1](HrVehicleV1EvidenceAssertions.ps1), now has
**75 passing offline regression checks** in
[VerifyHrVehicleEvidenceReaderV1.ps1](VerifyHrVehicleEvidenceReaderV1.ps1).
The tests use actual frozen build09 DTO/receipt types and System.Text.Json,
including PascalCase versus camelCase, duplicate/case-alias rejection, exact
request bytes, actor/session/resource/scope and server-trace preservation.
The parent independently reran all 75 checks, then used this same helper in the
read-only diagnostic: all three persisted operations passed again.
The original stopped runner remains unchanged; no successful live rerun is
implied by the corrected diagnostic.

Product vehicle regressions passed 340/340; fuel reconciliation 42/42 and
MVC/OpenAPI 328/328 also passed against build09. See
[historical vehicle evidence and restrictions](Vehicle-Remaining-Decision-Matrix-20261020.md).

Full HR production acceptance remains unclaimed: benefit permission provisioning,
benefit/recovery/outstation business fixture acceptance, report-worker SQL
acceptance and broader vehicle historical monetary/maintenance SQL scenarios are
not established by this asset/custody batch. Real face, external OTP and legal
activation remain explicitly excluded.

## Changed Files for Owner Integration

| Area | Files |
| --- | --- |
| Historical vehicle product fixes | `src/Cosmix.Application/HR/VehicleFuel/VehicleConsumedMaintenance.cs`; private proof call sites in `VehicleFuelWorkflow.cs` |
| Separate V1 test host | `src/Cosmix.Api/Development/HrVehicleTestingGuard.cs`, `HrVehicleTestingBoundary.cs`, `HrVehicleTestingDatabase.cs`; minimal selection/wiring in `HrBusinessTestingGuard.cs`, `HrBusinessTestingBoundary.cs`, `HrBusinessTestingDatabase.cs`, `LocalTesting.cs`, `src/Cosmix.Api/Program.cs` |
| Frozen V1 execution sources | `Invoke-HrVehicleV1.ps1`, `HrVehicleV1RunnerFunctions.ps1`, template and reviewed `HrVehicleV1Manifest` JSON files in this directory |
| V1 offline guards | `VerifyHrVehicleTestingV1.cs`, `VerifyHrVehicleRunnerV1.ps1` |
| Corrected read-only evidence verification | `HrVehicleV1EvidenceAssertions.ps1`, `VerifyHrVehicleEvidenceReaderV1.ps1`, `Inspect-HrVehicleV1Evidence-20261020.ps1` |
| Vehicle regression evidence | `VerifyVehicleRemainingBoundaries.cs`, `Vehicle-Remaining-Decision-Matrix-20261020.md` |
| Current status | This report, `HR-Continuation-20261020.md`, `HR-Business-Acceptance-Matrix-20261020.md`, `../SRS-Module-Map.md` |

The checkout contains other pre-existing changes. This list is not permission
to revert or overwrite them. No commit, push, merge, branch or worktree was made.
