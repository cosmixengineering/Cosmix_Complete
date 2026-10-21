# A03 implementation result — 13 October 2026

## Implemented

- Append-only `EmployeeBankVerification` journal ties verify/revoke to exact employee/bank record, actor, UTC date/time, version and predecessor. Private bounded text proof + reason is encrypted using server-owned purpose; references and full account data are excluded from current status/history DTOs.
- Five permissioned routes under `/api/v1/hr/employees/{employeeId}/bank`: current verification, verify, revoke, paged history and separately restricted evidence. Current Web MFA/All scope `hr.employees.read` + `hr.employees.bank.read` is required; `hr.employees.bank.verify` is additionally required for transitions/evidence. Permission revocation denies even idempotent replay. Stale employee/event versions, no-op state, wrong bank/employee, expired/replaced bank, timestamp regression and history conflicts fail.
- Existing current-bank update DTO resolves verification journal status; changing bank account creates a fresh unverified bank revision. Original bank data and original verification proof remain immutable.
- Hiring financial bank reads require independent bank-read; bank-containing draft creation/removal/approval requires bank-read + bank-request independently of salary permissions. The save replay authorization path also rechecks these bank rights.
- Existing international phone parser canonicalizes personal/company-SIM/login/emergency phones before draft/private snapshot storage and approved changes. Presented spaces/hyphens/parentheses are accepted; national numbers retain existing explicit-plus policy, with no guessed country. Employee domain storage rejects malformed/unnormalized contacts.
- Login identity uniqueness checks other non-rejected employee selections and live authentication bindings and reuse existing submit/approval rechecks and transaction protection. Existing identity/company-email uniqueness remains.
- Configurable approved-version/effective-date field policy supports controlled gender/marital/SIM values, father/spouse and optional required fields, age at joining, document-type expiry and probation. Missing draft fields are deferred to submission/approval; configured invalid values reject. Partial/unapproved/not-effective policy configuration fails with 503. No production taxonomy or threshold literals were supplied.
- OFFICE/SITE restrictions remain unchanged pending MD-012.

## Changed files

New files:
- `src/Cosmix.Domain/HR/EmployeeRecords/EmployeeBankVerification.cs`
- `src/Cosmix.Application/HR/Employees/EmployeeBankContracts.cs`
- `src/Cosmix.Application/HR/Employees/EmployeeBankService.cs`
- `src/Cosmix.Application/HR/Employees/EmployeeFieldPolicy.cs`
- `src/Cosmix.Infrastructure/Repositories/HR/EmployeeBankRepository.cs`
- `src/Cosmix.Infrastructure/Database/Configurations/HR/EmployeeBankVerificationConfiguration.cs`
- `src/Cosmix.Api/Controllers/HR/EmployeeBankController.cs`
- `docs/Employee-Bank-Verification.md`

Modified owned sections:
- `EmployeeFormContracts.cs` (presentational phone input bounds/record cloning)
- `EmployeeFormValidation.cs` (phone normalization)
- `EmployeeHiringContracts.cs` (optional BankVerificationId write result)
- `EmployeeHiringService.cs` (phone/field-policy and separate-bank permissions; root's concurrent A02 document changes preserved)
- `EmployeeApprovalMaterializer.cs`, `EmployeeApprovalService.cs` (policy/independent bank rights)
- `EmployeeUpdateSections.cs` (journal status)
- `EmployeeUpdateValidation.cs`, `EmployeeUpdateService.cs` (normalization/configured field rules)
- `src/Cosmix.Domain/HR/Employees/Employee.cs` (contact storage guards only)
- `src/Cosmix.Infrastructure/Repositories/HR/EmployeeHiringRepository.cs` (mobile identity uniqueness)

Root-owned wiring is already integrated: EmployeeBankService, EmployeeFieldPolicy, IEmployeeBankRepository=>EmployeeBankRepository; singleton EmployeeFieldPolicyOptions from config; sensitive management permission `hr.employees.bank.verify`. Existing phone normalizer remains registered. No direct root shared DI/context/catalog/snapshot edits were made by A03.

## Schema

Model mapping adds only `hr.EmployeeBankVerifications`: Id, EmployeeId, EmployeeBankDetailsId, Version, PreviousVerificationId, IsVerified, RecordedByUserId(200), RecordedAt and PrivateEvidenceReference(500). Same-employee/bank composite FK, same-bank predecessor composite FK, unique(bank record,version), unique non-null predecessor, positive-version/history/self-link checks and restrictive deletes. Existing IAppendOnlyEntity/IEmployeeProfileRecord guards require a tracked employee, protect history and participate in its SQL rowversion. Optional DbSet is not needed by the repository (`Set<T>` is used). Root owns reviewed new migration/snapshot/SQL generation. No database was migrated or touched by A03.

## Executed checks

- First TEMP red: exit 1, 3/3 expected failures (formatted international input rejected; malformed domain phone accepted; bank verification journal absent).
- Second TEMP red: exit 1, 3/3 expected failures for normalization/domain contact guard and absent configurable field policy.
- Basic green: Release TEMP dotnet run exit 0, assertions 3, failures 0.
- Expanded TEMP run: Release exit 0, assertions 27, failures 0. Real Application bank service + EmployeeOperation exercised with in-memory persistence/auth/file adapters: authorization denial, stale versions, ID tampering, exact evidence, verify/revoke/history, replay and permission revocation. Real domain rules, real international normalizer and approved fixture-only field rules exercised. Real offline EF model inspected and SaveChanges append-only write guard rejected a rewritten event before DB IO.
- `git diff --check` scoped to employee/domain/repository files: exit 0.
- Full solution Release build with isolated TEMP OutDir: Domain/Application/Infrastructure built; solution exits 1 due unrelated existing/current `SiteReportsController.cs` missing `SiteReportQueueListRequest` / `SiteReportQueueSiteResponse` (3 CS0246 errors, 0 warnings). Parent notified; A03 did not modify Attendance controller.

Standalone verification source is `a03-checks.cs`; `a03-checks-run.ps1` creates its console project only in TEMP and is rerunnable without a permanent repository test project. Build/standalone outputs do not prove SQL concurrency, transaction rollback/restart, real encrypted-storage deployment or authenticated HTTP UAT.

## Pending policy and acceptance

A03 is a concrete implementation plus policy framework, not complete production field-policy approval. SRS field labels do not provide gender/marital/SIM allow-lists, company ages, father/spouse applicability, required expiry document types or probation limits. Management must approve actual values/version/effective dates before configuring them. Empty configuration preserves existing baseline completeness. MD-012 broader driver/rider/purchaser/logistics/field taxonomy and scheduling/OT/claim mappings remains open; OFFICE/SITE unchanged. Policy publication/version-history UI remains unimplemented. Bank proof in this slice is private text attestation, not a new attachment-upload workflow. Reviewed migration/application, genuine authenticated HTTP/SQL races/restart persistence and deployment storage acceptance remain open.

No commit, push, database, real account or external provider side effects were performed. No permanent test project was created; unrelated dirty edits were preserved.

## Final A23 authorization rerun — 2026-10-13T13:05:20.9113610+00:00

The final standalone rerun passed: **Assertions: 27; failures: 0; SQL/provider/accounts side effects: none**, exit **0**. Final output is saved in a03-final-rerun.log; dependency SHA-256 values are saved in a03-final-dependency-hashes.json.

Only the audit a03-checks.cs fixture was adapted for the already-existing enhanced IsEmployeeActiveForAccessAsync authorization dependency. Its result is derived from the synthetic employee's exact ID, approved onboarding, Active status, joining date and the real EmployeeLifecycleAccessRules.IsReconciled rule. The fixture represents an initial approved hire with no lifecycle history rows; it does not blanket-authorize unknown IDs. The existing 27 assertions were preserved. This rerun covers the positive active-employment callback while retaining the existing verify/revoke, stale-version, ID tampering, permission-revocation/replay, exact private evidence, normalization, configured policy and append-only checks; it does not add separate SQL lifecycle acceptance.

The successful harness compiled only the audit Program.cs against frozen A23 Cosmix.Domain/Application/Infrastructure binaries supplied by the root in:
C:\Users\HUMA~1.KHA\AppData\Local\Temp\cosmix-part-a-20261013\model-final-artifacts\bin\Cosmix.Api\release

Exact command:
dotnet run --project "C:\Users\HUMA~1.KHA\AppData\Local\Temp\cosmix-a03-final-233297a24f5f47cabeaae879b0467450\Checks.csproj" -c Release --artifacts-path "C:\Users\HUMA~1.KHA\AppData\Local\Temp\cosmix-a03-final-233297a24f5f47cabeaae879b0467450\stable-artifacts"

There were **no project references and no production source rebuild** in the successful run. The TEMP harness used Microsoft.AspNetCore.App plus matching cached package dependencies: Microsoft.EntityFrameworkCore.SqlServer 10.0.12, libphonenumber-csharp 9.0.40, Microsoft.ML.OnnxRuntime 1.24.3, OpenCvSharp4 4.13.0.20260627 and PdfPig 0.1.16. ONNX/OpenCV/PDF dependencies were required for assembly metadata inspection by the existing offline EF configuration scan; no live provider was initialized or called.

Initial isolated harness attempts stopped on setup issues before a complete check result: a missing TEMP Infrastructure refint directory (CS2012), generated files accidentally included by the default TEMP compile glob (CS0579) and incomplete cached runtime dependencies for the EF assembly scan. Explicit Program-only compilation and stable binary references resolved those setup issues; the successful final output contains no warnings or failures. The earlier solution-build failure recorded above is historical; root separately reported the coordinated final Release and model38 checks passing. No broad solution/model/source check was repeated by this rerun.

No production source, EF model/snapshot, migration, shared/global files, SQL, real account, private employee file or external provider was changed. Audit fixture/report/log metadata and the isolated TEMP harness are the only changes for this final verification request.
