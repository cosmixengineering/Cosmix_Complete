# Legal-Review Evidence Recording

This slice records evidence only. Every dossier is `review_required`, `recorded_not_legally_verified`, with `activationAllowed: false`. It supplies no legal clearance, appointment, verified signature, activation workflow, payroll posting, deduction collection or payment release. Active payroll legal activation remains blocked. The counsel/HR/SuperAdmin sequence in `Legal-Applicability-Research-20261012.md` is a proposal, not formal authority. Possession of the recording permission grants technical evidence-recording access only.

## Evidence and Versions

The exact attendance policy and policy version are looked up through `IAttendancePolicyRepository`. Stored rules pass the existing rules codec; their canonical UTF-8 JSON SHA-256 must equal `expectedPolicySha256`. The server records that canonical JSON, source policy/version IDs and source version number. The dossier's effective interval must fall inside the source version interval. Historical source versions can be recorded; this never implies that they apply to current payroll.

A binding SHA-256 includes schema version 1, exact policy/version IDs, canonical rule hash, explicit supported jurisdiction code, full worker/establishment scope and start-inclusive/end-exclusive dates. Different source versions, rule hashes, jurisdictions, scope identifiers/text or dates have different chains. A new policy version cannot inherit another version's dossier, even when its rules are identical. Clients retain the returned `bindingSha256` for chain reads; this slice provides no global search/discovery endpoint and never looks up clearance from policy name or current province alone.

The initial request requires `expectedRevision: 0` and explicit `supersedesId: null`. Later requests require the exact latest revision and predecessor ID. At most 128 immutable revisions are retained per binding. Supersession appends an event; no update/delete/cancel/activation route exists. Changed material scope opens a distinct record-only chain rather than amending earlier evidence.

The private dossier body contains the reported reviewer identity/qualification assertion, reported signing timestamp, rule scope/source description, reported HR author, dated written HR applicability evidence, proof metadata, reason, recorded account/time, server-resolved scope evidence and both PDF metadata/private references. These are recorded assertions. The API does not authenticate the reported reviewer/HR author, validate professional standing/delegation, establish competent statutory authority, interpret the law or verify a handwritten/digital signature. The authenticated recorder identity is server-selected and separate from the reported reviewer identities.

## Scope Facts

Jurisdiction codes reuse the existing `PK-PB`, `PK-SD`, `PK-KP`, `PK-BA`, `PK-ICT` reference identifiers. No Head Office, city-name or federal fallback occurs.

An employee-specific scope requires an existing employee and explicit active staff category. One existing employment-category assignment must cover the entire interval without any overlapping/transferring assignment. Optional branch scope must match that assignment's actual `BranchId` and an existing active branch. These facts do not prove a statutory worker or establishment classification; `workerCategory` and `establishmentScope` remain reported descriptions requiring legal review. Non-employee category scopes check the active master but do not claim to have classified every employee in that category.

For an employee-specific site, the repository independently reads the actual Admin site-assignment module's dated history through `IEmployeeSiteAssignmentRepository.ListAsync`, bounds that history to 100 source rows and requires exactly one matching full-interval assignment. The dossier retains employment `assignmentId` and independently resolved `siteAssignmentId`, dates and version. Inspection of this checkout's `EmployeeSiteAssignmentRepository` and `EmployeeSiteAssignmentService` shows that this module stores dated site transfers as `EmployeeAssignment` rows, not a distinct `SiteAssignment` entity. The IDs can therefore coincide. The legal-review repository does not directly use the employment row's `SiteId` as site proof. This source discrepancy is explicit; no separate entity or historical authority was invented.

Every site scope requires an explicit end date and at most 366 business dates. The existing `IAttendancePayrollJurisdictionReader` must return the requested site/date/code and one exact configured jurisdiction version for every date. Missing, conflicting or changing configuration versions fail; split the interval at a version boundary even if the jurisdiction code is unchanged. The exact version ID is retained. Branch/general jurisdiction remains `recorded_hr_assertion` because this checkout supplies no independent dated branch-jurisdiction resolver; no historical branch jurisdiction is inferred. Scope evidence is resolved again after provider I/O and compared to the preliminary snapshot; a race fails before storage. Retained historical dossiers are readable after master changes and remain record-only, never legally verified current applicability.

## PDFs and Bounds

Both `signedSupportingPdf` and `hrSupportingPdf` are required actual uploaded bytes encoded as JSON base64. Each is bounded to 4 MiB, has a bounded `.pdf` basename without path separators/colon and a PDF header. The existing `IEmployeeFileScanner` must return exactly `Clean` with a recorded scanner version. Then the concrete PdfPig 0.1.16 inspector parses bytes in strict mode, with maximum stack depth 64, rejects encrypted/empty/over-200-page documents, and parses every page with positive dimensions. A filename/MIME declaration/header alone is insufficient. No path or arbitrary URL is opened, no document link is fetched and no external resource is used by this adapter. A scanned handwritten signature remains unverified; parser success plus Clean plus SHA-256 is not signature verification.

Both PDFs must pass before any private write. The service clones bounded uploaded buffers, computes SHA-256 from those exact bytes, and verifies their hashes again after scanning/parsing. PDF metadata contains plaintext SHA-256, size, pages, parser/scanner versions and UTC scan time. Documents and dossier/audit/receipt bodies reuse the encrypted `IPrivateFileStore`; private locators are server-issued and never accepted from callers or returned in DTOs. Downloads recheck authority and retained bytes' size/hash and use fixed attachment filenames. Existing storage can leave encrypted unreferenced objects if an eventual database commit fails; cleanup must follow the existing storage lifecycle, not delete immutable retained evidence.

POST JSON is bounded to 12 MiB, depth 16, rejects duplicate/unknown/missing members, quoted numeric values and ambiguous member case. All text is bounded and rejects control characters: worker category 200, establishment description 1,000, identities 300, qualification assertion 1,000, rule/source scope 8,000, written HR evidence 16,000, proof metadata 4,000, reason 1,000 and PDF names 150 characters. Timestamps require explicit non-default UTC and cannot be in the future relative to recording. Dossier body reads/writes are bounded to 128 KiB; preflight additionally checks the serialized metadata/canonical-policy/scope size with reserved reference overhead before provider/storage work. Parser input/page/depth bounds do not supply process isolation or a hard per-document CPU/decompression memory deadline; hostile-PDF resource isolation/load acceptance remains deployment work.

## API and Authorization

Base path: `/api/v1/attendance/policies/{policyId}/versions/{versionId}/legal-reviews`.

| Method | Suffix | Result |
| --- | --- | --- |
| POST | none | Record an immutable evidence revision; requires `Idempotency-Key` |
| GET | `?bindingSha256=...` | Read and validate up to 128 retained dossier bodies |
| GET | `/{dossierId}/pdfs/signed?bindingSha256=...` | Download original reported-signed PDF |
| GET | `/{dossierId}/pdfs/hr?bindingSha256=...` | Download original HR proof PDF |

POST fields are `expectedPolicySha256`, `jurisdictionCode`, `scope` (`workerCategory`, `establishmentScope`, explicit nullable `employeeId`, `staffCategoryId`, `siteId`, `branchId`), `effectiveFrom`, explicit nullable `effectiveTo`, `expectedRevision`, explicit nullable `supersedesId`, `reviewerIdentity`, `reviewerQualificationAssertion`, `reportedSignedAtUtc`, `ruleScopeAndSources`, `hrAuthorIdentity`, `hrEvidenceAtUtc`, `hrWrittenEvidence`, `hrProofMetadata`, `signedPdfName`, `signedSupportingPdf`, `hrPdfName`, `hrSupportingPdf`, `reason`. There is no input state/activation/signature-verified field. JSON names use the API's existing camelCase convention.

All operations use `AttendanceOperation` without resource callbacks, which demands current web MFA and an explicit All-scope dedicated grant. Reads require `attendance.legal-reviews.read`; writes require `attendance.legal-reviews.record`. No role/default/automatic grant or Admin/SuperAdmin override exists. Existing web cookie/CSRF middleware protects POST. An exact retry revalidates current access before returning its protected record-only receipt; changed metadata or PDF bytes under one key conflict. Standard receipts expire after 24 hours. Replaying the receipt returns the original historical evidence response, never clearance for today's scope or latest rules.

Scanner/parser work occurs outside the authorization lock. Finalization rechecks authorization, source/hash, scope facts and predecessor inside the existing serializable authorization transaction. Row, audit and idempotency receipt save together. SQL uniqueness independently prevents duplicate revisions/successors. `IAppendOnlyEntity` uses the existing context guard against updates/deletes. No financial consumer or activation endpoint is implemented or labeled complete.

## Exact Root Integration

Root owns all shared edits. Application DI needs the `Cosmix.Application.Attendance.LegalReview` namespace and:

```csharp
services.AddScoped<AttendanceLegalReviewService>();
```

Infrastructure DI needs that Application namespace and the existing Attendance repository namespace:

```csharp
services.AddScoped<IAttendanceLegalReviewRepository, AttendanceLegalReviewRepository>();
services.AddScoped<IAttendanceLegalReviewPdfInspector, AttendanceLegalReviewPdfInspector>();
```

`AttendanceLegalReviewRepository` also consumes existing `IEmployeeSiteAssignmentRepository` and `IAttendancePayrollJurisdictionReader`; keep their current registrations. The service uses the existing `AttendanceOperation`, policy repository, scanner, private-file store and `TimeProvider`. In `AccessPermissionCatalog.All`, import the LegalReview namespace and add:

```csharp
.. AttendanceLegalReviewPermissions.Definitions,
```

Do not seed or automatically assign either permission. Recording access is not legal activation authority.

Infrastructure requires `<PackageReference Include="PdfPig" Version="0.1.16" />`; root confirmed addition, restoration/lock refresh remains root-owned. Package/API references were checked against [NuGet 0.1.16](https://www.nuget.org/packages/PdfPig/0.1.16) and the [tagged parser options](https://github.com/UglyToad/PdfPig/blob/v0.1.16/src/UglyToad.PdfPig/ParsingOptions.cs). No project/package file was edited by this worker.

No DbContext or Program edit is needed: `db.Set<AttendanceLegalReviewDossier>()`, assembly configuration discovery and MVC controller discovery reuse existing infrastructure. Root alone generates/reviews migration 21 and snapshot after the granted build slot. Mapping creates `attendance.AttendanceLegalReviewDossiers`, unique `(BindingSha256, Revision)` and filtered unique `SupersedesId`, restricted UserAccount FK, restricted `(SourcePolicyId, SourcePolicyVersionId)` FK to `PolicyVersion(PolicyId, Id)`, and a restricted predecessor FK retaining source version/policy hash/binding. EF needs the corresponding alternate keys on PolicyVersion and dossier. Root must inspect these additive principal keys in the combined attestation/dossier migration. There is no activation column. Fixed state/mode, uppercase SHA-256, nonempty interval, revision/predecessor and UTC checks apply. Do not apply SQL or migrations to the database with unconfirmed employee provenance.

## Verification Status

`VerifyLegalReviewEvidence.cs` is a standalone file-based actual-service regression harness, initially written before production source. It calls the real `AttendanceLegalReviewService`, real `AttendanceOperation` and real PdfPig inspector; only auth/persistence/scanner/private-store boundaries are in memory. It covers recording/state, actual PDF hashes/parsing, replay, altered payload, stale predecessor, MFA/mobile/non-All/revoked authority, source/version/hash binding, invalid text/size/fake PDFs, non-Clean rejection before private store, authorization and scope changes during scanning, protected-evidence tampering, private-write failure, downloads and new-version non-inheritance. EF model/query translation and append-only guards use an interceptor forbidding SQL connections.

This worker has not restored, built or executed the harness. The face agent holds the build slot; root expressly prohibited competing builds/tests. Red/green execution, compilation and all passing claims are therefore pending. After root acquires the slot and integrates packages/DI/catalog, root can run:

```powershell
dotnet build Cosmix.Backend.sln -c Release --no-restore
dotnet run --file docs/attendance-completion/VerifyLegalReviewEvidence.cs
```

No SQL/fixtures, project/test-project creation, migration/snapshot generation or Git operations occurred. Static source reading is not compiled/query/behavioral acceptance. Real SQL race/rollback, authentication/session/MFA/CSRF, permission provisioning, actual ClamAV detection, encrypted storage failures/restart/replay, malformed hostile-PDF resource behavior and authenticated downloads still need root acceptance. Model uniqueness is not proof of tested competing writers. The offline harness does not exercise real transactions, the existing site-assignment module's persistence or legal/signature verification.
