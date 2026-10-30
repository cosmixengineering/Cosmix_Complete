# Product readiness verification — 30 October 2026

Update: see [the final partial-task verification and r8 handoff](Partial-Tasks-Verification-20261030.md) for subsequent fixes, 684 passing backend regressions and actual mobile remote CI results. Earlier counts and the DNS-only mobile limitation below describe the preceding run.

The current code and the face service have new verified fixes. This is **not** a claim that all company/production acceptance items are complete.

## Changes and fresh evidence

- Web: removed the 16 explicit-any errors using typed API data and guarded nested evidence records. Corrected attendance employee choices: employee numbers are integers and names are strings, not UUIDs. Empty correction reasons/evidence and review reasons no longer create fabricated approval/verification text. Unknown asset condition/shift is displayed as unrecorded. Formatted the 20 files reported by the formatter. Full `npm run verify` passed; three focused evidence-record tests passed.
- Backend: the general regression suite passed **631/631, zero failures/skips**. This excludes SQL/provider acceptance tests, which must run explicitly. Fresh disposable SQL schema/trigger check, company-readiness SQL queries and backup/restore/corrupt-backup rejection each passed separately (**3 SQL tests**). The backup test previously selected a hardcoded old database and silently returned if absent; it now requires explicit LocalDB selection, creates its own synthetic source/restore databases and checks restored data. CI separates these SQL tests and runs them after migration. Remote CI was not executed for this uncommitted working tree.
- Mobile: lint, formatting and type checks passed; **4 release-config tests** passed. This is not a signed release/device acceptance.
- Dependency audit: backend reported no high/critical advisories; fresh locked frontend install reported zero advisories. Current backend/mobile directory secret scans passed. Frontend directory scan found the ignored local `.env` Maps key; it is not copied into the isolated build or package. Historical Git Maps-key findings still need owner-side restriction/rotation and review; no clean-history claim is made.
- IIS: fresh frontend/backend package built for `https://192.168.1.20`, relative `/api/v1`, Production, HTTPS port 443 and a single site. All **117 site file checksums** matched. Idempotent SQL generated offline, not applied to the company database. Private face settings are excluded from the ZIP.

## Actual Ubuntu face-server work

Authorized SSH login used `adminlinux` on `192.168.1.50`. The frontend container was restarting: nginx could not resolve `compreface-admin`; inspecting the container showed **no Docker networks**, although Compose specified `cosmix-face_face-private`. Core and database containers were healthy.

Recreated only `compreface-fe` from its existing Compose definition. Database volumes, model services and credentials were preserved. The frontend rejoined its network, served HTTPS and had zero automatic restarts. A subsequent explicit frontend restart retained working HTTPS. Docker is enabled at boot; a full machine reboot was not performed. The cause of the original network detachment itself is not proven.

Verified from this laptop with normal TLS validation:

- `/` and `/core/status`: HTTP 200, including after restart.
- Both existing detection and verification API keys: authenticated stateless requests with synthetic non-face images returned expected HTTP 400/provider code 28. No employee enrollment or face image was used.
- Exported CA-file/model hashes matched the existing reviewed local configuration.
- Actual .NET `SilentFaceLivenessService` loaded both exported ONNX models and performed native inference. Actual `CompreFaceClient` using its private CA trust rejected the synthetic image as `face_not_detected`.

The older Ubuntu backend configuration had disabled feature flags. The prepared IIS configuration preserves the enabled flags/thresholds from the existing reviewed local backend configuration, with current server keys and new private model paths. It is not automatically applied to IIS.

Protected handoff: `C:/Users/Huma.Khan/AppData/Local/Cosmix/Deployment/20261030-face/DEPLOY-PRIVATELY.txt`. Keys never enter the repository/public ZIP. The CA signing key and Ubuntu TLS private key were not exported. The temporary SSH-account export directory was removed after transfer; original server credentials/models remain.

## Actual blockers remaining

1. `192.168.1.20` serves an IIS HTTP page; `/health/live` returns 404 and HTTPS port 443 is not reachable. RDP is reachable, but an authenticated Windows deployment session/settings have not been supplied. Ubuntu SSH credentials do not authorize Windows/IIS login. ERP database/auth/storage settings, the IIS certificate and live deployment/recycle checks remain.
2. Production SMS provider is unavailable. Initial enrollment/recovery delivery cannot be certified or bypassed.
3. Android release requires the real signing identity, current/backup pins and device verification. Its current production guard expects a DNS origin/system-trusted CA, so the new private-IP IIS plan also needs a reviewed LAN/mobile certificate-trust configuration before release; no production APK is claimed.
4. No Android device/emulator is currently attached. Complete enrolled-employee consent/device/radius/camera/check-in/checkout/history acceptance remains; component inference and synthetic requests do not prove it.
5. Approved real company policies, salaries, contribution/account mappings and approvers remain company inputs. Complete non-payroll Finance integration is not covered by the implemented payroll ledger.
6. Scheduled encrypted off-site backups, production disk/database encryption, alert delivery, remote CI and formal security assessment remain operational acceptance items.
7. Actual historical payroll/HBL acceptance remains deferred; a parallel real payroll month and a named authorized review/sign-off cannot be replaced by synthetic data.

Evidence: `D:/CosmixReleases/product-readiness-20261030`. Initial failed runs are retained there; `regression-final.trx`, `frontend-verify-final.log`, the separate SQL TRX files and the final face-native result describe the final verified state. No business payroll, bank payment or actual company approval was created.
