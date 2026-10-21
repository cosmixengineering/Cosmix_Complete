# Rehire source preparation handoff

This continuation contains only new unmapped Domain/Application source and a standalone audit harness. No API, service implementation, DI registration, permission-catalog entry, EF configuration/DbSet, migration, shared consumer change or operational activation is included. The fixed independent Admin proposal remains proposed company policy; actual management confirmation is pending. No policy values or real approval evidence were seeded.

The source is prepared to integrate a stable employee ID/number and one account with new immutable episode approval/terms. It does not modify Employee.JoinedOn, original onboarding, salary, assignments, auth, leave, attendance, documents, face data or existing disciplinary cases. Source/build checks do not prove SQL, providers, authenticated devices or operational rehire.

## New source scope

- Domain/HR/EmploymentEpisodes/EmployeeEmploymentEpisode.cs: immutable new episode evidence with stable identity, original onboarding, previous ended episode, exact new approval, new terms and frozen ban evidence. Factory validates structure; it never changes employment/account state and cannot reuse the original hiring decision.
- Domain/HR/EmploymentEpisodes/EmploymentEpisodeTerms.cs: stable source identity, explicit new joining/probation/assignment/compensation/approved-phone terms and independent final approval evidence.
- Domain/HR/EmploymentEpisodes/EmploymentEpisodeEligibilityRules.cs: Unknown/Clear/PermanentBan/Ambiguous resolution, exact same-employee disciplinary case/rowversion/approval/outcome provenance and register fingerprint. Permanent flags or permanent source outcomes always deny. Missing/unresolved/unreadable evidence never means clear.
- Domain/HR/EmploymentEpisodes/EmploymentEpisodeAccessRules.cs: pure account-reset projection; fifteen required retirement/reset actions; channels stay disabled; independent account blocks and lock are retained. Current ban evidence must still match the approved episode. The time-window predicate alone never authorizes access.
- Application/HR/Rehire/EmployeeRehireContracts.cs: frozen server candidate/current-source/reviewer/protected consumer proof/plan contracts, preserving employee, account and requester versions and identities.
- Application/HR/Rehire/EmployeeRehirePolicy.cs: explicit actual approval/version/hash/window, identity/authority/join/evidence/probation modes and eligible terminal statuses. Every default is unapproved. The sole prepared joining mode is a future business-day start; it is unavailable unless actually approved/configured. Partial-day/backdated returns are unsupported.
- Application/HR/Rehire/EmployeeRehireWorkflowRules.cs: current/frozen ban check first; stable identity/source/employee/request/account/requester versions and exact policy/source/evidence bindings; independent current web SMS MFA/fixed Admin review. A pending candidate stores requester identity separately from its optional final approval.
- Application/HR/Rehire/EmployeeRehireActivationPlanValidator.cs: distinct future preparation and due-start validation. Every consumer requires one exact current protected proof. Output is only a reviewable plan with transaction revalidation, historical retention and fresh disabled-channel setup requirements; there is no execution endpoint or operational-success response.
- Application/HR/Rehire/IEmployeeRehireRepository.cs: future repository, actual policy, authority, private submission, consumer-verifier and authoritative blacklist-resolver interfaces; no adapters/DI.

All paths above are under the nested cosmix-backend/src. The audit harness is `rehire-checks/RehireChecks.csproj` and `Program.cs`; it is outside the solution and uses TEMP artifacts. It opens no database or provider.

## Non-overridable permanent blacklist

The human directly clarified that permanent blacklisting must not be bypassed. SRS extracted line 278, SITE-011, also requires an authorized disciplinary approval before the 120 annual-late workflow has a termination/blacklist effect. This source adds no annual counter, period/reset worker, termination, deduction or automatic blacklist derivation.

Existing DisciplinaryCase retains employee ID, rowversion, approval request, private evidence and an opaque approved closure outcome. Its comment explicitly says it does not terminate/blacklist by itself. A trusted future resolver must inspect the complete lifetime register, verify exact authorized approvals and retained private object versions/outcome hashes, and interpret a reviewed known outcome schema. Missing schema/approval/decryption or conflicting case membership produces Unknown/Ambiguous.

Frozen/current ban evidence includes the nullable permanent flag, resolution status, source provenance, resolution version, register fingerprint and exact same-employee case versions/approvals/outcome evidence. Its semantic fingerprint excludes only observation time. A source/register change requires a fresh candidate. A permanent flag/outcome rejects immediately regardless of terminal-status label, proposed Admin approval, independent account status, override-looking fields, verified consumer proofs or a replacement employee ID attempt. A ban recorded after submission or approval also rejects due planning/account reset. No rehire override field or clearance operation exists.

An explicit Clear result is allowed only from the trusted complete-register resolver. An empty source list is not implicit clearance: it still needs an authoritative resolved register fingerprint/provenance/version. No resolver is wired by this slice, so missing actual evidence remains unavailable.

## Required consumer gates

`RehireActivationBoundary` has seventeen gates with individual `employee_rehire_<boundary>_not_ready`, `_evidence_binding_invalid`, `_evidence_stale` and `_evidence_ambiguous` errors:

1. stable_identity
2. previous_episode
3. new_approval
4. current_policy
5. employment_terms
6. required_documents
7. authentication_proofs
8. account_reset
9. fresh_access_setup
10. face_isolation
11. attendance_duty
12. historical_coverage
13. policy_and_entitlements
14. financial_history
15. atomic_persistence_and_receipts
16. legacy_data_reconciliation
17. employment_blacklist

Proofs are trusted server-verifier outputs, never HTTP client input. Each binds employee/previous episode/request/submission, current employee/request/account versions/security version, policy version/fingerprint, complete candidate fingerprint and exact private evidence hash/object version/window. Duplicate, missing, mismatched, expired/future-dated, unavailable or conflicting proof denies a plan. All boundaries must be reverified under the existing authorization transaction after private IO and at due start. A previously valid future plan cannot authorize a delayed write by itself.

Core blacklist errors are `employee_rehire_permanently_blacklisted`, `employee_rehire_blacklist_resolution_required` and `employee_rehire_blacklist_evidence_changed`. These precede proposal/approval planning. Missing actual rehire policy is `employee_rehire_policy_approval_required`; unsupported/unapproved modes have separate errors. Current employee/request/account/requester/source changes also have exact conflict codes. Due validation before the joining instant returns `employee_rehire_effective_start_not_due`.

## Actual integration still required

Management must approve the real rehire chain/eligible statuses, timing/probation/evidence/new terms, wider taxonomy and tenure/leave/policy/financial treatment. The permanent-ban constraint is already explicit; the annual counting/disciplinary workflow remains separately authorized work. Never use synthetic fixtures as configuration or actual approval.

After B's model/migration handoff, implement exact episode persistence/legacy reconciliation, same-employee restrictive ownership, one-account association, actor/action/key/payload receipts and protected audit transaction, new salary/assignment roots and all mapped consumer boundaries inventoried in `rehire-discovery.md`. Preserve old requests/approvals, compensation/status/assignment/access/biometric/financial/attendance evidence. Old open assignments, duty sessions, policy/OT/zone/team/project applicability and prior tenure cannot carry into a new episode implicitly.

The account plan requires: invalidate security version; revoke sessions/refresh capability, role assignments, devices, phone bindings, setup invitations, unfinished recovery, open OTP/MFA/reset proofs and active face enrollment; retain history/ciphertext under separately approved retention; clear current credentials/setup/activation; disable both channels. Only exact lifecycle-disabled previous-episode evidence can prepare PendingAccess; independent blocks/locks stay. Fresh approved grants/channels/phone/password/OTP/device/face setup remains necessary.

Operational wiring and consumer proofs are intentionally absent. Current terminal -> Active lifecycle behavior remains its existing explicit policy-readiness denial. This handoff must be described as rehire source preparation, not completed intake/approval or usable rehire.

## Executed verification

Fresh final Release harness: **118/118 passed, zero failures**. It compiled the actual Domain/Application/Infrastructure dependency graph with isolated TEMP artifacts, exercised the new pure code and inspected the real current EF model. None of the EmploymentEpisodes/Rehire preparation types appeared in the EF entity model. This is not a whole-solution/API or runtime model/snapshot signoff; the root integration owner retains those checks.

Observed red runs covered absent behavior (31 failures), missing account/due-phase guards (6 failures), missing blacklist guards (11 failures), and then the reset/pending-requester/same-employee-case gaps (6 failures). Focused fixes were followed by the fresh all-green 118-check run. Synthetic cases include independent/subject self-review, pending-request self-review before a final approval exists, source/account/requester version conflicts, policy/effective-window changes, forbidden old hiring-decision reuse, every consumer unavailable/stale proof, changed request/salary proof bindings, late permanent ban after submission/approval, status relabel/override/proof attempts, new-ID attempts, uncertain/unreadable ban provenance, case approval/employee/version bindings, and missing/current-ban reset denial. No annual-count ban, database/provider call or actual employee/account record was created.

Re-run from the workspace root:

```powershell
dotnet run --no-restore --project 'audit/hr-srs-2026-10-13/part-a-continuation/rehire-checks/RehireChecks.csproj' -c Release --artifacts-path 'C:\Users\Huma.Khan\AppData\Local\Temp\cosmix-rehire-source-checks'
```

The audit project is outside the backend solution; no permanent test project was added. No SQL, provider/device call, Git action, deployment, management approval, migration or activation was performed.
