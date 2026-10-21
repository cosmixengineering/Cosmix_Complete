# B06 actual offline employee observations

Confirmed human scope is **Operational/Red only**. Earlier Green/Amber/custom
ideas are superseded, not new zone types. No old/offline observation is live
tracking, verified entry/exit, an access grant, a punch or payroll evidence.

## Intake and recovery

`POST /api/v1/me/attendance/zones/monitoring-windows/{windowId}/offline-observations`
uses a permanent exact-case `Idempotency-Key` matching `[A-Za-z0-9_.:-]{8,100}`.
Body: original assignment/configuration/session IDs, exact retained notice SHA256,
UTC captured time, eventKindHint, writtenClaim (1-1000 characters). Unknown
properties, GPS, employee/account/device selectors and private references are
rejected. Current own permission and current verified mobile session/device
are required; authority comes from the server, never body metadata.

The original server-created notice/window/device must match. Capture is bounded
by registration/verification, notice, original window, exact configuration and
assignment, accepted check-in and latest retained employee/session WorkSession
revision. Accepted closure after capture is still respected. Site membership,
authority, original notice and configuration are resolved historically; future
capture, retrospective notice, expired assignment and conflicting overrides fail
closed. Existing live samples remain strictly live-only.

Explicit server `ZoneOfflineObservationOptions` policy is keyed to the original
ConfigurationVersionId, approved before notice, with authority evidence,
MaximumDelaySeconds and ReportRetentionDays. Defaults disabled/empty return
503 `zone_offline_policy_unavailable`; no deployment values/graces are selected.
Configured approved policy and claim storage append a real metadata-only event
and return 200 `evidenceStatus=unverified_employee_report`. Claim expiry is
capturedAt + explicit retention, not receivedAt + invented grace. Permission,
session/device, elapsed delay and retention are rechecked after storage I/O.
Authoritative grant intervals must be known and IsEffectiveAt the last clock
read after current-authority I/O; initial permission is not a permanent permit.

The encrypted claim includes full forensic bindings and policy. Ordinary
audit/24-hour receipt hold a request fingerprint and neutral result metadata,
not written text, GPS or private references. Event/receipt/audit commit under
the existing authorization transaction. Failed storage/current-auth commits no
event/receipt/audit; durable private allocation indexes support orphan cleanup.
There are no checkpoint/window/punch/salary mutations or new notifications.

Exact same-body/device/key replay uses the retained receipt and current auth.
Changed payload/device returns 409 `idempotency_key_reused`. Expired receipt
returns 409 `idempotency_key_expired`; absent receipt with permanent event key
returns 409 `zone_observation_already_received`. Neither permits a fresh key.

`GET .../offline-observations/reconciliation`, original key in header, reads
server employee/account/original-window/offline-source scoped metadata only.
It returns current retained report/responses, explicitly NOT the frozen original
receipt. Found=false is not resubmission authority; CanResubmit always false.
A new currently verified own device can read metadata but cannot automatically
submit the original claim. No claim text/private paths/keys/GPS are returned.
Expiry of claim text never deletes permanent report/key metadata.

## Existing response pipeline and storage

Own history/acknowledgement/dispute and factual review reuse existing endpoints.
Offline Kind is null, EventKindHint is the employee belief, EvidenceStatus stays
unverified even after review. Reviewer authority checks BOTH captured-time and
current scope; confidential ReasonCode/private restriction evidence is absent.

`IZoneClaimStore` uses isolated authenticated encryption purpose
`attendance:zone-written-claim:v1`, never the GPS vault. Separate provisioned
objects/expiry-index directories may not overlap ordinary, face or GPS storage.
Read binds event/employee/window/expiry and rejects expiry before and after I/O.
`ZoneClaimRetentionService.PurgeAsync(cursor, maximumEntries, ct)` performs bounded
1-500 entry scans and identity/purpose/expiry-bound deletes, including failed or
rolled-back allocations. Cross-process leases protect allocation/purge. Corrupt
objects/indexes fail closed for operator reconciliation. Physical deletion is
only at explicit approved expiry; no deployment deletion/hold policy is invented.
Refused deletion is a failed purge, never healthy completion. The real scheduled
worker uses the directly checked bounded pass: at most ten 100-entry pages,
cursor continuation, clean-complete health, failure/cancellation clearing health
and expired-cursor restart. Health freshness is infrastructure liveness, not a
report-retention grace or authority.

## Parent handoff and acceptance

EF delta is fixed: two nullable ZoneEvent fields WrittenClaimPrivateReference
(max500), WrittenClaimExpiresAtUtc; WrittenClaim CK; LocationRetention claim arm;
nonunique `(WrittenClaimExpiresAtUtc, Id)` index. Coordinates already nullable.
No entity/DbSet/shared config change. See B06SchemaProposal for root-owned B24.

Parent owns migration/snapshot/DbContext/core DI. Existing feature registration
entrypoints now wire ZoneOfflineObservationService, bound options, singleton
claim store, scoped cleanup and ZoneClaimRetentionWorker. The worker starts
only for configured enabled storage/intake and publishes independent cleanup
health after a successful bounded sweep; unavailable health blocks intake.
Approve actual authority/notice/delay/retention/deletion/hold policy before enable.
No shared DI, SQL, host execution, provider, catalog or Git was changed/run.

Standalone checks are integrated into B06Verify.ps1: existing VerifyZones and
VerifyZoneService plus B06VerifyPolicy, B06VerifyRegisteredContracts,
B06VerifyOffline, B06VerifyClaimRetention and B06VerifyClaimStorage. Registered
MVC/OpenAPI generation and actual feature/controller DI are verified without
starting a host. The parent VerifyRegisteredContracts.cs is untouched.

Parent reports B24 clone/original application, preserved old-column hashes/counts
and idempotent rerun verified. Those are root-owned checks, not child executions.
Actual claim transaction/constraint/concurrency/rollback acceptance remains
unverified here; so do real host startup and HTTP authorization; mobile original
notice/offline/retry/reconciliation UI; scheduled lifecycle/holds on deployed
storage; and existing notification recipient/non-regression acceptance.
Restart/orphan tests use private allocations without SQL rows, a rollback-
equivalent fixture, not a claimed real database transaction rollback.

## Deployment keys and approval

No deployment settings were edited or activated. Defaults disabled/empty:

- `ZoneOfflineObservations:Enabled`
- `ZoneOfflineObservations:Policies:<n>:ConfigurationVersionId`
- `ZoneOfflineObservations:Policies:<n>:ApprovedAtUtc`
- `ZoneOfflineObservations:Policies:<n>:AuthorityEvidenceReference`
- `ZoneOfflineObservations:Policies:<n>:MaximumDelaySeconds`
- `ZoneOfflineObservations:Policies:<n>:ReportRetentionDays`
- `ZoneClaimStorage:Enabled`
- `ZoneClaimStorage:Storage:Directory` (separately provisioned objects/expiry-index)
- `ZoneClaimStorage:Storage:KeyFile`, `KeyReference`, `MaxSizeBytes`
- `ZoneClaimStorage:Storage:DecryptionKeyFiles:<reference>` for approved rotation

Existing PrivateFiles, FaceTemplateStorage and ZoneLocationStorage roots are
used only to reject overlap; claim text never uses those storage purposes.
Approve actual authority, retained-notice coverage, exact delay, retention,
deletion/hold treatment, provisioned private storage/key ACL and operational
cleanup before activation. SYNTHETIC checks explicitly use 600 seconds/1 day;
those values are NOT deployment choices or company defaults.

## Exact B06 files

Runtime source (only the first two are the already-frozen EF delta):

- src/Cosmix.Domain/Attendance/Zones/ZoneEvent.cs
- src/Cosmix.Infrastructure/Database/Configurations/Attendance/ZoneConfigurations.cs
- src/Cosmix.Application/Attendance/Zones/ZoneContracts.cs
- src/Cosmix.Application/Attendance/Zones/ZoneAdministrationService.cs
- src/Cosmix.Application/Attendance/Zones/ZoneMonitoringService.cs
- src/Cosmix.Application/Attendance/Zones/ZoneOfflineObservationContracts.cs
- src/Cosmix.Application/Attendance/Zones/ZoneOfflineObservationService.cs
- src/Cosmix.Application/Attendance/Zones/ZoneClaimStorage.cs
- src/Cosmix.Application/Attendance/Zones/ZoneRegistration.cs
- src/Cosmix.Infrastructure/Repositories/Attendance/ZoneRepository.cs
- src/Cosmix.Infrastructure/Zones/EncryptedZoneClaimStore.cs
- src/Cosmix.Infrastructure/Zones/ZoneClaimExpiryIndex.cs
- src/Cosmix.Infrastructure/Zones/ZoneClaimRetentionWorker.cs
- src/Cosmix.Infrastructure/Zones/ZoneInfrastructureRegistration.cs
- src/Cosmix.Api/Controllers/Attendance/ZoneOfflineObservationsController.cs

Docs/checks:

- docs/attendance-completion/VerifyZoneService.cs
- docs/attendance-gap-b/B06SchemaProposal.md
- docs/attendance-gap-b/B06ConsumerContract.md
- docs/attendance-gap-b/B06Contracts.http
- docs/attendance-gap-b/B06Verify.ps1
- docs/attendance-gap-b/B06VerifyOffline.cs
- docs/attendance-gap-b/B06VerifyClaimStorage.cs
- docs/attendance-gap-b/B06VerifyPolicy.cs
- docs/attendance-gap-b/B06VerifyClaimRetention.cs
- docs/attendance-gap-b/B06VerifyRegisteredContracts.cs

No Domain/mapping changes were made after model FREEZE. No shared core DI,
DbContext, migration/snapshot, catalog, Auth, HR, Face or parent checks were edited.

## Red/green evidence

Observed red before source: missing report factory/store/reconciliation;
missing feature registrations/cleanup worker; grant expiry after private I/O;
Unicode JSON payload bound; and refused object deletion falsely completing purge.
Minimal owned source fixes are green. Nullable enum OpenAPI is correctly
oneOf(null, enum); only the test traversal was corrected, not a shared schema.
No warning suppression was used for the final source/checks.

Final combined runner exited 0: 403 checks, with rules16, existing service61, policy6,
registered DI/MVC/OpenAPI105 (348 discovered paths), workflow/API/SQL153,
worker18, encrypted storage44. All synthetic, with zero real SQL connections,
host starts, mobile logins, provider calls or deployment policy activation.
The full Release solution build passed with warnings-as-errors enabled, zero
warnings and zero errors. The runner supports an opt-in solution-only artifact
cache; every file-based check has its own output identity. Sharing their cache
was observed to select a stale entrypoint and was corrected before this run.

Executed from the repository root:

```powershell
pwsh -NoProfile -File docs/attendance-gap-b/B06Verify.ps1 `
  -ArtifactsPath 'D:\Cosmix-B06-TEMP-8f0925a0b4da47569315fecd1ec03651\final-evidence' `
  -ProjectArtifactsPath 'D:\Cosmix-B06-TEMP-8f0925a0b4da47569315fecd1ec03651\registered-current'
```

Logs: release-build.log, rules.log, live-service.log, policy-binding.log,
registered-contracts.log, offline-api-queries.log, claim-retention.log and
claim-storage.log in that final-evidence directory. Registered-contracts.log
contains the actual 105-check OpenAPI pass, not the earlier stale policy
entrypoint. Passing synthetic checks is not a claim of live acceptance.
