# Separate HR biometric registration

The requested HR workflow is a separate employee-search and camera page, not a
hiring wizard step. Existing hiring approval remains with Admin. Current backend
enrollment requires an active approved employee, active account, a verified
employee-owned registered mobile device, web MFA, scoped face-management access,
and configured biometric authority/provider/private storage. Those rules remain.

Implementation: `/hr/biometrics` in HR navigation; search real employees, select
one, load current enrollment metadata and eligible devices, capture from browser
camera with explicit notice, upload to the existing protected capture API, then
submit its session-bound token to the existing enrollment API. Display success
only after a validated active enrollment response for that employee. Replacements
carry expected enrollment ID/version. Keep images in memory, stop media tracks
on exit, and preserve unchanged retry keys on uncertain writes. No file upload,
automatic hiring approval, biometric-success simulation or stored browser image.

Backend adds permission-scoped reads of eligible device metadata and the current
active enrollment for the selected employee. No schema migration, account/device provisioning or provider
activation is part of the page. Enrollment is not a successful Check-In: mobile
capture, liveness/match, challenge consumption and geofence checks remain separate.

Verification: frontend lint/format/type/build; backend Release and existing face
regressions; inspect registered navigation and camera/error/empty states where
the running application permits. Real biometric acceptance requires a consenting
employee and working device/provider; do not claim it from UI/build checks.

## Executed verification and limits

- Frontend full `npm run verify` passed (lint, formatting, TypeScript and Vite).
  After the terminal-provider-error retry refinement, affected lint/format and
  production build were run again. The existing large-bundle warning remains.
- Backend Release build passed with zero warnings/errors. Existing
  `docs/attendance-capture/VerifyCaptureService.cs`: 223 checks passed, including
  scoped device/current-enrollment reads and denial cases; no real provider or
  SQL writes by that verifier.
- Browser: normal synthetic HR sign-in and local test MFA succeeded. Sidebar
  navigation opened `/hr/biometrics` and the separate search/camera-selection
  layout rendered. Employee search failed in both this page and the existing
  Employee Directory during the live check. No employee enrollment was submitted,
  camera permission requested, face image captured or real biometric stored.
- The running API was not restarted with the new read endpoints. A current API
  restart and successful directory/device reads are required before full live UI
  acceptance. Camera hardware, provider liveness/enrollment and real mobile
  Check-In acceptance remain unverified.

No provider activation, permissions grant, migration, business fixture changes
or automatic hiring approval was performed. Existing October repair-plan work
was not executed as part of this UI change.
