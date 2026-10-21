# Operational Employee Records

The public exports are `EmployeeAssetsPage`, `EmployeeSimsPage`,
`EmployeeCertificationsPage`, and the embeddable
`EmployeeRecordsWorkspace({ employeeId, kind })`. Kind is `Asset`, `Sim`, or
`Certification`. Feature CSS is imported locally.

Parent composition mounts `/hr/employees/:employeeId/assets`, `/sims`, and
`/certifications`, labelled Assets, Company SIMs, and Certifications. Navigation
uses `recordPermissions(kind)`: current All-scope `hr.employees.read` and the
family's actual read grant. Request and review controls use the independent
family request/review grants. The server checks MFA, fixed Admin assignment,
employment, approved policy, evidence ownership, scanner state and rowversions.

## Transport

All paths below use the existing authenticated `/api/v1/hr/employees` prefix:

- GET `/{employeeId}/{assets|sims|certifications}` and the corresponding `/history`.
- GET `/{employeeId}/assets/{custodyId}/history`.
- GET/POST `/{employeeId}/records/{Asset|Sim|Certification}/requests`.
- GET `/{employeeId}/records/{kind}/requests/{requestId}`.
- POST that request's `/cancel` or `/decision`.
- GET `/{employeeId}/current/basic` for the bound current employee rowversion.
- GET `/{employeeId}/documents` only with the existing document-read grant.

There is no permission, credit-tier, receiver-account or policy discovery API in
this slice. Approved credit-tier and receiving-account references are explicit
inputs, validated on the server. Without document-read access an evidence GUID
can be supplied; no private file metadata/download permission is inferred.

Explicit historical views disable mutations. Assets use a UTC instant;
repository custody includes an issue at/before that instant and a return strictly
after it. The service masks later return facts to null. SIMs use their actual
effective-date/non-returned-successor contract. Certification expiry is inclusive
on the expiry date. When `asOf` is omitted the server selects its own current
business date; the client does not reinterpret server expiry using browser time.

## Failure And Lifetime

`employee_record_policy_not_configured` (503) remains an explicit unavailable
policy error for submit/decision. `employee_sim_credit_policy_not_configured`
(503) remains an explicit missing approved-tier error for issue/transfer. Neither
failure activates policy or creates a receipt. Requester cancellation does not
require operational policy configuration in the actual service.

Unknown 5xx/network/timeout/invalid receipt outcomes retain the exact path,
payload and idempotency key in memory. Explicit retry cannot replace its captured
employee/target/request versions. Pending actions share one mutation lock;
version conflict requires reload. Successful receipts must match employee,
request and action. Drafts, retry keys and private queries reset on employee,
family, authenticated subject or grant changes; authentication-context refresh
also remounts the feature to match the shared HR abort lifetime. Late continuations are aborted.
No private state is stored in browser storage.

## Rerun Verification

Run from `C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_frotned`:

```powershell
npm exec -- eslint src/modules/hr/employee-records --max-warnings 0
npm exec -- prettier --check src/modules/hr/employee-records
node C:/Users/Huma.Khan/AppData/Local/Temp/cosmix-employee-records-verification-20261013/verify.cjs
node C:/Users/Huma.Khan/AppData/Local/Temp/cosmix-employee-records-verification-20261013/mutations.cjs
```

The temporary harness loads actual TypeScript source. `verify.cjs` reproduces
generic schema gaps and checks strict family DTOs, employee/resource binding,
request states, action restrictions, dates and exact intents. `mutations.cjs`
checks actual hook logic with isolated dependency ports for explicit policy 503,
uncertainty, changed-version retry refusal, conflict, concurrency, wrong receipts,
aborted replies and owned-cache cleanup. It does not prove React lifecycle or
real HTTP/CSRF behavior.

For browser interaction checks, run this outside-repository fixture server:

```powershell
node C:/Users/Huma.Khan/AppData/Local/Temp/cosmix-employee-records-verification-20261013/server.mjs
```

Open `http://localhost:18189/`. The harness imports the actual feature with
synthetic auth/transport confined to TEMP. It cannot authenticate or change
backend records. Its controls select transport failure, policy 503, conflict,
wrong employee receipt and a delayed reply. Inspect the visible write log for
unchanged payload/key after retry, toggle grants/subject to reset drafts/cache,
and release a delayed reply after revocation to check stale suppression.
Stop this fixture server with Ctrl+C after checks. It is not a product route.

Real acceptance still needs approved operational/SIM policy, genuine granted
accounts, configured business time and clean verified evidence from the scanner.
No provider, account, backend, policy, SQL or configuration changes are included.
