# Part B Delivery Status

13 October 2026. Scope: Attendance evidence, scheduling, face contracts,
corrections, zones and site reports in the existing backend. This does not
deliver Part C-G, React/React Native applications or production activation.

## Requirement Checklist

| Item | Implemented backend behavior | Remaining acceptance or approval |
| --- | --- | --- |
| B01 | Real Branch office boundary history; exact office membership/roster/shift/calendar/boundary pins in raw events, duty, corrections, calculation/payroll sources; transfer/overnight guards; original-shift checkout reminders | Approved real office geometry and authenticated SQL/mobile cases; genuine accepted IN needs actual face infrastructure |
| B02 | Independent explicitly configured duplicate/sequence/break/travel/duration/device assessment; raw offline evidence stays Unverified; atomic final evidence; proof/session/permission expiry and receipt reauthorization | Company limits, explicitly disabled groups and device observation mappings; real concurrent HTTP/SQL/device tests |
| B03 | Ordinary HR correction authority retained; dedicated fixed ADMIN medical/accessibility/out-of-boundary review, configurable evidence policy, retained decision-time authority, schema5 calculation reader, immutable source versions, factual first-unlocked-month carry-forward | Actual documents/scanner and authenticated persistence/concurrency; monetary effects remain Part F |
| B04 | Existing fail-closed one-to-one CompreFace/ONNX Development PoC and enrollment/challenge/device/expiry/single-use contracts; face-free Check-Out; documented Admin human alternative | Reviewed model/license/hash/preprocessing, deployed CompreFace/configuration, consented device captures, calibrated thresholds and formally approved production infrastructure/retention/enrollment/accessibility acceptance are absent |
| B05 | Durable committed-source retirement intents, replacement/revocation/capture/offboarding consumers, retry/restart recovery, retained use-denial and disabled-by-default worker | Approved physical disposal adapter and retention/hold/backup/key treatment are not supplied; no destruction claimed; actual SQL worker/concurrency acceptance remains |
| B06 | Confirmed Operational/Red scope; own safe instructions/acknowledge/dispute/review; independent encrypted expiring offline written claims; original duty/session/device binding and permanent own-key reconciliation | Approved notice/delay/retention/storage/cleanup/holds, real SQL/mobile/restart/concurrency and delivery acceptance; no Green/Amber/custom zone scope |
| B07 | Existing scanned attachment and structured report review reused; scoped queue discovery, retained response history and permanent own submission-key reconciliation; web/mobile request examples | Authenticated HTTP/SQL, actual scanner/outbox and uncertain-network/device acceptance; request packs are not implemented clients |
| B08 | CLOSED by the human's explicit decision: mobile punches and authorized HR corrections only | No file import or attendance-machine workflow will be added |

Admin authority here means fixed ADMIN on the same permission-bearing assignment,
explicit dedicated permission, web MFA, current/original employee scopes, no
requester/beneficiary self-decision and retained evidence. This does not transfer
company/Head Office authority from SuperAdmin or invent legal/payroll approval.

## Schema Evidence

Frozen B24: `20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims`.
It follows Office22 and A23, adds only three Attendance tables and ten nullable
source/claim columns plus restrictive keys/checks/indexes, and refuses downgrade
before destructive operations. Existing applied files are preserved.

The coordinated SQL owner, not this B implementer, applied the reviewed source
range. Its sanitized clone/original results were independently read by B:

- Both histories have 24 migrations; original rows, identifiers and source facts
  are preserved. Existing five accounts and fourteen employees were not replaced.
- The isolated clone also passed an idempotent SQL repeat. The original's repeat
  was NOT executed; its result explicitly records false.
- Reviewed B24 SQL SHA256:
  `A0EEFC4D51E1BF1A0FA4BB970A8DCE8094A336EF6D9219BAD54A599136B525F2`.
- No production target/provider was accessed. B performed no SQL/fixture writes.

Detailed source hashes/ownership are in
[the frozen B24 handoff](../integration/Part-B24-Source-Handoff-20261013.md).
This upgrade is schema/preservation evidence, not authenticated workflow acceptance.

## Verification Packet

Final parent checks are recorded separately from earlier source checkpoints.
`VerifyIntegratedSource.ps1` invokes the real existing standalone checks with
strict Release, independent runfile entrypoints and retained logs. It creates
only owned build outputs and restores its process TEMP/TMP overrides; it does
not seed SQL, start the API, grant access or activate providers/workers.

| Parent-executed check | Passed assertions | Boundary |
| --- | ---: | --- |
| Office source / boundary / model / persistence | 16 / 19 / 13 / 18 | Real source/domain/service/EF, synthetic IO or no-connection metadata |
| Capture / temporal authority projection | 147 / 14 | Actual capture, proof and replay gates, synthetic proof/IO; SQL translation only |
| B02 rules / evaluator / reader / management / persistence | 31 / 27 / 7 / 22 / 31 | Actual configurable policy/finalization, no company values or real device acceptance |
| Correction / factual carry-forward | 196 / 87 | Actual service, staged/retained authority and rollback simulation; no SQL approval |
| Audit / B03 registered contracts | 47 + 12 / 155 | Actual service plus forbidden-connection translation; actual OpenAPI/JSON, 349 paths |
| Calculation / selected repository query and DI | 181 / 22 | Actual schema5 consumer, authority reader/alias and bounded queries; no SQL connection |
| Office payroll sources / jurisdiction DI | 38 / 12 | Historical bindings and exact legacy JSON, no legal activation |
| Payroll producer / input service / OT | 55 / 73 / 70 | Existing real services and explicit synthetic policies/IO, no financial posting/payment |
| Reminders / reminder query | 44 / 28 | Original-shift service and actual SQL translation, no delivery |
| Offline zone intake | 153 | Actual Unverified service/reconcile/API/query, protected IO synthetic |
| Site report service / query / contract | 196 / 32 / 14 | Actual retained status/key/scopes, no live scanner or HTTP |
| Dev/Prod composition / registered contracts / B24 review | 26 / 15 / 15 | Actual DI/MVC/OpenAPI/EF source, 349 paths and 44 migration operations |

The initial integrated group stopped on a concurrently incomplete journal; that
failed record is retained and superseded by affected green reruns. After source
freeze, one correction check had a different source hash than its previous run;
it was rerun instead of adopting the stale result. Final strict four-project
Release has zero warnings/errors. All assertions remain source/offline checks,
not authenticated SQL or biometric acceptance.

Logs and per-check source hashes are retained under
`D:/CosmixVerification/gap-b-parent-20261013/`: `integrated-immutable`,
`integrated-unaffected`, `integrated-final`, and `integrated-frozen-checks`.
No old output was deleted. Each runfile retains a distinct output identity.

The final authority writer and selected calculation reader share the same
scoped transaction-bound journal. Explicit permission intervals are protected
privately because ordinary PermissionGrant JSON omits them. A missing outer audit
is never proof of authorized staging. Committed history requires the exact decision,
source, private authority snapshot and outer audit; later revocation cannot rewrite
that history, while new decisions/replays still require current authority. All
special beneficiary Approve/Return/Reject and replay cases return 403
`correction_beneficiary_cannot_decide`. Ordinary HR rules are unchanged.

Feature handoffs retain exact source/check lists and executed boundaries:

- [Office capture](B01OfficeCapture-Contract.md)
- [Capture assessment](B02Integration-Handoff.md)
- [Admin/HR corrections](B03-Office-Corrections-Handoff-20261013.md)
- [Office calculation/payroll consumers](../attendance-policies/Office-Calculation-Payroll-Consumers-20261013.md)
- [Durable retirement](../attendance-capture/PartB05-Durable-Retirement-20261013.md)
- [Offline zones](B06ConsumerContract.md)
- [Site reports](B07ConsumerContract.md)
- [Client-independent UAT](../attendance-completion/Attendance-UAT-Guide.md)

Assertion counts overlap across harnesses and are not a full acceptance percentage.

## Exact Live-Test Blockers

1. The preserved selected test database has five accounts. Its legacy supported
   host verifier expects exactly three synthetic accounts and a stale pinned pipe.
   The SQL owner invoked the actual verifier and documented the expected refusal.
   A reviewed bounded profile plus explicit account classification/fixture approval
   is needed; do not delete accounts, bypass/relax the guard or silently adopt another
   target. New permission definitions are not operational grants.
2. The human confirmed that neither reviewed ONNX model nor CompreFace deployment
   nor consented test captures/devices are available. The desktop deployment guide
   explains acquisition and setup; source checks cannot manufacture these tests.
3. Company capture-validation and alternative-document policies must be explicitly
   configured with approval references. SYNTHETIC test policies are not production
   defaults or legal clearance.
4. Zone cleanup and physical biometric destruction require their separate approved
   retention/holds/storage/backup/key treatment. They remain disabled/unavailable.
5. Payroll posting/settlement, legal activation and positive month finalization are
   Part F dependencies. Factual corrections never create payment/refund evidence.

## End-To-End Next Cases

After the supported synthetic host profile is approved, run authenticated office
boundary/roster publication, configured policy reads/writes, ordinary HR and Admin
alternative correction decisions, summary/source history, factual locked-period
adjustments, own report reconciliation and independent offline zone review. Verify
SQL rows, exact receipts/audits/notifications, private evidence, rollback, expiry,
revocation and actual separate-connection competing writes.

Positive face-verified office/site IN and genuine/wrong-person/photo/screen spoof
acceptance additionally need the real provider/model/device prerequisites. OUT
remains face-free, but requires genuine retained accepted duty and its boundary.
No synthetic adapter result is permission to publish a real biometric pass.

**Full Part B or full Attendance acceptance is not achieved.** Source delivery,
schema preservation, live authenticated acceptance, provider/device acceptance,
production approvals and client implementation must remain separate claims.
