# Live face and external acceptance checks — 28 October 2026

This follows the local payroll verification report. The user excluded three-phone testing,
confirmed no SMS provider is available, nominated Admin/SuperAdmin for final acceptance
review, and explicitly consented to using their own face with the laptop camera.

## Checks performed

- Actual Ubuntu CompreFace service: 16/16 live diagnostic checks passed, including pinned-CA
  transport, valid detection/verification credentials, no-face rejection, missing/wrong-key
  rejection, blocked raw-core inference, image validation and configured encrypted vault.
- All five Docker services running; core and database healthy, core OOM false and restart
  count zero. Both server ONNX files match their approved hashes. Actual Linux CPU inference
  ran for both models with synthetic tensors (approximately 9.97 ms and 6.48 ms mean). This
  tensor diagnostic is not live-person evidence.
- HP HD Camera captured 640×480 live frames with operator consent. The diagnostic uses the
  production CompreFace client/TLS and SilentFace model implementation; it writes no enrollment
  or attendance and keeps image buffers in memory, clearing them after the run.
- First attempt returned `face_not_detected`. A subsequent attempt detected both faces but
  returned `face_provider_timeout` during the liveness/match stage. No accepted attendance was
  created. Server resource checks did not show an OOM/restart. Root cause of that one timeout
  is not established; do not label it fixed by later successful requests.
- After the operator confirmed ready/framing, both live frames passed both liveness models
  and matched at 0.99989 (configured similarity requirement 0.85). Detection took 0.71/0.51 s,
  local liveness 45/9 ms and matching including independent detection 1.74 s. This diagnostic
  run temporarily used a 30-second request timeout in memory only.
- A separate repeat with the original persisted 10-second timeout also passed both liveness
  checks and matching, similarity 0.999, match stage 1.66 s. No deployed timeout, face score
  threshold, authority, enrollment or payroll record was changed.

These results establish live-camera/component success for one consenting operator, not an
employee-bound mobile attendance punch, a deployment-wide false-accept rate, a spoof benchmark
or production acceptance. Screen-photo/video and printed-photo checks remain separately
recorded scenarios. The screen-replay check has been requested from the operator.

## External inputs still required

- Production SMS: user explicitly confirmed there is still no provider. No vendor account,
  paid service, credentials or delivery receipt was invented; no real SMS sent.
- Bank/historical comparison: the two supplied HBL file hashes still match the previously
  inspected blank-amount templates. No completed actual month or bank-result file was found
  in the top level of Downloads/Documents/Desktop. Bank upload/transfer was not performed.
  HBL's official indexed FAQ identifies its business portal as
  `https://businessbanking.hblpay.com/fcmclient/`; that public URL does not supply corporate
  login, an approved payable run or bank acceptance. Source:
  https://www.hbl.com/app-html-2162022/HBL-Pay-Business-Banking-FAQs.pdf
  (search excerpt available; direct PDF fetch returned 403).
- Final company review: Admin/SuperAdmin is the role specified by the user. A role name is
  not a recorded human sign-off. Existing payroll/payment maker-reviewer separation remains;
  no approval was fabricated or independent-review rule bypassed.

Evidence: `D:/CosmixPayrollVerification20261028/live-acceptance/face-live.log`,
`server-check.log`, `model-inference.log`, `camera-live.log`, `camera-preview-test.log`,
`camera-timings.log`, `camera-ready-test.log`, `camera-default-timeout.log`, `server-load.log`
and `readiness.json`. No raw camera images were saved by the diagnostic.
