# HR Remaining SRS Audit 19 October 2026

The initial inspection found fabricated operational data, false success paths and missing management screens. The finite checklist below records **10 baseline gaps**, not a claim that all source files were audited. Historical evidence and line numbers below describe that baseline unless marked updated.

## Latest Follow-up Status

Later same-day continuation: [integrated implementation and verification](HR-Production-Continuation-20261019.md)
supersedes this table's pending durable-export and legacy-vehicle implementation
statements. Full bounded rich-HR CSV/XLSX/PDF jobs, reviewed legacy trip attribution
corrections and site-fact fixes are now implemented and checked offline. Live
acceptance is still separate. [Read-only diagnostics](LocalDB-Diagnostics-20261019.md)
confirm the original `Cosmix_Testing` catalog is ONLINE, not deleted.

This checkpoint supersedes the earlier in-progress ownership notes, not their
unexecuted live acceptance gates. Exact source lists, executed logs and limits are
in [the final follow-up record](HR-Followup-Verification-20261019.md).

| Baseline item | Current implementation / controlled verification | Remaining acceptance |
| --- | --- | --- |
| 1 Attendance list/detail | Fabricated facts and local success removed; actual factual sources and correction links; controlled checks passed | Authenticated saved/reloaded flows |
| 2 Shifts | Existing real shift/schedule workflow reused; fake policy/count/success branches removed | Live schedule/concurrency checks |
| 3 Employee dossier | Authorized fields only; missing/private fields no longer receive sample data | Authenticated field-scope UAT |
| 4 Biometrics | Honest read-only metadata, no fake capture/enrollment/revoke | Real enrollment/provider work remains deferred |
| 5 Sites | Real site/dated boundary and factual snapshots; no live-location/compliance fabrication | Browser UAT; documented repository semantics unchanged |
| 6 Benefits/recovery UI | Management award/history/correction/recovery/encashment review plus typed v4 settings implemented and controlled checks passed | Authenticated persistence and business configuration |
| 7 Consumed correction | Approved unused removal/consumed Admin record implemented; workflow 517/517 plus source/evidence/quota checks passed | Actual SQL atomicity/concurrency; separately approved recovery policy for the outstanding obligation |
| 8 Vehicles | Dated/mid-month backend and management UI implemented; final dated suite 25/25 and web checks 66 passed | Ambiguous assignments and already-approved legacy splitting retain explicit documented limits; live UAT |
| 9 Outstation | Deployment/home-break/calculation/claim management UI implemented; 44 author and 17 parent controlled checks passed | Approved rate/day rules and authenticated UAT |
| 10 Rich HR exports | Current-page row-count feedback corrected | Durable larger rich-HR exports/PDF still not implemented; existing factual Attendance export jobs remain intact |

Final backend Release build04 and final combined frontend TypeScript/Vite build
passed. No migrations or SQL fixtures were applied. These results do not mark the
whole HR SRS production-accepted or turn the historical unchecked live gates below
into passed tests.

## Scope and Evidence

- Source: [original SRS v1.2](C:/Users/Huma.Khan/Downloads/HR_Attendance_Employee_Mobile_SRS_v1.2.docx), read in memory through `FileStream` with `FileShare.ReadWrite` while Word could retain the file. SRS references below identify its requirement IDs or sections; code links identify inspected line locations.
- Current baseline: [SRS module map](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/docs/SRS-Module-Map.md:316), [HR scope](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/docs/hr-srs-remediation/HR-Scope-Continuation-20261019.md:3), and [latest benefit/recovery/vehicle results](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/docs/hr-srs-remediation/HR-Benefits-Recovery-Vehicles-20261019.md:88). Older statements that benefits/recovery are only settings or commitments are superseded.
- Confirmed overrides: Admin reviews leave/benefits; published policy selects encashment eligibility; personal leave belongs on mobile; policies require explicit effective-dated values. Mobile punches and authorized corrections remain the attendance channels. Locked months are not reopened.
- Excluded: Accounts posting, settlement, bank payments, automatic wage deductions, and actual face/OTP provider acceptance. Missing approved policy values are configuration dependencies, not permission to invent defaults.
- Read-only inspection covered the routed attendance/employee/site/shift pages, leave and vehicle/allowance clients, corresponding selected backend contracts/services, report paths, and dated verification records. Mobile implementation, all backend/frontend source, and current deployed state were not exhaustively audited.
- Snapshot: 19 October 2026, approximately 15:10 PKT. Parent benefit correction, vehicle-worker changes and the hiring-hook fix are concurrent work. Their later completion needs separate source and test evidence.
- This audit ran no build, host, SQL, migration, provider or Git mutation. It read existing test logs. At audit creation, its only file write was this report. Subsequent user-authorized SiteDetailPage repair must be evaluated separately.

## Prioritized Checklist

### 1. P1 Frontend attendance review invents facts and reports unsaved actions as successful

**Evidence:** [AttendanceProcessingPage.tsx:139](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_frotned/src/modules/attendance/pages/AttendanceProcessingPage.tsx:139) hashes employee/date to fabricate punches, hours, face/geofence flags and exceptions; site/department/shift are assigned by list index. [Line 441](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_frotned/src/modules/attendance/pages/AttendanceProcessingPage.tsx:441) exports those rows locally. [Line 491](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_frotned/src/modules/attendance/pages/AttendanceProcessingPage.tsx:491) displays manual-punch success without a request. The employee detail uses [SAMPLE_MONTH_LOGS at line 39](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_frotned/src/modules/attendance/pages/EmployeeAttendanceDetailPage.tsx:39), browser-only OT state at line 168, and an alert-only recalculation at line 531. These pages are wired to live routes.

**Backend/test distinction:** Real scoped factual reads already exist at [AttendanceReportsController.cs:16](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Api/Controllers/Attendance/AttendanceReportsController.cs:16). Build06 recorded capture 147, correction 196 and daily-calculation 193 passing offline checks; these do not test the fabricated React pages. SRS ATT-009, section 7.3, REP-002 and AC-003 apply.

- [ ] Close when list/detail/export use the same authorized employee/date evidence, missing evidence stays unavailable, and each permitted correction/recalculation shows a retained server result after reload. No arbitrary web raw-punch path or local OT entitlement. Test empty/denied/error responses, selected dates, cross-midnight evidence, locked periods and lost responses. Attendance-detail implementation belongs to Halley's assigned scope.

### 2. P1 Frontend shift creation and assignment can fail while the UI claims success

**Evidence:** [ShiftsPage.tsx:175](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_frotned/src/modules/attendance/pages/ShiftsPage.tsx:175) derives schedules from names and invents grace, OT multiplier, half-day thresholds and night pay. [Line 294](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_frotned/src/modules/attendance/pages/ShiftsPage.tsx:294) swallows create failure before inserting a synthetic shift/version. [Line 395](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_frotned/src/modules/attendance/pages/ShiftsPage.tsx:395) explicitly reports assignment success in the catch branch. Successful assignment also adds an invented 15 workers. The seed action publishes hard-coded schedules.

**Backend/test distinction:** [ShiftsController.cs:23](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Api/Controllers/Attendance/ShiftsController.cs:23) exposes actual shift detail; existing schedule/roster components are available. The older scheduling test report is not evidence for these current success branches. SRS HR-004, SHIFT-001/003/004, OT-005 and the dynamic-policy override apply.

- [ ] Close when effective schedules/policies and actual assignment counts drive the view; create/assign only succeeds from validated server responses; retries preserve their operation identity; and 400/403/409/network failures retain an actionable unsaved state. Verify saved/reloaded schedules, concurrent timeline changes and no inferred policy values. No new defaults or seed action should substitute for approved configuration.

### 3. P1 Employee attendance dossier fabricates identity, emergency and medical information

**Evidence:** [EmployeeAttendanceDetailPage.tsx:211](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_frotned/src/modules/attendance/pages/EmployeeAttendanceDetailPage.tsx:211) fills absent emergency contacts, blood group, CNIC, birth date, addresses and contacts with literal values. Line 250 supplies invented job/manager details. Line 317 always labels the employee active and biometrically enrolled. A failed or restricted profile query can therefore produce a convincing false dossier.

**Backend/test distinction:** Separate protected employee and medical reads exist at [EmployeesController.cs:25](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Api/Controllers/HR/EmployeesController.cs:25) and line 35. This finding establishes fabricated display values, not a demonstrated backend disclosure. SRS section 5, ROLE-004 and SEC-003 apply. No current browser regression was executed by this audit.

- [ ] Close when every displayed identity/contact/employment/status field comes from its authorized source, medical information respects its separate permission, and missing/denied data has no sample fallback. Verify two different employees, missing emergency details, inactive employment, denied identity/medical reads and reload. Halley owns this file; do not assign a second implementation worker.

### 4. P1 Biometric UI fabricates active enrollment and acknowledges failed capture or revocation

**Evidence:** [EmployeeBiometricSection.tsx:54](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_frotned/src/modules/hr/components/employee/EmployeeBiometricSection.tsx:54) initializes active enrollment and successful attempts. Empty/error responses preserve that state. [Line 289](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_frotned/src/modules/hr/components/employee/EmployeeBiometricSection.tsx:289) posts capture using a constant device ID, catches failure, then invents an active enrollment and confidence. Line 333 similarly acknowledges revocation after errors or without a real request.

**Backend/test distinction:** [FaceVerificationController.cs:62](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Api/Controllers/Attendance/FaceVerificationController.cs:62) separates capture from enrollment: capture returns a token, never verified enrollment. Provider acceptance remains deferred. SRS HR-008 and FACE-006/007 apply; this UI defect is independently fixable with controlled transport tests.

- [ ] Close when initial/failed reads show unknown or unavailable, only validated enrollment responses produce active status, capture alone never does, and failed revoke preserves the server-known state. Test no enrollment, permission denial, provider unavailable, capture-only success and stale-version revoke without contacting a real provider. Halley owns this component.

### 5. P1 Site detail combines real employee names with fictional deployment and location evidence (targeted frontend repair completed; acceptance pending)

**Pre-repair evidence:** The inspected version of `SiteDetailPage.tsx` defined fallback workers, check-ins, confidence, supervisor and geofence at former line 58, substituted preset boundaries at former line 494, and overlaid directory names/contacts onto fictional workers at former line 519. Former lines 625-640 labelled that output an active deployment roster and on-site attendance. A subsequent in-memory rendering check reproduced the false `100% Inside Active Geofence` claim against empty API responses before the repair.

**Current repair:** [SiteDetailPage.tsx:135](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_frotned/src/modules/hr/pages/SiteDetailPage.tsx:135) now gates access and validates the route; line 152 reads actual site metadata, operational details and effective-date geofence configuration with response/site binding. The page has no presets, directory-name substitution, inferred duty status, invented face scores, synthetic worker positions or claimed geofence compliance. [Line 92](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_frotned/src/modules/hr/pages/SiteDetailPage.tsx:92) maps only recorded coordinates; assignment effective dates and session-start UTC values remain explicit. Failed reads hide their stale evidence. Refresh reloads all three sources.

**Backend limits deliberately not disguised:** [SiteOperationalRepository.cs:58](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Infrastructure/Repositories/Attendance/SiteOperationalRepository.cs:58) selects the latest geofence rather than the effective boundary; the repaired page ignores that field and uses the dedicated geofence API. Assignment selection at line 118 omits a future-start exclusion, so the page labels these as dated assignment records, not a current deployed headcount. Lines 136 and 158 obtain open sessions started within 24 hours and their original check-in punches, not a live location feed. Latest shift versions are likewise not proof of an effective current schedule, so synthetic active-shift claims were removed. These backend semantics were inspected, not changed. SRS ROLE-002, ATT-005/006/007, section 6.1 and SEC-008 apply.

**Verification:** Non-emitting application TypeScript check, scoped ESLint and scoped Prettier check passed. Nineteen in-memory render/interaction checks passed for empty/loading/error states, permission denial, invalid/cross-site responses, future-boundary rejection, missing and zero-valued GPS, supervisor mismatch, search and full refresh. Four mocked map-effect checks passed for exact coordinates, incomplete-GPS omission/text-safe labels, tile failure and lifecycle cleanup. No test files were added. These are controlled component checks, not authenticated API/SQL acceptance or real-browser map/layout proof. No shared frontend build or host was started because other workers own the coordinated build; Halley was not exposed through the available chat tools.

- [ ] Acceptance still required: run the coordinated frontend build and authenticated browser checks for unknown site, empty roster, missing boundary, denied/failed reads, responsive map/table layout and reload. Verify any required current deployment/schedule semantics against corrected effective-date backend evidence before restoring those claims. The targeted frontend honesty defect is repaired; this item does not certify live workforce tracking or the full operational endpoint.

### 6. P1 Extended leave settings and Admin benefit/recovery work are not integrated into web management

**Evidence:** [leave-policy/contracts.ts:25](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_frotned/src/modules/attendance/operations/leave-policy/contracts.ts:25) strictly accepts only settings versions 1/2; [LeaveSettingsPage.tsx:208](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_frotned/src/modules/attendance/operations/leave-policy/LeaveSettingsPage.tsx:208) publishes version 2. Backend [LeavePolicySettings.cs:19](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Application/HR/Leaves/LeavePolicySettings.cs:19) supports v3 encashment/recovery and v4 benefits/fulfilment. A valid v3/v4 row fails the current frontend policy-array parser. Searches of the inspected HR/attendance routes and clients found no benefit grant/history or recovery-work review integration, despite [LeaveBenefitsController.cs:21](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Api/Controllers/HR/LeaveBenefitsController.cs:21) and [LeaveRecoveryWorkController.cs:36](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Api/Controllers/HR/LeaveRecoveryWorkController.cs:36).

**Backend/test distinction:** Award, unused revocation and actual recovery-work claims are implemented. Build06 records benefit workflow 332/332, recovery claims 79/79 and recovery policy 46/46. This is primarily a frontend gap, not missing backend models. SRS LEAVE-011/012/013 and section 9.1 apply.

- [ ] Close when web can read retained v1-v4 policy history, explicitly configure supported extended settings, and expose permitted Admin benefit/history/recovery review with server reasons and exact versions. Keep employee leave submission on mobile; remove residual personal encashment navigation from the web management experience where it conflicts with that override. Verify one v4 policy among legacy rows, rejected self-approval, partial recovery, OT exclusivity and policy-selected cash eligibility. Do not offer arbitrary encashment amount overrides: the backend rejects them.

### 7. P1 Consumed benefit correction still lacks the approved review workflow in the inspected baseline

**Evidence:** [LeaveBenefitService.cs:143](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Application/HR/Leaves/LeaveBenefitService.cs:143) rejects revocation when the year has utilization, reservation or encashment history with `leave_benefit_consumed_adjustment_review_required`. [VerifyLeaveBenefitWorkflow.cs:260](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/docs/hr-srs-remediation/VerifyLeaveBenefitWorkflow.cs:260) tests that denial, not a successful consumed correction. Positive aggregate balance cannot establish that a particular award is unused.

**Ownership and boundary:** Parent implementation is in progress. The latest user decision authorizes unused removal plus a consumed Admin review record, with no automatic salary or future-leave cut. Historical per-award allocation is unavailable. This audit does not certify the upcoming implementation or require a new accounting workflow. SRS LEAVE-011, APR-003 and AC-010 apply.

- [ ] Close against the approved design: Admin explicitly pins source ledger allocations; an immutable full award withdrawal is paired with protected consumed-only capacity credit; exact source release dynamically withdraws the corresponding credit; no source units are allocated twice. Preserve retained policy/day units and history. Test no usage, partial/full usage, reservations, encashment, exact and unrelated releases, duplicated allocation, changed sources, concurrent/replayed decisions and current Admin authority. Review the parent's current files before accepting this item; do not create a competing implementation.

### 8. P1 Vehicle management frontend implemented and frozen; integrated acceptance outstanding

**Current evidence:** [VehiclesPage.tsx:59](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_frotned/src/modules/hr/pages/VehiclesPage.tsx:59) now composes management-only views; all personal `/me/vehicles` forms/calls were removed from this page without changing shared client types or the mobile backend. [VehiclePolicyManagement.tsx:149](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_frotned/src/modules/hr/components/vehicle-management/VehiclePolicyManagement.tsx:149) provides retained history, explicit schema-3 rules, validation, fingerprint-bound publication and immutable succession configuration. [VehicleMonthlyManagement.tsx:99](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_frotned/src/modules/hr/components/vehicle-management/VehicleMonthlyManagement.tsx:99) provides server preview/partitions, submission, independent review, history and nullable entitlement. [VehicleIncidentManagement.tsx:33](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_frotned/src/modules/hr/components/vehicle-management/VehicleIncidentManagement.tsx:33) exposes initial review and retained amendments. [VehicleTripAttribution.tsx:46](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_frotned/src/modules/hr/components/vehicle-management/VehicleTripAttribution.tsx:46) exposes dated source segments, exact policy/commute references, proposal history and independent review. No new shared route/sidebar/permission registration is needed; backend MFA, configured authority and source validation remain authoritative. SRS VEH-003/004/005/008/009 apply.

**Retry and denial boundary:** [VehicleManagementShared.tsx:33](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_frotned/src/modules/hr/components/vehicle-management/VehicleManagementShared.tsx:33) coordinates only the new management writes. Pending intents block tab/record changes and SPA route departure; uncertain intents require an explicit discard/reconciliation warning. The local `useBlocker` follows the existing router pattern while allowing `/sign-in` and revoked authentication. Exact payload/key retry uses the unchanged transport hook; reload/close retains a native warning. A denied policy-list refresh unmounts and clears the selected workspace and cancels/removes its private queries. Denied monthly history hides and clears preview/entitlement data; a generation check prevents late preview responses from restoring cleared evidence after reload.

**Freeze verification:** 66 in-memory checks PASS, including blank-versus-zero settings, explicit schema-3 succession, preview invalidation, exact versions/fingerprints, incident null/amount handling, UTC segments, self-review denial, management-only access, retained idempotency retry, pending/uncertain tab/month/SPA guards, sign-in/auth-revocation exceptions, policy selection -> 403 -> reload and late monthly preview denial. New SPA and policy-denial checks failed against the pre-fix source before passing. Scoped ESLint and Prettier PASS; full application `tsc -p tsconfig.app.json --noEmit --incremental false --pretty false` PASS with exit 0. Vite production bundle PASS with `configLoader: 'runner'`, `write: false`: 2,441 modules and 18 generated outputs, no shared `dist` writes. No permanent test files, shared hooks/navigation, backend code, SQL, migration, host or Git writes. Parent reports backend build04 plus seven affected suites PASS; that result was not independently rerun here. These controlled checks are not authenticated browser/HTTP/SQL acceptance.

- [ ] Finish coordinated full type/build and authenticated browser/HTTP/CSRF/SQL acceptance, including stale sources, concurrent decisions, reload/reconciliation, permissions and responsive rendering. Preserve the [dated vehicle handoff's genuine limits](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/docs/hr-srs-remediation/Vehicle-Dated-Attribution-20261019.md): ambiguous multiple same-day assignments require an approved selection/allocation rule; already-approved legacy trips cannot be retroactively split by the pending-trip workflow. No insurer settlement, wage deduction, automatic rate or guessed approval policy is claimed.

### 9. P1 Outstation deployment and home-break management have no corresponding web flow

**Evidence:** [DeploymentsController.cs:20](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Api/Controllers/HR/DeploymentsController.cs:20) exposes scoped deployment request/decision, home-break request/decision, calculation and retained claim review. [AllowancesPage.tsx:63](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_frotned/src/modules/hr/pages/AllowancesPage.tsx:63) only selects ordinary claims/review/policies, while [finance-messages.ts:62](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_frotned/src/modules/hr/api/finance-messages.ts:62) still calls the deployment workflow unavailable. No deployment or home-break client calls were found in the inspected HR/attendance clients.

**Backend/test distinction:** The earlier HR-scope record reports outstation 65/65 and deployment-policy 36/36 offline checks. Missing actual company rates/counting choices are separate configuration dependencies. SRS ALLOW-002/003/004 applies.

- [ ] Close when authorized HR can request/review deployment, independently review departure/return home breaks, inspect pending versus calculated eligibility, and submit/review the retained claim through these APIs. Test policy changes within a trip, partial days, missing rules producing null amounts, pending returns not closing approved breaks, duplicate meal prevention, stale sources and reload. No Accounts settlement screen is part of this closure.

### 10. P2 Rich HR exports remain current-page CSV/XLSX and overstate the downloaded row count

**Evidence:** [HrReportService.cs:123](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Application/HR/Reports/HrReportService.cs:123) accepts only CSV/XLSX and generates bytes in the request. [HrReportsController.cs:80](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Api/Controllers/HR/HrReportsController.cs:80) explicitly exports the requested page. [HrReportsPage.tsx:188](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_frotned/src/modules/reports/pages/HrReportsPage.tsx:188) reports `totalCount` as exported rows, even when only a page was returned.

**Existing flow that must not be overlooked:** [AttendanceReportsController.cs:37](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Api/Controllers/Attendance/AttendanceReportsController.cs:37) already has factual attendance CSV/XLSX/PDF jobs, status and authorized download. The gap is the richer HR report catalogue, not the absence of all export jobs. Prior report tests demonstrate bounded CSV/XLSX projections and permissions, not a large rich-HR export job. SRS REP-001/002/003 and section 13 Reports apply.

- [ ] Close when current-view export reports actual returned rows and clearly identifies its page/filter; required larger HR exports and PDF use an authorized durable job/download lifecycle, reusing existing infrastructure where appropriate. Test more than one page, empty late pages, preview/export parity, revoked access, job retry/restart and readable PDF/XLSX output. Do not count a report-job entity alone as completion.

## Acceptance Evidence and Exit Gate

The build06 log was read and confirms zero warnings/errors. The 34 retained suite logs were read across `managed-fa0e8bddaeb341d2a1239e699e0d8d8c`, `managed-5471b2f1158d4aa19c303a3795ba2d56` and `managed-6c599973cc534515a69f9d171b417362` under `D:/CosmixVerification/part-c-parent-20261013`. They include real service behavior with synthetic collaborators, query translation, composition, generated contracts and offline model inspection. They are **not 34 authenticated integration tests** and do not establish the correctness of later concurrent edits.

| Evidence | What it establishes | What remains |
| --- | --- | --- |
| Build06 and 34 retained suites | Recorded baseline compiled; selected offline behavior passed | New correction/vehicle code requires its own fresh relevant results |
| Current frontend inspection | Directly traceable false-data/success branches and contract gaps above | Controlled browser regressions on the repaired routed pages |
| Prior frontend scheduling/browser results | Earlier synthetic transport and selected historical API behavior | Current source cannot inherit those passes automatically |
| [Admin onboarding record:42](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_frotned/docs/HR-Admin-Onboarding-2026-10-15.md:42) | Historical retained-submission errors were disclosed, not fabricated away | Recheck after the assigned hiring-hook fix; a complete fresh candidate workflow is not established by an empty pending queue |
| Current backend acceptance boundary | No current authenticated SQL acceptance claimed | Later authorized HTTP identity/CSRF, persistence/rollback, concurrency, private-file access and notification delivery evidence |

For complete HR acceptance, retain finite end-to-end evidence for draft/save/documents/submit/Admin review/access provisioning, effective employment changes and suspension, factual attendance/corrections, leave/benefit/recovery, deployment and vehicle decisions, and scoped report downloads. Verify reload, retry, stale versions and cross-employee denial in those flows. Face/OTP actual-provider testing remains deferred. This is a future acceptance gate, **not authorization to start a host, create fixtures or apply any migration now**.

No additional model-folder inventory or Accounts backlog is implied by this checklist. Close each numbered item with its own source change and relevant test evidence; do not label the whole HR panel complete from a product build alone.
