# Isolated authenticated acceptance

These manually invoked harnesses are for the explicitly approved synthetic
`Cosmix_Testing` run. They are not test projects or application startup hooks.
The operator owns database setup, migrations, backups, fixture preparation,
credential handling, host lifetime and access grants. Nothing here cleans up or
recreates existing data.

The current scripts restrict fixture and evidence paths to
`C:\Users\Affan.Ahmed\AppData\Local\Cosmix\AccessAcceptance\20261009`.
Credentials, cookies, tokens, SQL identifiers, protected references and decrypted
evidence remain private. Do not copy the private evidence into the repository.

## HTTP workflow

`Invoke-IsolatedAcceptance.ps1` receives real authenticated clients from
`docs/access-provisioning/LocalAcceptanceHttp.ps1`. The setup wrapper is
`docs/access-provisioning/RunAuthenticatedAcceptance.ps1`.

- `Client`: fixed Admin with separately granted shift administration and target
  site roster permissions; fixed role status alone grants no scheduling access.
- `AdminClient`: fixed Admin for geofence administration; may be the same client.
- `DeniedClient`: HR with target-site roster read and self service, used to prove
  another-site/employee and fixed-Admin geofence denials.
- `SelfClient`: the HR employee named by the private fixture manifest.

`Run` refuses an existing evidence file. It creates three synthetic shifts,
submits two identical assignment requests concurrently, checks immediate replay,
changed payload, stale timeline and missing CSRF, then creates, supersedes and
cancels temporary shifts. It proves a fresh root can follow a cancelled root,
active original pins block publication, cancellation allows safe publication,
normal replacement/end records remain append-only, and employee schedules resolve
through historical site membership. Optional clients add self/scope and geofence
checks. All successful request bodies and responses are checkpointed privately.

`Resume` supports only the observed early checkpoint: exactly three shifts, one
assignment, four successful receipts and no temporary change. It additionally
checks that actual history matches those saved identities. It recreates no rows.
This handles the initial harness CSRF-header issue without resetting fixtures.
Other partial states require deliberate inspection; there is no generic rerun.

`Replay` requires completed evidence and current authenticated clients, typically
after restarting the real host. It resubmits the original exact request/key pairs
and compares the original frozen responses. `DeniedReplay` requires the original
actor to retain shift management while its target-site roster grant has been
revoked, followed by a fresh login. It checks site writes and shared publication
return 403 instead of releasing protected old results. The runner itself never
changes roles or grants.

## Persisted evidence

`VerifyIsolatedPersistence.cs --help` is a compile-only entry point and reads no
files or database. With `--evidence <private scheduling-evidence.json>` it opens
only integrated-auth `(localdb)\MSSQLLocalDB` / `Cosmix_Testing`, using the private
setup settings and completed evidence. It performs no database or file writes.

The verifier checks actual SQL versions, assignments/endings, temporary parent and
exact staff manifests, amendment graph, and completed/rejected receipt counts.
Through the real encrypted private store it independently reads every scheduling
receipt, original audit and written temporary/amendment proof. It compares them
with accepted HTTP results, validates preserved publication site scope and both
previous/current affected manifests, and emits only aggregate pass counts.
Geofence results are checked against their original SQL organization journal.

`New-IsolatedEvidenceSql.ps1 -EvidencePath ... -OutputPath ...` optionally generates
a separate bounded read-only SQL script in the same private directory. It never
opens a database connection. The operator may execute that script independently;
it returns aggregate counts and does not select private references or plaintext
evidence.

Successful compilation or script parsing alone is not behavioral evidence. Record
live HTTP, SQL/decryption, restart replay and revocation outcomes separately.
