# Part A continuation: staff taxonomy discovery

This initial discovery phase was followed by the isolated taxonomy implementation. Current behavior and execution evidence are in `cosmix-backend/docs/Staff-Taxonomy.md` and this folder's `taxonomy-verification.md`; their explicit no-publication fallback rule supersedes the initial design wording below.

Read-only source analysis on 2026-10-13. No product source, EF model/snapshot/migration, core DI/catalog, SQL, provider, host, account or Git mutation was performed. This note is the only output. Line numbers describe the current shared checkout and may move as Part B proceeds.

## Exact restriction

There is no OFFICE/SITE enum or SQL code-list constraint. `src/Cosmix.Domain/HR/StaffCategories/StaffCategory.cs:9-14` accepts a guarded arbitrary code; `src/Cosmix.Infrastructure/Database/Configurations/HR/StaffCategoryConfiguration.cs:11-16` stores a unique required code (max 30) and rowversion. `src/Cosmix.Domain/HR/EmployeeAssignments/EmployeeAssignment.cs:10-29` already retains staff category, nullable branch/site and start-inclusive/end-exclusive assignment history.

The application/repository gates are:

| Use | Exact current source |
|---|---|
| Master create/reactivate | `src/Cosmix.Application/HR/Organization/OrganizationMasterService.cs:38-39,79-80`: only OFFICE/SITE. Codes are immutable. Existing other codes can be read/renamed/deactivated but cannot be created/reactivated. |
| Draft reference selection | `src/Cosmix.Infrastructure/Repositories/HR/EmployeeHiringRepository.cs:100-106`: active OFFICE/SITE only, SITE forbids branch. |
| Hiring submission | `src/Cosmix.Application/HR/Employees/EmployeeHiringService.cs:201-208`: OFFICE requires branch. |
| Approval/materialization | `src/Cosmix.Application/HR/Employees/EmployeeApprovalMaterializer.cs:132-137`: active supported category, OFFICE requires branch and forbids initial site; SITE forbids branch. SITE can be approved without a site, but no effective site roster can then resolve. |
| Employment amendments | `src/Cosmix.Infrastructure/Repositories/HR/EmployeeUpdateRepository.cs:79-98`: keeps the previous category, accepts only OFFICE/SITE and enforces category-specific branch rules. Category conversion is absent. |
| Site assignment | `src/Cosmix.Application/HR/Employees/EmployeeSiteAssignmentService.cs:63-65`: only SITE with no branch; later assignment row preserves category. |
| Scheduling eligibility | `src/Cosmix.Infrastructure/Repositories/Attendance/RosterRepository.cs:133-143`: only active OFFICE/SITE, mandatory site for SITE, mandatory active office for OFFICE. `:157-177` rejects other categories during effective-site lookup. |
| Runtime My Shift/roster | `src/Cosmix.Application/Attendance/Rosters/EmployeeRosterResolver.cs:13-17` selects SITE+site/no branch or OFFICE+branch/no site. `OfficeRosterResolver.cs:11-12` repeats OFFICE condition. |
| Office punch provenance | `src/Cosmix.Application/Attendance/Offices/OfficeAttendanceSourceReader.cs:25-32` requires the historical membership's code to be OFFICE. |
| OT category/eligibility | `src/Cosmix.Application/Attendance/Overtime/OvertimeEligibilityService.cs:20-24,34-37,66-67`: OFFICE always denied, all non-SITE unavailable, profile-setting writes SITE only. `OvertimeContracts.cs:119-120` repeats OFFICE denial in unavailable implementation. |
| OT source/request | `src/Cosmix.Application/Attendance/Overtime/OvertimeSources.cs:56-59` forces OFFICE denial; `OvertimeRules.cs:24-25` accepts only SITE. `OvertimeEligibilityVersion.cs:5,15-16` has only GlobalSite/Employee targets, not a category-target setting. |

## Consumers and current boundaries

- Capture, summaries and leave indirectly depend on the roster resolver: `Attendance/Punches/AttendanceCaptureService.cs:22,220`; `Attendance/DailySummaries/AttendanceSummaryService.cs:13,115`; `HR/Leaves/LeaveService.cs:14,54`. Changing only master/hiring validation would leave these new employees unsupported.
- `Attendance/Calculation/AttendanceCalculationSnapshotProducer.cs:57-59` currently requires `roster.SiteId`. `Rosters/RosterContracts.cs:41-42` supports an additive `BranchId`, but office calculation must coordinate with Part B. Its fingerprints include the roster (`:38-40`); taxonomy identity/hash should be retained in roster/capture/OT evidence so policy changes are detectable.
- `Infrastructure/Repositories/Attendance/SiteRosterRepository.cs:79-82` already includes every site-associated assignment irrespective of category. New site-mapped categories would enter temporary-shift staff manifests; `SiteRosterResolver.cs:25-32` checks exact affected employee membership. Do not exclude new categories from a temporary-change impact check after allowing them to resolve that site's roster.
- `Infrastructure/Repositories/Attendance/OfficeRosterRepository.cs:19-25,40-46` reads category and office membership without restricting codes. Office/schedule relationship and cross-midnight membership checks remain required.
- Generic attendance-policy applicability already uses StaffCategoryId: `Infrastructure/Repositories/HR/AttendancePolicyRepository.cs:43-57,61-66`; immutable resolution/fingerprint checks are in `Application/HR/Policies/AttendancePolicyResolver.cs:27-57`. Leave eligibility is already approved category IDs (`LeaveRepository.cs:66-67,110`, `LeaveTreatmentRules.cs:20`). No broad category should inherit a leave or attendance rule implicitly.
- Legal-review scope uses category ID/code plus exact historical branch/site evidence (`AttendanceLegalReviewRepository.cs:29,61-81`); broader category mapping cannot be taken as legal activation.
- Claims Application folders `HR/Allowances`, `HR/VehicleFuel`, `HR/AdvancesLoans` contain only README files, each explicitly stating at line 9 that business implementation is absent. Domain claim models alone are not executable claim consumers.
- Shared routing already filters category IDs (`Application/Common/Approvals/ApprovalRouteResolver.cs:29-38`). It supports allowance/fuel/vehicle-maintenance/leave-encashment/advance/loan/policy-exception and preserves fixed hiring/status/leave/correction/OT routes (`:11-17`). D/E must resolve the factual historical category, freeze its taxonomy evidence and route fingerprint, and implement the actual eligibility/decision/payment workflow.

## Minimal executable design

1. Add schema-isolated typed taxonomy contracts, canonical codec and dated resolver under `Application/HR/StaffCategories`. Each approved version supplies exact category codes/names; assignment-location requirements; supported schedule basis (existing office or site); explicit outside-OT applicability; and exact references to attendance/leave/meal/deployment/fuel eligibility policies. No category name should imply rate, allowance, geographic exemption, role or authority. Required mapping decisions are explicit; omission is unavailable, not false/zero/default.
2. Retain current OFFICE/SITE semantics as the historical baseline. A new approved mapping cannot make OFFICE OT-eligible. For broader categories, exact effective-date resolution must return one immutable version + canonical hash + category rule. Missing approval, missing category, overlaps, unknown schema, inconsistent authority and unsupported location/scheduling shapes fail closed. New policy changes may not retroactively reinterpret locked dates.
3. Persist publications through existing `Policy`/`PolicyVersion` tables with a dedicated schema and `PolicyCategory.EmployeeException`, avoiding an EF model change. `PolicyVersion.cs:11-25` already stores immutable schema JSON, effective dates, private document reference, approval reference, publisher and time; `Policies/Types.cs:3-13` supports the existing category and distinguishes ApprovedWorkflow/DirectHr. Use a dedicated exact taxonomy authority contract and verify its subject/decision/actor/proof. DirectHr is confirmed for attendance publication (`AttendancePolicyService.cs:72-74`), not automatically for every new HR policy. Generic approved IDs alone are insufficient (`AttendancePolicyRepository.cs:31-36`). A configuration timeline can be an interim resolver input, but does not complete publication/history administration.
4. Root integrates the resolver into OrganizationMasterService and the HR hiring/submission/materializer/update/site-assignment validations. Use dated mapping to validate branch/site selections; preserve incomplete drafts and recheck frozen submission at independent approval. Update protected evidence schemas so exact policy version/hash is frozen. Add future-dated category-conversion request only under an explicitly approved authority; do not rewrite an assignment's old category.
5. Part B integrates schedule-basis selection and provenance, roster/capture/summary/calculation evidence, and OT mapping. Preserve the GlobalSite/Employee history semantics: existing GlobalSite rows do not silently authorize other categories. Broader categories inherit the common outside switch only when the approved mapping explicitly says so and binds the dated taxonomy proof. Reuse the confirmed OT formula and source-evidence checks; do not alter or seed rates.
6. D/E consume the taxonomy result and explicit eligibility policy references when their claim workflows exist. Store exact source assignment/date, category mapping, verified session/deployment and route versions. An approved taxonomy label or routing rule is not itself a meal/fuel entitlement.

The framework can compile and prove unavailable/ambiguous/legacy/supported mappings with synthetic standalone checks while actual business values remain unconfigured. That is software readiness, not operational policy approval or full A03.2 completion.

## Genuine remaining business inputs

- Exact approved code/name list (SRS names examples, not stable codes) and effective dates/evidence for logistics, driver, rider, purchaser, field/complaint and any other category. SRS extracted lines 139,168,537 (MD-012).
- For each category, required base office/city or site, whether both can coexist, which actual existing schedule it follows, and how daily field/job changes behave. Existing office/site resolvers support mutually exclusive location shapes; mobile/job-based schedule sources need additional design if selected. Do not fabricate an office/site to fit the current model.
- Which broader categories join the confirmed outside-staff common OT switch, and their applicability boundaries. Office denial and the confirmed basic/source-month-calendar-days/nine-hour formula are settled in `docs/attendance-completion/Overtime.md:9-25`; do not reopen them. SRS lines 181,275,281 specify configured category eligibility, not blanket entitlement for all named examples.
- Explicit meal/deployment/fuel eligibility mappings and required evidence/authority. SRS lines 309-319 distinguish office/stationed-site/outstation treatment; line 322 limits vehicle policy to complaint resolution/field visits. Stationed-site dinner amount remains MD-008, not a taxonomy default.
- Exact authority/permissions and approval/evidence for taxonomy publication and existing-employee category conversion. The user's approval to implement is not a set of business mappings.
- Migration/reconciliation disposition for existing non-OFFICE/SITE codes and assignments, preserving all old evidence. A legacy code cannot automatically acquire attendance, OT or claims rights simply because it exists.

Named early-reporting exceptions in MD-012 must use effective-dated policy assignments; category expansion must not grant early-time credit implicitly (SRS extracted lines 183,264,537).

Rehire is separately blocked by employment-episode identity/approval/joining/compensation/assignment evidence decisions (`docs/Employee-Lifecycle.md:51-54`) and should be designed by its lifecycle owner, not bundled into taxonomy.

## Safe file ownership proposal

An independent taxonomy implementer may own only new files in `src/Cosmix.Application/HR/StaffCategories/` (contracts/codec/resolver/publication use-case interfaces), a new `Infrastructure/Repositories/HR/StaffTaxonomyRepository.cs` using existing policy entities, a new HR taxonomy controller, and dedicated documentation/audit harness files after root confirms scope. Root owns existing OrganizationMasterService and Employee* integration, core DI/catalog and publication authority. Part B owns existing Attendance/Rosters, Offices, Capture, Calculation and Overtime integration. D/E own claims consumers. No taxonomy EF-visible change is needed for the proposed existing-policy storage route.

Frozen A23 and earlier migrations must remain intact; Part B owns the next model/snapshot window (`docs/integration/Part-A-B-Ownership-20261013.md:22`). No SQL/provider/host/Git write window was opened for this discovery.
