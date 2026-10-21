# A02 execution result — 13 October 2026

Implemented the approved private employee document lifecycle in the latest dirty backend and its
existing web document consumer. No commit/push, SQL connection/application, account operation,
provider activation, key generation, production configuration or private employee file was used.

## Implemented

- Retained the PDF/PNG/JPEG extension/MIME/signature checks, 10 MiB limit, AES-GCM private storage,
  trusted scanner interface and scan-before-availability. Upload bytes are owned across awaits;
  scanner copies cannot mutate the stored upload/digest. Unknown or throwing scanners fail closed.
- Added current/expired/revoked/archived lifecycle state and explicit permissioned expire/revoke/archive
  routes with printable reason, employee version, document version and idempotency receipt. Withdrawal
  is terminal; archive retains the object. No restore or delete operation was invented.
- Added append-only ordered document history with exact safe before/after states, actor, reason,
  time, revision and scan attempt. Immutable upload/privacy/date/object metadata cannot be replaced;
  every persisted mutation requires matching history. Unicode reasons at the existing 1000-character
  limit fit the bounded 20000-character escaped snapshots.
- Rescan start commits an unavailable active attempt before scanner I/O and clears manual verification.
  Interruption stays unavailable. Unchanged same-key retry can finish the attempt; a new explicit
  versioned intent replaces an interrupted attempt. Stale attempts cannot complete. Clean rescan
  requires a new manual decision. Failed/rejected/in-progress/conflicting evidence remains unavailable.
- Downloads authorize, read private objects outside the authorization transaction, then reauthorize
  current web rights and refresh/recheck current evidence/version/business-date validity. Denied
  plaintext buffers are cleared. Existing private attachment/no-store/nosniff/sandbox behavior remains.
- Added safe status/history and required/missing/expiring document/certification metadata routes.
  Identity/profile-photo requirements reuse current hiring rules. Future issue, expired/withdrawn
  evidence and incomplete/expired certification requirements block readiness. Valid upcoming expiry
  is a reminder; the query's explicit horizon is not an approved notification schedule.
- Current approved certification tips use the business date and A05's successor/status predicate.
  Their exact approved proof can be outside the original selected document manifest. Draft proof
  still must be selected. Generic unavailable selected IDs remain separate factual blockers; renewal
  never guesses that an old selected attachment can be discarded or ignored.
- Web rescan/verification now send ExpectedDocumentVersion; rescan/withdrawal require Reason. Existing
  in-memory uncertain payload/key handling remains. Lifecycle/scanning states and permission-gated
  withdrawal controls are rendered from actual server metadata.

## Changed files owned by A02

Backend:

- `src/Cosmix.Application/HR/Employees/EmployeeDocumentService.cs`
- `src/Cosmix.Application/HR/Employees/EmployeeDocumentContracts.cs` (new)
- `src/Cosmix.Application/HR/Employees/EmployeeDocumentCompleteness.cs` (new)
- `src/Cosmix.Domain/HR/EmployeeDocuments/EmployeeDocument.cs`
- `src/Cosmix.Domain/HR/EmployeeDocuments/EmployeeDocumentHistory.cs` (new)
- `src/Cosmix.Infrastructure/Repositories/HR/EmployeeDocumentRepository.cs` (new)
- `src/Cosmix.Infrastructure/Database/EmployeeDocumentWriteGuard.cs` (new)
- `src/Cosmix.Infrastructure/Database/Configurations/HR/EmployeeDocumentConfiguration.cs`
- `src/Cosmix.Infrastructure/Database/Configurations/HR/EmployeeDocumentHistoryConfiguration.cs` (new)
- `src/Cosmix.Api/Controllers/HR/EmployeeDocumentsController.cs`
- `docs/Employee-Document-Lifecycle.md` (new)

Web:

- `cosmix_frotned/src/modules/hr/components/documents/EmployeeDocuments.tsx`
- `cosmix_frotned/src/modules/hr/schemas/hiring.ts`

Standalone verification sources and temporary-project runner are under this audit directory:
`VerifyEmployeeDocuments.cs`, `VerifyEmployeeDocumentService.cs`, `VerifyEmployeeDocumentLifecycle.cs`,
`VerifyEmployeeDocumentGuard.cs`, `RunEmployeeDocumentChecks.ps1`. No permanent test project was added.

## Executed verification

RED, before implementation:

- Domain baseline: **0 passed / 7 failed**, because dated availability, scan attempt and lifecycle
  methods were absent. The failures identify expired evidence and interrupted/stale scan behavior.
- Real EmployeeOperation/EmployeeDocumentService with synthetic in-memory ports: old download returned
  private bytes after grants were revoked during file I/O (**failed**).
- Added Unicode boundary check reproduced a valid 1000-character reason failing the original new
  3000-character escaped history limit (**7 passed / 1 failed**); then corrected the bound.

Latest GREEN, against fresh Application/Domain Release assemblies after the final actor-bound retry,
Unicode and approved-certification updates:

- `dotnet build src/Cosmix.Application/Cosmix.Application.csproj --artifacts-path <A02 TEMP>/Artifacts -c Release --no-restore -m:1 -p:UseSharedCompilation=false -p:OutDir=<A02 TEMP>/ReleaseBuild/`
  **succeeded, 0 warnings / 0 errors**.
- `VerifyEmployeeDocuments`: **8 passed / 0 failed**.
- `VerifyEmployeeDocumentLifecycle`: **20 passed / 0 failed**. Covers authorization revocation during
  I/O, cleared denied buffers, dedicated permissions, stale document versions, receipt replay/conflict,
  medical privacy mismatch, canceled same-key scan retry, withdrawal during scanning, failed/invalid
  scanner responses, malware verification/review denial, mutation-safe upload, signature/size denial,
  natural expiry, selected evidence, independent certificate expiry and approved renewal evidence.
- Original download-race harness: **passed**, revoked actor receives 403.
- Web `npm run build`: **passed** TypeScript/Vite. Existing bundle-size warning remains.
- Scoped document-file ESLint and Prettier checks: **passed**.
- Backend owned-file `git diff --check`: **passed**.

Material integration checks still assigned to root:

- The isolated Infrastructure attempt failed with **20 CS1061 errors** in concurrent A05
  EmployeeAssetCustody/EmployeeCertification/EmployeeSimAssignment configuration edits, compiled
  against their earlier Domain snapshot. No A02 error was reported. Root's coordinated final
  solution/model build must run after the A04/A05 sources settle.
- `VerifyEmployeeDocumentGuard` is prepared for the current combined Infrastructure output; it has
  not yet run. It checks mapped restrictive history/concurrency/constraints, exact paired history,
  pending-review withdrawal without frozen profile rewrite, denied missing history/deletion/object
  replacement/scanner rewriting and immutable history. Run the supplied runner with the final
  Release output: `RunEmployeeDocumentChecks.ps1 -BuildDirectory <root Release output>`.
- Global `npm run verify` reached and passed lint, then failed formatting on four unrelated Attendance
  files: `AttendancePages.tsx`, `EligibilityPages.tsx`, `ProposalForm.tsx`, `ReviewUi.tsx`. Build was
  skipped by that command; the separate build above passed. Root should rerun global verification
  after that writer settles. Those files were preserved by A02.
- No authenticated HTTP/browser, SQL transaction race, real ClamAV malware detection or physical
  encryption-key backup/restore drill was claimed.

## Root-owned integration and schema

Root has integrated the exact requested source wiring:

- DI: `IEmployeeDocumentRepository -> EmployeeDocumentRepository`.
- Permission catalog: `hr.employees.documents.expire`, `.revoke`, `.archive`, sensitive management.
- DbContext: invoke `EmployeeDocumentWriteGuard.Validate(this)`; exclude only Modified
  EmployeeDocument from the frozen profile-record revision guard. Added uploads keep the original
  pending-form block. Modified docs retain a tracked parent, exact document rowversion and history;
  approval compares the frozen document versions in the serializable transaction.
- EmployeeDocumentResponse adds optional LifecycleStatus, IsScanInProgress and HistoryRevision;
  MapDocument(date), current snapshot/approval mapping and protected draft-requirement helper are
  root-owned. Shared post-hire evidence validation should use IsAvailableOn(date).
- Current-history repository reuses A05 EmployeeCertificationStatus/current successor fields; it
  returns only current tips at the configured business date and preserves terminal status blockers.

Root owns generation/review of schema/snapshot/migration files. Required model additions are:
LifecycleStatus default Current, nullable lifecycle actor/reason/time; IsScanInProgress default false,
nullable ActiveScanAttemptId/ScanStartedAt; HistoryRevision default 0; alternate document key
(Id,EmployeeId); new `hr.EmployeeDocumentHistory`, restrictive employee/document FKs, unique
(EmployeeDocumentId,Revision), bounded reason/actor and 20000-character snapshot mappings.
No earlier migration was edited and no migration was applied. Legacy history is not fabricated.

## Configuration and policy remaining

Approved ClamAV deployment/coverage, storage/key/ACL configuration, key rotation/backup and actual
restore acceptance remain external work. No actual approved configuration was supplied or enabled
by A02. Category-specific required-document rules, notification schedules and retention/hold/orphan
cleanup approval remain explicit decisions. Private objects and incomplete/unreferenced allocations
are retained; no age-based cleanup, arbitrary orphan deletion, provider or clean scan was invented.
Face capture/template storage remains separate. These implementation checks do not certify a usable
production scanner, completed live activation or a completed restore drill.
