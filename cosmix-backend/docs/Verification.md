# Foundation verification

## Current verification

The project manager requested removal of all automated test projects and files
on 05 October 2026. The solution now contains only Domain, Application,
Infrastructure and Api. The GitHub workflow restores locked dependencies and
builds the solution; it no longer runs automated tests.

Run from the repository root:

~~~powershell
dotnet restore Cosmix.Backend.sln --locked-mode
dotnet build Cosmix.Backend.sln --configuration Release --no-restore
~~~

Build success confirms compilation, not runtime behavior or full SRS acceptance.

## Historical checks

### Initial model increment on 05 October 2026

- Release build passed with zero warnings/errors; four application projects retained.
- Locked packages were restored unchanged. The isolated execution environment used a local
  feed fetched from NuGet with certificate-verified HTTPS because Windows TLS credential
  access was unavailable. Vulnerability auditing was disabled for that offline restore;
  this increment is not a package vulnerability assessment.
- EF generated AddInitialHrAttendanceModels and SQL successfully; has-pending-model-changes
  reported no model/snapshot differences. SQL Server execution was not performed.
- A temporary verification program outside the repository validated 17 mapped types and
  all 32 combinations of four immutable entity types, update/delete and four SaveChanges overloads.
  It also checked incomplete drafts, atomic rejected draft edits, normalized email, overnight
  shifts, unverified punch evidence, coordinate rejection, disabled-auto-detection protection
  and rejection of caller-supplied employee numbers.
- Development API smoke checks returned liveness 200, OpenAPI 200, unauthenticated Department
  read 401 and unconfigured database readiness 503. Only the existing Department endpoints
  were advertised. The local smoke process disabled the inaccessible Windows EventLog sink;
  repository logging settings were unchanged.
- Source review found no important actionable issue. No automated test projects were recreated.

### Original foundation

Before removal, the initial foundation passed 40 automated checks and a separate
SQL Server LocalDB and HTTPS API smoke check. These results belong to the original
foundation revision, not to an active test suite in the current checkout.
The temporary verification database and API process were cleaned up at that time.
The original code is retained in normal Git history.

No full HR/attendance workflow, production deployment, face provider, identity
issuer, mobile/web client or production load target is certified by these checks.
