# Direct HR policy and Admin leave implementation

User confirmation, 12 October 2026:

- An explicitly permitted HR operator publishes Attendance policy directly, without a second reviewer.
- The policy form requires explicit late/half-day and ordinary deduction configuration. A rejected or
  incomplete proposed policy never replaces a valid published policy. This does not stop salary,
  change compensation records, manufacture zero pay or initiate deductions.
- Employee leave applications go directly to Admin for one review. There is no intermediate HR
  approval. Requester/beneficiary cannot decide their own application. Approved cancellation is
  a separate controlled Admin decision and retains the original evidence.

Implementation plan in this existing checkout:

1. HR policy owner delivers immutable direct publication, explicit dated applicability, history,
   controlled replacement/ending and an internal resolver. Preserve prior approved versions and
   locked periods. No implicit population precedence or automatic account grants.
2. Leave owner delivers self intake, protected evidence, bounded Admin queue, decisions, returned
   resubmission and cancellation, and exact per-day allocations for Attendance. Existing approvals,
   audit, retry and current-role/scope infrastructure are reused.
3. Calculation owner connects exact duty, pinned roster, real policy and verified leave producers
   through an internal provenance snapshot. Public daily results expose time/classification, not
   private salary inputs. Existing OT rules and protected award evidence remain authoritative.
4. Root integrates registrations, strict HTTP contracts, backwards-compatible summary fingerprints
   and incremental schema changes, then runs service/query/model checks and Release/HTTP regressions.
5. Migration files may be prepared locally. No database access/writes, test fixtures, Git actions,
   real account changes or live provider activation are included in this implementation run.

## Implemented workflows

- Eleven additional attendance-policy operations implement direct HR definition, preview/publication,
  immutable history, explicit employee/department/category/site applicability, replacement/ending and
  historical resolution. Validation and financial form recommendations make thirteen total policy
  operations. New publication requires schema 3 with explicit financial settings; schema 1/2
  canonical history stays unchanged.
- Nineteen leave operations implement self intake directly to Admin, decision/return/resubmission,
  separate approved cancellation, private scanned evidence and explicit HR type/settings publication.
  Historical shift/day allocations, approved policy/holiday revision, fiscal balances and protected
  decisions remain bound together. Self type/balance/submission-preview reads and reviewer policy
  discovery are registered. Quotas use exact day fractions from each old policy, so changing
  an eight-hour day to nine hours cannot create extra leave entitlement.
- The real daily calculator validates actual policy, roster, historical employment, leave, raw/corrected
  duty and protected OT evidence. It returns time/classification and a safe provenance digest.
  Approved OT is excluded from ordinary elapsed time; future absence and unreviewed raw exceptions
  cannot become calculated payable duty. Explicit HR manual duty retains its original failed raw evidence.
- Existing summary fingerprints remain compatible. Time-only results explicitly carry
  `PayrollReady=false`; month preview cannot mistake them for verified financial calculations.
  This introduces no additional approval step and never changes compensation or stops salary.

See [policy endpoints](Direct-HR-Policies.md), [leave endpoints](Leaves.md),
[daily calculation](../attendance-policies/Daily-Calculation.md), and
[client request examples](Policy-Leave-Client-Examples.md).

## Local schema

Migration 19, `20261012125614_AddDirectPoliciesAndAdminLeave`, and its reviewed incremental SQL
are prepared locally, **not applied**. It adds policy authority/revision metadata, nullable policy
approval references, exact leave allocations with restrictive shift links and immutable submission
identities. Existing authority defaults remain approval-backed; new direct HR rows cannot carry
fabricated approvals. The downgrade refuses recorded workflow history before dropping anything.
All **36** source/designer hashes for migrations 1–18 remain unchanged. Model/snapshot agreement
and forward/downward script protections passed **29** offline checks. No database was connected.

## Initial slice verification and remaining dependencies

These counts record the initial policy/leave slice; the later financial/company and expanded leave
checks supersede them in [current verification](Verification.md). The integrated Release solution
build passed with **0 warnings / 0 errors**. Standalone feature
checks passed: policy **41 service + 14 actual query/model**, leave **54 service + 15 persistence +
36 actual query**, and daily calculation **67 rule/service + 10 actual query**. These use synthetic
collaborators or SQL translation with connection/command execution guards, not a live database.
The strict MVC JSON boundary passed **15**, OpenAPI transformer/exporter **138**, and legacy
summary fingerprint compatibility **4** checks. Further registered HTTP and affected regression
results are recorded in [Verification](Verification.md).

The face provider remains explicitly deferred. Real authenticated SQL/private-storage/competing-write
acceptance, pending migration application and mobile/device integration remain unexecuted for this
slice. Ordinary monthly deduction previews and immutable inputs are now implemented separately;
see [payroll inputs](Payroll-Inputs-Implementation.md). Legal payroll activation, carry-forward financial interpretation,
KPI/waiting effects, payroll handoff/settlement and the extra-leave/encashment/recovery extensions
are not claimed complete. Office staff without the confirmed site schedule source remain explicitly
unavailable for schedule-dependent leave/time; no office hours are invented.

Work stayed in this checkout, preserving unrelated changes. No GitHub, commit, branch, worktree,
automation, live account, biometric provider or database write was performed. Actual salary
disbursement is a separate boundary; a configuration warning is never authority to withhold salary.
