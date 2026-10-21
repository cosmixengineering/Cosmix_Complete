# B06 model-window handoff for parent B24

Supersedes the validation-only/no-scalar checkpoint. The user explicitly opened
the A23 model window and requested real intake. Exact delta communicated in
this chat BEFORE Domain/configuration edits:

- `ZoneEvent.WrittenClaimPrivateReference`: nullable string, max 500.
- `ZoneEvent.WrittenClaimExpiresAtUtc`: nullable UTC DateTimeOffset.
- No new discriminator: existing `Source = EmployeeReport (3)` and
  `IsOffline = true` identify new metadata-only claims.
- Existing Latitude/Longitude/AccuracyMeters are already nullable. No nullable
  alteration, new entity/table/DbSet, permission, enum or zone type is required.
- Extend existing `CK_ZoneEvents_LocationRetention` with a metadata-only claim
  arm, preserving both old location arms.
- Add `CK_ZoneEvents_WrittenClaim` to require paired reference/expiry, source=3,
  offline=1, explicit captured UTC <= received, expiry > received and captured,
  and NULL GPS/location references whenever claim evidence is present. Old
  rows with BOTH claim fields NULL remain valid.
- Add a nonunique `(WrittenClaimExpiresAtUtc, Id)` index for retained metadata.

Only owned `Domain/Attendance/Zones/ZoneEvent.cs` and the existing ZoneEvent
mapping in `Infrastructure/Database/Configurations/Attendance/ZoneConfigurations.cs`
are edited. Parent remains sole B24 migration/snapshot owner; shared DbContext,
DI, Office provenance/configuration, catalog and SQL are untouched.

Claim text and full original metadata go to a NEW dedicated expiring encrypted
claim store, purpose `attendance:zone-written-claim:v1`, not the GPS vault or
ordinary audit. Ordinary audit/24-hour receipt retain only request fingerprint
and neutral result metadata. Durable allocation indexes permit orphan cleanup
after transaction rollback/crash; bounded expiry scanning and identity-bound
delete purge the claim object while immutable report/key metadata remains.

Policy defaults disabled/empty. Actual authority, notice coverage, exact maximum
delay and claim retention are explicit per original ConfigurationVersionId;
none are selected for deployment. Synthetic approved policies test true intake.
Runtime/retention wiring, one migration and SQL/mobile/notification acceptance
remain parent/deployment work, not authorization to run SQL here.

Historical duty reads use existing employee/session/latest retained revision,
accepted opening source/assessment/face proof and captured interval. Office
models/configurations remain untouched. Confirmed Operational/Red only.

Ancestor messaging is unavailable through the app send-message tool in this
native child. This live checkpoint/document and final result are the parent
handoff, not a claim of successful tool dispatch.
