# Cosmix: company LAN IIS deployment

Target frontend: **https://192.168.1.20/**

Target API: **https://192.168.1.20/api/v1/**

This package is prepared for one Windows IIS site on HTTPS port 443. `/80` is not a port. Do not create an `/api` sub-application or host the frontend separately. Only `site` is the public site directory; `instructions`, manifests, SQL, backups and private configuration stay outside it.

## 1. Server prerequisites

On **192.168.1.20**, IT installs IIS (Static Content, Management Console and Application Initialization) and the **.NET 10 x64 Hosting Bundle**. Installing only the SDK/runtime is insufficient: IIS needs ASP.NET Core Module V2. If IIS was installed after the bundle, repair the bundle. Restart IIS in the agreed maintenance window.

Use an actual SQL Server service instance accessible from this server. Do not point IIS at the developer laptop's LocalDB. Keep the existing database, encrypted files and their matching keys together; do not generate replacement encryption keys for existing data.

## 2. Trusted HTTPS certificate

Have company IT issue a server-authentication certificate whose **Subject Alternative Name contains IP Address: 192.168.1.20**. A certificate with only a DNS name or only a CN is not sufficient. For this private LAN address use the company/internal CA. Install its public root/intermediate certificates into the appropriate trust stores on clients; never distribute the CA private key. Certificate trust must work without clicking through browser warnings.

An optional request template is included. On the **server**, open elevated Windows PowerShell in this instructions directory:

```powershell
certreq.exe -new .\192.168.1.20-certificate.inf C:\CosmixCertificateRequest.req
```

Send only the `.req` to IT's CA operator. Ask them to preserve the IP SAN and Server Authentication EKU. After IT returns the issued certificate, run on the same server:

```powershell
certreq.exe -accept C:\Path\To\Issued-Cosmix.cer
```

Verify it appears in **Local Computer > Personal > Certificates**, has its private key, a valid date range and a trusted chain. The template deliberately keeps the leaf private key non-exportable; certificate recovery/reissuance belongs to IT. No certificate/private key has been generated on this development laptop.

## 3. Copy the package and configure IIS

The package now includes a guarded installer for this single-site layout. Run it **locally on 192.168.1.20 in elevated PowerShell 7.4 or later** after installing IIS/Hosting Bundle and the issued certificate. `-WhatIf` checks the actual server, trusted/revocation-valid certificate, exact IP SAN, required runtime and binding conflicts without changing IIS:

```powershell
.\instructions\Setup-CosmixIisHosting.ps1 -PackageDirectory C:\Cosmix\Releases\20261030-lan -CertificateThumbprint YOUR_INSTALLED_CERTIFICATE_THUMBPRINT -WhatIf
```

After reviewing the result, repeat without `-WhatIf` to create **CosmixErp / CosmixAppPool**, one HTTPS binding and read-only release permissions. The site remains stopped with automatic startup disabled. The installer refuses a running/different existing site, shared pool or conflicting binding; it never replaces another deployment, applies SQL, imports secrets or opens firewall ports. Configure those remaining items below before activation. If a final ACL operation fails after IIS commits, the dedicated site remains stopped; fix the ACL and rerun the exact same package. No automatic deletion/rollback is attempted.

For an offline package-only check, run the command with `-PackageOnly` and no certificate argument. This validates files and topology, **not** server prerequisites or certificate trust. `Test-IisDeployment.ps1` exercises input/certificate rejection locally without installing certificates or changing IIS. Manual equivalent steps remain below for IT review.

1. Extract the archive to a versioned private release folder, for example `C:\Cosmix\Releases\20261030-lan`. The IIS physical path will be its `site` subfolder.
   Run `instructions\Test-IisPackage.ps1 -PackageDirectory C:\Cosmix\Releases\20261030-lan` before adding private settings. It must report zero hash mismatches. The checksum manifest detects copy/corruption errors; it is not a digital publisher signature.
2. In IIS Manager create **CosmixAppPool**: No Managed Code, Integrated pipeline, Enable 32-Bit Applications=False, Start Mode=AlwaysRunning, Idle Time-out=0, Maximum Worker Processes=1, Load User Profile=True. Keep rapid-fail protection enabled.
3. Create website **CosmixErp**, physical path `C:\Cosmix\Releases\20261030-lan\site`, pool `CosmixAppPool`. Set HTTPS, IP `192.168.1.20`, port `443`, hostname blank, SNI unchecked, and select the issued certificate. Check existing bindings first; do not replace another site's certificate or binding. Keep this new site stopped until configuration and migrations are ready.
4. Set the root application's **Preload Enabled=True**. Do not convert `wwwroot` into an application.
5. Grant `IIS AppPool\CosmixAppPool` Read & Execute on `site`. Grant Modify only on approved private storage/temp directories outside the site; protect key files separately. Never grant the pool write permission across the release directory.
6. Permit TCP 443 from the approved company LAN in Windows/network firewall. HTTP port 80 is optional: a same-site HTTP binding will use the app's HTTPS redirection to port 443. Keep HTTPS-only if port 80 is occupied or unnecessary. Do not send login credentials over HTTP.

The package already sets Production, `WebFrontend__Enabled=true`, `AllowedHosts=192.168.1.20`, allowed CORS origin `https://192.168.1.20` and HTTPS redirection port 443. React uses relative `/api/v1`; there is no localhost API dependency. JWT issuer/audience are token identifiers and do not need changing to the IP address.

## 4. Required private application configuration

Server address configuration is complete, but address configuration alone does not supply database credentials, authentication keys or provider settings. In IIS 10, IT can enter the existing approved settings in **Configuration Editor > system.applicationHost/applicationPools > CosmixAppPool > environmentVariables**. These are server-side secrets in protected IIS configuration, not browser `VITE_` variables. Restrict access to IIS configuration/backups. Recycle the pool after changing them.

Required areas, using double underscores for nested keys:

| Setting/area | What IT supplies |
|---|---|
| `ConnectionStrings__CosmixDatabase` | Actual SQL Server/database connection with encryption and a trusted SQL certificate; no developer LocalDB or blanket `TrustServerCertificate=True`. |
| `Jwt__SigningKey` | Existing private signing key (at least 32 bytes); preserve during migration. |
| `WebAuthentication__...`, `MobileAuthentication__...` | Approved enabled login/rate/OTP settings. Copy only reviewed production settings, never synthetic host flags. |
| `MobileAuthentication__Crypto__KeyVersion`, `VerifierKeyBase64`, `EncryptionKeyBase64`, `MaxDeliveryLifetimeSeconds` | Matching existing private crypto settings under the same `MobileAuthentication__Crypto__` prefix. |
| `PrivateFiles__Directory`, `PrivateFiles__KeyFile`, `PrivateFiles__KeyReference` | Existing encrypted evidence store, matching key and immutable key reference; retain configured historical decryption keys. |
| Face provider/model configuration | Existing approved CompreFace/liveness settings and private credentials; verify reachability from the IIS server. |
| `PunchRetention__RetentionDays=180`, `PunchRetention__IsPolicyApproved=true` | User-approved GPS retention; configure the existing worker interval. |

Preserve the ASP.NET Data Protection ring under the stable pool identity/profile and prove recovery/recycle continuity. Do not copy `COSMIX_LOCAL_TESTING_SETTINGS`, `COSMIX_EXISTING_TESTING_HOST`, `COSMIX_FRESH_TESTING_HOST` or development fixture flags.

**SMS provider is still unavailable.** The package does not bypass OTP or turn on fake delivery. Initial SMS onboarding/reset and affected readiness checks will remain unavailable until the real provider is configured. Existing authenticator accounts still need their correct migrated data/keys and complete auth configuration.

Google Maps browser key is intentionally empty in this package to avoid shipping the developer key. If Maps is needed, put an approved browser-restricted key in the public hosting profile and rebuild; restrict its allowed referrer to `https://192.168.1.20/*` and APIs in the Google console. Map-independent UI is packaged normally.

### Prepared face-provider settings

On 30 October the existing CompreFace keys, CA and two ONNX models were retrieved from 192.168.1.50 and checked privately. They are **not inside this public ZIP**. The private handoff is `C:/Users/Huma.Khan/AppData/Local/Cosmix/Deployment/20261030-face/DEPLOY-PRIVATELY.txt` on the development laptop. Follow it to transfer the selected files to `C:\CosmixPrivate\Face` on the IIS server with restricted permissions.

Then run the included helper in elevated Windows PowerShell on the IIS server:

```powershell
.\instructions\Import-FaceSettings.ps1 -ConfigurationPath C:\CosmixPrivate\Face\face-server.iis.private.json -AppPoolName CosmixAppPool
```

It checks the face CA/model hashes and imports only face settings into the pool environment without printing values. The helper's syntax was checked locally; its IIS mutation has not been run on the target server. Recycle the pool and test real connectivity after import. This does not replace the ERP database/authentication configuration or establish employee biometric acceptance. The older raw Ubuntu `backend.private.json` includes disabled flags; use the prepared `face-server.iis.private.json` instead.

## 5. Database rollout and activation

Take a coordinated, verified SQL/private-file/key backup. Stop old writers before applying reviewed migrations using a separate deployment identity. If `database/migrations.sql` is included, it is the idempotent script for this build: the DBA must confirm the intended database, review pending operations and execute it against that database, saving output. It contains no connection credentials and is not executed by this package.

Deploy schema and binaries together. New journal triggers can make old binaries incompatible; do not blindly roll back just the DLLs. Keep the prior release and database backup. Start the site only after configuration, access permissions and migrations are ready.

After approved activation, set the site's **Start Automatically=True** as well as the pool's **AlwaysRunning** setting, then test recovery after a planned recycle/restart. The preparation installer deliberately leaves site auto-start off to prevent an unconfigured deployment serving after a reboot.

## 6. Verify the actual server

From a trusted client browser open `https://192.168.1.20/sign-in` and refresh it. There must be no certificate warning. Verify:

- `/health/live`, `/health/ready`, `/health/workers`, `/health/payroll-ready`: investigate 503 instead of treating it as success; worker startup may initially be pending and missing SMS remains a real readiness issue.
- `GET /api/v1/does-not-exist`: 404, not a React HTML success page.
- Authorized test login, CSRF/cookies, refresh, permission denial, and session continuity after an application-pool recycle.
- Enrolled employee device/consent, radius, real camera/face check-in, saved attendance and checkout/history. A successful frontend page is not proof of attendance/payroll acceptance.

For 500.30/500.31 inspect Windows Event Viewer and Hosting Bundle/runtime/configuration first. Do not enable Development or Swagger publicly to diagnose. If temporary stdout logging is necessary, use a private log directory and disable it immediately after diagnosis; do not share unredacted logs.

Prepared locally does not mean installed or live-tested on 192.168.1.20. Company-side certificate issuance, SQL/settings and IIS activation are still required.

The instructions directory also carries `Test-OperationalHealth.ps1`, `Invoke-EncryptedRecoveryBackup.ps1` and `Production-Operations.md`. Health monitoring checks each endpoint's HTTP status **and its expected plain-text Healthy body**; an IIS/SPA HTML page with HTTP 200 is a failure. Run the health command against `https://192.168.1.20` after activation and connect its nonzero exit/missed execution to the company's actual alerting system. Off-site backup requires the separately configured repository, custody and coordinated recovery snapshot described in the operations document.

References: [Microsoft IIS hosting](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-10.0), [Microsoft HTTPS bindings](https://learn.microsoft.com/en-us/powershell/module/webadministration/new-webbinding?view=windowsserver2025-ps), [IIS administration from PowerShell 7](https://techcommunity.microsoft.com/blog/iis-support-blog/managing-iis-in-powershell-7-fixing-get-iisapppool--get-iissite-issues/4416919), [IP SAN matching without Common Name fallback](https://learn.microsoft.com/dotnet/api/system.security.cryptography.x509certificates.x509certificate2.matcheshostname).
