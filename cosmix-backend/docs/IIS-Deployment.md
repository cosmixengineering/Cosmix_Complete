# IIS deployment: React and API on one HTTPS site

The approved LAN address is now **https://192.168.1.20**. The packager defaults to `deploy/iis/company-lan.json`, adds the allowed host/origin and HTTPS port to the published IIS configuration, and builds the frontend in an isolated directory. Follow `deploy/iis/HOST-192.168.1.20.md` (also copied into the release instructions) for the exact server/certificate steps. The old two-application `Setup-CosmixIisHosting.ps1` is retired.

The recommended package serves the React build from `wwwroot` and the existing API at `/api/v1` on one IIS site. Source projects remain separate. No Vite development server, ARR proxy, IIS URL Rewrite module or `/api` child application is needed. Do not convert `wwwroot` into a child application. Hosting the existing API under an `/api` child app would change its path base while its controllers already declare `/api/v1`.

Create a new package from the repository root:

```powershell
./scripts/Publish-IisPackage.ps1 -OutputDirectory D:/CosmixReleases/REPLACE_WITH_NEW_RELEASE_DIRECTORY
```

The command builds React with `/api/v1`, publishes the framework-dependent .NET app, copies only generated browser assets into `site/wwwroot`, removes development settings from the package and enables `WebFrontend__Enabled` in the package's IIS config. It emits a SHA-256 file manifest. Build logs and intermediate build artifacts are siblings of `site`; publish **only `site`**. Keep private JSON/keys, logs, SQL backups and uploads outside it. Never deploy the entire source repository.

Prerequisites on the approved Windows host:

- IIS with ASP.NET Core Module V2 from the current .NET 10 Hosting Bundle (repair the bundle if IIS was installed afterward), Static Content and Application Initialization features; a current .NET 10 x64 runtime.
- Dedicated x64 application pool, `.NET CLR Version: No Managed Code`, integrated pipeline, one worker process, `Start Mode: AlwaysRunning`, `Idle Time-out: 0`, `Load User Profile: True`; site/application `Preload Enabled: True`. Keep rapid-fail protection enabled. Verify actual restart/recycle behavior and background-worker health.
- Approved HTTPS binding and certificate, correct hostname/DNS and firewall. Do not bind the package to a guessed production domain. HTTP does not support the Secure login cookies.
- The pool identity receives Read/Execute on the versioned site folder, write access only to the explicitly configured private stores/key ring and required temp paths, and the intended SQL permissions. LocalDB under a developer's login is not a production IIS database. Use a supported SQL Server service instance and dedicated least-privilege database access. Migrations use a separate operator identity.

Provide production configuration privately through the application pool's environment variables (IIS 10) or a controlled deployment secret mechanism, including `ConnectionStrings__CosmixDatabase`, JWT/OTP and cookie/protector keys, private evidence/vault/key locations, face provider/model settings, retention and worker configuration. Do not copy `COSMIX_LOCAL_TESTING_SETTINGS`, `COSMIX_EXISTING_TESTING_HOST`, `COSMIX_FRESH_TESTING_HOST` or any synthetic host flags into IIS. The checked-in defaults do not constitute production login configuration.

Data-protection keys must persist under the stable pool identity/profile or an explicitly provisioned protected key ring. Back up both the ring and the associated protection/key-custody material; prove a restart and isolated recovery before accepting the deployment. A copied key file alone does not establish recoverability across machines or identities. See `Production-Operations.md` for coordinated migrations and backup/restore.

Deployment order: create an offline versioned site directory; take and verify the coordinated backup; stop old writers; apply the reviewed pending migrations using the deployment identity; point the stopped application at the new `site`; configure its approved settings/binding/ACLs; start and preload. Retain the previous binaries and backup. A schema rollback must be reviewed; do not blindly start older binaries after new journal triggers are installed.

Validate through the actual HTTPS binding:

1. `/sign-in`, `/account` and a permitted deep route still load after browser refresh; HTML uses `Cache-Control: no-store`.
2. Unknown `/api/...`, `/health/...` and missing assets return errors rather than the SPA HTML. Unsafe requests to unknown SPA paths must not succeed.
3. `/health/live`, `/health/ready`, `/health/workers`, `/health/payroll-ready` return the expected states. Initial worker pending status and missing production SMS must remain visible.
4. CSRF/cookies, password plus enrolled MFA, session refresh, permission denials and session continuity across recycle are tested with authorized test accounts. Do not lower SameSite/Secure cookie protections to compensate for a routing mistake.
5. Verify the approved Android origin/pins against this certificate chain, full attendance and recovery, then actual payroll acceptance separately.

This source change and package build do not install IIS, create a production site, provision certificates, enable SMS or record company sign-off. The server address is confirmed, but its trusted certificate, private deployment settings and actual activation still need to be supplied/verified on the server.

Reference: [Microsoft ASP.NET Core on IIS](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-10.0).
