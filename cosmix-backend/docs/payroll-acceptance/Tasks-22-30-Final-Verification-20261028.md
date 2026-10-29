# Tasks 22–30: final local verification, 28 October 2026

Later continuation: [consenting live-camera checks and remaining external inputs](Live-Face-And-External-Acceptance-20261028.md).
It adds actual single-person camera/liveness/match results; the earlier no-live-person status
below is historical. Three-phone testing has since been excluded by the user.

Real SQL, service, Android-emulator and server checks were performed with synthetic payroll
inputs. These results do not certify bank transfers, historical salary evidence, physical
biometric acceptance or human company sign-off.

| Task | Verified evidence | Remaining acceptance |
|---|---|---|
| 22 Bank export | Fresh HBL same-bank/mixed-bank template and protected-export regression tests pass. | Reviewed real export accepted by HBL portal; no upload/transfer attempted. |
| 23 Confirmation | SQL synthetic lifecycle covered independent confirmation, protected evidence, partial/failed/confirmed states and duplicate protection. Restored proofs validate. | Actual bank confirmation from Finance. |
| 24 Reconciliation | Restored salary/payment/reversal proofs validate: confirmed 32,200, outstanding 0, ledger difference 0. | Real bank/payroll reconciliation. |
| 25 History | Emulator shows confirmed 32,200, outstanding 0, historical failed 5,000 and reversed 12,000; prior native payslip/PDF evidence retained. | Real employee phone acceptance. |
| 26 Reports | Settlement recovery and fresh tests pass; report adapter 89/89 controlled offline checks. Local report notification dispatch repaired/completed. | Review actual company month reports. |
| 27 Deployment | Fresh tested Release published/restarted, 31 migrations recorded, three health routes 200, synthetic password/OTP login, workers and isolated backup restore verified. | Production profile/service, external SMS delivery and operational owner acceptance. |
| 28 Integration/mobile | Backend 551/551, zero skipped; web verify/build and mobile lint/format/type checks pass; emulator history, CompreFace TLS and ONNX loading verified. | Three staff phones and consenting-person camera/liveness/spoof tests. Only one emulator; no new native APK build this continuation. |
| 29 Historical comparison | Retained synthetic September comparison MATCH, one employee, zero differences. | Genuine completed payroll month and independent bank/accounting evidence. |
| 30 Parallel live month | Synthetic salary → approvals → accounting → payments/reversals cycle and recovery validated. | Actual live cycle and named HR/Management/Finance sign-off. |

## Repairs and fresh checks

CompreFace frontend had no Docker network attachment and restarted because it could not
resolve `compreface-admin`. Recreating only that frontend from its existing Compose restored
the private network. TLS health/core status passed. Compose, nginx, both ONNX files and server
certificate hashes remained unchanged. The fresh backend checker loaded both models and
reached the provider using actual adapter TLS validation. No biometric enrollment was fabricated.

Initial fresh regression had three failures: two SQL fixtures created empty attendance locks
rejected by the current membership guard, and an authorization-lock test contended with parallel
SQL writers. Fixtures now retain actual summary membership; the dashboard case queries a
different missing day. Lock-concurrency tests run in a collection excluded from parallel
execution. Production guards were not relaxed. Related checks passed 12/12, then the full
regression passed **551/551, zero skipped**. One-shot company/acceptance fixture execution
suites were excluded; disposable SQL verification connection was explicitly supplied.

A COPY_ONLY/CHECKSUM backup of `Cosmix_Payroll_Payments_20261028` was restored as
`Cosmix_Payroll_Recovery_20261028`. VERIFYONLY and CHECKDB passed. All 258 protected files
were copied with ACLs and hash-checked. Actual frozen-payroll/settlement readers decrypted
salary, independent approval, confirmation and reversal proofs. Gross 33,000, deductions 800,
net/confirmed 32,200 and zero ledger difference match the retained expectation. Original
payroll/payment databases were not reset.

A real-SQL queue probe in that recovery database verified producer deduplication, exclusion
while a worker lease is live, and recovery after lease expiry. Two claims produced exactly one
in-app delivery and one completion audit; a further dispatch found no work. No external message.

Local `AttendanceJobs` was enabled in both private testing settings, with private backups.
The worker exposed an old report notification using retained `hr-reports-r1` storage rather
than the active local store. Fourteen encrypted objects were restored without overwriting
differing content, and the retained decryption key registered; active encryption key unchanged.
The payload was decrypted/validated before a guarded one-record operational retry. Five failed
attempts remain retained, with an encrypted before-state and
`outbox.requeued_after_storage_restore` audit. Maximum attempts is bounded at six. The actual
worker completed attempt six; completion was not forced. Both local jobs are now Completed
without failure codes. Two completion audits sharing their correlation belong to the report
and notification separately.

Final runtime's four product-assembly hashes match the fresh tested build. Password/OTP login,
own payment-history HTTP 200, restricted bank/settlement HTTP 403 and logout 204 passed after
restart. Management denials do not claim authorized Finance HTTP happy-path acceptance.
`/health/live`, `/health/ready`, `/health/payroll-ready` all return 200 Healthy in Development.
The 180-day retention worker completed a cycle with zero eligible records. Healthy local
configuration does not certify production SMS, physical hardware or company approval.

Web verification passed with its existing approximately 1.46 MB main-bundle warning.
Mobile lint/format/TypeScript passed; fresh native UI inspection retained correct payment
totals. Physical camera/liveness and real SMS delivery remain untested.

## Evidence locations

`D:/CosmixPayrollVerification20261028/final-acceptance/` contains:

- `results/regression-final.trx`, `sql-fixes.log`, `web-verify.log`, `mobile-verify.log`.
- `face-repair.log`, `face-strict-final.log`, `runtime-manifest.json`, `publish.log`.
- `restore-only.log`, `recovery-result.json`, `job-recovery-result.json`.
- `legacy-notification-recovery.json`, `active-queue-final.txt`.
- `health-published-final.json`, `authenticated-published-final.json`, `readiness-final.json`.
- `runtime-final.stdout.log`, `runtime-final.stderr.log`, `emulator-final.xml`.
- `report-dispatch.log`, linking the separate 89-check offline adapter evidence directory.

Read-only inventory labels tests it does not execute as NotRun; the dedicated evidence above
supplies restore/runtime checks separately. `production_ready` and company acceptance remain
false. Required real inputs: actual payroll month/bank results, production SMS configuration,
three physical phones and named company reviewers. Synthetic sheets cannot replace them.
