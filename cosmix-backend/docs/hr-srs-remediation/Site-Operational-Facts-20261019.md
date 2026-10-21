# Site operational factual corrections

## Pre-edit coordination

Owned contract change required: `SiteOperationalDetailsResponse.StartsOn` becomes
`DateOnly?`, retaining a missing stored start date instead of inventing today.
Existing field names remain compatible. `WorkedMinutesSoFar` remains only elapsed
minutes since the interpreted open-session start, never approved work or pay.

The repository consumes already registered `TimeProvider`,
`ISiteRosterRepository` and `ISiteGeofenceRepository`. No shared DI change is
required. Native parent messaging is unavailable in this subagent; the app tool
rejected sending to the native ancestor. This notice precedes production edits.

## Changes and retained semantics

- One injected clock snapshot supplies Asia/Karachi's business date and elapsed
  session minutes. Employment starts are inclusive and ends exclusive.
- Ambiguous current employment, including overlapping membership at another
  site, returns `employee_assignment_history_conflict` (409), before dictionary
  construction. Supervisor employment follows the same dated rule.
- Current geofence uses the existing effective-version reader. Each check-in's
  raw coordinates are evaluated only against its retained same-site boundary,
  using the existing accuracy-aware evaluator. Missing boundary/accuracy or
  unusable accuracy yields null distance/containment; raw evidence remains.
- Site shifts use the existing immutable roster state/resolver, including
  withdrawals, temporary replacement/cancellation/supersession and effective
  versions. Legacy individual assignments do not supply site schedules.
  Mutable shift activation does not erase retained effective schedules.
  Missing or conflicting schedules raise existing scheduling conflicts instead
  of fabricating zero minutes. This introduces the existing site-roster schema
  readiness guard to this read; incompatible deployments return its existing 503.
- Only latest session revisions can appear open. Later closed or reassigned
  revisions suppress earlier open rows. Future starts are excluded. The existing
  trailing 24-hour reporting window remains; it is not a maximum duty length.
  Worker count is distinct employees; session rows remain separate.
- A linked punch belonging to another employee/site or action raises
  `site_operational_punch_history_conflict` (409).

`CheckInTimeUtc` retains the interpreted session-start meaning, which can differ
from a corrected session's original punch time. Worker designation comes from
current effective site membership; master names/status are current metadata.
The endpoint is a current overview with retained check-in evidence, not a general
historical/as-of API. No live-location, current-presence, approved work, payroll or
duty-limit claim is introduced. Controller descriptions still use older "live"
wording; controller ownership is outside this change.

The existing `SiteMasterService`/`OrganizationOperation` current web MFA and
All-scope `hr.sites.read` path remains unchanged. No shared DI, controller,
authorization, Domain, migration or schema files were edited.

## Verification and parent handoff

Standalone source: `VerifySiteOperationalFacts.cs` (31 scenarios).

RED executed against
`D:/CosmixVerification/hr-backend-20261019/hr-corrections-build04/bin/Cosmix.Api/release`:
**3 passed, 28 failed**, with expected factual failures including the original
duplicate-key exception. Compiler succeeded. Exact retained output:
`D:/CosmixVerification/part-c-parent-20261013/managed-40818fec6389416b9f92ef68097d8d1c/VerifySiteOperationalFacts.log`.

The standalone harness executes the actual compiled repository LINQ over
synthetic entities, with a site-history interface stub for the schema-gated
reader. A connection interceptor forbids any database access. No data is saved.
It proves projection/resolution behavior, not SQL execution or HTTP acceptance.

`git diff --check` passed for the two product files. The parent reported the
coordinated `hr-production-build01` Release build succeeded with **0 warnings and
0 errors**. Its frozen Release inputs are recorded in the verification run's
`inputs.json` as
`D:/CosmixVerification/hr-backend-20261019/hr-production-build01/bin/Cosmix.Api/release`.

Parent-executed `VerifyManaged.ps1` GREEN: **31/31 scenarios passed, 0 failures**.
The retained site log was read back and confirms this exact result:
`D:/CosmixVerification/part-c-parent-20261013/managed-50675dece1184ad48fbc557b89f8660e/VerifySiteOperationalFacts.log`.
The parent build and standalone GREEN handoff are complete. This remains offline
projection/resolution verification with the limits above; it does not establish
SQL execution, authenticated HTTP, live tracking or approved work duration.

Reproduction against that frozen Release:

```powershell
& ./docs/attendance-gap-c/VerifyManaged.ps1 `
  -ReleaseDirectory 'D:/CosmixVerification/hr-backend-20261019/hr-production-build01/bin/Cosmix.Api/release' `
  -Sources 'docs/hr-srs-remediation/VerifySiteOperationalFacts.cs'
```

No database connection, host, external provider or migration is part of this work.
