# A05 independent spec and quality review

Reviewed the newest dirty source in `C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend` against `AGENTS.md`, `docs/Employee-Records.md`, the approved A05 plan at parent `audit/hr-srs-2026-10-13/document-build/document-text-final.txt` lines 94–102, the extracted SRS, and `part-a/a05-result.md`. Read-only review: no production-source, Git, SQL, migrations, host, provider or real-account mutation. TEMP-only verification was allowed; this report is the requested review artifact.

## Scoped rereview after root fixes — passed

Both original findings below are resolved in the inspected current source. No new unresolved finding was found in this narrowly scoped rereview. Only the requested `cosmix-backend/docs/employee-records/VerifyEmployeeRecords.cs` verification source was edited; production code, migrations, SQL and live systems were not changed by the reviewer.

- `EmployeeRecordRepository.ActiveCertificationAsync` now reserves the latest credential identity across Active, Suspended and Revoked statuses. The existing service Issue guard therefore rejects the original identical revoked identity/proof with `certification_already_recorded`; no new proposal or certificate history is created. A distinct credential number with current separate proof can still be requested and independently approved, while the old credential remains visibly Revoked.
- `EmployeeFormValidation.ValidateProfile` now compares proposed same-reference custody intervals before materialization using trimmed ordinal case-insensitive references and half-open intervals. The original overlapping returned pair, case/whitespace equivalent references and an overlapping open/returned pair all reject with `employee_asset_proposal_overlap`. Adjacent return/issue boundaries, disjoint periods, different references, zero-length non-occupancy and incomplete draft fields remain accepted.
- Added 11 focused assertions at `docs/employee-records/VerifyEmployeeRecords.cs:184–237`; the fake current-tip identity query was already aligned by root. The original 52 checks still pass before the added assertions, and all 63 checks pass together.

Fresh execution: `dotnet run --project "C:/Users/Huma.Khan/AppData/Local/Temp/cosmix-a05-rereview-fa4c1857621143c4899e5da7c66ece2e/Verify.csproj" -c Release --artifacts-path "C:/Users/Huma.Khan/AppData/Local/Temp/cosmix-a05-rereview-fa4c1857621143c4899e5da7c66ece2e/artifacts"` exited 0 against the current project source and printed:

```text
PASS: 52 existing standalone behavior/model checks.
PASS: 63 standalone behavior/model checks; synthetic interfaces, no live SQL/provider or account mutation.
```

The executable references the actual Infrastructure/Application/Domain projects, uses synthetic authorization/repositories/private bytes and inspects the offline EF model. Live SQL, authenticated HTTP, scanner/storage deployment, rollback/range-lock acceptance and migration application remain outside this verification.

## Original findings — resolved by root

### P2 — Terminally revoked credential can become Active again through Issue

- Location: `src/Cosmix.Infrastructure/Repositories/HR/EmployeeRecordRepository.cs:52–53`, called by `src/Cosmix.Application/HR/Employees/EmployeeRecordService.cs:231–233`.
- Trigger: revoke an existing certification/license, then submit `Issue` with the same employee, type, number, issuer, issued/expiry dates and exact retained certificate evidence. A different authorized fixed Admin approves it.
- Cause: the duplicate-current-identity query excludes `Revoked` tips. Issue requires no predecessor, and the proof stays verified/current when certification status changes. The new row is created as an independent Active root. Current projection then returns both the Revoked tip and a new Active tip with the same credential and attachment. This bypasses the documented terminal revocation and can make the revoked credential appear available in downstream completeness/report facts.
- Minimal fix: reserve a current certification identity across every status, including Revoked, when validating Issue and number-changing renewal; reject reissuance of the same revoked identity. Keep any reinstatement/reissue policy separately explicit and configuration approved.
- Evidence: the current-source TEMP harness first passes all existing 52 checks, then successfully requests and approves exactly this reissue and asserts that both Revoked and Active tips exist with the identical retained proof. No SQL provider was invoked.

### P2 — Hiring overlap checks omit pairs within one submission

- Location: `src/Cosmix.Infrastructure/Repositories/HR/EmployeeHiringRepository.cs:72–81`; related form loop at `src/Cosmix.Application/HR/Employees/EmployeeFormValidation.cs:64` and materialization at `src/Cosmix.Application/HR/Employees/EmployeeApprovalMaterializer.cs:48–58`.
- Trigger: a hiring submission contains two asset proposals with the same reference, both returned, with overlapping non-empty intervals; for example `[2026-10-09, 2026-10-12)` and `[2026-10-10, 2026-10-12)`. There is no pre-existing saved custody for that reference, and both proposals otherwise have valid conditions, evidence and receiving accounts.
- Cause: each proposal is checked only against database custody rows before either proposed row is added. No form/submission/materializer validation compares the proposals with one another. The materializer creates and returns both rows before the batch save. Both rows have `ReturnedAt != null`, so the preserved filtered unique index on active `AssetReference` excludes both. The asset write guard permits Added legacy hiring custody without fabricating operational events. A single approved submission can therefore create overlapping retained custody, and an earlier as-of projection can show duplicate custody for the same asset.
- Minimal fix: before querying existing custody, compare all complete proposals grouped by normalized asset reference and reject intersecting non-empty half-open intervals; keep adjacent returns/issues valid. Preserve the global existing-row check and active filtered index.
- Evidence: the TEMP regression calls the real `EmployeeFormValidation.ValidateProfile` with the overlapping returned proposals and it accepts them. Remaining acceptance is established by the inspected real reference-validation loop, materializer staging and index filter, not by live SQL execution. An empty database satisfies both independent existing-row queries.

## Verification and reviewed behavior

- TEMP project: `C:/Users/Huma.Khan/AppData/Local/Temp/cosmix-a05-review-85b8b38b25cd4b7193f0e56d4f131c57/Verify.csproj`; copies the existing harness and adds the two focused regressions. It references the actual current Infrastructure/Application/Domain projects and uses synthetic repositories, authentication grants and private bytes.
- Executed `dotnet run --project <TEMP>/Verify.csproj -c Release --artifacts-path <TEMP>/artifacts`, then reran with `--no-restore`. The first current-source run exited 0 and printed the existing 52-check pass plus the terminal-revocation reproduction.
- DI, all nine independent sensitive permissions, current web MFA/All scope checks, fixed Admin on the review permission assignment, self-review denial, policy version/effective gating, exact employee ownership/Standard evidence and current verification/validity/hash checks, compact internal receipts, and DetectChanges before write guards are wired in the inspected source.
- Transfer source-close/intermediate save and receiving issue remain within the common serializable authorization transaction with the exclusive shared authentication application lock. Expected target version, active unscheduled target/receiver employment and separate receiving proof are revalidated at approval. Current-record/successor checks preserve append-only SIM/certification history; asset filtered uniqueness and event/one-time-return guard remain present.
- A02 uses the latest applicable certification tips and their exact current verified Standard evidence independently of the selected generic document manifest. Generic selected historical document IDs remain factual until a separate Admin-approved selection changes them. No false missing-renewal bug is reported for that intentional behavior.

## Boundaries

This review does not establish actual SQL range-lock/unique/concurrent rollback behavior, authenticated HTTP/CSRF, real storage/scanner behavior, migration/backfill execution or production acceptance. Approved policy values, deployment prerequisites and Part G notification/report delivery are explicitly separate; their absence is not reported as a code bug. Legacy returned SIM roots do not reconstruct prior status in the current-tip as-of API; this is a historical-data limitation to clarify during migration/acceptance, not a finding about a newly generated revision chain.

