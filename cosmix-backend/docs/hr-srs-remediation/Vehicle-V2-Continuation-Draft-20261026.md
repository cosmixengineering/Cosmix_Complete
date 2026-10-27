# V2 partial-run continuation: approval draft

Status: documentation only. This is not an executable packet, a selected profile,
or permission to run a host, write SQL, provision keys, copy evidence, or resume
the previous runner. Parent reconciliation and explicit continuation approval are
required. No implementation changes accompany this document.

## Observed stop and immutable baseline

Parent reports 29 HTTP requests, three MFA sessions, successful concurrent
vehicle creation, all 12 synthetic fixture rows committed, and exactly one
pending service: `f31f46a9-3bf3-4bd4-b34e-61125bc27e11`. Host PID 8032 exited.
Its approval returned 409. Read-only SQL/authenticated evidence inspection found
numerically equal retained/SQL values with different decimal scales, including
`1200` versus `1200.000` and `50` versus `50.00`. The production comparison fix is
owned separately; this document neither implements nor certifies it.

Reuse the existing vehicle with registration `TEST-HR-V2-20261026`, its actual
API-created GUID, and the complete retained fixture graph. Fixture IDs follow
`26102602-{n:0000}-4000-8000-{n:000000000000}`:

| n | Existing object |
| --- | --- |
| 1 | VehicleFuel policy |
| 2, 3, 4 | Policy versions V1, V2, V3 |
| 5 | Closed H custody |
| 6 | Legacy trip |
| 7 | Original mileage |
| 8 | Original fuel claim |
| 9, 10 | Original trip/fuel approval headers |
| 11, 12 | Original trip/fuel approval decisions |

All 12 rows, the vehicle, the pending service's immutable columns, their private
references, and all prior evidence bytes remain unchanged. Only the existing
pending service's approval header may transition Pending -> Approved, with its
normal decision/rowversion changes. Do not exempt the entire approval table from
preservation checks. Existing V1/O1 data and unrelated data remain unchanged.

The pending service is expected to be H's first Tuning service, 2026-09-17,
odometer 1200, eligible/claimed outstation distance 200, workshop
`TEST-HR-V2-WORKSHOP`, 50 PKR, evidence
`TEST-HR-V2-20261026-17-Tuning`. Reconciliation must confirm these values, its
approval header GUID, actual vehicle GUID, exact saved submission body and key,
receipt identity/expiry, and the absence of an approval decision/receipt. None of
these missing identifiers may be inferred from the marker or newly generated.

## Required reconciliation before approval

Produce a private, redacted reconciliation manifest and a public digest. It must
pin the actual vehicle/header/receipt IDs, all baseline row memberships and
hashes, retained object references and purposes, ciphertext lengths/hashes,
source directory membership, and any unreferenced/partial objects. Preserve
orphans as evidence; they are not permission to delete, repair, or reuse a name.
Record the failed approval's exact key/body/status and whether any auxiliary
audit, receipt, or file write occurred. A 409 alone does not establish rollback.

Last verified SQL binding: `Software-PC1\LOCALDB#8C03C2BC`, named pipe
`np:\\.\pipe\LOCALDB#8C03C2BC\tsql\query`, SQL PID 22112, catalog
`Cosmix_Testing`, owner/operator `COSMIXRND\Huma.Khan`, family
`D9C34627-18A2-41CF-A804-821E39516303`, files
`C:\Users\Huma.Khan\Cosmix_Testing.mdf` and
`C:\Users\Huma.Khan\Cosmix_Testing_log.ldf`. Revalidate these, ONLINE/read-write
state, exactly two files, no active custom triggers/synonyms/external
dependencies, and all 26 source/applied migrations ending
`20261022133924_AddComplianceWorkstreams`. No instance start, attach, replacement
catalog, or automatic retargeting is part of continuation.

Require exactly the three existing synthetic accounts with normal current
employment, web access, MFA, and fixed vehicle permissions:

| Actor | Account ID | Employee ID |
| --- | --- | --- |
| H: hr@example.invalid | 8475187f-bf3b-4f9c-a3ed-4bac32253005 | 35d19922-70a8-4c80-8578-e1029360e9fc |
| A: admin@example.invalid | 94a83b62-5b56-4450-9175-2a7a9e3261bf | 7c505086-87ea-4fd2-980e-823ecccbe36a |
| S: superadmin@example.invalid | 6c7ab9c5-c94c-4ad7-a67e-6143a970d26f | 3e1e81a4-c2c2-452b-b784-41792aa3aad0 |

Confirm no nonterminal OTP deliveries; no correction/reconciliation proposals;
exactly the one V2 pending service; no other trip/fuel/mileage/service additions;
and no successful receipt for any supposedly unexecuted command. Do not renew
expired custom grants. All original successful receipts needed for replay must
still be unexpired. Do not extend their 24-hour lifetime or rewrite fingerprints.

The existing V3 window ends 2026-10-27 at Karachi midnight
(`2026-10-26T19:00:00Z`). Admit no continuation command at or after that instant.
No clock substitution or policy-window extension is included. Unrelated vehicle
business must remain inactive because policy selection is global by date.

## Exact remaining business work

There are **19 new successful business commands**, no vehicle creation, no seed,
and no new source trip, fuel claim, mileage, custody, policy, or policy version.

1. A approves the exact existing pending service. Reuse the original failed
   approval key and `{}` body only after proving it has no committed receipt or
   decision. If either exists, reconcile that outcome instead of approving again.
2. H submits and A approves the remaining three pre-correction services:
   EngineOverhaul on September 17, then Tuning and EngineOverhaul on September 18.
3. A submits one correction of fixture trip 6; S independently approves it.
   Retain the exact 100-km September 15 segment 18:00Z-19:00Z under fixture ID 2 and
   100-km September 16 segment September 15 19:00Z-September 16 01:00Z under
   fixture ID 3. Odometer intervals remain 1000-1100 and 1100-1200; no commute ID.
4. A submits one reconciliation of fixture fuel claim 8; S independently approves
   it. Expected comparison is original 1000 PKR, corrected 1500 PKR, difference
   +500 PKR, settlement state `NotRecorded`. This is not a payment or liability.
5. H submits and A approves Tuning and EngineOverhaul on September 19 and 20.
   Each kind's retained consumption chain ends at 50, 100, 150, 200 km.

All seven new service bodies retain vehicle ID, odometer 1200, outstation km 200,
the exact workshop, currency PKR, numeric kind 1/Tuning or 2/EngineOverhaul, and
amount 50/75 respectively. Evidence is unique per slot:
`TEST-HR-V2-20261026-{17|18|19|20}-{Tuning|EngineOverhaul}`. The already submitted
September 17 Tuning slot has no new submission key or new create attempt.

Retain the original finite key vocabulary:
`TEST-HR-V2-20261026-service-{Tuning|EngineOverhaul}-2026-09-{17..20}` and
`-approve`, plus `-correction`, `-correction-review`, `-reconciliation`, and
`-reconciliation-review`. Keep one exact key per logical operation; no second
namespace or alias may generate another write. Freeze original request JSON and
the real service fingerprint semantics; do not normalize existing receipt hashes.
Review decision is numeric 1. Correction/reconciliation concurrency tokens come
only from authenticated context/results; the guard captures the new request IDs.

## Continuation host and boundary design

A future explicitly selected Development-only continuation composition must load
the reconciled retained state, not call the original unused-database guard.
Its initial state pins the vehicle and pending service IDs and all seven empty
slots. Do not treat mere marker presence as authorization. Do not set an old
in-memory `seeded` flag or relax the original new-run profile. The continuation
must have **no mapped seed endpoint and no call to CreateRowsAsync/SeedAsync**.

Use owned HTTPS localhost:7481 only, deny query/path/encoding ambiguity, keep the
normal authentication/CSRF/permission pipeline, bind actors to actual fresh
browser/MFA sessions, and require current independent fixed-role review. Remove
all global hosted workers. Dispatch local OTP only for the admitted account and
challenge. No latest-challenge fallback, OTP brute force, fabricated auth result,
real SMS provider, face provider, or external side effect is permitted.

Exactly three fresh MFA sessions, one per pinned account, with at most three
login attempts; old cookies are not imported. A separately provisioned future
auth/key namespace must reject old browser tokens without deleting/revoking old
database sessions merely to restart. Keep account/phone bindings and privileges
unchanged. Authorization must be rechecked after evidence IO by existing service
transaction checks. There is no fixture callback because there is no reseeding.

Allow only the six existing auth routes (csrf, login, local-testing/otp,
otp/verify, logout, /api/v1/me/permissions), the seven new maintenance submissions,
approval/replay of the eight pinned/captured services, and context/submit/review/
history for the single fixture trip and fuel claim. Allow the existing first
service submission **only as exact-key receipt replay**, with an explicit
precondition that its reconciled receipt is present and usable; no action path
may turn a missing/expired receipt into a new submission. Vehicle create, seed,
policy/custody changes, generic lists, other resource IDs and endpoints stay shut.

Same logical operation may overlap for a named concurrency probe. Different
operations must not overlap. Any unexpected success of a negative probe,
unrecognised ID, 5xx, storage failure, timeout, cancellation, counter overflow or
uncertain commit closes admission. Stop and preserve evidence; no automatic
retry, resume, cleanup, or second attempt under a fresh key.

## Existing-reference decryption and new evidence

Preserve the entire old root
`C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261026V2`, including
files, keys, configuration and run logs. Never reset its empty-directory checks,
overwrite files, move objects, rewrite references, decrypt to a plaintext corpus,
or copy a key into source control. Capture a read-only inventory before/after.

The existing `EncryptedPrivateFileStore` authenticates purpose, storage key,
object version, SHA256, key reference and plaintext length as AES-GCM associated
data. It opens objects from **one configured directory**. `DecryptionKeyFiles`
adds keys, not old directory lookup; adding the old key to a fresh directory alone
cannot read the retained evidence.

Proposed minimal implementation, subject to approval: a profile-local
`IPrivateFileStore` adapter around two existing encrypted stores. No shared store
format or crypto change is needed:

- Old-reference reads are restricted to the reconciled reachable-reference plus
  purpose allowlist, using the original `files` directory, original private
  `keys/files.key`, and original `hr-vehicle-v2` key label. Old access is read-only.
  Recursively include legitimate references inside authenticated retained
  receipts/snapshots; include no orphan merely because its filename exists.
- All writes go to an approved fresh continuation root with a separate object
  directory, new 32-byte key and unique active key label. The concrete path and
  label must be frozen in the approval packet before provisioning; none is
  created or selected by this draft. New filenames/versions use the existing
  `CreateNew` writer. Never write through the old-store instance.
- Route reads by exact approved key-label/reference membership, never by caller
  paths, file existence search, or fallback after authentication failure. New
  references must belong to this run's successful allocations. Missing key,
  incorrect purpose/hash/version, unexpected old reference or reparse/ACL drift
  fails closed. Do not change DB references to point at copied objects.
- Validate owner-only/private paths and no links for both stores. Preserve old
  file contents, readonly flags and ACLs. No delete/retention interface is exposed
  by this adapter. Hold a single-run write-attempt counter before each allocation;
  preserve incomplete objects on failure as the existing writer does.

Expected new private objects for the successful continuation are **49**:
7 submissions x (eligibility + result + audit) = 21;
8 service approvals x (result + audit) = 16, reusing their retained eligibility
reference for decision evidence; 2 proposal snapshots plus their result/audit = 6;
2 adjustment decisions plus their result/audit = 6. Cap allocations at 49; exact
replays, read contexts/history and planned denials allocate zero business
objects. Prove these counts against the final corrected implementation before
approval. An orphan or extra allocation is a stop, not a cleanup instruction.
Auth/data-protection key files and redacted logs are separately inventoried and
are not included in this encrypted-business-object count.

## Request and SQL budgets

Proposed finite acceptance schedule is **77 HTTP requests**, hard ceiling 100:

| Group | Requests |
| --- | ---: |
| Three sessions: pre-login csrf, login, OTP read, verify, authenticated csrf, permissions, logout, revoked-cookie permissions | 24 |
| Remaining successful primary business commands | 19 |
| Exact replay of all eight service submissions and all eight approvals after successors, plus both proposals/reviews | 20 |
| Trip/fuel context and trip/fuel history | 4 |
| Correction stale submit; changed-payload submit x2; self-review x2; stale review x2 | 7 |
| One extra distinct-key simultaneous submission for each correction/reconciliation; one extra same-key simultaneous service approval | 3 |

The winning submissions/approval of the concurrency pairs are already in the 19
primaries. Extra proposal keys are the explicit `-duplicate` probes and must fail
without another header. Extra service approval replays the same decision. Retain
the seven `-stale`/`-self`/changed-reason probes from the original bounded contract.
Do not repeat vehicle creation/concurrency or seed; those outcomes already exist.
The 23-request headroom is not permission for new writes, logins, automatic retries,
or arbitrary reads. Any needed packet revision must list its exact requests.

Expected deltas **from the reconciled partial-run baseline**:

| Table/object | New rows / permitted mutation |
| --- | --- |
| Vehicles, policies, versions, custody, trip, mileage, original fuel | +0, unchanged |
| VehicleServiceRecords | +7; existing pending record immutable |
| ApprovalRequests | +9: seven services and two proposals; existing service header approved |
| ApprovalDecisions | +10: eight service decisions and two proposal reviews |
| IdempotencyReceipts | +19, existing receipts unchanged |
| UserSessions / RefreshTokens / MobileOtpChallenges / OtpDeliveries | +3 each for fresh sessions; only this run's normal auth lifecycle mutations |
| AuditEvents | 44 expected business events: 19 commits, 21 successful replays, four audited reads; total including auth at most 100 |
| AuthRateBuckets | At most 100 new rows; updates only exact reviewed auth keys for these accounts/global limits |
| All other business tables, schema and migration history | +0, unchanged |

Auth bucket/account mutable fields must be explicitly enumerated, not whole-table
exclusions. Preserve old sessions and old table members; allow normal logout/
consumption transitions only for the three new sessions and their auth objects.
Auth audit/rate bounds and any old-state exceptions must be verified during
reconciliation before approval. No work grants, role changes, payroll, provider,
outbox, cleanup, migration, repair, legal activation or payment writes are allowed.

## Gates before an execution request

The parent must first reconcile the exact retained state and all failed-command
effects, then freeze the corrected production build and prove decimal-scale
equivalence using the retained pre-fix bytes. Changed amounts, policies,
predecessors, dates, actor bindings and protected references must still fail.
No rewriting old JSON/hashes or blanket removal of integrity comparisons.

Prepare a finite continuation manifest, separate future storage/auth provisioning
plan, and request vectors. Offline tests must cover old-reference decrypt routing,
unavailable/wrong-key refusal, no old writes/no seed, reused pending service,
seven-slot limits, three MFA/100-request ceilings, replay after successors,
concurrency/denials, and revocation after private IO. Use the existing standalone
verification route; no new test project. Preserve before/after SQL memberships,
old evidence hashes and all new allocations; record a stopped run as partial.

Only after these concrete reconciliation and offline results are available should
the parent ask for approval of the continuation packet. This document does not
authorize the implementation, provisioning, host start, or the 77-request run.
