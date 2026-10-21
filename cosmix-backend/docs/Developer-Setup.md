# Start here

Project: Cosmix.Backend. This is a runnable backend foundation, NOT a completed HR module.
All four application projects are under src.

## 1. Get the code

Each developer uses their own GitHub account and approved repository access.

~~~powershell
git clone https://github.com/cosmixengineering/cosmix-backend.git
cd cosmix-backend
git switch develop
git pull --ff-only origin develop
~~~

Existing clones only need the last two commands after committing/stashing their own work.
Never discard someone else's changes to get a clean checkout.

## 2. Open the solution

Use a current Visual Studio 2026 release supporting the installed .NET 10 SDK,
with the ASP.NET and web development workload. .NET 10 targeting requires
Visual Studio 18.0 or newer; use an up-to-date matching SDK/IDE combination.
Open Cosmix.Backend.sln and set **Cosmix.Api** as the startup project.
React and React Native remain separate clients.

Official compatibility reference:
https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs

## 3. Restore and verify

Run these in the repository root, using the Visual Studio terminal or PowerShell.

~~~powershell
dotnet restore Cosmix.Backend.sln --locked-mode
dotnet tool restore
dotnet build Cosmix.Backend.sln
dotnet dev-certs https --trust
~~~

global.json allows stable .NET 10 feature-band updates from 10.0.100 onward.
Package versions and package lock files are committed. Update them intentionally together.

## 4. Configure your own database

Install SQL Server Developer/Express or LocalDB. Each developer needs a separate database.
The following example uses Windows LocalDB and integrated authentication; it stores no password.
Replace the database name with your own development database name.

~~~powershell
dotnet user-secrets set "ConnectionStrings:CosmixDatabase" "Server=(localdb)\MSSQLLocalDB;Database=CosmixHr_YourName;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True" --project src/Cosmix.Api
~~~

TrustServerCertificate=True is for local development only. Production requires a valid trusted
database certificate, encrypted connections and least-privilege accounts.
Development user secrets are outside Git, but are NOT an encrypted production secret vault.
Use the approved server secret store for deployment.

InitialFoundation creates the Department reference table. AddInitialHrAttendanceModels adds
the first employee, organization, assignment, document, site, shift and raw-punch tables.
CompleteReviewedHrAttendanceDataModel expands this to 115 mapped business tables, including
employee/vehicle insurance expiry and reminders. CorrectReviewedModelLifecycles adds two tables
and sixteen columns for the reviewed fixes, bringing the model to 117 tables. Read [Migration Review](Migration-Review.md)
and [SQL notes](sql/README.md) before any database creation or migration application.

The latest recorded upgrade of the original `Cosmix_Development` database is migration 12,
`20261008074438_AddEmployeeChangeJournal`; see [Employee Updates verification](Employee-Updates-Verification.md).
The isolated `Cosmix_Testing` database has a separate migration and acceptance history. Read the
[current attendance verification record](attendance-completion/Verification.md) for its latest
upgrade outcome, remaining migrations and acceptance limits. Always inspect the intended target's
actual migration history before applying a reviewed range; neither database's history describes yours.

The following setup incident is historical evidence from 7 October 2026.
Those migration scripts were originally reviewed offline. On 7 October 2026 the existing
`Cosmix_Development` used by SSMS was backed up, then upgraded from
`CorrectReviewedModelLifecycles` through the reviewed authentication migrations. At that stage,
the verified state was seven migration-history entries and 122 business tables, ending at
`20261007090713_AddAccountRecoveryAndOwnerInitialization`. Only pending migrations were applied.
An earlier CLI missing-database result came from a different running LocalDB instance
resolving under the same alias. Before applying anything, compare the actual server identity,
database and migration history with SSMS; do not create a replacement or attach/delete files
just because an alias returns a different catalog. The verified instance pipe was used as a
process-local connection override. That did not configure a separately launched Visual Studio
process. On 7 October the confirmed existing target was also saved as
`ConnectionStrings:CosmixDatabase` in the API's Development User Secrets, outside Git.
Pipe IDs change when the LocalDB instance restarts; re-identify the same instance/database and
update only the local setting if that happens. Never create a replacement database to fix a stale
pipe. No connection string or secret was committed.

Retained acceptance scripts and commands under `docs/access-provisioning`, `docs/group-rosters`
and `docs/attendance-completion` may bind a specific machine, private directory, run and reviewed
artifact hashes. They document that acceptance environment and are not general setup commands.
Use the current verification record and the exact approved target before executing or adapting them.

**Windows packaged Codex and Visual Studio can see different User Secrets folders.**
On this workstation, a write from the packaged Codex process to `%APPDATA%` was redirected to
the Codex package's `LocalCache/Roaming` directory. Visual Studio could not see that copy,
even though the user name, displayed path, Development environment and UserSecretsId matched.
Run the configuration command in Visual Studio's terminal or a normal Windows terminal for
the actual developer account, then restart debugging. A successful `user-secrets list` or API
check inside Codex alone does not verify Visual Studio's configuration. Do not copy secrets
into the repository or remove the missing-connection guard. Verify `/health/ready` and
`/openapi/v1.json` on the actual Visual Studio launch URL. This was corrected and verified
on 7 October without changing application code, tables or migrations.

Historical full-chain SQL artifacts end at their named migration; none should be assumed to
represent the current model. They are for separately authorized empty targets. An authentication
incremental script alone cannot initialize an empty database.
Inspect the confirmed target's actual migration history before choosing the pending range.
Full-chain and incremental scripts are alternatives for different starting states; do not run both.
Apply migrations explicitly only to the confirmed development target; the application never
migrates on startup. The command below is for that separately authorized application step.

~~~powershell
dotnet ef database update --project src/Cosmix.Infrastructure --startup-project src/Cosmix.Api -- --environment Development
~~~

The API requires a configured connection string. Without it, resolving the authentication
repositories/DbContext throws `Configure ConnectionStrings:CosmixDatabase using user secrets
or environment variables.` even on a request intended to reach a health endpoint. Do not remove
this guard or point it at `master`. Configure the API's User Secrets as above, use the
`Cosmix.Api` Development launch profile, stop debugging and start again. Environment variables
override User Secrets, so also check for an empty or stale `ConnectionStrings__CosmixDatabase`
override. Missing or unreachable database tables fail readiness; startup never creates them.

## 5. Run

Press F5 with Cosmix.Api as startup, or:

~~~powershell
dotnet run --project src/Cosmix.Api
~~~

Local profile: https://localhost:7240 and http://localhost:5240 (HTTP redirects to HTTPS).
Web cookie login requires an HTTPS frontend too: use `https://localhost:5173` or
`https://localhost:3000`, which are explicitly allowed in Development CORS. Configure and trust
local certificates for each client/server separately; this task does not change OS trust.
Use the same hostname consistently, credentialed requests and the CSRF lifecycle documented
in Shared Authentication. HTTP frontend origins remain for non-cookie/reference development;
they cannot complete Strict-cookie browser login against an HTTPS API. Do not disable cookie
security or antiforgery to make a mixed-scheme setup work.
If a port is occupied, change your local launch profile and coordinate client URLs.
Visual Studio opens `/swagger` for interactive API documentation. It reads `/openapi/v1.json`
and supports entering a valid bearer token through Authorize; it does not create a login/token.
Both Swagger UI and OpenAPI are disabled outside Development. Open /health/live for the process,
and /health/ready for the configured Department database schema.
The project includes Cosmix.Api.http for Visual Studio's HTTP editor.

## 6. Understand authentication status

Shared employee mobile SMS OTP request/resend/verification, passwordless activation, rotated
refresh tokens, current-state bearer validation, session/device revocation and dynamic access
services are implemented locally. This is not the completed authentication rollout.

Read [Shared Authentication](Shared-Authentication.md) for exact routes, approved-phone
provisioning, configuration and client contracts, and [Verification](Shared-Authentication-Verification.md)
for checks performed and their limitations. `MobileAuthentication:Enabled` defaults to `false`.
Do not enable it until the migration, explicit operating settings, cryptographic keys and
reviewed SMS gateway/provider are configured and verified. Missing configuration returns
unavailable, not a fake successful login. No test-login endpoint or seeded account exists.

JWT validation checks the persisted account/session, security version, employee, client,
current eligibility and device state. Permission claims or role names do not authorize access:
permissions and record scopes are read server-side from the same role assignment.
Store the private JWT signing key and separate OTP secrets outside Git; never send them to clients.

Web email/password + SMS OTP, secure cookie/CSRF transport and initial Admin-set password
provisioning are implemented. Web tokens are not accepted as bearer headers; management uses
current persisted web SMS proof. Fixed Admin/SuperAdmin are web-only. HR/managers may use
mobile, but mobile sessions expose only self-service permissions and never satisfy web MFA.

The web/fixed-role and account-recovery migrations are applied on the independently confirmed
existing development target; see the current verification report before applying any further
migrations. No new database is needed. Password recovery, controlled phone recovery and the
one-time local first-SuperAdmin command are implemented. No actual SuperAdmin has been created:
approved real identity/evidence and private setup inputs are still required. Configure explicit
password/OTP settings and the selected SMS provider before enabling login.
`WebAuthentication:MinimumPasswordLength` must be explicitly chosen before initial credential
setup; default zero denies that command. Do not use a fabricated administrator token or approval
history to bypass those prerequisites.

Bounded live-SQL schema, rowversion, authentication transaction and two-context OTP checks
now pass; broader races/load, SMS delivery, browser/mobile integration and deployment acceptance
are still required. Follow the [Next Delivery Plan](superpowers/plans/2026-10-06-next-backend-delivery.md)
for the order of work and required approvals.

## 7. Start a task

~~~powershell
git switch develop
git pull --ff-only origin develop
git switch -c feature/authentication
~~~

Developer 1 starts with authentication/access contracts, then employee/HR features.
Developer 2 can start sites/shifts and pure attendance-rule tests in parallel, then
integrate protected attendance flows after the shared identity/employee contracts are ready.
Read Work-Ownership.md and SRS-Module-Map.md.

## 8. Add a migration without clashing

Agree one migration owner for each merge window. Do not both edit the model snapshot concurrently.
Merge latest develop into your feature branch before generating the final migration.
Never delete/rewrite an applied/shared migration; add a corrective migration.

~~~powershell
git fetch origin
git merge origin/develop
dotnet ef migrations add DescribeNextApprovedSchemaChange --project src/Cosmix.Infrastructure --startup-project src/Cosmix.Api --output-dir Database/Migrations -- --environment Development
~~~

Replace the example name with the actual future change. Do not generate another initial or
employee-master migration: the reviewed model migration already exists. Generate SQL for its
explicit previous-to-new migration range and review constraints/data-loss risks before applying.
Migration generation and database application are separate steps.

## 9. Commit and submit

~~~powershell
dotnet build Cosmix.Backend.sln --configuration Release
git status
git diff
git add <specific-changed-files>
git commit -m "Implement approved authentication flow"
git push -u origin feature/authentication
~~~

Replace placeholders with actual paths. Create a pull request into develop; the other
developer reviews it. The PM approves scope. Never force-push main/develop.
main is the reviewed release baseline; develop is the shared integration branch.
Branch protections and required reviewers must be configured separately by the repo owner.
