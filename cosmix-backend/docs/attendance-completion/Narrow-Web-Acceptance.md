# Prepared narrow attendance acceptance runner

The user authorized the bounded 12 October pre-push run. Current execution results and any
reconciled continuation are recorded in [verification](Verification.md). Parser checks alone are
not HTTP/SQL acceptance. Do not run the example manifest: placeholders intentionally fail validation.
The concrete manifests are machine-specific ignored files; retained scripts do not authorize future runs.

`Invoke-AttendanceWebAcceptance.ps1` has an offline `Inspect` default. `Execute` additionally
requires the exact human-approved manifest SHA-256, matching completed schema evidence, the
owned localhost host and the retained three synthetic credentials. The hash identifies the
reviewed work; it does not itself grant approval. It creates a one-use marker and sanitized
JSONL journal before the first HTTP mutation. Partial runs require manual reconciliation;
the script refuses an automatic rerun and never resets data or overwrites old evidence.

## Exact proposed additions

SuperAdmin uses supported access APIs to create three fresh run-prefixed custom roles and
three assignments. Existing roles, assignments, credentials and employee history are unchanged.

| New role | Recipient and scope | Permissions |
|---|---|---|
| `RUN_EMPLOYEE` | Existing Admin, only retained HR employee | Corrections submit/review/decide; summaries read/calculate; reports read/export |
| `RUN_MONTH` | Existing Admin, All | Only month-locks read/finalize; global month APIs genuinely require global authority |
| `RUN_SELF` | Existing HR, Self | Only corrections.self and summaries.self |

The assigned permissions use the exact `attendance.*` codes listed in the example manifest.
The original SiteA roster grant remains revoked. No roster/site permission is added. The script
checks that retained revoked assignment before and after the feature checks. Granting the
new roles invalidates prior Admin/HR sessions; fresh real MFA logins are required and verified.

The only successful attendance business write is at most one factual summary for the existing
HR employee on **2026-10-09**, with `expectedRevision=0`, no accepted/open duty fixture and no
prior saved revision. Its policy/payable/late/overtime outputs remain null. The service writes
the protected input, audit evidence and idempotency receipt through real SQL transactions and
the existing encrypted private store. The same key must replay the same revision; changed
reason with the same key and stale revision with a fresh key must conflict. The runner also
exercises self identity, management scope denial, missing CSRF, a scoped review queue, absent
carry-forward source lock, and global month readiness/finalize denial.

`AttendanceJobs.Enabled` stays **false**. The current report service checks that option before
queuing, so export requests correctly return `503 attendance_export_worker_not_enabled`.
This phase does not claim report generation, queued report receipts, notification delivery,
competing HTTP writes, mobile authentication, actual face verification or payroll acceptance.

## Prerequisites and phase contracts

1. Finish all current local schema work, build and offline checks. Pin the final migration IDs,
   reviewed source-to-target SQL script and its hash. The final range is 13 through 17; the
   reconciled partial run uses the reviewed 14-to-17 suffix, never an unbounded update to latest.
2. Prepare a fresh manifest in `docs/attendance-completion`, derived from
   `AttendanceAcceptanceManifest.example.json`. Fill the baseline fixture/access/settings
   hashes without publishing their credentials. Pin the final web runner and HTTP helper hashes and exact
   `existing-three-attendance-factual-v1` role profile. Review the additional grants and single
   factual summary write with the user.
3. Only after separate approval, root's schema phase verifies the exact retained synthetic
   baseline, backup and migration result. New evidence belongs to
   `ExistingPrivateRoot/runs/RunId`; preserve all retained objects and key files. The schema
   receipt `schema-result.json` must bind Database, RunId, TargetMigration, ManifestSha256
   and `FixturesChanged=false`, `PermissionsChanged=false`, `KeysChanged=false`.
4. Start only the new owned HTTPS localhost host using the original private settings and keys.
   Explicitly pin jobs, zone monitoring, retention and zone storage to false in the host environment,
   and validate they are absent/false in the higher-priority private JSON. Record a **new** child
   `host.json` using CreateNew with ProcessId, actual process StartedAtUtc, BaseUri, RunId,
   SettingsSha256 and AttendanceJobsEnabled=false. Do not overwrite the prior `host.json` or
   stop a process selected only by port. Review pending lifecycle work before starting the
   existing lifecycle worker as well. The root host owner stops only this verified process.
5. Execute the web runner against the reviewed manifest hash. The existing guarded local OTP
   helper obtains genuine web MFA for exactly the retained SuperAdmin/Admin/HR accounts.
   Cookies, OTPs, CSRF material and passwords stay in memory and are never journalled. New
   roles/assignment IDs and the summary identity are retained in the private child journal.
6. Root verifies retained schema/business rows and private references after the HTTP run,
   confirms no period, correction, carry-forward, raw punch or outbox delivery was invented,
   and reports actual results separately from offline assertions. The script does not issue
   raw SQL, create a host, upgrade schema, enable workers or delete/revoke any old grant.

## Historical success remains a separate fixture decision

The three existing employees were hired October 9. Existing rosters start later, with an
October 29 transfer, and no locked source period exists. The current fixture cannot honestly
success-test a past manual duty, completed global month or historical carry-forward. Do not
backdate their hiring, rewrite historical assignments, bypass the clock, seed fake policy
outputs or fabricate a direct-lock success to clear this limitation.

Any later historical fixture proposal must separately list fresh synthetic employment and
approved onboarding provenance, historical site assignment, immutable shift/geofence versions,
closed source session/correction evidence, exact protected summary/session links and an actual
valid source lock manifest. A new login account is unnecessary for management-scope testing,
but any new employee/fixture is outside this narrow approval. Real final-lock success also
requires the approved policy calculation to be available; a synthetic policy substitute is
not production acceptance. These additions are not performed by this runner.
