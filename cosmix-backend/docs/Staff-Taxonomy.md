# Staff taxonomy publication and dated mappings

The taxonomy framework uses the existing `Policy`, immutable `PolicyVersion`, generic approval, organization journal, audit and receipt tables. It introduces no entity, EF mapping or migration. Its dedicated policy code is `HR-TAXONOMY-STAFF`, namespace prefix is `HR-TAXONOMY-`, rules schema is `hr-staff-taxonomy-v1`, and approval request type is `HrStaffTaxonomyPublication`. Other policy schemas cannot supply taxonomy authority.

No broader category, management approval, role grant, eligibility value, rate, allowance or external-provider setting is seeded. Real category values, authority and operational acceptance remain required. A request is a protected proposal; it has no published mapping and does not activate employment, attendance, access or payroll.

## Authority and access

The `StaffTaxonomyAuthority` configuration section contains `Mode`, `ApprovedVersion`, `EffectiveFrom`, and exclusive `EffectiveTo`. Mode defaults to `Unavailable` (0). Only explicitly approved `IndependentFixedAdministrator` (1), a nonblank approved version and a valid current/intended-start window allow new request/decision writes. Configuration is deployment-owned; this feature does not grant a caller permission to alter it. Changing the authority stamp requires a fresh proposal. Saved publication proof preserves the original authority stamp; later expiry does not erase historical approval.

All administration requires a live Web SMS-MFA session and an All-scope grant. `hr.staff-taxonomy.read` reads metadata, proposals, history and effective mappings. `hr.staff-taxonomy.request` submits a proposal. `hr.staff-taxonomy.review` additionally requires the same grant's current fixed Admin/SuperAdmin assignment. The requester cannot review their proposal. Role names or a custom role's permission alone cannot satisfy fixed review authority. The shared permission catalog and DI registrations are coordinated by the integration owner; there are no automatic grants.

Writes require the exact `Idempotency-Key` format used by Organization operations, required reason, private written evidence and expected SQL rowversions. A business result, private audit reference, append-only organization journal and receipt commit in the existing serializable authorization transaction. Identical retries preserve IDs and original versions; different payloads under a key fail. Live rights, fixed role and applicable authority/resource checks run before replay. Receipts contain safe result identifiers and state, not policy evidence or storage paths.

After private I/O, writes reacquire the exact original session/identity and role assignment, current grant and fixed review status. Resource, candidate rowversions/revision/state, locks and referenced policies are checked again, followed by a fresh actor and business-date/authority-window guard at the final save boundary. A replacement session or different role assignment cannot substitute for the original writer. Fixed role codes follow the domain's canonical `ADMIN`/`SUPERADMIN` contract.

## Routes and payload fields

Routes are under `/api/v1/hr/staff-taxonomy` and use current browser CSRF protection.

The three POST actions validate the original bounded request bytes before MVC binding. Duplicate properties (including nested flags and decision values), unknown or case-mismatched members, missing required fields and non-strict numbers fail with 400. Requests are limited to 196,608 bytes; decision/cancel bodies to 49,152 bytes, including chunked bodies. Oversize bodies fail with 413. Valid bodies are rewound for normal typed binding; these filters are feature-local.

| Method | Suffix | Result |
|---|---|---|
| GET | root | Reserved policy identity/revision/rowversion, or no record before the first request |
| GET | `/effective?categoryCode=...&on=...` | Dated category mapping, policy identity/hash, or explicit unavailable state |
| GET | `/history?page=...&pageSize=...` | Retained immutable publications and verification state |
| GET | `/requests?page=...&pageSize=...` | Retained proposal/decision states |
| GET | `/requests/{id}` | Exact protected proposal and authority version |
| POST | `/requests` | New protected proposal; does not publish |
| POST | `/requests/{id}/decision` | Independent approve, return or reject; approval creates one immutable version |
| POST | `/requests/{id}/cancel` | Current requester abandons a pending proposal; no version is published |

The request JSON fields are `expectedPolicyVersion` (null for the first request), `expectedRevision` (0 before the first request), `effectiveFrom`, exclusive nullable `effectiveTo`, `rules`, `reason` and `writtenEvidence`. Existing policies require their current Base64 SQL rowversion and current revision. Reasons are 1–500 characters; private written evidence is 1–8000. New effective starts must be after the current Pakistan business date, later than every previous taxonomy start, and outside locked attendance periods. Only one pending request may exist.

Decision JSON contains `expectedPolicyVersion`, `expectedRequestVersion`, `decision`, `reason`, and `writtenEvidence`. Decision values follow existing `ApprovalState`: 1 Approved, 2 Returned, 3 Rejected. The current policy revision, original publication timeline, both rowversions, exact protected proposal, current authority stamp and required policy references are checked again. Return/reject retain the proposal and decision evidence and do not publish a version. A fresh key is required for a distinct action.

Cancellation JSON contains both expected versions, `reason` and `writtenEvidence`. Only the original requester with current Request authority may cancel a pending request. This remains available when publication authority expires/changes so a stale proposal can be replaced; it never publishes or enables policy. Its reason/evidence is retained in the protected audit. Cancelled requests remain in history, and cancellation retries still recheck requester/current rights.

## Mapping schema and interpretation

`rules` supplies required `schemaVersion: 1` and a complete `categories` array. Every category has required `code`, `name`, `workLocation`, `scheduleBasis`, `requireSiteBeforeActivation`, `outsideOvertimeInheritance`, and `policyReferences`. Codes follow the existing staff-category rules: immutable uppercase canonical code, maximum 30 characters. Names are maximum 150. Unknown JSON members, duplicate properties/codes, missing booleans and unknown enum values are rejected. Canonical JSON is sorted deterministically and must fit the existing 32,000-character policy limit.

`workLocation` and `scheduleBasis` support existing Office (1) and Site (2) sources and must agree. Other work-location/scheduling shapes require a separate implementation; a fake office/site is never supplied. A Site mapping may explicitly require a site before activation. Every scheduled Site duty requires a real site; every activated Office mapping requires a real office and forbids a site. Draft validation allows incomplete required selections while rejecting forbidden ones.

The complete mapping must retain the confirmed baseline entries:

- `OFFICE`: Office location/schedule, no site and no outside-OT inheritance. Office OT cannot be enabled by a taxonomy version.
- `SITE`: Site location/schedule, optional initial site before activation, and common outside-OT inheritance. This flag does not enable common OT or override employee/common denial.

Every category supplies exactly one reference for each use: Attendance (1), Leave (2), Meal (3), Deployment (4), Fuel (5). Applicability is explicitly NotApplicable (1) with no version, or Required (2) with an exact immutable `policyVersionId`. Omission and Unconfigured (0) are not publishable. Required references must exist in the corresponding policy category and cover the proposed period. This reference check is not proof of underlying policy authority or eligibility: consumers must verify their own exact policy, assignment/session/deployment evidence and authority. A taxonomy decision, category label or approval route alone never calculates or pays a claim.

Successive full taxonomy revisions use later effective starts and preserve all old rows. The latest started revision of the one taxonomy policy applies; an expired successor does not restore an older open-ended revision. Competing same-date/policy sources, unknown schemas, invalid proof and missing category mappings remain unavailable. A pending-only policy reservation retains the confirmed OFFICE/SITE baseline. A valid active first future publication also preserves that exact original baseline and fingerprint for dates before its first effective start; broader codes remain unavailable there. This narrow historical exception requires complete verified, ordered, active publication history. It never restores baseline after a version starts, expires, is inactive or has invalid/unknown evidence. It creates no backdated policy version and changes no old assignment or evidence.

Publication validity requires its exact request type/subject/stage, approved independent decision, fixed-review assignment evidence, matching original authority stamp, exact private request/decision hashes and canonical rules, matching effective period, complete ordered predecessor versions and their saved timeline fingerprint, publisher and time. Private I/O failures propagate; invalid evidence does not become eligibility.

## Integration contract

`IStaffTaxonomyResolver.ResolveAsync(string categoryCode, DateOnly on, CancellationToken ct)` returns `StaffTaxonomyResolution` with state, mapping, policy-version ID and immutable mapping fingerprint. `ResolveForRangeAsync(code, from, toExclusive, ct)` returns unavailable if the interpretation changes or expires within an assignment/overnight interval. Call these inside the caller's existing authorization/consistency transaction; the resolver itself is not an authorization boundary.

`StaffTaxonomyResolver.RequireMapping(result)` rejects unavailable results. `ValidateLocation(result, branchId, siteId, StaffTaxonomyValidationPhase)` and `MissingLocationFields(...)` support Draft, Activation and ScheduledDuty checks. Hiring must recheck approved mapping at submission/decision, preserve exact policy evidence in protected snapshots, and retain dated assignments. Attendance must select the declared schedule/boundary source and pin mapping identity/hash in roster/capture/summary/OT evidence. The integration owner and Part B own their existing files. Existing `GlobalSite` OT history must not silently authorize broader categories without the explicit dated approved mapping.

Claims modules must bind factual source date/assignment, this taxonomy result, their separate policy/proof and the shared route fingerprint. Rehire remains a separate employment-episode workflow. Category conversion requires separately approved authority and dated assignment history; this publication API does not perform conversion.

## Verification boundary

Standalone checks use actual codec, resolver, authority, operation, service and domain entities with synthetic in-memory persistence/auth/private-file collaborators, plus the actual controller/filter with ASP.NET resource contexts and MVC-style body deserialization. They do not establish SQL contention/rollback, genuine storage/scanner/provider, authenticated browser/device, actual employee or operational policy acceptance. Release builds use isolated TEMP artifacts. No permanent test project, schema/database application, provider/account provisioning, deployment or Git mutation is included.
