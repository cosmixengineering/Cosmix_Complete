# R1 exact-job dispatcher

Implementation ownership: only the new
`src/Cosmix.Api/Development/HrReportTestingDispatcher.cs` and the standalone
verification files listed below. The R1 host/profile/boundary and runner belong
to the parent and host agent. No production repository, worker, handler or
authorization implementation is changed by this dispatcher.

## Integration

```csharp
public sealed class HrReportTestingDispatcher(
    CosmixDbContext db, IPrivateFileStore files,
    TimeProvider clock, HrReportExportService exports)
{
    public Task<bool> DispatchAsync(
        Guid jobId, AuthenticatedAccount actor, CancellationToken ct);
}
```

Register scoped only in `HumaHrReports20261020R1`. Resolve the dispatcher in a
**fresh DI scope** so its context and the export service dependencies share one
clean unit of work. Keep the existing `IDurableJobRepository` registration.
`HrReportExportHandler` and its preparation/apply interface are public; this
integration needs no reflection, friend assembly or visibility modification.

The host boundary must capture the successful 202 response ID, require the exact
replay to return that same ID, and serialize/latch a single invocation after a
successful authenticated status-controller read. Pass the actual current HR
web-MFA actor/session. Never accept a dispatcher target from an extra endpoint,
arbitrary configuration ID or an unbound status request.

Current host integration returns the first captured `pending` status response
after dispatch finishes. Read status again and require lowercase `completed`,
the exact ID and `TotalCount == ReturnedCount == 1`. Processor `true` means an
attempt was handled, including recorded failure; it is not proof of export
completion. Any exception or ambiguous result stops the batch without retry.

Private configuration must have `AttendanceJobs:Enabled=false`. The R1 host
removes hosted workers and replaces the in-memory options with an enabled,
single-attempt configuration. Application name is `Cosmix.HrReports.R1`, private
file key reference `hr-reports-r1`. Parent startup guards own the absent/new root,
25-migration baseline, permission preflight, authentication and bounded HTTP
sequence. The dispatcher does not create directories, accounts or permissions.

## Exact claim

The public dispatcher verifies the pinned named-pipe connection and actual
server/database/owner/operator/family/PID/files/transport identity before reading
the exact report ID and its exact `(message type, job-ID deduplication key)`.
The decorator independently rechecks that target inside its claim transaction.

`HrReportAcceptanceJobRepository(db, target, capture, files, clock)` implements
`IDurableJobRepository`, constructing the normal concrete repository with that
same context. Capture binds job ID, message ID, full requester identity, canonical
normalized employee-only filter, protected scope reference, server correlation,
and request timestamp. It admits only the fixed synthetic HR identity,
`employee-master`, PDF, today's Karachi date, and exactly one selected employee.
The retained JSON is purpose-read through the real private store, length/hash
checked and checked for duplicate/case aliases and unknown fields.

Claim selects by exact job and message primary keys, never `ClaimNext` or a queue
scan. Only an untouched zero-attempt Pending message with no lease/completion/
failure metadata qualifies. The serializable EF transaction uses the mapped
rowversion concurrency predicate, saves exactly that one tracked row, and commits
before returning a claim. Cancellation, stale rowversion, invalid bindings,
ambiguous commit and second use stop without a fallback candidate or retry.
Preparation uses the unchanged public HR handler; completion/failure delegate to
the existing repository after verifying the issued claim and database identity.
The processor receives only that handler and `MaximumAttempts=1`.

The original handler stages the report-ready notification intent as part of
completion. No notification handler or global worker is run here, so that
intent remains pending. Actual authorization/session/source rechecks stay in the
original HR handler; this acceptance adapter does not substitute for them.

## Offline verification

- `VerifyHrReportAcceptanceJobRepository.cs`: 89/89 checks against real domain
  types and the real SQL Server EF provider, with a non-network ADO proxy.
  Covers exact predicates, target/job/owner/session/filter/payload mismatches,
  rowversion conflict, cancellation, commit failure and lost acknowledgement,
  single-use behavior, real repository completion and stale-lease failure checks.
- `VerifyHrReportTestingProfile.cs`: 126 native frozen-API checks for the R1
  configuration/boundary, empty hosted-service set, unchanged repository
  registration, exact filters/formats, request budget, anonymous denial,
  lowercase 202 capture/replay, changed replay and uncertainty latch, and bound
  login-challenge response handling.
  Reflection calls existing internal guards on synthetic contexts only; it
  neither changes private fields nor bypasses production handler authority.
- `VerifyHrReportAcceptanceJobRepository.ps1`: standalone CSC build/run on `D:`,
  hashes copied inputs, no test project, no assembly patching.

```powershell
./docs/hr-srs-remediation/VerifyHrReportAcceptanceJobRepository.ps1
./docs/hr-srs-remediation/VerifyHrReportAcceptanceJobRepository.ps1 -NativeDispatcher `
  -ReleaseDirectory D:/CosmixVerification/hr-backend-20261020/hr-report-r1-host-check/bin/Cosmix.Api/release
./docs/hr-srs-remediation/VerifyHrReportAcceptanceJobRepository.ps1 -ProfileChecks `
  -ReleaseDirectory D:/CosmixVerification/hr-backend-20261020/hr-report-r1-host-check/bin/Cosmix.Api/release
```

Retained offline evidence:

- Adapter: `D:/CosmixVerification/hr-backend-20261020/report-target-offline-33ccea7b9b774f3ba6814cb9ea72ead0`
- Native built adapter: `D:/CosmixVerification/hr-backend-20261020/report-target-offline-9d3dc6484ad741d19f773538973f7ef6`
- Profile: `D:/CosmixVerification/hr-backend-20261020/report-target-offline-71d2c9b0fbb64108b3aff86fd3f166ab`

For build11, pass its frozen release directory with `-NativeDispatcher` and then
with `-ProfileChecks`. These modes compile only the standalone test against the
built implementation. Default mode overlays the owned dispatcher source against
the pre-R1 build09 and must not be used with an API already containing those types.
An independent Release build passed with zero warnings/errors at
`D:/CosmixVerification/hr-backend-20261020/hr-report-r1-dispatch-review`.
The parent's final build11 still owns the final combined source snapshot.

Adapter compilation reports two CS1701 reference-unification warnings from the
existing SqlClient/.NET reference combination; all checks pass. Profile
compilation is clean. These checks do not execute SQL, start a host, write private
files, render a live report, or prove actual SQL locking/concurrent-worker
behavior. The profile suite does not simulate the full password/MFA/status
success sequence or rerun the legacy C03/C04/V1 suites.
