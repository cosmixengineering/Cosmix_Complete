# HR Gap Closure Continuation

**Date:** 13 October 2026. **Scope:** the existing nested backend and React web projects; React Native excluded. **State:** this continuation's supported integration code and verification completed; the full seven-gap scope remains incomplete as detailed below. This report distinguishes source delivery from live operational acceptance; it does not certify seven production-ready modules.

## Delivered In This Continuation

- Office boundary selection, exact as-of reads and future publication using actual Company Office APIs. Geometry, accuracy, jurisdiction and approval reference have no invented business defaults. Branch/latest-version concurrency and the exact publication receipt are checked. Company authority remains SuperAdmin-only on the backend; site/geofence authority is not reused as Company authority.
- Attendance capture-validation policy preview, immutable history and publication. Duplicate tolerance, active-duration, travel and device-risk groups require an explicit Enabled/Disabled choice and approved configured values. No client payroll penalty or biometric bypass is introduced.
- Employee asset, Company SIM and certification current/history records and scoped request/detail/cancel/decision surfaces. Action-specific inputs, exact employee versions, evidence requirements, segregation of duties and unchanged uncertain retry are enforced. A deployment without the approved record policy remains unavailable; the frontend does not configure that policy.
- Employee document completeness/expiry window, activation blockers and paged scan/verification/withdrawal history. These are authorized reads, not a scanner-success replacement or a reminder-delivery claim. Revoking read permissions removes both evidence views.
- Attendance audit contracts retain Office and capture-validation provenance instead of stripping it. Backend audit trace wiring passed retained-evidence and authorization checks below. Historical site responses remain compatible; no raw punch, pay or policy history is rewritten.
- Protected routing, permission-qualified navigation and employee-detail links. Existing login and local testing OTP remain unchanged.

## Seven Gap Status

| Gap                                    | Current source delivery                                                                                                                                                                                                             | Not yet closed                                                                                                                                                                                                                        |
| -------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Operational attendance web             | Current scheduling, daily/register/calendar/audit, exceptions, corrections, OT, ordinary leave, zones, site-report review, reports and closure/input reads use supported APIs. Office boundary and capture-policy views added here. | Authenticated SQL/browser acceptance across real positive/negative workflow fixtures; unsupported actions remain unavailable.                                                                                                         |
| Real face verification                 | Existing self-hosted CompreFace/ONNX adapters, capture binding, single-use proof, fail-closed behavior and durable retirement source exist. No cloud face API or Python service added.                                              | Reviewed model/license/hash/preprocessing, private deployment, live spoof/wrong-person/replay/device tests, approved privacy/retention/accessibility and operational acceptance. The user confirmed artifacts/deployment unavailable. |
| Office attendance source               | Office rosters and separate Office boundary/source implementation exist; web reads/publication now consume the actual contracts.                                                                                                    | Current Office SQL/HTTP/device acceptance and downstream provenance cases on the reviewed current schema. A source build is not this acceptance.                                                                                      |
| KPI/waiting/studying/benefits/recovery | Ordinary effective policies/leave and existing factual sources continue.                                                                                                                                                            | The dedicated finalized engines/workflows are not delivered. Missing decision rows must be resolved before their specific scores, credits, penalties, encashment or cash effects can be implemented/activated correctly.              |
| Meals/fuel/maintenance/advances/loans  | Retained data models and prepared inputs exist. This continuation does not claim new claim/payment engines.                                                                                                                         | Application workflows, confirmed-source calculations, explicit approval routes and Finance consumption remain to implement; the specific outstanding meal/fuel/overhaul/loan terms remain on the pending-policy sheet.                |
| Payroll closure                        | Immutable ordinary/late inputs, bounded reconciliation, no-charge handoff and manual Finance attestations exist. Supported web reads/preparation remain separate from posting/payment authority.                                    | OT/claim/benefit/credit source consumption, complete reconciliation, formal legal activation and positive final-month acceptance. No payment instruction or automatic deduction activated here.                                       |
| Fourteen reports/lifecycles            | Factual scoped attendance exports exist. Asset/SIM/certification current/history/request web and document expiry/history reads added here.                                                                                          | The complete fourteen-report catalogue, source-dependent KPI/meal/fuel/financial reports and actual lifecycle reminder delivery/acceptance are not delivered.                                                                         |

Missing decisions block only their own behavior. Known SRS baselines and previous confirmations are not being reasked. The single authoritative sheet is `cosmix-backend/docs/attendance-completion/Pending-Policy-Sheet-2026-10-13.md`, also on Desktop. Policies are not invented to turn an unavailable workflow into a false success.

## Actual API Groups Integrated Here

- `GET/POST /api/v1/company/offices/{branchId}/attendance-boundaries`, with optional `asOfDate` on reads.
- `GET /api/v1/attendance/capture-validation/policy/preview`, `GET .../history`, `POST /api/v1/attendance/capture-validation/policy`.
- `GET /api/v1/hr/employees/{id}/assets`, `/assets/history`, `/assets/{custodyId}/history`; corresponding SIM/certification current/history reads.
- `GET/POST /api/v1/hr/employees/{id}/records/{kind}/requests`; exact request detail, decision and cancel subresources.
- `GET /api/v1/hr/employees/{id}/documents/completeness?expiringWithinDays=...` and `GET .../documents/{documentId}/history` with privacy/cursor parameters.
- Existing authenticated attendance punch/session audit and own-history APIs consume Office/capture trace contracts; no new write endpoint invented.

## Files

New feature folders: `src/modules/hr/employee-records/**`, `src/modules/attendance/operations/capture-policy/**`.

Parent-owned new files: `attendance/schemas/office-boundaries.ts`, `attendance/pages/OfficeBoundariesPage.tsx`, `attendance/components/geofences/OfficeBoundaryForm.tsx`, `hr/schemas/document-evidence.ts`, `hr/components/documents/DocumentEvidence.tsx`.

Integration edits: Attendance and HR public indices; operational routes/navigation; `app/router/AppRoutes.tsx`; `layouts/AppLayout.tsx`; employee-detail links; `EmployeeDocuments.tsx`; attendance `review/contracts.ts`. Backend bounded trace changes: `AttendanceAuditService.cs` and `docs/attendance-completion/Audit-Reads.md`. A compiler-discovered syntax repair in `Infrastructure/Zones/EncryptedZoneClaimStore.cs` corrects three C# character escapes without changing the intended rule. Concurrent Zone configuration syntax was corrected by its other writer before this continuation could patch it; that change is not claimed here.

## Verification

| Check                                              | Final result                                                                                                                                                                                                                      |
| -------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Frontend `npm run verify`                          | Passed: lint, formatting, TypeScript and production build. Existing large initial-chunk advisory remains (881.87 kB); this is not a production load test.                                                                         |
| Backend solution Release, isolated TEMP output     | Passed, zero warnings/errors. Earlier retries encountered concurrent Zone source syntax; the final fresh build passed after current source corrections.                                                                           |
| Office boundary contracts/navigation               | 30 checks passed, including cross-office/date/receipt rejection and no site-to-Company scope borrowing.                                                                                                                           |
| Document completeness/history contracts            | 12 checks passed.                                                                                                                                                                                                                 |
| Office/capture audit frontend compatibility        | 8 checks passed; Office fields retained, mixed source rejected, legacy site response unchanged.                                                                                                                                   |
| Parent offline browser                             | 11 checks passed at desktop/narrow viewports; unchanged uncertain retry, revoked grants, and confirmed withdrawal refresh tested. Screenshots inspected.                                                                          |
| Employee records                                   | Parent reran 31 contract/helper and 11 mutation/cache checks: all passed. Worker browser verified evidence filtering and identical retry; broader record browser acceptance had connection timeouts and is not counted as passed. |
| Capture-validation policy                          | Parent reran 31 helper and 17 offline browser checks: all passed. Exact endpoints/settings/permissions, stale publication and delayed responses covered.                                                                          |
| Backend audit trace                                | Parent reran 85 service/authorization checks and 12 offline SQL translation checks: all passed. No SQL connection was made.                                                                                                       |
| Existing report/closure/navigation/blob regression | 49 checks passed.                                                                                                                                                                                                                 |
| Actual HTTPS frontend smoke                        | 23 anonymous route/login/responsive/no-business-write checks passed, including the new protected routes. This is not successful authenticated backend acceptance.                                                                 |
| Independent scoped review                          | One P2 stale document-evidence finding, reproduced and fixed. Confirmed revocation refreshes the list, completeness and open history. The same browser failure passed after the fix. No other scoped P1/P2 finding returned.      |

These suites overlap; their counts are not a completion percentage. TEMP fixture browser tests are isolated from real network/SQL/providers and must not be described as authenticated live acceptance. No permanent test project or screenshot artifact folder was added to either source repository.

### Employee Asset Accounting Boundary

Assigned assets have separate custody/history records linked to the stable Employee ID, not just a text note in the hiring form. The implemented asset surface captures an asset reference, effective issue time, issue condition, private evidence, return condition/receiver, transfer target and retained decisions/history. Current custody, historical custody and request state remain separate. It is not the company-wide stock/procurement/purchase inventory module; the backend currently identifies assets by its supported asset reference rather than inventing an inventory master.

Live asset operations require approved operational policy, permissions, current reviewed schema and authenticated acceptance. No account or policy was created to bypass those conditions.

## Run And Restrictions

Existing frontend URL: `https://localhost:5173`. From `cosmix_frotned`, run `npm install` when dependencies are absent, then `npm run dev`. The backend HTTPS endpoint must be running with the approved testing target and actual permitted fixtures for authenticated integration tests; it was not listening during this continuation's environment checks.

All source changes remain local. No commit, push, database/migration application, account creation, scanner/SMS/face activation, production change or React Native edit was performed by this continuation.

## Later Policy And Face Setup Continuation

The PM authorized continued supported implementation and supplied the signed Site Staff
Duty Hours/Meal Allowance (5 pages) and Motorcycle Maintenance/Tuning (2 pages) PDFs.
Both complete texts were read; material source pages were rendered and visually checked.
The single pending-policy sheet now records the clarified KPI/no-direct-salary-link,
50/50 long-leave recovery split, meal continuity/home-break, SIM tiers, tuning and fixed
overhaul treatment. Explicit source rules are not being asked again. The site 10:15
inclusive late rule and office exclusive rule have distinct staff scopes; the existing
exclusive calculator still needs versioned reconciliation, not historical policy rewriting.

### Latest Bank Instruction

The PM explicitly cancelled new bank verification. Five new, unmounted frontend files
created during this continuation were removed. No new bank-verification page, navigation,
API integration or fixture was delivered. Existing bank-detail forms, historical data and
preexisting backend work were not deleted. Discarded bank-probe results are not counted.

### Delivered Source

- Foundation Fields, Lifecycle and Records policy administration: current/as-of reads,
  publication history, proposals, request details, requester cancellation and independent
  approval/return/reject using the real supported contracts. Structured vocabulary,
  required-document and lifecycle/records controls replace free-form JSON guesses.
- Staff taxonomy: current/effective category resolution, retained verified publications,
  structured Office/Site category mappings, explicit policy applicability/version
  references and proposal/detail/cancellation/independent-decision surfaces. Reference
  catalogue and downstream claim readiness are not fabricated when the API lacks them.
- Exact All-scoped navigation and protected routing through the HR public index.
  Context/grant changes discard private drafts and owned query caches; uncertain writes
  retain their exact body/path/key. Company/Head Office access remains separate.
- Existing self-hosted face adapters preserved; added a one-time configuration-readiness
  validator and local command. Diagnostics return setting names and fixed codes, never
  secret values. Optional local artifact inspection checks pinned bytes/graph loading,
  not accuracy or live verification. Shared registration and activation settings unchanged.
  Durable retirement documentation corrected; physical disposal remains unavailable.

Frontend files: `src/modules/hr/foundation-policies/**`,
`src/modules/hr/staff-taxonomy/**`, `src/modules/hr/policy-navigation.ts`, HR public index,
`src/app/router/AppRoutes.tsx`, `src/layouts/AppLayout.tsx` and this report/module README.
Backend additions: `FaceVerification/SelfHostedFaceConfigurationReadiness.cs` and
`FaceVerification/Test-FaceConfiguration.ps1`; two face setup/PoC docs updated.
No entity, migration, shared DI, provider key or application activation file changed here.

Actual API groups:

- `/api/v1/hr/foundation-policies/{Fields|Lifecycle|Records}`: GET current/as-of, history,
  requests and request detail; POST request, decision and cancellation.
- `/api/v1/hr/staff-taxonomy`: GET current, effective, history, requests/detail;
  POST request, decision and cancellation. Exact controller DTOs are authoritative.

### Fresh Verification And Boundaries

| Check                                                    | Result                                                                                                                                                                                                                                |
| -------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Parent `npm run verify`                                  | Passed: lint, formatting, TypeScript and build. Initial chunk advisory remains, now 961.56 kB; not a production performance acceptance.                                                                                               |
| Parent backend Release, isolated TEMP output             | Passed, zero warnings/errors; no database/host startup.                                                                                                                                                                               |
| Parent policy navigation                                 | 17 permission/session/scope assertions passed; no Company-to-policy scope borrowing or bank link.                                                                                                                                     |
| Parent Foundation contract/helper and feature TypeScript | 13 behavioral checks plus feature-local TypeScript passed.                                                                                                                                                                            |
| Parent Foundation offline browser                        | 8 synthetic checks passed: typed controls, exact retry/CSRF, stale/self decisions, history, grant/context clearing and narrow/desktop layout.                                                                                         |
| Parent Staff Taxonomy contracts/mutations                | 28 contract/access checks and 8 operation/retry/abort checks passed.                                                                                                                                                                  |
| Parent face configuration probe                          | 49 offline assertions passed against the fresh Release assemblies; no usable ONNX inference, provider HTTP or SQL.                                                                                                                    |
| Face implementation worker regressions                   | 395 retained provider/lifecycle/protection/readiness/enrollment/capture assertions passed; worker-run, not independently rerun in full by parent.                                                                                     |
| Parent actual HTTPS frontend smoke                       | 15 anonymous protected-route/login/responsive checks passed; no business API calls. Wrong initial test label was corrected; login source was unchanged.                                                                               |
| Independent Foundation/Face scoped review                | No confirmed actionable finding at final source check; no live SQL/provider/storage acceptance.                                                                                                                                       |
| Core HR live preflight                                   | Blocked: recorded LocalDB instance stopped, historical pipe failed. No current SQL inventory or successful authenticated hiring acceptance established. Existing scanner answered PING/VERSION; this is not a Clean document verdict. |

The initial central verification found one unformatted taxonomy operation file; it was
formatted before the passing central run. Initial direct Node taxonomy probes needed
the existing TEMP TypeScript loader; their corrected loader runs passed. These harness
setup failures were not backend workflow defects or database integration tests.

### Still Not Complete

All seven non-mobile gaps are not production-ready. Complete hiring/clean-document/
approval/transfer live acceptance needs the reachable existing authorized testing target,
reviewed current schema and already approved synthetic fixtures. No replacement database,
account, grant, scanner bypass or SMS configuration was created to get a green result.

Actual face acceptance still needs the reviewed licensed ONNX export/semantics/hash,
calibrated decisions, private CompreFace deployment/credentials, real-device spoof/race
testing and approved privacy/retention/disposal/accessibility. Adding credentials alone
cannot certify those requirements. KPI/waiting/studying/benefits/recovery, operational
claims/loans, complete payroll consumption and all fourteen source-dependent reports
remain as specified in the status table and narrowed pending-policy sheet above.

Existing frontend URL remains `https://localhost:5173`; the current backend was unavailable
for authenticated integration. All delivered source stays local; no commit or push.

### Final Review And Rerun

The independent taxonomy review returned one P2: retained JSON size was underestimated
for plus signs, backticks and escaped quotes. The oversized-plus proposal was reproduced
as an incorrect client acceptance, then fixed in `staff-taxonomy/contracts.ts`. The
20 boundary checks failed before the fix and passed afterward; 96 actual compiled .NET
codec length comparisons and the 28 existing contracts passed. The reviewer independently
checked all 65,536 UTF-16 units and 16 complete rules; the original 35,441-character
proposal is now rejected and no remaining actionable issue was found in this fix.

The parent reran all 19 taxonomy browser regressions after the final schema change,
including verified history, all three independent decisions, successful supported
proposal receipts, invalid evidence and feedback clearing. These remain synthetic
offline tests, not live SQL/approval acceptance. Read-only typecheck, lint and formatting
also passed. A fresh final `npm run verify` passed after the fix; its initial chunk is
961.73 kB, superseding the earlier size recorded in the table. The size advisory remains.

One final TEMP file write failed because C: reported zero free space. The attempted
cleanup of two inspected, run-owned temporary runner directories was blocked by the
tool and was not retried through another mechanism. After 420 MB free space became
available, full verification completed. No company/project/database cleanup was performed.
The encoder parity checks ran in memory; no bank-verification work was reintroduced.

## Current Eight-Area Follow-Up, 13 October 2026

The latest request adds Core HR live acceptance and rehire to the previous seven-gap
discussion. The following status supersedes earlier preflight statements; it does not
certify eight operationally complete areas.

### Source Delivered In This Follow-Up

- Retirement and Suspended-to-Active release use the existing employment-status
  request/independent-decision APIs. They remain gated by published lifecycle settings.
  There is no terminal-to-Active rehire shortcut or automatic restoration of credentials,
  devices or face enrollment.
- Archive/unarchive and paged archive history use the existing lifecycle contracts.
  Current/All/Archived employee-directory filters are integrated, with pagination reset.
  Archiving is directory metadata, not deletion of attendance or financial history.
- New batch document completeness/expiry API and React report at
  `/hr/reports/document-expiry`. A user selects 1-25 employees and an explicit 1-365-day
  read window. Every selected employee resolves before private evaluation; current web
  MFA and All-scope read permissions are revalidated before returning the complete result.
  The client rejects mismatched/duplicate/partial/date-inconsistent results. No document
  bytes, medical files, export permission, notification schedule or payment is introduced.
- Schema-3 attendance policies can explicitly choose `strictly_after` or `at_or_after`.
  This represents the confirmed Office/Site late-boundary difference without rewriting
  historical policies. Explicit comparison carries new calculation provenance; legacy
  canonical bytes and comparison behavior remain unchanged. Legacy schemas reject the
  newly introduced JSON member even when its value is null. No policy was published.
- The one existing pending-policy sheet now includes the genuinely missing rehire
  decisions and records the implemented boundary reconciliation. Known source amounts,
  thresholds and prior PM confirmations are not being requested again.

### Actual Completion Status

| Area                             | Current status                                                                                                                          | Remaining before operational acceptance                                                                                                                                                                                 |
| -------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Core HR live acceptance          | Hiring, document, approval/access, record and sensitive-change code exists. Current testing database was found with SELECT-only checks. | Authenticated positive/negative SQL/HTTP hiring, clean evidence, approval/access, transfers and sensitive-change fixtures. No successful end-to-end run is claimed.                                                     |
| HR policy administration         | Foundation-policy and staff-taxonomy React screens use actual APIs; this follow-up adds explicit arrival boundary choice.               | Authenticated publication/review acceptance. New bank-verification work remains excluded by the later PM instruction; existing bank data/history was not deleted.                                                       |
| Rehire/lifecycle                 | Retirement, suspension release and archive web flows are implemented against existing APIs.                                             | New employment-episode persistence/service/API/approval activation is not implemented. Approved rehire eligibility, decision chain, evidence and prior-episode balance treatment are needed.                            |
| Real face verification           | Existing self-hosted adapters and fail-closed configuration diagnostics are retained.                                                   | Reviewed licensed ONNX artifact/semantics/hash, CompreFace deployment/credentials, real-device/spoof/wrong-person/replay acceptance, privacy/disposal/accessibility approvals. Not a credentials-only completion claim. |
| Advanced attendance/leave        | Ordinary policies, source facts and explicit Site/Office boundary calculation are supported.                                            | Final KPI, waiting, studying/make-up, extra six-day grant, encashment and recovery engines/decisions remain incomplete; only their specific unconfirmed effects are withheld.                                           |
| Claims/loans                     | Retained models/prepared inputs and source policy facts are available.                                                                  | Operational claim and loan services, case approval, remaining price/cycle terms, repayment and Finance consumption are not delivered. Models/forms are not completed engines.                                           |
| Full payroll closure             | Ordinary/late saved inputs and existing reconciliation/attestation code remain. Historical calculation/payroll regressions were rerun.  | OT/claims/benefits consumption, full financial reconciliation and a final accepted payroll-ready month. No automatic unconfirmed payment or deduction.                                                                  |
| Reporting/reminders/self-service | Factual attendance reports and employee-record evidence reads exist; selected-population document-expiry report added.                  | All fourteen source-dependent SRS reports, delivered expiry reminders and complete employee financial self-service remain incomplete. A report window is not a reminder schedule.                                       |

### Current Testing Target And Safety

SELECT-only discovery identified existing `Cosmix_Testing` on
`CosmixUat_d893653346`, owned by the current Windows user. It currently reports
24 migration-history entries, 149 user tables, 14 employees and 5 accounts;
latest migration is `20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims`.
The retained database-family metadata matches that target. The default
`MSSQLLocalDB` was started and inspected but has no attached Cosmix catalogue.
No database, schema, migration, account, grant or synthetic business row was created,
attached, restored or modified by this follow-up.

The existing runtime testing profile binds an older instance and requires exactly
three fixture accounts. The discovered target has five. That safety gate was not
weakened and no accounts were deleted to make it pass. The current database binding
and already-approved fixture ownership must be reconciled explicitly before live
authenticated write acceptance. Source and isolated synthetic verification continued.

### Executed Verification

- Full backend Release build: passed, zero warnings/errors, isolated existing TEMP output.
- Boundary regression: reproduced an old-parser rejection before adding supported
  comparison; 22 final equality/history/version/early-arrival/missing-duty checks passed.
  The reviewer-found present-null legacy bypass was reproduced and fixed before rerun.
- Existing actual backend scripts rerun in memory against fresh Release assemblies:
  121 schema-3/legacy-byte checks, 181 daily calculation/service checks and 55 payroll
  input/producer/protected-compensation checks passed. No SQL/network/financial acceptance.
- Document report: 21 client contract checks passed; parent reran the worker's 107 isolated server
  assertions for authorization, exact population, cancellation, privacy and dates.
- Lifecycle worker: 9 contract/render and 18 offline browser checks passed. Its bounded
  directory integration review added 8 passing browser checks; no live SQL writes.
- Full frontend verification passed after feature integration. Its initial bundle-size
  advisory remains (974.93 kB at that run); this is not performance acceptance.
  Final post-review frontend verification also passed; the current initial chunk is
  975.18 kB. Document-report browser verification initially found search/pagination
  result-clearing omissions: both were fixed and the same 22/22 offline browser checks
  then passed. Independent review also reproduced a stuck pending state after same-account
  permission refresh; the existing policy-screen epoch-remount pattern was reused to
  discard the old workspace and suppress its delayed response. The reviewer cleared
  that finding with 17 focused assertions, and all 23/23 browser scenarios passed,
  including fresh reading while an old request remains delayed. Parent inspected desktop/mobile screenshots; no overlapping text/page
  overflow was observed. Parent actual HTTPS anonymous testing passed 27 protected-route,
  login-field and responsive checks, with no business API request. Authentication code
  was not altered and authenticated end-to-end acceptance is still not established.
- Final independent scoped review cleared the two reproduced P2 findings (legacy
  present-null policy JSON and same-account report cancellation). Backend/frontend
  batch contracts and All-scope boundaries match. No further reproduced finding was
  reported within that scope; this is not a full-repository security certification.

Frontend files added/changed: lifecycle request helpers/forms/workspace/schema;
`components/employee/EmployeeArchive.tsx`, `EmployeeArchiveFilter.tsx`,
`employee-archive-directory.ts`; `pages/EmployeesPage.tsx`, `DocumentExpiryPage.tsx`;
`schemas/document-evidence.ts`; policy `PolicyRulesFields.tsx`/`contracts.ts`;
HR public exports, protected routes, navigation and scoped checkbox layout CSS.

Backend changes: `AttendancePolicyRules.cs`, `AttendanceDailyTimeCalculation.cs`,
the snapshot producer/calculator/payroll producer's calculation-version binding;
`EmployeeDocumentService.cs`, `EmployeeDocumentContracts.cs` and its existing controller.
No model mapping, migration, shared authentication/provider configuration or mobile
code changed in this follow-up. Existing concurrent changes remain preserved.

New/read API: `POST /api/v1/hr/employees/documents/completeness/batch` (JSON read
with cookie CSRF). Existing integrated APIs: lifecycle requests/decisions/cancellation,
`POST .../{id}/lifecycle/archive`, `GET .../{id}/lifecycle/archive/history` and employee
directory `includeArchived`/`archivedOnly`. Current frontend server remains
`https://localhost:5173`; use `npm run dev` from the existing frontend directory.
Changes stay local, without commit/push. Initial owner setup, testing OTP, scanner and
SMS configurations are unchanged.
