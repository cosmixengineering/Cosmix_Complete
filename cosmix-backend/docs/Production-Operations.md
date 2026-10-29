# Payroll operations and activation

This deployment is not production accepted merely because compilation, health probes or a synthetic recovery drill pass. Use the exact release binaries and additive migrations together. In particular, the closed-bank-month journal trigger requires the updated EF model: stop old writers before applying that migration.

## Hosting and release

The current face adapter depends on `OpenCvSharp4.runtime.win`. Use a supported Windows host for the existing binary; do not publish this package to Linux and assume local ONNX/image inference will work. The private Ubuntu CompreFace server is a separate service. A Linux API port needs its own native image-codec dependency and inference validation.

1. Prepare a dedicated, least-privilege service identity and a supported .NET 10 runtime with current security updates. Host the API under IIS/ASP.NET Core Module with an always-running application pool, preload and restart recovery. The deployment must persist the existing data-protection key ring and private storage across application-pool recycling. Do not give the runtime identity schema-alter privileges; use a separate migration identity.
2. Select the approved HTTPS API origin and web CORS origins. Store secrets outside Git and the web root. Restrict the origin/network and private service ports at the reverse proxy/firewall. Do not enable LocalTesting or synthetic authentication in Production. The mobile release reads its approved origin and pins from the separately documented release configuration.
3. Pause API writers and workers. Take a coordinated recovery set before applying migrations. Rehearse migration with the retained release on an isolated restored database. Publish compatible binaries, apply only pending reviewed migrations, then restart. Do not run old binaries against the new journal trigger.
4. Check `/health/live`, `/health/ready`, `/health/workers` and `/health/payroll-ready`. The payroll readiness probe correctly remains 503 while production SMS/login recovery is unconfigured. Do not weaken it to obtain a green deployment. Worker health is initially pending until execution, and expires when work stops. A running OTP worker is not proof of carrier delivery. These endpoints return status only; restrict monitoring routes at the ingress to trusted operators.
5. Exercise a session before/after process restart, independent recovery, an authorized payroll read and a denied employee/admin boundary. Confirm worker and HTTP health alarms through the actual monitoring service before launch.

## Encrypted backup and restore

`scripts/Invoke-EncryptedRecoveryBackup.ps1` uploads an explicitly selected coordinated SQL backup, immutable private-evidence snapshot and recovery manifest into a preinitialized encrypted restic repository. The input SQL backup hash must match the manifest. Partial backup exit codes, missing/overlapping key custody and repository-integrity failure all fail the job. Production mode rejects local destinations. `-LocalDrill` is explicitly synthetic and cannot establish off-site acceptance.

Use approved restic binaries, a dedicated backup identity and private `RESTIC_REPOSITORY` / `RESTIC_PASSWORD_FILE` configuration. Do not put passwords in scheduled-task arguments. The storage account must have appropriate off-site geography, retention/immutability and access controls; a remote URL alone proves none of these. The script does not initialize, prune, forget or overwrite existing evidence. Filesystem ACLs and encryption of the SQL/staging/private volumes must be verified on the actual host. SQL TDE and backup encryption, where supported by the selected SQL edition, require separate certificate escrow and restore rehearsal. Do not switch on disk/TDE encryption without verified recovery-key custody.

During a maintenance window, stop all application/worker writers, create a COPY_ONLY SQL backup WITH CHECKSUM and a consistent private-file snapshot, then restart only after both snapshots exist. Restore that backup to an isolated database and run DBCC CHECKDB before marking the manifest's restore check true. Never manufacture those assertions for a company backup. A manifest contains:

```json
{
  "schemaVersion": 1,
  "coordinatedSnapshot": true,
  "sqlRestoreVerified": true,
  "keyCustodyVerified": true,
  "keyCustodyReference": "REPLACE_WITH_REVIEWED_ESCROW_RECORD",
  "snapshotAtUtc": "REPLACE_WITH_ACTUAL_UTC_TIME",
  "sqlBackupSha256": "REPLACE_WITH_ACTUAL_SHA256"
}
```

The manifest describes performed checks; the upload command cannot independently prove an operator's assertions. An immutable private snapshot must remain unchanged while it is read. Keep data-protection, JWT/OTP, file-store, face-vault and backup repository keys in independently protected escrow with a separately approved custodian and restore procedure. Passing `KeyCustodyPaths` prevents declared custody paths from accidentally entering the data snapshot; it cannot detect undisclosed copies of keys. Restore the exact restic snapshot to an empty private directory, verify checksums, restore SQL under a new name, and verify protected evidence with the separately recovered key material. Record actual restore duration and compare it with approved RPO/RTO.

Schedule backup only after the production host, maintenance snapshot process, off-site destination, retention and RPO/RTO are approved. Schedule `Test-OperationalHealth.ps1` against the approved HTTPS origin every minute and alert on nonzero exit/missed execution through the organization's monitoring system. Schedule recurring full recovery drills. Neither a local task nor a message to an external monitoring service has been created without those deployment details.

## Dynamic company setup and acceptance

HR/Admin must publish their own effective-dated shifts, rosters, attendance/grace/OT/leave/deduction rules, salary proration and approval routing. Salary master, earning/deduction revisions and statutory applicability require actual employee inputs and independent approval. Finance must approve ledger accounts, open accounting periods, effective posting mappings and the selected HBL formats. No sample policy, equal statutory allocation or sample salary is promoted into real company configuration.

Then use the authoritative month preview, resolve its missing-source errors, prepare complete employee groups, independently approve HR and Management, hand off to Finance, post, export and reconcile. Employee approvers' own salary belongs in a different independently reviewed group. Return an HR-approved unlocked batch for correction, then reprepare; do not edit frozen earnings. A locked correction requires the controlled accounting correction process, not an unlock shortcut.

Actual HBL upload/historical comparison remains deferred at the user's request. A parallel live month and named authorized reviewer sign-off remain company actions; do not mark them passed using synthetic evidence. Mobile remains employee-facing, including employees who hold an Admin role on the web. It has no SuperAdmin acceptance panel.

## Face timeout diagnostics

CompreFace transport logs only constant operation/outcome labels and elapsed milliseconds: detection/verification, response-received/timeout/cancelled/unavailable/invalid-response. It never logs images, employee IDs, endpoints, keys, headers or response payloads. A received response is not a biometric pass. Per-request cancellation and no-retry behavior remain enforced. Correlate these entries inside the existing request logging scope with proxy/CompreFace processing time and local ONNX queue/inference behavior. Do not raise biometric thresholds, retry face submissions automatically or disable TLS verification to hide timeouts.

The earlier real-face timeout is not proven resolved by an offline transport test. Full employee acceptance still requires a current enrolled employee and device: login, consent, radius, live camera/liveness, saved check-in, location checkout and history. Provider outages, outside-radius, revoked device/consent and failed liveness must leave no accepted attendance. A prior component-only live camera check cannot substitute for this flow.
