# V2 Continue01: Bounded Continuation

Status: preparation only. The parent owns the single approved host/HTTP execution.
No SQL acceptance is claimed by the offline verifier.

## Identity and Inputs

- Profile: `HumaHrVehicle20261026V2Continue01`.
- New private root: `C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261026V2Continue01`.
- Existing target: `Software-PC1\LOCALDB#8C03C2BC`, PID `22112`, named pipe `np:\\.\pipe\LOCALDB#8C03C2BC\tsql\query`, `Cosmix_Testing`, exactly 26 migrations. Owner, database family, physical files, and process are pinned by the V2 target guard.
- Existing vehicle: `2349b065-e018-4a1c-9917-0808ea4e634b`.
- Pending service: `f31f46a9-3bf3-4bd4-b34e-61125bc27e11`; header: `183f96a7-b507-410e-99ad-f9e832cc3ef3`.
- Hume manifest: `D:\CosmixVerification\vehicle-v2-continuation-c537fea73e3c41b483e5d5ef4a2e064c.json`.
- Manifest SHA256: `F7E273D086484CD633B941C477F25F09F14281B861963E6908C99B0F41B98E95`.

The original V2 directory, settings, keys, results, 12 source rows, two existing receipts, and ten encrypted evidence objects are retained. The host's read-only old-file adapter accepts only the ten authenticated reference/purpose pairs in the manifest. No create or seed endpoint is used.

## Finite Contract

44 business requests, plus 24 login requests and six logout/revocation checks: expected 74 HTTP requests, hard maximum 100, three genuine local MFA sessions. OTP retrieval is synchronous and attempted once per new challenge; unexpected status stops the run. Every request must precede `2026-10-26T19:00:00Z`.

1. Two concurrent same-key approvals of the existing pending service, using separate clients/connections.
2. Three remaining pre-correction services, each submitted by HR, approved independently by Admin, and approval replayed.
3. Trip correction and fuel reconciliation contexts, Admin proposals, SuperAdmin reviews, and the original seven changed-payload/self-review/stale denial probes.
4. Four post-correction services, each submitted, independently approved, and approval replayed.
5. After all successors, replay both submission and approval for September 17 and 19, both service kinds. The original Tuning17 submission retains integer `1200` and `50`, its exact evidence text, and its original key. Historic submission responses remain `Pending`; approval replays remain `Approved`.

Expected additions: seven services, nine approval headers, ten decisions, nineteen receipts, and three each of sessions/refresh tokens/challenges/deliveries. No new vehicle, policy, trip, mileage, fuel, custody, payment, or migration. Expected new encrypted business files: 49. The original fuel amount remains 1000; independent reconciliation must report 1500 and +500 PKR, settlement `NotRecorded`.

## Preservation

Inspect rechecks all 162 table memberships and per-row hashes from the pinned manifest and the complete original V2 file inventory. Both preflight and postflight validate the exact old-root inventory, including file and directory attributes, directory membership, file lengths, and content hashes. It also checks the exact main role/assignment/All-scope bindings and current grants for HR, Admin, and SuperAdmin; other HR assignments are not removed or treated as an error.

Preflight requires zero open `WebLogin` challenges for all three actors, including expired-but-open challenges. All existing OTP rows remain fully hashed; there is no OTP-table or OTP-field exemption.

The only business-row transition is the exact pending header's `State`, `ClosedAt`, and `RowVersion`. Its other columns and every other old header remain hashed. The final guard requires the real Admin approval decision, matching closure timestamp and retained eligibility reference. Partial failure may leave that header unchanged; it may not reject, delete, or backdate it.

Existing account security fields remain protected; login counters must start and finish at zero with no lock. Existing non-exempt rate buckets remain hashed. The specific global/account rate buckets used by normal authentication are checked for bounded counter consumption or an in-run reset with unchanged window duration. These are not whole-table exclusions.

Private `start.json` retains the continuation baseline memberships, rate keys, header/rate field values, packet hash, manifest hash, and prior-file hashes. Failure and finalization errors remain separate. Acceptance is written only after the owned host stops and preservation passes. Never retry, clean, repair, rebaseline, or reuse an existing continuation root automatically.

## Parent Commands

Run the offline verifier first:

```powershell
.\docs\hr-srs-remediation\VerifyHrVehicleRunnerV2Continue01.ps1
```

Using the parent's frozen release directory, inspect without writes, then execute once with its exact returned packet hash:

```powershell
.\docs\hr-srs-remediation\Invoke-HrVehicleV2Continue01.ps1 -ReleaseDirectory $frozenRelease -Mode Inspect
.\docs\hr-srs-remediation\Invoke-HrVehicleV2Continue01.ps1 -ReleaseDirectory $frozenRelease -Mode Execute -ApprovedPacketSha256 $inspectedPacketHash
```

Vectors generated from the actual sequence: `D:\CosmixVerification\hr-vehicle-v2-continue01-request-vectors-02.json`.
SHA256: `C6A9E2F60A01B2E2F0CDE841195F42575FF1945FA5C1AED8E9224CA9162B01C0`.
The first two vector entries must be admitted before either is completed when testing concurrent admission.

## Retained Limitation

The original V2 run matched 161/162 original table hashes. Its old OTP revocation was observed, but equality of other OTP fields was not established. This continuation does not retroactively fix or certify that run. Its explicitly approved baseline is the later pinned reconciliation manifest. Live SQL/host behavior and boundary integration must be verified by the parent; offline sequence tests do not establish acceptance.
