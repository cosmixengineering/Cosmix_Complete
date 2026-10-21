# Corrective data-model fixes

**Goal:** Fix the five reviewed defects at baseline `30aada1`, with offline reproduction and a new reviewed corrective migration. No database access, business APIs/UI, new test projects, commit or push. The user's follow-up also authorizes Development-only Swagger UI.

**Publication follow-up:** After completion, the user authorized reviewing, committing
and pushing only these fixes to `develop`. This supersedes the earlier commit/push
restriction; the prohibition on database access remains in force.

**Architecture:** Existing four .NET 10 projects, one EF SQL Server context; preserve every shared migration. Root owns context, migration/snapshot and docs. Independent agents own document scan, membership and invitation domain/configuration files.

**Spec:** User's five-issue fix list, approved SRS v1.2 (unchanged SHA-256 `0d7216815c24a476d82f12c46cdd97d953a8a50217ecdf10d7cf52332c403e71`) and existing coverage/review. User clarification received in this chat: "Haan — correction aur resubmission". Returned allowances support correction/resubmission on the same claim/cycle with earlier decisions preserved. No separate PM sign-off was supplied or independently verified; the decision source is recorded in Migration-Review.md.

## Constraints and decisions

- Checkout `C:\Users\Affan.Ahmed\Desktop\cosmix-backend`, develop, HEAD `30aada1`, no newer local commits. Preserve unrelated `UpgradeLog.htm`.
- Reproduce each defect before its production fix; temporary file-based checks under ignored `artifacts/fix-review`, no test project or database connection.
- Scan results are ordered by their recorded evidence timestamp; exact duplicates are idempotent, equal-time conflicts fail closed, and newer rescans require current safe verification.
- Membership termination keeps the original schedule and adds a one-time termination fact with effective date, reason, actor and recording time/business date; it cannot extend access or rewrite completed periods.
- Invited account recovery validates replacement access before revoking the old invitation/resetting the account; account and invitation changes must be persisted atomically.
- Group shift withdrawal follows the immutable `PolicyAssignmentEnd` pattern, without roster precedence or attendance recalculation.
- Allowance correction retains the claim's employee/kind/eligibility key and immutable submission/decision history. Preserve the eligibility unique index and add settlement safeguards; never fabricate legacy evidence.

## Tasks

- [x] Verify baseline/status and read architecture, SRS, coverage/review; record existing migration hashes.
- [x] Reproduce and fix stale document scans; check duplicate/conflict/newer rescans and verification.
- [x] Reproduce and fix scheduled team/project early termination; check history, invalid dates and concurrency mappings.
- [x] Reproduce and fix invited-account access recovery; check approval/scope/account matching, old tokens, expiry and reuse.
- [x] Reproduce unsupported group withdrawal, add immutable evidence/configuration and verify guards/FKs.
- [x] Reproduce returned allowance resubmission failure; implement approved correction/history and duplicate settlement protection.
- [x] Integrate context, run all focused checks, obtain independent review and Release build.
- [x] Generate new corrective migration, inspect Up/Down, preserve previous migration hashes and generate explicit-range incremental/full SQL scripts offline.
- [x] Update existing coverage/review docs with each issue's status, evidence and limitations.
- [x] Add requested Swagger UI, pinned package/lock and launch profile; verify Development/Production HTTP behavior without DB-backed requests.

## Verification focus

Same-time contradictory scan results must never remain available; invalid operations must leave objects unchanged. Termination may shorten future access only and retain original dates. Invalid replacement access cannot revoke a working invitation. Withdrawal must retain approving evidence and original assignment. Returned-claim cycles cannot create a second payable claim or lose earlier submitted values/decisions. All database constraints/transactions remain unexecuted until separately authorized.

## Results and rulings

- Every original defect was reproduced before its production fix. Final integrated check: 965 passed, 117 entities/268 restrictive FKs. Previous focused checks 8+61+25+88; new focused checks 68+87+61+107+65 plus both roster/allowance reproductions passed. Zero DB connections.
- Ruling: membership original schedule remains immutable; one-time termination fields are sufficient history. A trusted business date is a server input, not a client permission decision.
- Ruling: preserve scan verification evidence, bind new verification to exact scan. Existing unbound verification fails closed instead of being backfilled with guessed linkage.
- Ruling: returned correction changes amount/policy/calculation/evidence/payment dates, not the cycle/currency/deployment/session identity. Immutable submissions and per-claim snapshot uniqueness preserve history; settlement state/unique indexes enforce local duplicate protection.
- Independent review found and resolved allowance approval-decision persistence and old snapshot-reference reuse gaps before migration generation.
- New migration `20261006105213_CorrectReviewedModelLifecycles`: 34 Up operations including preflight, 28 Down operations. Two tables/sixteen columns added. Six older migration/Designer hashes unchanged; snapshot EF-generated only.
- 4,318 offline migration checks passed, exact scripts verified and EF reported no pending model change. Incremental starts `20261006074233_CompleteReviewedHrAttendanceDataModel`; full chain starts `0`. Both end at the new migration, under `docs/sql`.
- Swagger UI 10.2.3 restored from existing local cache; 22 HTTP checks passed. Release build zero warnings/errors. No business endpoints, database integration, new test projects, commit or push.
