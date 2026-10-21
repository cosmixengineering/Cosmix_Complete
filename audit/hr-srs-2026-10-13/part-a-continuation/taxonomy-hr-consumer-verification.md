# HR taxonomy consumer verification — 13 October 2026

Backend-only source continuation in the shared dirty checkout. Existing unrelated changes were preserved. No SQL, provider, host, real employee/account, migration, EF-visible field, DbContext, global DI/catalog/permission, or UI action was performed by this consumer owner. Rehire remains disabled and unchanged.

## Review correction: separate historical and activation evidence

Independent review found that comparing a historical joining-date version to today's activation version rejected every fresh ordinary OFFICE/SITE hire across a publication boundary, even after resubmission. That comparison is superseded. Fresh protected submissions now retain the original `Taxonomy` joining-date evidence and separately append optional `ActivationTaxonomy` after the existing foundation-field token. Each interpretation is revalidated independently at approval. Today's activation code/baseline/version/fingerprint must match the frozen activation identity; its selected business date remains evidence but does not expire a proposal merely because approval occurs the following day under the same immutable version.

Ordinary past joining dates may therefore use an older baseline or immutable version while activation uses a freshly reviewed current version. The historical joining mapping is checked in Draft phase; the current mapping is checked in Activation phase. A WorkLocation or ScheduleBasis change raises `employee_staff_taxonomy_location_changed_review_required`; the flow does not implicitly convert initial employment. Old snapshots without the new activation pin remain approvable only when both historical and current interpretation are the exact legacy baseline. Publication requires one fresh proposal, not an endless resubmission loop.

Additional pre-fix RED run: **20 passed, 5 failed**, reproducing first effective publication with past OFFICE/SITE joining dates, an unchanged-semantic successor identity, next-day review, and the explicit semantic-location conflict. Fresh corrected GREEN: **25 passed, 0 failed** against actual hiring and materializer consumers. Publication-boundary scenarios use real Policy/PolicyVersion entities, canonical taxonomy rules and the actual pure dated resolver; protected-store and repository ports remain synthetic. Fresh corrected Application Release build: **0 warnings, 0 errors**. The existing foundation regression baseline was also rerun after this correction: **68 passed, 0 failed**. No full-solution build was repeated; root owns final integration verification.

## Delivered consumers

- `src/Cosmix.Application/HR/Organization/OrganizationMasterService.cs`: arbitrary guarded staff-category reference metadata may be created and reactivated. Existing request grammar, 30-character maximum, uppercase canonicalization, immutable codes, authorization, rowversion, history and audited retries remain. Metadata does not consult or publish the eligibility taxonomy; this avoids a create/publication dependency cycle.
- `src/Cosmix.Infrastructure/Repositories/HR/EmployeeHiringRepository.cs`: active selected categories resolve on the proposed joining date, or the current business date in an incomplete draft. Draft location checks use the resolver's Draft phase. An arbitrary unconfigured category cannot gain eligibility from its metadata.
- `src/Cosmix.Application/HR/Employees/EmployeeHiringContracts.cs` and `EmployeeHiringService.cs`: optional protected historical and activation taxonomy fields pin category code, resolution date, immutable version ID, fingerprint and baseline identity before submission. The optional foundation-field policy token is pinned at the same proposal stage. Office mapped submissions require a branch; the approval decision supplies initial Site, so a Site draft/submission can omit it. Approval revalidates the joining-date pin and today's activation identity against their separate frozen evidence. A changed fingerprint, version identity, or foundation-field policy token requires a fresh reviewed proposal even if the human label has not changed. Old unpinned snapshots remain readable and approvable only under the exact legacy taxonomy and absent foundation-field authority. Existing snapshot schema 1 remains backward compatible.
- `src/Cosmix.Application/HR/Employees/EmployeeApprovalMaterializer.cs`: the historical joining mapping is checked in Draft phase and the current approved mapping drives Activation phase location checks. Different immutable versions with compatible location/schedule semantics are supported. It retains legacy OFFICE/SITE semantics and legacy baseline error codes. Required Site is enforced at materialization before staging the employment graph.
- `src/Cosmix.Infrastructure/Repositories/HR/EmployeeUpdateRepository.cs`: amendments validate the existing category's effective-date approved mapping and location shape. The existing amendment still preserves StaffCategoryId and SiteId; arbitrary category conversion was not added.
- `src/Cosmix.Application/HR/Employees/EmployeeSiteAssignmentService.cs`: dated approved Site mappings can receive site assignments; Office mappings remain denied. The assignment preserves category and employment references.

Root made the adjacent `EmployeeApprovalService` call pass `revalidateTaxonomy: true`, and supplied `EmployeeFieldPolicy.AuthorityVersionAsync`. Production DI already registers the resolver. Optional resolver constructor parameters preserve pre-existing standalone fixture call sites; the fallback resolves only the exact legacy OFFICE/SITE baseline.

## Executed evidence

Standalone source: `taxonomy.HrConsumerChecks.cs`; reproducible runner: `run-taxonomy-hr-consumer-checks.ps1`. The runner creates a temporary console project referencing the real Application project; it does not create a repository test project.

After correcting synthetic fixture setup, the pre-change RED run had **4 passing baseline checks and 10 expected behavior failures**: master create/reactivate whitelist, missing proposal taxonomy pin, accepted fingerprint drift, accepted unavailable activation date, accepted broad unpinned approval, missing Office branch requirement, unsupported broader materialization, and rejected broader Site assignment. No production edits preceded that behavioral RED run.

Fresh post-change GREEN: **18 checks passed, 0 failed**. Coverage includes metadata guards/no eligibility prerequisite, proposal immutable version/fingerprint pin, unchanged-label fingerprint replacement, joined-date/current activation-date mismatch, legacy snapshot compatibility, Office branch, activation-only Site requirement, Office Site denial, Site assignment date/category preservation, foundation-field token replacement, newly configured field authority versus an old snapshot, actual materialized assignment shape, and no staged graph after failed activation.

Temporary execution project:
`C:/Users/Huma.Khan/AppData/Local/Temp/cosmix-taxonomy-hr-checks-ac348489189e49d38737655b8a9b84e7/Checks.csproj`.

Fresh Application Release build: **0 warnings, 0 errors**. Existing `part-a/RunFoundationFixChecks.ps1` regression baseline: **68 checks passed, 0 scenarios failed**, with actual domain/services/controller metadata and synthetic collaborators.

The attempted full solution Release build failed outside these owned consumers at `SelfHostedFaceConfigurationReadiness.cs:31`: missing `ConfigurationBuilder.AddJsonStream` dependency/extension in concurrent Part B work. Root was notified and owns final fresh full-solution Release, global DI/model and A03 integration checks after that owner's correction. Do not treat the Application build as full-solution acceptance.

## Remaining boundaries

The focused checks use synthetic authorization, repository and protected-store collaborators. They verify actual consumer behavior, not SQL transaction rollback, authenticated HTTP, production private storage, provider/device acceptance, or operational approval of real taxonomy values. The approved existing isolated database remains root-owned.

Future appointments whose joined-date taxonomy identity differs from today's activation identity fail closed at approval. A separate reviewed future-activation path is not introduced here. Existing-employee category conversion remains excluded; it requires its own approved effective-date authority and evidence workflow.

Attendance scheduling, capture, OT and other Part B consumers must consume this taxonomy under their owner's separate patch. These HR changes alone do not make new categories usable in Attendance or create allowance/meal/fuel/OT entitlement. Frozen A23 and migrations remain untouched by this owner.
