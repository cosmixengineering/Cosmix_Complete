# Isolated attendance acceptance scope and execution

The user authorized this prepared bounded run during the 12 October pre-push pass.
Schema is now at migration 17 in `Cosmix_Testing` only. See [current verification](Verification.md)
and [pre-push readiness](../Pre-Push-Readiness.md) for final results and limits. The plan below
documents the allowed scope; a manifest hash identifies reviewed artifacts and is not itself approval.
These scripts are retained machine/run-specific acceptance tooling, not general setup commands.

## Retained baseline

Only Windows-authenticated `(localdb)\MSSQLLocalDB`, **Cosmix_Testing**, retained run
`ACC202610098E9C6465`, private root
`C:\Users\Affan.Ahmed\AppData\Local\Cosmix\AccessAcceptance\20261009`.

Read-only inventory on 9 October confirmed thirteen migrations, latest
`20261009064639_AddDirectSiteSchedulingAndTemporaryAmendments`, exactly three synthetic employees
all hired 2026-10-09, two assignment rows from that date, no period locks and no outbox jobs.
The initial sandbox SQL connection timed out; the same SELECT-only inventory succeeded under
reviewed local execution. No schema/data was changed. Private configuration files were parsed
without printing credentials. This is inventory evidence, not behavioral acceptance.

Preserve original role assignments, HR's future transfer, SiteA's recorded revocation, receipts,
private objects and keys. Never rerun the initializer, backdate hiring or create a fake policy-ready
historical month. Unexpected data/migrations stop the run.

## Prepared bounded run

1. `Invoke-AttendanceWebAcceptance.ps1 -Mode Inspect` performs offline manifest, private-file,
   ACL, fixed target and exact permission-profile checks. It makes no SQL or HTTP request.
2. `Upgrade-AttendanceAcceptance.ps1` verifies the baseline and retained revoked grant with
   SELECT-only checks; rejects lifecycle changes that could trigger reconciliation; creates a
   new private run child; takes a COPY_ONLY/CHECKSUM backup and runs RESTORE VERIFYONLY.
   It repeats baseline/hash checks before executing the exact reviewed incremental SQL.
   The reviewed incremental script covers migrations 14–17.
3. `Run-AttendanceAcceptance.ps1` owns the upgrade, guarded HTTPS host and cleanup. It binds
   runner/SQL/settings/Release hashes, rechecks lifecycle/job state, launches hidden on
   `https://localhost:7249`, and stops only its PID with matching start time. Existing settings
   and keys are reused unchanged; attendance jobs and actual zone monitoring stay disabled.
   Existing LocalTesting identity guards, password/MFA/CSRF and current authorization remain.
4. Through supported SuperAdmin APIs, create exactly three new named roles/assignments:

| Recipient | Scope | Permissions |
|---|---|---|
| Synthetic Admin | Employee: retained HR employee | corrections.submit/review/decide, summaries.read/calculate, reports.read/export |
| Synthetic Admin | All | month-locks.read/finalize only |
| Synthetic HR | Self | corrections.self, summaries.self |

All permission names carry the `attendance.` prefix. Global month-lock operations genuinely
require All scope; no general All access is introduced. Existing definitions stay unchanged,
and the old SiteA roster grant stays revoked. Fresh MFA login follows access invalidation.

5. Execute only the factual web slice: at most one summary revision for 2026-10-09; exact and
   changed-payload retry and stale revision behavior; missing CSRF, self identity and denied scope;
   correction queue; carry-forward with absent source returns conflict; current month lock remains
   not ready. No raw punch, duty, ready-policy or lock fixture is seeded. Report requests are
   expected unavailable while their worker is disabled.
6. Retain sanitized append-only results and private receipts. Never blindly repeat or automatically
   downgrade a partial run. Never overwrite original host metadata or stop an unrelated listener.

`AttendanceAcceptanceManifest.example.json` deliberately contains placeholders. Finalize its run ID,
complete migration target/count/script, hashes, permissions and business date; review code and
successfully inspect that exact manifest before execution. Concrete manifests are ignored local files;
the reusable example remains in source. Original partial journals/backups must never be overwritten.

## What this does not establish

- Real provider/liveness, mobile accepted Check-In or device-to-HTTP acceptance.
- Positive policy-ready lock or historical carry-forward SQL success: retained fixtures have no
  pre-October employee history and no approved local calculation engine.
- Worker lease/restart/rollback, downloads, competing SQL writes or biometric retention.
- OT/zone SQL success until a separately explicit extension includes their fixtures and inputs.
- Production SMS, scanner, monitoring, provider, load or client/UAT readiness.

Standalone synthetic service/model/query checks are reported separately. No fake authentication,
provider or policy is installed in production DI. No test project, new account, historical rewrite,
key rotation or paid/external call belongs to this run.

## Concrete runs (12 October)

The initial manifest was hardened before execution to pin the HTTP helper and explicitly require
disabled zone retention/storage despite private configuration precedence. Executed run
`ATT202610125683251FC7EF`, manifest SHA256
`C52BC92EFF2B18571B99BFFE9D2CCF2C02B5BAE0AE16B172E696A1EF77B576FD`, took a verified backup,
applied migration 14, then stopped on a new-column CHECK compilation error in migration 15.
No roles or business fixtures had been created. Its backup/journal were preserved.

Only unapplied migrations 15/16 were fixed, without changing CHECK predicates. A rollback-only
15–17 SQL test passed. Reconciled run `ATT20261012R45B3DDA713`, manifest SHA256
`5F437443725510C86FA1309639DEA6E7700CD0E3AC08D17E940FF058AC2FBCE3`, verified the exact 14
baseline and original backup, took a fresh verified backup, then applied only 15–17. It created
the three described synthetic roles/assignments and verified old-session revocation. A later web
assertion required correction because the denied employee had no historical assignment and
returned explicit 409 before scope evaluation. Its journals/grants were preserved for the bounded
checks-only continuation. The final continuation result is linked above.

Checks-only continuation manifest SHA256:
`5C2B3BEA1A7722BDF880B3C508E0ED2D14BAFC816760D55227A809A33DE9688C`.
It reused all grants/schema and wrote exactly the one permitted summary. Its separate
`web-continuation-1` journal recorded 39 successful assertions, then a global month-preview
409 because synthetic administrators have no historical duty assignments. No finalize call
or fake historical fixture followed. A separate authenticated read confirmed the error code.
Independent persistence verification passed 59 SELECT/private-read checks and decrypted three
protected objects. The partial HTTP journal remains partial. Future harness handling of this
exact fixture limit was corrected without rerunning completed writes or overwriting evidence.
