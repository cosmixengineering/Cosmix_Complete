# Future shift publication — 9 October 2026

The existing `POST /api/v1/hr/shifts/{id}/versions` route now validates a future
immutable version against the active site-only schedule before saving it. Its
request and response contract are unchanged. It still requires a date strictly
after today and the latest version; it does not insert versions into history.

## Authority and immutable evidence

Publication requires `attendance.shifts.manage` with All scope and
`attendance.rosters.manage` for **every affected site** (Site or All scope). A
permission for one site does not authorize the other sites using the same shift.
Authorization, impact reads, schedule validation, the append, audit and receipt
remain inside the existing serializable scheduling operation.

The operation stores the original additional permission requirements inside its
protected result envelope. Replay rechecks both current impact and those original
site requirements, even if a later assignment ending removes a site from the
current impact query. Existing v1 receipts cannot establish that extra authority
and fail closed on the new publication path. Other scheduling operations retain
their existing receipt format.

Active temporary changes retain their exact original/replacement version IDs.
A publication that would change an active temporary change's original returns
`temporary_change_base_changed`; it never changes the written evidence or frozen
staff manifest. A canceled original no longer blocks publication, and an exact
replacement-only reference never inherits a newly published version. Retired
individual assignment history remains stored but does not veto the active
site-only publication path.

## Impact validation and limits

- Discover at most 128 affected sites. Applicable legacy approvals and unsupported
  population kinds fail closed; ended intervals do not become active again.
- Read each site's bounded immutable schedule, then validate its assignment,
  schedule-version and active-temporary boundaries with the proposed version.
- Recheck site availability, occupied final overnight dates and locked periods.
  Existing global future-lock protection on shift publication is retained.
- Load at most 2,000 HR membership rows per affected interval, including all other
  site/office rows for its employees. Reject overlapping HR history and overnight
  transfers on either side of the site's membership boundary. Exact-midnight and
  rest-day endings do not occupy the next date.
- The site repository's existing bounded timeline reads and direct-schema guard
  remain in force. Truncation returns a conflict; no partial impact is accepted.

`SiteRosterImpactValidator` supplies the shared timeline/membership check for
publication and direct site roster mutations. Publication supplies the proposed
version when an adjacent employee membership requires reading another site.

The service does not change employee eligibility, HR history, wages, punches or
temporary evidence. Runtime schema and permission provisioning prerequisites are
documented with the site scheduling module.

## Evidence

`VerifyPublication.cs` exercises real `ShiftService`, `ShiftVersionPublication`,
`SchedulingOperation` and site rules with synthetic repository/authentication/file
collaborators. It creates no test project and makes no SQL connection.

Before implementation, its safe assigned-site scenario failed with
`shift_has_group_or_temporary_assignments`, and its missing-roster-authority
scenario received that blanket 409 instead of a permission denial. The initial
compile-only attempt found an unused local harness helper; after correcting the
harness, these were the two observed behavioral failures.

The dedicated scenarios cover immutable effective dates, each affected site's
permission, protected retries, active original pins, canceled originals, pinned
replacement-only use, both schedule overlap boundaries, outgoing/incoming
transfers, ambiguous HR history, exact midnight/rest days, site availability,
schema readiness, locks and stale versions. The older service harness now asserts
that retired individual history cannot veto publication; its existing individual
service/history checks remain.

Actual runs for this slice:

| Check | Result | Boundary |
|---|---|---|
| `VerifyPublication.cs`, after independent-review fixes | 41 passed; zero failed scenarios | Real service/operation and site rules with production-shaped synthetic IO, including revoked original-site access after later ending and HR boundary cases |
| `VerifyQueries.cs` | 10 passed; zero SQL connection attempts | Actual repository query builders translate to bounded SQL, including exclusive endings and other-site membership rows |
| Existing `../shifts-rosters/VerifyServices.cs` | 135 passed | Earlier run before the shared-validator extraction; final integration reruns belong to the integration record |

Independent review found that the first membership fixture returned null when no
HR row existed, whereas production throws `employee_assignment_unavailable`.
The stricter fixture reproduced failures for first hires and harmless final
assignment endings. The shared validator now calls the repository only when a
covering HR row exists; an office row with a null site still receives validation.
Regression cases accept daytime, exact-midnight and rest-day final duties, retain
overnight rejection, and propagate invalid office/category failures.

Review also exposed an excluded `DateOnly.MaxValue` transfer boundary. A duty on
the preceding valid business date could spill into that final date undetected.
That regression first accepted publication unexpectedly. Membership-boundary
validation now includes the final date and still resolves duty only on its valid
preceding business date. The combined stricter regression run had six failed
scenarios before these fixes and zero afterward.

The final coordinated Release build is recorded by the integration owner. These
synthetic checks do not prove SQL query execution,
live rowversion/locking behavior, rollback, authenticated HTTP, metadata visibility
or encrypted-disk replay. No database, migrations, snapshot, branch or Git remote
was changed by this publication slice.

Run sequentially with cached dependencies:

```powershell
$env:TEMP = Join-Path $PWD 'artifacts/site-geofences-temp'
$env:TMP = $env:TEMP
dotnet run --file docs/shift-publication/VerifyPublication.cs --configuration Release --property:RestoreSources=$PWD/src/Cosmix.Application --property:NuGetAudit=false --disable-build-servers -m:1 -tl:off
dotnet run --file docs/shifts-rosters/VerifyServices.cs --configuration Release --property:RestoreSources=$PWD/src/Cosmix.Application --property:NuGetAudit=false --disable-build-servers -m:1 -tl:off
dotnet run --file docs/shift-publication/VerifyQueries.cs --configuration Release --property:RestoreSources=$PWD/src/Cosmix.Infrastructure --property:NuGetAudit=false --disable-build-servers -m:1 -tl:off
```
