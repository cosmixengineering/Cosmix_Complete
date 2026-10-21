# FaceVerification adapters

`UnconfiguredFaceVerificationProvider` deliberately cannot enroll or verify anyone until an approved
adapter replaces it. No success simulation is registered. `EncryptedFaceTemplateStore` reuses the
existing AES-GCM implementation behind a distinct interface, dedicated nonoverlapping directory and
separate keys; default configuration is unavailable and never falls back to ordinary document storage.
No keys, provider credentials or real biometric fixtures are created by these registrations.
`FaceVerificationPolicy` is explicitly configuration-bound with an unconfirmed default; registration
does not invent approved authority, thresholds, expiry/retry values or retention. The feature-specific
enrollment journal repository reuses existing audit/receipt tables and the authorization transaction
lock. Its audit-only completion path rejects business mutations before every save and before commit.

Provider/runtime/liveness/enrollment authority and retention/deletion acceptance remain outstanding.
See `docs/Attendance-Capture.md` and `docs/attendance-capture/Verification.md`.
