# Actual live HR API checks - 26 October 2026

## Executed results

Both batches used actual HTTPS requests, the normal password/MFA/CSRF/session
pipeline and SQL persistence on the original verified `Cosmix_Testing`.
They did not use mocked HTTP responses or bypass authentication.

| Batch | Checks | HTTP requests | Scope |
| --- | ---: | ---: | --- |
| Auth01 | 16/16 | 22 | Two synthetic HR/Admin password -> local MFA -> permissions -> CSRF -> logout/revocation |
| Read01 | 32/32 | 38 | Same16auth checks plus16 business read, validation, missing-record and permission cases |

These are not48unique business scenarios. Repeated authentication assertions
are counted separately in the two retained runners.

Read01 actual assertions, for both synthetic accounts unless stated otherwise:

- Own benefit history: HTTP200 with exact empty array.
- Invalid year date: HTTP400, not a server error.
- Admin benefit history without the required current grant: HTTP403. Expired
  temporary grants were not renewed or ignored to make a positive test pass.
- Recovery queue: Admin HTTP200/empty; HR HTTP403.
- Missing self recovery-work ID: HTTP404.
- Fuel list restricted to the synthetic HR employee: HTTP200,zero rows.
- Missing historical trip-correction source: HTTP404.
- Missing fuel-reconciliation source: HTTP404.

No application defect was demonstrated in these live cases. There is therefore
no claim that an unobserved business bug was fixed. Test-host preparation added
strictly selected profile bindings and kept unbounded retention workers disabled;
normal application authentication and business authorization were not weakened.

## Target, preservation and final state

- `Software-PC1\LOCALDB#48AA6F9F`, PID26564, catalog `Cosmix_Testing` ONLINE.
- Owner/operator `COSMIXRND\Huma.Khan`; original family
  `D9C34627-18A2-41CF-A804-821E39516303` and original MDF/LDF paths matched.
-26applied migrations matched current source. **No migration applied.**
- Each batch compared156protected SQL snapshots and seven originalC01 files:
  unchanged. No employee, role/grant, policy, financial or vehicle write.
- Auth01 completed with total database counts: accounts3,migrations26,sessions150,
  challenges166,deliveries166,rate-buckets141,audits4427. These are total retained
  counts, not amounts inserted by this run. Separate timestamp-bounded SQL
  reconciliation found exactly two Auth01 sessions, both revoked.
- Read01 completed totals: accounts3,migrations26,sessions152,challenges168,
  deliveries168,rate-buckets153,audits4446. Auth and request-audit changes only.
- Owned host18316(Auth01) and18724(Read01) both exited. Database left running.
  No cleanup/reset, real SMS/face call, legal activation or payment.

Owner-private evidence roots (do not expose configuration, keys or cookies):

`C:/Users/Huma.Khan/AppData/Local/Cosmix/HrAcceptance/Huma20261026Auth01/results/`

`C:/Users/Huma.Khan/AppData/Local/Cosmix/HrAcceptance/Huma20261026Read01/results/`

Both contain `acceptance.json`, `start.json`, host logs and `host-final.json`.

## Reviewed runtime and guards

Auth01 frozen runtime:
`D:/CosmixVerification/hr-backend-20261026/auth01-build01/bin/Cosmix.Api/release`.
Packet SHA256:
`4ABF4DAB338F4FBC488A380A2701C000909C5CEE76E67CADBD5DCF01AD98F3F2`.

Read01 frozen runtime:
`D:/CosmixVerification/hr-backend-20261026/read01-build01/bin/Cosmix.Api/release`.
Packet SHA256:
`00403F05B909B846AEF2079D82D38B5099C077DA63187EE8EE1997594C2F24A3`.

Both Release builds: zero warnings/errors. Read01 is not a general HR test host:
it allows only eight exact business GET URLs, six existing auth routes, two named
synthetic accounts, loopbackHTTPS7481 and at most80HTTP requests. Arbitrary query
parameters, alternate employees and business-write methods are rejected before
transport/service execution. Actual normal permissions remain enforced too.

New read-boundary test first failed against the prior runtime because that
boundary did not exist. After implementation,486standalone checks passed
(100readallowlist,116profile,11composition,143priorboundary,116Auth01profile),
plus32PowerShell runner checks. Logs:
`D:/CosmixVerification/part-c-parent-20261013/managed-513c4c8293ae4dd8a2d78b3bd7aab33d/`.
These offline guards are not counted among the live HTTP results.

## Still not accepted

This run does not exercise award -> consume -> correct/resolve, approved actual
recovery work -> fulfilment, or legacy trip -> correction -> changed-amount fuel
reconciliation/maintenance chains. Their valid historical source graph is absent.
Missing source404 and empty list200 are correct boundary responses, not positive
lifecycle acceptance. App/device/face availability is NOT the blocker for them.

The [business fixture manifest](Business-SQL-Manifest-20261026.md) remains a
preparation artifact, not a record of inserted history. Its synthetic legacy
corpus, shared policy effects and remaining execution-packet work must not be
silently replaced with invented approved history. Production readiness remains
unaccepted; real provider/device and deployment acceptance are separate.
