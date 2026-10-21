# Clone Build and Test Repair

Goal: Remove reproducible compilation and test failures in the cloned project without claiming SRS completion.
Architecture: Keep the existing Clean Architecture, CQRS, audit actor assignment, and enum wire values.
Tech stack: .NET 10, EF Core, xUnit, React, TypeScript, Vite, Vitest.
Spec: User request to continue fixing errors; AGENTS.md and CLAUDE.md define local conventions.

## Constraints and Review Focus

- Work only in this dedicated clone; do not change the older applications.
- Do not migrate or seed the user's database, commit, push, or deploy.
- Preserve payment enum values and existing checkout API contracts.
- Keep missing business workflows separate from build readiness.
- Review trusted stock-transfer audit actor assignment and JSON serialization.

## Tasks

- [x] 1. Reproduce the solution build failure and add targeted regressions.
  - Files: Domain invoicing enum/payment record, Web invoicing request, Application complete-stock-transfer command, corresponding unit tests.
  - Interface: InvoicePaymentMethod must not collide with checkout PaymentMethod; CompleteStockTransferCommand(Guid) supports server-assigned UserId and audit metadata.
  - Test: Focused invoicing enum and stock audit tests, followed by solution build.
- [x] 2. Resolve newly exposed compile errors using existing APIs, not new business features.
  - Files: Only compiler-reported implementation/call sites.
  - Test: Rebuild after each root-cause group; preserve errors in analysis logs.
- [x] 3. Repair stale permission coverage and run available backend/frontend suites.
  - File: tests/NOIR.Domain.UnitTests/Common/PermissionsTests.cs.
  - Interface: Permissions.All matches every declared permission group, including new ERP groups.
  - Test: Domain, Application, Architecture tests; frontend typecheck, build, unit tests.
- [x] 4. Review the diff and record observed passes and unverified deployment limitations.
- [x] 5. Repair verified new-ERP missing-table runtime failures with an additive ApplicationDbContext migration.
  - Files: EF-generated Migrations/App files and snapshot; tests/NOIR.IntegrationTests/Endpoints/ErpSchemaSmokeTests.cs.
  - Interface: Existing ERP models are represented in a fresh SQL Server database after migrations; existing tables/data remain unchanged.
  - Test: Stock transfer, indent, BOQ, account, journal and invoice list APIs on disposable GUID-named LocalDB databases. Never apply to the user's configured database.

## Progress

- Baseline: clean clone at c450026; solution build fails with 16 errors (enum collision and missing audit members). Domain baseline has one stale permission-count test failure.
- Execution: User explicitly requested fixes in this clone. No commits or changes to the user's configured database are part of this repair.
- Compile repair: renamed only the new invoice enum (unchanged numeric/string values), aligned the stock completion audit contract with sibling commands, fixed accounting optional-parameter method-group binding and typed stock PagedResult factories. Solution build passed with 0 errors.
- Regression evidence: 6 invoice enum tests passed; stale permission-count test failed (150 vs 174), then updated to include the 7 existing ERP groups. Targeted Application tests: 3 passed and 6 stock pagination cases failed on the expected off-by-one error; handlers now convert normalized page numbers to zero-based indexes.
- Broader verification: Domain 2975/2975, Application 8591/8591, Architecture 46/46; frontend typecheck/build and 140/140 unit tests passed. Updated stale toggleable module count 38 to 42 (catalog total 50, core 8). Moved PO comparison helpers from Entities to Services without changing matching logic.
- Scope extension: New ERP runtime smoke tests failed 3/3 with HTTP 500; captured SQL logs confirm missing SalesInvoices/StockTransfers/StockIndents tables. Generate/review additive schema migration rather than weakening tests or ignoring missing tables. Only disposable test databases may be migrated.
- Schema repair: EF-generated 20261012095756_AddErpModuleSchemas adds 34 tables and 111 indexes only. Idempotent SQL has no DROP/ALTER TABLE and uses one transaction. No existing migrations were edited. On a fresh disposable LocalDB database, 6 ERP list endpoint cases and 12 existing auth tests passed (18/18). EF reports no pending model changes. Additive-operation/payment-string-storage guards added to Application unit tests.
- Review scope decisions: stock completion still does not post inventory, before-state audit resolver coverage and PO matching business defects remain baseline issues documented by the previous audit. They are not silently treated as complete by this build/schema repair.
- Final verification: solution Debug build/test (integration excluded by filter) passed with Domain 2975/2975, Application 8593/8593 and Architecture 46/46, 0 compile errors and 109 warnings on the final incremental run. Selected real-SQL integration tests passed 18/18. Frontend checks passed 140/140. Full integration/production release/browser coverage remains unrun.
- Final independent read-only review: no actionable patch-induced regressions. Reviewer confirmed all 94 pre-existing snapshot entity mappings are unchanged, including JSON mappings; 13 new FKs and rollback order are consistent. Rollback data loss in the new tables, populated upgrade, tenant-isolation/JSON/payment round trips and persisted audit checks remain deployment verification work, not claimed as passed.
