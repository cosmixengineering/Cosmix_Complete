# HR foundation policy verification — 13 October 2026

The standalone module in `Application/HR/FoundationPolicies`, its dedicated repository and controller
are implemented. It adds versioned request/review/publication, current/history/proposal reads and
requester cancellation using existing policy, approval, audit and idempotency tables. No migration,
mapped entity, database change, account, provider request, host or Git mutation was performed.
Employee consumer adapters, composition/catalog registration and operational acceptance are separately owned.

## Executed evidence

| Check | Actual result |
|---|---|
| Full `dotnet build src/Cosmix.Api/Cosmix.Api.csproj -c Release`, isolated TEMP artifacts, locked restored dependencies | Passed all four projects; 0 warnings, 0 errors |
| [Behavior harness](VerifyFoundationPolicies.cs), real freshly compiled Application/Domain assemblies and synthetic repositories/private bytes | **81 passed** |
| [Actual EF query/model harness](VerifyFoundationPolicyQueries.cs), SQL Server translation and connection-rejecting interceptor | **18 passed**, zero database connections or commands |
| Whitespace check | `git diff --check` returned no whitespace errors; unrelated pre-existing line-ending notices were retained |

Behavior checks cover absent and first-pending baseline compatibility; explicit publication authority;
exact family/rules validation; real category-code lookup; independent fixed Admin review; expected policy,
request and latest-version conflicts; protected request/decision/cancellation replay; revoked permission,
session, MFA and non-All-scope denial; current/history/private-reference boundaries; authority/session expiry
after private IO; proof-to-rules binding; corrupted proposal evidence and foreign approval type denial;
dated successor cutover; future first publication; explicit overlaps; expiry without older-rule resurrection;
strict unknown/duplicate/missing/case/coerced JSON; and feature-local enum binding through default MVC options.
Actual lifecycle and record family publication/typed reads are also exercised, including explicitly disabled
lifecycle behavior and rejection of DirectHr operational authority.

Meaningful red evidence preceded the relevant fixes: the initial actual policy reader was absent;
a nonexistent category was accepted instead of rejected; a stale proposal had no cancellation path;
and JSON accepted by strict validation failed through default MVC binding. All of those paths now pass.
The actual bounded repository expression is translated directly, rather than copied into a test-only query.
Unapproved inactive definitions and unpublished foreign reserved-code collisions also cannot disable the
configuration baseline; actual publications still fail closed for invalid definition/authority or dates.

Temporary file-based builds encountered shared-source compilation failures in the independently owned
Attendance zone/correction work while it was changing, and one missing isolated `refint` directory.
They were reported to the parent; the later complete Release build succeeded. The final 81-check behavior
run used a TEMP-only console wrapper referencing the fresh real Application/Domain Release DLLs, avoiding
another shared-source compilation race. No permanent test project was added to the checkout or solution.

## Reproduction

Run the behavior source as a .NET 10 file application, placing its artifacts outside the checkout:

```powershell
dotnet run --file docs/foundation-policies/VerifyFoundationPolicies.cs -c Release --no-launch-profile --artifacts-path "$env:TEMP/cosmix-foundation-policy-verification"
dotnet run --file docs/foundation-policies/VerifyFoundationPolicyQueries.cs -c Release --no-launch-profile --artifacts-path "$env:TEMP/cosmix-foundation-policy-query-verification"
```

Use ordinary locked restore before the isolated Release build when its artifact directory has no assets.
The file harnesses have no real employee or policy values; their synthetic labels and grants are test-only.
The query harness rejects all attempted connections, including accidental future query execution.

## Acceptance boundaries

These checks do not prove actual SQL transaction rollback/range locks/contention/restart, authenticated
HTTP/CSRF, a deployed private-file provider/scanner/key configuration, real management policy approval,
or production use. The test transaction adapter is synthetic; it does not simulate database rollback.
No actual authority version/date window, category document list, field vocabulary/limits, retirement/release
enablement or record authority configuration was invented. Management's outstanding inputs remain required.
Future/expired/invalid actual publications return unavailable and never fall back to deployment configuration.

Read [the contracts](../Hr-Foundation-Policies.md) before configuring or consuming this module. A first
unapproved proposal alone retains baseline compatibility; requester cancellation can clear it after authority
expires without publishing rules or creating a fabricated approval.
