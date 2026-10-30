# Deployment readiness audit — 30 October 2026

The checked r8 runtime package remains intact, but company IIS activation and production acceptance remain blocked by actual server/configuration prerequisites. No IIS state, production database, certificate store, backup repository, scheduler, SMS provider or employee data was changed in this audit.

## Fresh evidence

Evidence directory: `D:/CosmixReleases/product-readiness-20261030/deployment-atoz`.

| Check | Result | Scope |
|---|---|---|
| Offline IIS installer regression | 18 passed, 0 failed | Synthetic certificates, input validation and parser check; no IIS mutation or certificate-store install. |
| r8 package-only setup | Passed | `https://192.168.1.20`, one site, `/api/v1`; plan deliberately leaves the site stopped and does not apply migrations. |
| r8 site manifest and migration SQL | 117 verified files, 0 mismatches; SQL present (600,241 bytes) | Read-only package check; no migration executed. |
| r8 archive SHA-256 | `C9F0163E96FF116C14A273FF4CC4CC264423A6D3B6E748BE2D8C241119E9D1BA` | Matches prior release evidence. Archive was not rebuilt or replaced. |
| Company `http://192.168.1.20/health/live` | 404, HTML | Does not establish a running ERP API. |
| Company `https://192.168.1.20/health/live` | Connection actively refused | No TLS or application acceptance possible at this address during the check. |
| Local `https://localhost:7240` | All four health probes healthy | Exact status/content checks at 10:36:41 UTC; this is the existing local testing host, not company production. Initial HTTP probes used the wrong scheme and failed; the HTTPS recheck is authoritative. |
| Backup-script regression on Windows PowerShell 5.1.26100.9711 | 3 passed, 0 failed | Synthetic SQL bytes/evidence and fake restic executable; no real upload/restore. |
| Backup-script regression on PowerShell 7.6.5 | 3 passed, 0 failed | Same success, partial backup and integrity-failure scenarios. |
| Operational health-monitor regression | 6 passed, 0 failed | Isolated healthy, HTML fallback, degraded, wrong-body, unavailable and oversized-response doubles. |
| Mobile draft PR #1 final commit CI | Source, Android and secrets successful on push and PR; CodeQL skipped | Checked commit `efdad9a156d5bb03cc169b4ec7be454b01ad0b7b`; no push, merge or workflow rerun performed. |

CI evidence: [PR #1](https://github.com/cosmixengineering/Cosmix-Mobile-App/pull/1), [PR source/Android run](https://github.com/cosmixengineering/Cosmix-Mobile-App/actions/runs/41039652858), [PR security run](https://github.com/cosmixengineering/Cosmix-Mobile-App/actions/runs/41039652937). These successful checks do not prove a company-signed Android release or device/SMS acceptance.

## Reproduced defect and fix

`cosmix-backend/scripts/Invoke-EncryptedRecoveryBackup.ps1` declares PowerShell 5.1 support, but wrote the success report with `FileStream.Write(byte[])`, an overload absent in that host's .NET Framework. A successful fake upload and repository check then failed with `Cannot find an overload for "Write" and the argument count: "1"`, leaving an empty report. The test reproduced this failure before the source change (`backup-ps51-red.log`).

The only production-script change in this audit is `Write($bytes, 0, $bytes.Length)`. The new `Test-EncryptedRecoveryBackup.Tests.ps1` verifies a valid success JSON report and proves partial backup/integrity failures create no success report, on both PowerShell hosts. Temporary synthetic files are confined to a unique verified temp directory and removed; prior restic environment settings are restored. Windows PowerShell tests used process-only `-ExecutionPolicy Bypass`; no machine execution policy changed.

The r8 archive contains the old helper. Use the separately identified helper supplement before running backups under Windows PowerShell 5.1, or incorporate this source correction into the next verified package. This correction does not require a backend rebuild or database migration. Existing runtime/site hash evidence must not be relabeled as proving a newly built runtime.

Supplement: `D:/CosmixReleases/Cosmix-IIS-r8-backup-helper-fix-20261030.zip`; SHA-256 `41D100674ACF317C6A38ACAF8053F93DB44771961E24DCE4E79052E9298FC38E`. It contains the corrected backup helper, synthetic regression script, README and file hashes. Keep these files outside the public site root; follow the existing operations prerequisites. The original r8 archive remains unchanged.

## Actual activation prerequisites

1. **Windows/IIS access:** an authorized operator on `192.168.1.20`, elevated PowerShell 7.4+, the .NET 10 x64 Hosting Bundle/ANCM, IIS features, dedicated pool, and TCP 443 access. No authenticated Windows access was supplied. Ubuntu face-service credentials are not Windows credentials.
2. **HTTPS:** the actual installed private-key certificate with IP SAN `192.168.1.20`, Server Authentication EKU, valid chain/revocation and client trust. The certificate request template is preparation, not issuance. Do not substitute the face-server CA unless it actually issued this IIS certificate.
3. **Private configuration and data:** actual supported SQL Server service/database and least-privilege identity, matching existing JWT/OTP/data-protection/evidence keys, private stores/ACLs, enabled production authentication and reviewed face settings. Do not substitute developer LocalDB or synthetic authentication flags. Review pending migration SQL, rehearse recovery, stop old writers, and execute using the separate migration identity.
4. **SMS:** real provider choice/account/credentials, approved sender and verified delivery/retry/expiry behavior. No provider was supplied. A healthy local test worker or generic OTP challenge is not carrier delivery. Production readiness must remain unavailable when required login/recovery configuration is missing.
5. **Off-site backup/recovery:** approved encrypted repository, backup identity/password file, immutable coordinated SQL/private-file snapshots, separate key escrow/custodian, retention/geography, actual SQL restore/DBCC and encrypted-file recovery, agreed RPO/RTO. None is established by the synthetic script test. No real backup was run or manifest attestation fabricated.
6. **Scheduling/alerts:** after selecting the host, snapshot process and approved destination, install the real backup schedule, one-minute HTTPS health monitoring, missed-execution/nonzero alerts and recurring recovery drills under their intended identities. No scheduler or alert destination was supplied or changed. Validate actual alert delivery and reboot/recycle recovery; the installer leaves site auto-start off until activation.
7. **Android distribution:** actual public IIS CA, current and independently retained backup SPKI pins, company keystore/alias/private build credentials, approved signing-certificate fingerprint/version and distribution route. Then verify signed APK/AAB, merged release policy, device TLS/pin rejection, LAN/VPN reachability, upgrade, employee login and real camera/liveness/attendance. Existing debug APK/CI builds do not satisfy these gates.

Follow `deploy/iis/HOST-192.168.1.20.md`, `cosmix-backend/docs/Production-Operations.md` and `cosmix_mobile/docs/Android-Production-Release.md`. The next company-side action is to supply the named server/certificate/configuration inputs and execute their reviewed activation/recovery steps. It is not safe or accurate to invent those values to call deployment complete.
