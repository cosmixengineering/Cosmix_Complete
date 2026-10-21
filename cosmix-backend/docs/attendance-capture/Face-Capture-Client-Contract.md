# Bound camera capture client contract

Backend contract only. No React/React Native implementation or real device test is included.

The mobile app calls the ERP backend. CompreFace service credentials and ONNX model files
remain on the server; the app never calls CompreFace directly. The current local Swagger is
`https://localhost:7240/swagger/index.html`. On a physical phone, `localhost` is the phone,
so a reachable backend HTTPS address and trusted certificate are still needed for device UAT.

For the configured self-hosted provider, server-approved storage-use boundaries apply independently
of capture-token expiry. A token's expiry is not a guarantee that configuration, authority, challenge
or storage-use permission remains valid. Expired/retired/untracked storage denies release; the
client must handle explicit unavailable/expiry responses. No client may set a retention boundary,
adopt an old storage object or request deletion. See [lifecycle](Face-Storage-Lifecycle.md).
Provider selection is explicit configuration in every environment and remains disabled by default.
Real employee uploads require approved biometric authority, retention/deletion and device/infrastructure acceptance.

## Enrollment (authorized web operator)

Use the existing current web MFA session and `X-CSRF-TOKEN`, with explicit scoped
`attendance.face.manage`. The registered device must already belong to the target employee.

1. POST `/api/v1/hr/employees/{employeeId}/face-enrollments/captures` with a new
   `Idempotency-Key`, `registeredDeviceId`, `imageBase64` and `reason`.
2. Copy only the returned `providerEvidenceToken` string, not the entire response JSON.
3. POST the existing `/face-enrollments` route with that token as `providerEvidenceToken`,
   the same registered device, the current `expectedActiveEnrollmentId`/`expectedActiveVersion`
   (explicit null for first enrollment), reason and a different enrollment-operation retry key.
4. Server detection/liveness must succeed and existing authorization/version checks must pass.
   The capture-upload response is not successful enrollment or attendance.

The token is bound to this operator's account/session and target employee/device. A changed
web session requires a new capture; it is not a transferable provider credential.

## Check In (registered mobile session)

Use the existing current mobile bearer session and the employee's registered device.

1. POST `/api/v1/attendance/face/challenges` with an explicit future punch idempotency key.
2. POST `/api/v1/attendance/face/challenges/{id}/captures` using a new capture key and:

```json
{
  "challenge": "<exact server challenge>",
  "expectedVersion": "<challenge version>",
  "imageBase64": "<camera JPEG/PNG bytes encoded as base64>"
}
```

3. POST the existing challenge `/verify` endpoint with the same challenge/version and the
   returned capture token. Identity and liveness are computed on the server; no booleans,
   employee ID, provider URL, file path or client face score are accepted.
4. Only a current passed result may be submitted to existing `/api/v1/me/attendance/check-in`,
   using the originally bound punch key, attempt ID and challenge secret. Scheduling, geofence,
   lock, current authority/device/enrollment and single-use proof checks still apply.

Uploads accept only PNG/JPEG, at most 512 KiB decoded bytes, no data-URI prefix or whitespace.
The raw JSON limit is 710,000 bytes. Duplicate/unknown/case-aliased members are rejected.
Valid image decoding and exactly-one-face checks belong to the provider too; an upload token
does not certify valid/live pixels. Use the returned exact expiry, which is clamped to the
challenge expiry and configured capture lifetime (maximum five minutes).
Canonicalize camera orientation before encoding: the configured adapter rejects EXIF APP1
JPEG and EXIF/animated/transparent/interlaced PNG. Use three-component JPEG or opaque,
noninterlaced 8-bit RGB PNG. Do not send a raw mobile HEIC image.

## Retry And Failure Handling

Keep the exact key/body for an uncertain upload. The original token/expiry is retained, not
extended; replay cannot allocate another image. A capture can be claimed only for its original
verification or server enrollment operation. Another operation conflicts. Before releasing
bytes, the reader repeats current authorization and challenge/enrollment checks transactionally.
Expired/completed/revoked challenges, disabled capture configuration and stale versions fail.
An expired token needs a new valid capture; do not repeatedly change keys to hide a conflict.
Verification retries preserve the recorded outcome, reason, expiry and version, while
`canSubmitCheckIn` is recomputed from the current authorized session/device, unexpired and
unconsumed proof, active compatible enrollment, policy, provider and storage configuration.
A historical `Passed` outcome alone does not authorize another Check In.
An ordinary no-face, multiple-face or low-confidence detection is a failed verification,
not a provider outage. A failed completed challenge needs a fresh challenge/capture within
the configured retry limit. Malformed provider replies, credentials and transport failures
remain unavailable. Handle `face_evidence_use_expired` explicitly: configuration capability
does not inspect an active enrollment object's remaining storage-use lifetime; re-enrollment
may be required. Avoid parallel verify submissions while a previous request is unresolved.

Missing model/provider/storage/policy configuration is explicit unavailability, never a successful
face result. Template/capture contents, private storage locators and thresholds are not returned.
Durable retirement can close storage use after a committed terminal operation. Approved physical
disposal, orphan cleanup, backup/key retention and deployment acceptance remain separate prerequisites.

Check Out uses the original geofenced endpoint and **no face capture, challenge or proof**.
