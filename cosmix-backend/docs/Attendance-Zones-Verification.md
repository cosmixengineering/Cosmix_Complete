# Zones verification record

All work is local. Migration 16/17 schema is applied only to approved isolated `Cosmix_Testing`.
No zone duty fixtures, production permission grants, face-provider calls or live employee tracking were performed.

Actual 12 October checks: geometry/configuration **16**, real monitoring/administration service with
synthetic I/O **52**, actual SQL query/model/migration **27**. The combined **587** HTTP assertions cover **107**
new Attendance and **58** existing employee operations, including all **18** zone operations.
These are offline/synthetic and anonymous checks, not database or device verification.

Coverage and outstanding execution:

- `docs/attendance-completion/VerifyZoneService.cs`: actual application service with synthetic in-memory adapters; current identity, notice, checkpoint conflicts, retries, changed GPS on reused key, duty/transfer/expiry gates, location visibility, self/reviewer outbox staging, privacy projection and strict sample JSON fields.
- `docs/attendance-completion/VerifyZoneQueries.cs`: exact production SQL Server query translation behind a connection-rejecting interceptor, plus EF provenance/retention/checkpoint constraints. It does not open a connection.
- `VerifyZoneLocationStorage.cs --synthetic-private-files` passed **43** checks with a fresh
  owner-only OS-temp directory and disposable key. It covered encryption/reopen, purpose/identity
  binding, expiry, deletion, orphan indexing, corrupt evidence, scan bounds and cross-adapter lease
  cancellation. Owned fixture cleanup completed; no SQL, accounts, retained keys or configuration changed.
- `VerifyZoneStoreCoordination.cs` first reproduced the writer bypassing the cleanup gate, then
  passed **2** wait/cancellation checks without filesystem fixtures after the fix. The OS-held
  cross-adapter lease was also exercised by the real-file run above; true separate-process crash
  recovery and multi-instance operation are not claimed.
- Final coordinated Release build and API authentication/OpenAPI regression owned by the parent.

Prepared checks are not marked passed without executed output. Real SQL atomicity, competing processes,
mobile device identity, management-approved runtime configuration and private-storage recovery remain
outstanding. Monitoring and retention flags default to false; schema application alone does not enable them.
