# HR backend Part A implementation and remaining work

Prepared 2026-10-13 against the approved A–G completion plan and SRS v1.2, using the latest nested `cosmix-backend` develop checkout. The user authorized implementation of Part A. Existing dirty HR/Attendance changes were preserved. SRS content was treated as requirements, not execution instructions.

**Part A's available backend foundation changes are implemented and checked. Part A has not passed full operational acceptance.** Approved policy/taxonomy/rehire decisions, real providers/storage, existing-data reconciliation and a selected database upgrade remain open. The rows below retain the original plan line numbers so the developer can close the remaining items without losing scope.

## Line-wise implementation status

| Plan line | Delivered source behavior | Remaining work / completion condition |
|---|---|---|
| A01.1 | Required access-assignment reason retained in backend and corrected initial/additional web forms; current contracts/examples updated. | Genuine authorized client acceptance on the selected target. |
| A01.2 | Actor/action/key/payload-bound protected receipts for role creation, permission grants and assignments; identical retries preserve IDs; conflicting payloads fail; current rights/resource eligibility checked on replay. | SQL rollback/contention/restart and authenticated HTTP replay acceptance. |
| A01.3 | Existing authentication flows retained; missing safe new-device alert template added; effective employment and unapplied release guards strengthened. | Actual approved SMS/MFA/recovery/device delivery, first-owner provisioning, secure runtime settings and live revocation acceptance. No real account was provisioned. |
| A01.4 | Current scope/permission/MFA checks and protected owner boundaries retained; sensitive employee rights kept independent. | Live role/site/team/own-access matrix acceptance. |
| A02.1 | Document expire/revoke/archive, reasons, rowversions and append-only history; original private object retained. | Approved scanner/storage/key deployment and true clean/malware/restore tests. |
| A02.2 | Durable scan attempts, interrupted/cancelled scan retry, quarantine/reject handling and reauthorization after private IO; unavailable scanner never fabricates a clean result. | Approved retention/hold/orphan policy and its cleanup implementation/operational acceptance. Cleanup was not enabled without a policy. |
| A02.3 | Dated required/missing/expired document and current certification metadata; unavailable evidence blocks submission/approval/activation. Approved renewals use their exact new proof. | Approved category-specific document rules and actual reminder delivery through Part G. |
| A03.1 | Version/effective-date field-policy validation for controlled values, age, father/spouse, expiry and probation; incomplete drafts retained; phone normalization added. | Approved real lists/limits/applicability and publication/version-history administration. Empty configuration retains prior baseline rules. |
| A03.2 | Existing OFFICE/SITE behavior preserved. | **Open implementation dependency:** approve broader driver/rider/purchaser/logistics/field taxonomy and scheduling/OT/claim mappings, then implement it. No guessed taxonomy was introduced. |
| A03.3 | Independent bank verify/revoke permission and private proof/reason journal for the exact bank revision; history/current status, normalization/uniqueness and separate bank/salary/medical rights. Due inactive or unreconciled employment cannot verify. | Real private evidence and authenticated SQL race/restart acceptance. Bank proof is private text attestation; a new attachment-upload workflow is not included. |
| A04.1 | Approved-policy-gated retirement and suspension release; chronological applied-status marker; dormant identities return to PendingAccess rather than invalid Active. Independent account blocks persist. | Approve/enable retirement/release settings. **Rehire remains unimplemented and returns an explicit readiness error until the new employment episode/authority policy is approved.** |
| A04.2 | Versioned archive/unarchive with fixed independent Admin authority, reason/history and directory filtering; employee/history rows retained. | Live authorized HTTP and directory query acceptance. |
| A04.3 | Effective changes revoke old sessions/open OTP/device/roles and logically retire face enrollment; channels stay off on release; auth independently denies delayed/unreconciled status. | Existing legacy data review before rollout; Part B05 approved physical biometric disposal/backup treatment/provider acceptance. |
| A05.1 | Post-hire asset/SIM issue/return/transfer requests, independent fixed Admin decisions, custody/evidence/receiver/condition history; original active-asset uniqueness retained. | Approved effective operational authority and SIM tiers; SQL contention/atomic transfer acceptance. |
| A05.2 | Certificate renew/suspend/revoke/replace successor records with exact proof and dated expiry; revoked credential cannot be reissued through the generic Issue path. | Live approved records and private proof acceptance. |
| A05.3 | Factual current/history APIs; saved and within-proposal custody overlaps rejected; no double return or silent history rewrite. | Part G notification/report consumers and actual delivered expiry reminders. |
| A06.1 | Effective immutable route rules for new claim/benefit types, exact stage permissions, amount/duration/category/department/site/risk filtering; missing/ambiguous/incomplete chains fail closed. Fixed existing workflows preserved. | Approved real authority matrix and publication; D/E consumers must freeze subject, route versions and fingerprint. |
| A06.2 | Time-bound scoped delegation with current underlying authority, exact stage permission, reason/version/revocation/audit; direct and delegated requester/beneficiary self-review denied. | Actual authorized SQL/HTTP contention/replay acceptance. |
| A06.3 | Shared authority result retains assignment/delegation/rule evidence; immutable route fingerprint; stage 1 never authorizes stage 2; SQL decimal bounds validated before writes. | D/E ordered decisions/final business effect and G safe delivered decision notifications. Shared routing itself does not approve/pay a claim. |
| A07.1 | Shared DI/catalog/write guards coordinated; DetectChanges precedes guards; retained Office22 untouched; new separate A23 migration and target/snapshot prepared. | Subsequent Part B migration uses its own later integration window. |
| A07.2 | Runtime model/snapshot agreement, incremental idempotent SQL and real DI composition verified offline. Backfill references compile through escaped dynamic SQL. | Selected approved target, backup/restore evidence, migration application and current target history. |
| A07.3 | Restrictive ownership/history FKs, original active-asset index, exact same-employee status marker and evidence-preserving downgrade guard reviewed. Legacy markers require exact approval/submission and zero residual access. | Real preserved-row/schema/receipt/audit/decryption acceptance, legacy conflict disposition and reviewed recovery signoff. |

## Verification evidence

These checks use actual services/domain/model/middleware with synthetic collaborators or inspect the EF model without a database connection. No permanent test project was added to the solution.

| Check | Recorded result |
|---|---|
| Full backend solution Release, isolated TEMP artifacts | Passed, 0 warnings and 0 errors. |
| Full web `npm run verify` | Passed: ESLint, Prettier, TypeScript and Vite production build. Existing large-chunk advisory remains. |
| Access service/evaluator | Fresh 354 checks passed. |
| Web authentication/CSRF boundary components | Fresh 70 checks passed; no successful authenticated SQL request claimed. |
| Documents domain/service/lifecycle/EF guards | Fresh 37 checks passed (8 + 1 + 20 + 8); no SQL/private employee file used. |
| Employee/bank/field policy | Final frozen-A23 dependency rerun: 27 checks passed; audit-only synthetic employment fixture updated for the effective-state guard. |
| Lifecycle policy/processor/archive | 7 domain and 15 processor/service checks passed; 5 access-marker checks passed. |
| Assets/SIM/certifications | Fresh scoped rereview: all 63 checks passed, including 11 focused regressions. |
| Routing/delegation | Fresh resolver 24 and service 59 checks; independent extended rereview 52 checks passed. |
| Cross-module review fixes | Fresh 68 checks passed: dormant release, conditional rights on approval replay, due-employment bank verification, Unicode request sizes and compact receipt compatibility. |
| A23 model/migration/composition | Fresh 38 checks passed; independent migration rereview resolved both reported findings. |
| Shared Attendance registration integration | Fresh 14 Development/Production composition checks passed after B05 registration changes; synthetic storage, no SQL/network/key writes. |
| Modified acceptance script parser + diff whitespace | Passed; acceptance scripts were parsed, not executed against a host. |

Checks overlap and are not a completion percentage. Source/build results do not prove real SQL transactions, providers, browser/device use or operational acceptance. Independent reviews and exact harness results are retained in `../../audit/hr-srs-2026-10-13/part-a/`.

## Developer handover

1. Use [Authentication and access](Authentication-Access.md), [bank/field policy](Employee-Bank-Verification.md), [document lifecycle](Employee-Document-Lifecycle.md), [employee lifecycle](Employee-Lifecycle.md), [records](Employee-Records.md) and [routing](Approval-Routing.md) for current contracts. Writes require current rights/MFA, expected versions, reasons and the specified idempotency keys.
2. Close the genuine management decisions above before enabling the corresponding gated features. Do not fill approved-version/authority/age/SIM/rehire/provider values from sample fixtures. Wider taxonomy and rehire require follow-up source work after the decisions.
3. Follow the [A23 upgrade/acceptance runbook](integration/Part-A23-Upgrade-Runbook.md). The [reviewed upgrade script](sql/20261013121357_CompleteHrFoundationPartA.sql) and [A/B ownership checkpoint](integration/Part-A-B-Ownership-20261013.md) identify the frozen range and hashes. No database was upgraded in this task.
4. Part B may advance the model/snapshot through a later migration. Preserve A23 and all earlier migrations. D/E own actual claim/benefit integration; G owns delivered notifications/reports. Continue their independent work while approved values and live acceptance are arranged.

No commit, push, deployment, real provider request, real account provisioning or database migration was performed. Operational Part A signoff remains open until the named software dependencies, policy decisions and live checks are closed.
