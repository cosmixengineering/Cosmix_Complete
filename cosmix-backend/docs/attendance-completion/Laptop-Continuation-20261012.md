# Huma laptop Attendance continuation - 12 October 2026

## Source and ownership

The user allowed one initial fast-forward pull from `origin develop`. It advanced
`70adaf7` to `3e4e424`. No further synchronization, commits, branches or worktrees are
authorized. The existing `src/Cosmix.Api/Cosmix.Api.http` modification was preserved;
its before/after SHA256 was identical. This checkout is the backend under
`Downloads/Cosmix_Erp_software/cosmix-backend`, not the other backend checkout.

The latest expected migration 20 and Attendance handoff documents are now present.
Source-laptop verification is retained evidence, not fresh verification on this laptop.
The initial local Release build passed with zero warnings and zero errors.

## Requirement-to-code gaps

| Requirement | Actual source | Remaining acceptance/work |
|---|---|---|
| Site schedule, My Shift, geofences | Existing scheduling and capture services | Authenticated local SQL and overnight/transfer tests |
| Corrections and factual carry-forward | Existing immutable correction/session/ledger services | Financial valuation and real SQL concurrency/storage tests |
| Policies, leave, daily calculation | Existing direct publication, Admin leave and daily producer | Authenticated writes, protected evidence and historical checks |
| Ordinary/late deduction inputs | Existing schema-3 producer and immutable revisions | Confirmed posting provenance, cross-month reconciliation and legal activation |
| OT | Existing eligibility/request/review and confirmed formula | Exact payroll handoff/settlement and authenticated acceptance |
| Month finalization | Existing preview/frozen manifest and guards | Verified financial dependencies and exact calculation revision retention; currently unavailable |
| Company/site jurisdiction | Existing SuperAdmin settings and dated site source | Signed legal scope and formal activation authority, not a province toggle |
| Face verification | Existing enrollment/challenge/proof orchestration; unavailable provider | Server-owned capture, actual CompreFace/ONNX adapter, model artifact and real-device acceptance |
| KPI, waiting, extra leave, encashment/recovery | SRS requirements and some models only | Approved formulas, scope, evidence, authority and settlement rules |
| Notifications/reports/workers | Existing outbox and private factual reports | Authenticated persistence, worker races and private-storage/restart acceptance |

No row is a claim of full workflow acceptance merely because a route or model exists.

## Database inspection (read-only)

The LocalDB alias reported stopped and its start command failed; a running process
and enumerated named pipe independently identified the actual existing server:
`Software-PC1\LOCALDB#842DD4FB`, using
`np:\\.\pipe\LOCALDB#842DD4FB\tsql\query`. Pipe identities are ephemeral.
The connected login/owner is `COSMIXRND\Huma.Khan`.

`Cosmix_Testing` has **12**, not 17, applied migrations, ending at
`20261008074438_AddEmployeeChangeJournal`. It contains three known synthetic login
accounts, eleven employee records, eight drafts, and retained business/audit data.
The additional eight employees are not marked with the three original `TEST ONLY`
names. Their synthetic provenance must be confirmed before any write. No backup,
migration, fixture, key or account write has been executed by this continuation.

Do not run the source-laptop initializers against this database. They bind another
user's private paths, baseline and fixture journal. Preserve all existing data and
keys; review pending migrations 13 onward and verify a backup before upgrading.

## Confirmed financial decision

Only explicitly assigned Accounts/Finance permission holders may attest handoff or
posting. A saved revision is not posting. A handoff receipt is not confirmed settlement.
Receipts must retain exact revision, employee/month, line keys/amounts, status/time,
external evidence and actor. If 200 PKR was actually deducted and corrected to 100,
the later adjustment is a 100 PKR credit. If the 200 was never posted, no refund exists.
No payment release or fabricated posting is authorized.

## Legal authority boundary

Counsel clearance, HR applicability evidence and SuperAdmin final activation are a
**proposed** authority model only. Individuals have not been formally appointed and
no signed clearance has been supplied. Active payroll penalties remain unavailable.
Independent implementation must not silently turn this proposal into approved law.

## Shared-file coordination

Root owns shared DI, permissions, project/package files, DbContext, migrations and
snapshot. Agents have disjoint new-file scopes for financial attestations, provider
infrastructure, legal research and the remaining-SRS decision register. Builds run
sequentially. No agent is authorized to write SQL fixtures or mutate Git.

## Executed local additions

- Two encrypted face-capture upload contracts, opaque account/session/employee/device/challenge
  tokens, single-operation claims and audited current-binding reads. Capture disablement and a
  revocation between preflight/replay were reproduced and fixed. Original face proof consumption
  and face-free geofenced Check Out are unchanged.
- Actual private CompreFace detection/1:1 HTTP adapter and hash-pinned ONNX inference path with
  bounded native decoding, BGR preprocessing, score semantics and timeout handling. It uses local
  encrypted reference images, not a remote face collection. Defaults remain unavailable.
- Manual immutable Accounts/Finance handoff -> posted -> settled evidence, exact protected source
  revision/line validation, permissions/current and retained scopes, audit, retries and a confirmed
  posting reader. This is not an external payroll adapter or monetary reconciliation.
- Versioned legal-review dossiers with actual PDF parsing, scanner gating, private uploads/downloads,
  policy/jurisdiction/scope binding, concurrency, audit and retries. They never activate payroll rules.
- Official-source legal research, remaining-SRS decision register and laptop-specific guarded
  preflight/backup/upgrade/authentication tooling. Those tools have not been executed.

Fresh results: Release 0 warnings/errors; capture 103; crypto/JSON 33; provider transport/semantics
122; attestations 148; legal evidence 46; payroll producer 49; month locks 43; existing model 22;
new migration model/script 16; anonymous HTTP/OpenAPI 1014. These counts are assertions, not
end-to-end workflows. See [complete verification boundaries](Verification.md).

The approved source already contains the core attendance workflows. New independent work does
not complete financial cross-month reconciliation, financial carry-forward/finalization, statutory
activation, biometric retention/deletion/device acceptance or the unresolved SRS extensions.

## Schema And SQL Artifacts

New local migration: `20261012151917_AddAttendanceFinancialAttestationsAndLegalReviewEvidence`.
It adds two evidence tables and a policy/version composite key; Down rejects retained history.
All 40 migration 1-20 source/designer hashes remain unchanged. The snapshot matches the current model.

- [20-to-21 incremental SQL](../sql/20261012151917_AddAttendanceFinancialAttestationsAndLegalReviewEvidence.sql):
  SHA256 `76FC882A9132623C20DC3016CC55155DD8CC1124474162DC246D398CFFDFEE6D`.
- [Prepared pending range 13-to-21](Huma-Attendance-Pending-13-21.sql):
  SHA256 `CFFC7568F2F4ECF06452C0FC73CC4EE8BEF36EDA3D79FF55A0CCFCB43973EF60`.

Neither script was applied. Pending older migrations alter nullable interpretation/approval fields,
add evidence and constraints, and retain EF migration-history inserts; they are not a from-zero
or idempotent reset script. Inspect actual target/history and preserve keys/data. A verified backup,
exact target/run manifest and synthetic-record ownership approval must precede any application.

## Owner Integration List

Application/Domain: new FaceCapture contracts/service/reader; server actor fields in existing face
provider inputs and FaceService; new payroll-attestation entity/rules/contracts/service/reader;
new legal-review entity/contracts/service; shared DI and four dedicated permission definitions.

Infrastructure: new CompreFace/Image/SilentFace/PoC adapters; scoped gated provider registration;
new financial/legal repositories/configurations/PDF inspector; migration 21/designer/snapshot;
ONNX/OpenCV/PdfPig/Hosting package pins and the two affected package lockfiles.

API: protected capture envelope and strict JSON filter; two existing face controllers' capture
actions; financial attestation and legal-review controllers; protector registration in Program.

Documentation/checks: new crypto/provider/financial/legal/migration standalone files; capture and
HTTP verifier extensions; research/decision/setup records; two SQL artifacts; README, Architecture,
SRS map, SQL map, UAT and verification updates. Existing `Cosmix.Api.http` is the user's change,
not a deliverable from this task. No commit or remote integration was performed.
