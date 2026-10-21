# Current Part A/B integration ownership

Part A execution uses the latest nested `cosmix-backend` checkout. Root owns Part A access/HR integration, shared DI, permission catalog, DbContext, configurations integration, migration snapshot and new coordinated Part A migration. Preserve all existing dirty source. Concurrent Part B implementers own Attendance feature files; coordinate any shared source changes through this record before editing.

Part A prepares and verifies migrations offline. There is **no Part A SQL write window** open, no database target selected for a Part A upgrade in this execution, and no provider/account/provisioning changes performed. Do not apply schema or rewrite prior applied migrations on behalf of Part A. Existing Office migration 22 is retained; no duplicate office migration will be generated.

Part A result ledger: `../../../audit/hr-srs-2026-10-13/part-a/progress.md`. A coordinated follow-up migration will be named `CompleteHrFoundationPartA`; Attendance schema additions must use a later independent migration after handoff. Keep real target names/connection details, credentials and private evidence out of this file.

## A23 source freeze and model handoff

Part A freezes `20261013121357_CompleteHrFoundationPartA` after 38 current-source offline model/migration/composition checks, a full Release solution build with zero warnings/errors, and independent migration rereview resolving dynamic-SQL and legacy-reconciliation findings. Four new HR history tables are incremental from retained Office22. No SQL connection was opened by the migration checks or upgrade script preparation. The [upgrade runbook](Part-A23-Upgrade-Runbook.md) retains approved-target, restore, legacy-review and real acceptance gates.

SHA256 at the handoff checkpoint:

| File | SHA256 |
|---|---|
| A23 migration `.cs` | `377D2ED05117EF50B2A6F37F2E0FAFF64E15B00E42FDDAA0066D5689AAD04AA9` |
| A23 `.Designer.cs` | `D17BFEFB4FD0A490FA623268667AD0B9404AF7393ADA04A63C08785CBDA3E02C` |
| `CosmixDbContextModelSnapshot.cs` at A23 | `BFE33267F048C1DC4720EC56262C4C63940BA0CACC1B4298EB41EB471BC23308` |
| A23 idempotent SQL | `787DB2C00217F6F9026CAA3AC56D9E5053CE71AA8925AE79304308133F8A1EF8` |

The Part B owner may now begin the next coordinated EF/model window for Attendance additions. Preserve Office22 and A23 migration, designer and SQL files; generate a **later separate** Part B migration and advance the snapshot from A23. A subsequent snapshot change is expected and does not authorize rewriting the frozen A23 target. Part A will make no further EF-visible changes in this window. B-owned capture/provider registration extensions may bind complete B05 services. Shared core DI/catalog/DbContext additions must remain scoped to the agreed B feature and retain all Part A registrations and write guards, with a fresh integrated build/composition/model check before B handoff. Source ownership does not open a SQL/provider/deployment write window.
