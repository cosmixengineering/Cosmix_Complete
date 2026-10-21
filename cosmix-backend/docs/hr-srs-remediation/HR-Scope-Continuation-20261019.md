# HR Backend Scope Continuation

## Scope Correction

The owner's latest instruction is to complete the supplied
`HR_Attendance_Employee_Mobile_SRS_v1.2.docx` HR backend, not the downstream
Accounts system. This supersedes the earlier implementation order which put
financial posting and settlement ahead of remaining HR work. Existing Accounts
code and unrelated dirty changes are preserved; no new Accounts workflow is
authorized by this continuation.

SRS sections 1.1, 1.2, 1.3 and AC-007 distinguish traceable approved HR inputs
from downstream accounting. The SRS includes an Accounts-facing handoff, not
automatic salary disbursement or a compulsory accounting ledger inside HR.

| HR owns in this continuation | Outside this continuation |
| --- | --- |
| Employee records, documents, lifecycle and authorized access | Accounts ledger posting and settlement |
| Effective schedules, raw attendance, corrections and reviewed factual history | Bank transfer or salary release |
| Leave eligibility, balances, requests, decisions, recovery evidence and configured benefits | Collection or payment of an HR-calculated amount |
| Approved overtime and configured allowance/vehicle eligibility and claims | Insurance settlement and automatic employee liability deduction |
| Scoped self-service, team views, reports, exports, audit and notifications | Production legal certification or an invented legal approval authority |
| Exact approved inputs and traceable handoff contracts | Unapproved production biometric or SMS activation |

The SRS is source material, not permission to undo later confirmed decisions:
site-based scheduling, direct authorized reviews, Admin special-case approvals,
no locked-month reopening, mobile punches plus HR corrections only, and the
separate Operational/Red zone model remain unchanged.

## Dynamic Policy Rules

Company values are explicit, effective-dated settings. Rates, eligibility,
thresholds, partial-day treatment and rounding are not seeded from examples.
Missing or unapproved settings produce a clear pending/unavailable result, not
zero money or a guessed entitlement. Retained approved evidence must not be
repriced by a later policy edit. Configurability does not itself approve a rule.

## Ownership

Six agents were dispatched. After the owner clarified scope, financial work was
stopped before product edits. Its worker was reassigned to the HR team leave
calendar. Other scopes are leave/recovery, outstation, vehicle policies, private
HR storage verification and HR permission review. The parent owns report
context, shared wiring and final build/verification. No duplicate payroll or
employee module is being created.

The parent's unfinished Finance-only regression experiment was removed from the
working test file after the redirect; previous Finance tests remain unchanged.
Its retained run reported a protected allowance-line proof gap. That finding is
deferred to the Accounts scope, not silently marked fixed.

## Verification Boundary

No database migration application, database setup/seeding, application host,
real employee accounts, provider calls, permanent configuration or Git writes
are authorized here. Standalone synthetic/component tests do not establish
authenticated SQL acceptance. The old A02 packet is not reused or regenerated.
Actual private-file tests, where executed, use only a fresh unique verification
directory and fresh synthetic test keys confined to that directory; they do not
touch existing HR storage or application key configuration.

## Executed Verification

Fresh Release build completed with **zero warnings and zero errors**:

`D:\CosmixVerification\hr-backend-20261019\hr-scope-build\bin\Cosmix.Api\release`

Build log: `D:\CosmixVerification\hr-backend-20261019\hr-scope-build.log`.
The frozen build was not overwritten during verification.

| New standalone suite | Actual result |
| --- | --- |
| VerifyHrReportContext | 12/12 |
| VerifyRecoveryFulfilment (reservation integrity, not actual fulfilment) | 38/38 |
| VerifyDeploymentPolicyBoundaries | 36/36 |
| VerifyVehiclePolicyPublication | 70/70 |
| VerifyHrSelfServicePermissionBoundaries | 52/52 |
| VerifyLeaveTeamCalendar | 58/58 |

All six exited successfully. Evidence, source copies, compiler responses and
dependency hashes are in
`D:\CosmixVerification\part-c-parent-20261013\managed-5d02c9710c4740a8961e48a732a98a00`.
These execute real services with synthetic repository/authorization/storage IO,
not authenticated HTTP/SQL.

### Existing Flow Regressions

| Standalone suite | Actual result on the same frozen Release |
| --- | --- |
| VerifyLeaves | 67/67 |
| VerifyLeaveRecoveryReplay | 51/51 |
| VerifyEncashmentWorkflow | 30/30 |
| VerifyEncashmentValuation | 80/80 |
| VerifyOutstationAllowances | 65/65 |
| VerifyVehicleEntitlements | 46/46 |
| VerifyVehicleRepository | 35/35; zero SQL attempts |
| VerifyEssLeaveConsistency | 37/37 |
| VerifyEssFinancialBoundary (compatibility only, no new Accounts work) | 21/21 |
| VerifyHrReportXlsx | 81/81 |
| VerifyHrReportBoundary | 370/370 |
| VerifyHrReportResults | 204/204 |
| VerifyHrReportsRemaining | 422/422 |
| VerifyHrReportQueries | 242/242; offline SQL translation |
| VerifyHrReportOvertime | 12/12 |
| VerifyAttendanceComposition | 50/50; services resolved, no host started |
| VerifyRemediationContracts | 184/184; 491 generated paths, not live requests |
| InspectContinuationModel | 25 compiled migrations, zero model differences, zero SQL attempts |

The initial regression group is retained at
`D:\CosmixVerification\part-c-parent-20261013\managed-9303dec312ea44cb87b9d93e7dfd8a26`.
It passed the first twelve suites, then correctly stopped when the old CSV test
assumed a fixed seven-row metadata header. After adding Filter/Returned Records,
that fixture read metadata as report data. The fixture now locates the actual
metadata separator, preserves independent data/escaping parity checks, and adds
24 metadata assertions. No production code was weakened to satisfy this test.
The subsequent 422-check run and query/OT/composition checks passed at
`D:\CosmixVerification\part-c-parent-20261013\managed-9fb5fa7a4bae4ced8d1879a1195726b7`.
That group then stopped at one old OpenAPI prose assertion: the controller now
says `pending with null money` rather than `pending with no amount`. The assertion
was aligned with the actual contract and strengthened to require configurable
values and no inferred 40-hour treatment. It was not replaced with a route-only
assertion. The final OpenAPI/model run passed at
`D:\CosmixVerification\part-c-parent-20261013\managed-f205716408234c1a90b1c7e79ad1d4dc`.

**Final total: 23 standalone suites, 2,263 successful checks/scenarios, plus the
offline model inspection.** Mixed scenario/assertion counts are not a coverage
percentage or full HR acceptance. Existing CS1701 framework-reference unification
warnings occur in the standalone OpenAPI compiler; the product Release build has
zero warnings. All final invocations exited 0. No test process or API host remains
running. The existing migration snapshot line-ending warning was observed during
`git diff --check`; no snapshot or migration was edited in this continuation.

Independent review caught a newly introduced outstation decision-ordering problem:
post-storage validation queried a decision before it had been saved. Validation
now runs before staging the decision, and the fixture exposes only saved decisions.
Another regression prevents a pending return from authorizing a later home break.
The reviewer found no new material issue in those revised changes. Pre-change
retained claim compatibility still needs a true historical fixture, not only the
current codec's legacy serialization test.

Actual private HR storage verification passed **20 scenarios / 159 assertions**
against the earlier `fixed-build`, using unchanged production storage code.
This included independent AES-GCM decryption, separate writer/reader processes,
tamper, key rotation and controlled Windows access failures. Exact evidence:
`D:\CosmixVerification\hr-backend-20261019\storage-tests-4bbc7452506a4da3ad136e0589e7bbb1`.
See [the storage report](Private-Storage-Continuation-20261019.md) for limits.
It is not authenticated upload/download, SQL atomicity or production ACL acceptance.

| Frozen assembly | SHA256 |
| --- | --- |
| Cosmix.Api.dll | 042F59EFEA5CCB3A85B4E2094D9811B34858E3B7ED38A7A4AB7BFB11715883C4 |
| Cosmix.Application.dll | 0AD91AAB22F036446050370465898029AB42E8DF5B982B9169F98F784950AC89 |
| Cosmix.Domain.dll | F3E235711B5A1DF09C903840B8B230D36470E1A820AED70A94E0E3AFB370B37F |
| Cosmix.Infrastructure.dll | 0C60C82820C04D619D1C584DA321F30AADF8FBD7F3B3AE2644D5B75DF815FC2E |

## HR Acceptance Cases

These are acceptance cases, not claims of executed HTTP/SQL tests.

1. Publish an explicitly configured vehicle policy with a future finite period,
   retained rule version and dedicated management permission. Missing fields,
   unknown configuration, invalid staff/workshop periods and overlapping policies
   must fail. A successful retry must not create another policy version.
2. For an approved outstation trip, configure counting, departure/return treatment,
   rate, currency and rounding. The Monday 18:00 to Wednesday 10:00 example must
   remain pending when those rules are absent. Policy changes inside a trip need
   explicit transition rules; changing policy must not invent a home-return break.
3. Retained approved trip claims must preserve their original policy-day evidence,
   approved home breaks, calculated amount and decision. Pending or rejected
   return evidence must not erase an approved open break.
4. Leave recovery commitment creation, retries and history must validate the full
   retained reservation chain, including other leave requests sharing that day's
   limit. Missing predecessors or cycles must not silently free capacity. This
   remains a promise of future work, not confirmed fulfilment or an earned benefit.
5. Report JSON, CSV and XLSX must retain normalized filters, page/size, total
   matching records and actual returned count. An empty late page is not a zero
   total. Unsupported report types and export formats must return validation errors.
6. Self-service profile/request reads must recheck current session, employee and
   permission after source IO and persist an audit before releasing data. Denied
   reads must not disclose another employee's data.
7. Administrative medical/injury-medical evidence download requires the existing
   `hr.employees.medical.read` grant scoped to the beneficiary as well as current
   fixed-role leave-review authority. Revoke either during envelope, content or
   audit IO and verify that no bytes are disclosed. Own mobile evidence access
   remains distinct and does not require administrative medical permission.
8. The team calendar must authorize every selected employee for current and
   historical dates, including empty results. A transferred or unpermitted
   employee must not leak leave dates through a mixed selection. Own calendar
   identity must come only from the authenticated session.

## Additive Client Contracts

- `GET /api/v1/leaves/team-calendar?EmployeeIds=<authorized-guid>&From=2026-10-01&To=2026-10-31&Page=1&PageSize=25`
  requires `attendance.leaves.team-calendar.read` and current web MFA. Multiple
  IDs use repeated `EmployeeIds` query parameters. No implicit all-team query.
- `GET /api/v1/leaves/mine/calendar?From=2026-10-01&To=2026-10-31&Page=1&PageSize=25`
  uses `attendance.leaves.self`; no client-supplied employee ID.
- `GET/POST /api/v1/hr/vehicles/policies` and
  `GET /api/v1/hr/vehicles/policies/{id}` expose the policy catalogue to explicitly
  authorized management. Validate at `/policies/validate`, preview at
  `/policies/{id}/publication-preview`, then publish at `/policies/{id}/versions`
  with the exact returned revision, rowversion and preview fingerprint. List and
  read immutable versions at `/policies/{id}/versions[/{versionId}]`.
  Writes retain normal CSRF and idempotency requirements; no permission is granted
  automatically. Raw `rules` keys use the explicit PascalCase vehicle schema;
  missing properties are not interpreted as disabled settings.
- Optional allowance `deployment.policyTransition` is `1` for each local calendar
  day's effective policy or `2` for an elapsed cycle wholly inside one policy.
  It must match the configured counting basis. Absent transition selection retains
  single-policy behavior, not permission to guess across policy changes. An open
  approved home departure stays open until its return is independently approved.
- HR report metadata now includes normalized `filter` and `returnedCount`; CSV
  and XLSX add Filters and Returned Records. `totalCount` remains total matching
  records, not the current page length. Unknown report type/export format is 400.

These are backend contracts. Separate React/React Native views were not changed
or tested in this continuation.

Production data, new grants, policy publication and account changes are not
authorized merely by this checklist. Execute only in a separately approved
synthetic HTTP/SQL acceptance setup.

## Remaining HR Work, Not Accounts Work

| SRS area | Exact remaining requirement |
| --- | --- |
| LEAVE-011 extra/KPI-linked benefits | Approved award eligibility, authority, periods and leave-versus-cash exclusivity; configurable definitions alone are not an earned entitlement workflow. |
| LEAVE-013 recovery | Qualifying actual work, partial fulfilment/correction rules and exclusive allocation against OT or other make-up credits, with auditable fulfilment records. Current commitment history does not fulfil work. |
| Vehicle monthly adjustments | Historical nearer-assignment precedence and month-based adjustment evidence; do not infer an approved payable adjustment from a baseline commute setting. |
| Vehicle policy/cycle transitions | Existing open-ended policy succession and historical maintenance-cycle changes need preserved-history semantics; the new finite-period publisher must not rewrite prior versions. |
| Vehicle incidents | Amended HR incident decisions with immutable revision evidence; insurer settlement and actual recovery belong to Accounts and are excluded here. |
| Dynamic company settings | Actual approved values and evidence must be configured. Synthetic test policy values are not company defaults. |
| Acceptance | Authenticated HTTP/SQL persistence, concurrent requests, notifications/workers and client-contract UAT on a separately approved isolated target remain unexecuted here. |

Face/OTP provider acceptance remains explicitly deferred. Production financial
penalties remain gated by legal review; this is not a requirement to implement
an Accounts ledger before completing independent HR workflows. Full HR SRS
acceptance is not asserted while the HR rows above remain outstanding.

## Changed-File Handoff

Paths below are relative to this existing checkout. They identify this turn's
edits, not ownership of all pre-existing dirty content in those files.

- Shared wiring: `src/Cosmix.Application/DependencyInjection.cs`,
  `src/Cosmix.Application/Authentication/Access/AccessPermissionCatalog.cs`.
- Team calendar: `src/Cosmix.Application/HR/Leaves/LeaveTeamCalendarContracts.cs`,
  `LeaveTeamCalendarService.cs`, `LeaveTeamCalendarProjection.cs`, and
  `src/Cosmix.Api/Controllers/HR/LeaveTeamCalendarController.cs`.
- Recovery: `src/Cosmix.Application/HR/Leaves/LeaveRecoveryReservations.cs`,
  `LeaveRecoveryEvidence.cs`, `LeaveEncashmentService.cs`.
- Medical evidence: `src/Cosmix.Application/HR/Leaves/LeaveService.cs`,
  `LeaveEvidence.cs`, `LeaveReviewAuthority.cs`.
- Self service: `src/Cosmix.Application/HR/EmployeeSelfService/EmployeeSelfService.cs`
  and `src/Cosmix.Api/Controllers/EmployeeSelfService/EmployeeSelfServiceController.cs`.
- Outstation: `src/Cosmix.Domain/HR/Allowances/DeploymentValuation.cs`,
  `DeploymentContinuity.cs`, `src/Cosmix.Application/HR/Allowances/DeploymentClaims.cs`,
  `DeploymentService.cs`, `src/Cosmix.Api/Controllers/HR/DeploymentsController.cs`.
- Vehicles: `src/Cosmix.Application/HR/VehicleFuel/VehiclePolicyContracts.cs`,
  `VehiclePolicyRulesCodec.cs`, `VehiclePolicyPublication.cs`, `IVehicleFuelRepository.cs`,
  `src/Cosmix.Infrastructure/Repositories/HR/VehicleFuelRepository.cs`,
  `src/Cosmix.Api/Controllers/HR/VehicleFuelControllers.cs`.
- Reports: `src/Cosmix.Application/HR/Reports/HrReportContracts.cs`,
  `HrReportService.cs`, `HrReportCsvExporter.cs`, `HrReportXlsxExporter.cs`,
  `src/Cosmix.Api/Controllers/HR/HrReportsController.cs`.
- Standalone verification: the six new suites listed above plus
  `docs/hr-srs-remediation/VerifyPrivateStorageContinuation.cs`,
  `VerifyHrReportBoundary.cs`, `VerifyHrReportsRemaining.cs`,
  `VerifyRemediationContracts.cs`, `VerifyDeploymentPolicyBoundaries.cs`, and
  `docs/attendance-completion/VerifyAttendanceComposition.cs`.
- Handoff records: this file, `LeaveTeamCalendar-20261019.md`,
  `Private-Storage-Continuation-20261019.md`, remediation `Verification.md`,
  `docs/SRS-Module-Map.md`, and attendance verification/UAT index links.

No application connection strings, grants, existing credentials, migration
sources or Git state were synchronized. No separate client was implemented.
