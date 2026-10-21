# B03 correction and factual carry-forward client contract

Updated: 13 October 2026. Backend/API contracts only; neither web nor mobile UI is implemented by this work. Examples in `B03Contracts.http` are unexecuted integration examples, not evidence of authenticated HTTP or SQL acceptance.

## Authority and routing

| Workflow | Permission and authority | Client |
|---|---|---|
| Own sources, requests, history, cancellation, safe alternative requirements | `attendance.corrections.self`, server-derived employee and requester | Existing mobile self session or web MFA |
| Submit/read/cancel my requests for scoped employees | `attendance.corrections.submit`; current and retained original scopes | Web MFA |
| Ordinary correction/carry-forward reviewer | `attendance.corrections.review` and/or `attendance.corrections.decide`; one different authorized HR reviewer | Web MFA |
| Medical, accessibility, out-of-boundary alternative reviewer | `attendance.corrections.alternatives.decide` AND actual fixed `ADMIN` on the same current permission-bearing assignment | Web MFA only |
| Evidence-policy configuration/history | `attendance.corrections.alternatives.configure` AND fixed `ADMIN` on the same current **All-scope** assignment | Web MFA only |

Role names in client payloads/claims are not authority. A custom role called Admin, unrelated Admin assignment, ordinary HR grant, or SuperAdmin assignment alone does not authorize alternatives. No grant is seeded automatically. Live session, grant effective time, current and protected original scope checks apply on reads, writes and receipt replay. A requester cannot decide their own case.

Management controllers use normal authentication metadata; the actual services enforce the dedicated permission and same-assignment fixed Admin authority. Web writes require the existing browser session/context cookies, fresh `X-CSRF-TOKEN`, and `Idempotency-Key`. Swagger's cookie input cannot manufacture an HttpOnly login session. Self/mobile requirements do not authorize management operations.

## New registered operations

All paths below start with `/api/v1`. All operations are authenticated and no-store. Policy and decision bodies have an 8192-byte transport limit. Successful operations return their typed DTO with 200; errors use the existing ProblemDetails convention.

| Method | Path | Response / purpose |
|---|---|---|
| GET | `/me/attendance/corrections/alternatives/requirements?kind=1` | Safe `CorrectionAlternativeRequirements` for current self session |
| GET | `/attendance/corrections/alternatives/policies` | Immutable policy version history; configure authority required |
| POST | `/attendance/corrections/alternatives/policies` | `CorrectionAlternativePolicy`; explicit company evidence requirements |
| GET | `/attendance/corrections/alternatives/review` | `CorrectionQueueResponse`; Admin alternative queue only |
| GET | `/attendance/employees/{employeeId}/corrections/alternatives/{id}` | `CorrectionDetailResponse`; protected exact alternative case |
| POST | `/attendance/employees/{employeeId}/corrections/alternatives/{id}/decide` | `CorrectionDecisionResponse`; Approve/Return/Reject |
| GET | `/attendance/carry-forward/alternatives/review` | `CarryForwardRequestPage`; Admin locked-month queue only |
| GET | `/attendance/employees/{employeeId}/carry-forward/alternatives/{id}` | `CarryForwardDetailResponse`; frozen evidence and proposed target |
| POST | `/attendance/employees/{employeeId}/carry-forward/alternatives/{id}/decide` | `CarryForwardDecisionResponse`; factual-only decision |

The existing self and scoped employee submit/history/detail/cancel routes remain. No duplicate submit workflow is added. Ordinary HR queues omit explicit alternatives; dedicated Admin detail does not expose ordinary HR cases. Legacy decide routes also enforce the alternative permission if the exact case is explicit; clients should use the dedicated routes for alternatives.

## Evidence requirements, without invented defaults

Wire enums retain the existing numeric JSON representation: `alternativeKind` 1=Medical, 2=Accessibility, 3=OutOfBoundary; `kind` 4=ManualSession; decision 1=Approved, 2=Returned, 3=Rejected. Ordinary cases omit `alternativeKind`, `expectedAlternativePolicyId` and `alternativeEvidenceDocumentIds` rather than emitting new null members. Existing Site request/source byte and hash contracts remain unchanged.

Safe requirements only return `status`, `kind`, `policyId`, `version`, `effectiveFrom`, nullable `requiresSupportingDocuments`, and `allowedDocumentTypes`. Missing policy is `not_configured`, not an implicit written-only policy or fabricated medical-certificate rule. No reviewer, private path, document bytes or approval reference is exposed in this self response. The policy is selected by current submission time, not by inventing retrospective legal approval for the duty date. Future versions are not effective until their date arrives.

Configuration must explicitly include `kind`, `effectiveFrom`, `expectedLatestVersion`, `requiresSupportingDocuments`, `allowedDocumentTypes`, `approvalReference`, and `reason`. Expected latest version is 0 only for the genuinely first version. First version may start today/future; subsequent versions must be strictly later than today and the preceding effective date. Unknown JSON properties are rejected. This versioning retains an immutable protected audit record and receipt using existing storage; no EF schema addition is required.

Company-approved written-only policy: `requiresSupportingDocuments=false`, explicitly configured allowed types. If documents are required, allowed types cannot be empty. At most eight unique selected document IDs may accompany a proposal. They must be existing employee-owned, available on the duty date, configured-type, clean-scanned and verified documents. Evidence pins SHA256, rowversion and scan/verification identity/time. IDs are not storage keys. Do not upload files through the correction request or attach another employee's records. No medical document type is chosen automatically.

## Unlocked source and submission

1. Read self `/me/attendance/corrections/sources` or scoped `/attendance/employees/{employeeId}/correction-requests/sources`, using the retained shift-start `businessDate`. Pass `rawPunchId` only for the actual retained raw source. Missing checkout selects the exact open session from the preview with null raw punch.
2. Copy `sourceFingerprint`, latest assessment/summary IDs, and exact target session ID where required. Nullable assessment/summary IDs must appear explicitly as null when the preview actually contains null. Do not infer a missing calculation is zero duty.
3. For ordinary missing/failed Check-In submit `kind=4`, UTC start/end, reason and written evidence. A different HR reviewer decides. For an explicit alternative, also copy `alternativeKind`, the **currently effective** configured `expectedAlternativePolicyId`, and selected verified `alternativeEvidenceDocumentIds` (explicit empty array is valid only for an explicitly configured written-only policy).
4. POST to the existing self or scoped correction-request route with one original idempotency key. A Pending response is a received proposal, not approved attendance or a successful face check. Alternative routing is `admin_alternative_review_required`; normal routing remains `hr_review_required`.
5. Requester reads/cancels through existing own history/detail/cancel. Written evidence is protected; pending cases are immutable. Returned cases may be linked to a fresh proposal, with refreshed source/policy evidence and a new submission key. Do not edit a returned record in place.

Office previews carry `officeSource` and explicit Office IDs in `originalPunchContext` and `sessionRevisions`. The authoritative source binds actual historical `branchId`, `officeRosterAssignmentId`, `employeeAssignmentId`, `shiftVersionId`, `officeGeofenceVersionId`, `holidayCalendarId`, boundary version and jurisdiction. Do not map an Office branch to a Site ID or submit these identities as client-granted authority. The server re-reads historical membership, roster, boundary/calendar and exact opening sources. Later revisions retain opening Office fields, including overnight/transfer boundaries.

Normal Site correction evidence reads legacy schemas 1/2/3; applicable normal Site uses schema3. Normal Office uses schema4. Explicit Admin alternatives use protected schema5 with exact case, policy and documents. These are internal immutable evidence schema versions, not a client-supplied `schemaVersion` field. The calculator integration is owned separately and must validate actual special Admin decision evidence, not just accept the new schema number.

## Dedicated decision contract

POST `{ "expectedVersion": "<fresh rowversion>", "decision": 1, "reason": "<review reason>", "alternativeKind": 2 }` with the current version/case from protected detail. Version is the existing 12-character base64 SQL rowversion representation. Dedicated routes reject missing `alternativeKind` with 400 `alternative_decision_kind_required`; a mismatched stored kind conflicts. Invalid enum, unknown fields and unsupported decisions are rejected by existing validation.

Approve/Return/Reject all need the dedicated Admin grant, actual same-assignment authority, live web MFA, current/original scope, and no self-review. Approval revalidates source and configured supporting evidence before mutation. Changed/revoked documents or changed effective policy require a refreshed proposal. Return/reject preserve immutable evidence and do not create corrected duty. Approved alternatives remain **corrected ManualSession duty**; they never fabricate accepted raw, successful face verification, a provider bypass, salary, legal certification or production face activation.

The special beneficiary restriction applies even when a supervisor submitted the
request, and also applies before receipt replay. All such Approve/Return/Reject or
replay calls return403 `correction_beneficiary_cannot_decide`; no decision, adjustment
or notification is created. Normal HR contracts remain unchanged.

For timeout/lost response retry only the same key and identical body. Changed body with the original key conflicts; expired/revoked authority cannot replay confidential receipts. Fetch current detail before a new decision; do not generate a new key blindly to repeat a financial or factual effect. Stale rowversion/source rejects without silent refresh.

## Locked source and factual carry-forward

Use existing `/me/attendance/carry-forward/sources` or `/hr/employees/{employeeId}/attendance-carry-forward/sources`. Copy the exact source lock event, frozen summary, fingerprint, prior adjustment (explicit null when none), and proposed **first later unlocked** target month. A calculation/source that cannot be authoritatively verified stays unavailable, not zero.

Submit through existing `.../carry-forward/requests`. Exact alternate fields follow the unlocked contract. Existing requester history/detail/cancel remain available. Admin alternative queue/detail/decision are the new dedicated routes above. Every original and current source **and target** scope is reauthorized; an Office branch is never a fake Site. Queries use Employee/Department/Team/Project/All grants as applicable to actual retained Office membership, not a newly invented Branch permission.

Protected factual evidence preserves Site schema1, Office schema2 and alternatives schema3. It binds frozen lock/summary/session IDs, the prior virtual interpretation, configured special evidence and actual approved fixed Admin decision. Approval appends a signed factual delta to the previewed target; no source lock reopens, original raw/session is rewritten, or target worked session is manufactured. Changed first eligible target or prior evidence conflicts and requires a new preview/submission. Successive corrections compare against the last approved virtual interpretation, preventing duplicate full-duty deltas.

`financialState=pending_policy` is retained. Factual minutes are not automatically payable, a salary credit/recovery, posted/settled money or payment authorization. Financial reconciliation and legal activation are separate.

## Queue and failures

Both dedicated queues use `CorrectionQueueRequest`: half-open `from/to`, maximum 366 days; scope 1=All, 2=Employee, 3=Site, 4=Department, 5=Team, 6=Project; `targetId` required except All; state 0=Pending, 1=Approved, 2=Returned, 3=Rejected, 4=Cancelled; pageSize 1-100. Follow encrypted `nextCursor` with identical filters even for empty pages. Ordinary and alternative queue purposes differ; cursors are account/filter-bound and cannot be exchanged. At most 200 candidates are scanned; no hidden-record total is exposed.

Important outcomes: 401 invalid/revoked authentication; 403 dedicated role/grant/MFA/scope denial; 404 unavailable/non-owned/nonmatching request; 409 stale source/version/target, absent policy (`alternative_evidence_policy_required`), changed policy/documents, missing configured documents; 400 malformed contract, missing explicit decision case or unknown field. Storage/dependency failure remains failure, not a mock approval. Existing normal HR path rejects known reliable outside-boundary duty with `out_of_boundary_admin_alternative_required`.

## Verification limits

`B03VerifyRegisteredContracts.cs` generates real MVC/OpenAPI with actual transformers without executing Api Program, starting a listener/worker, resolving a database or calling providers. Service regressions use real business/authorization code with explicitly synthetic IO collaborators; translation checks forbid SQL connections. These do not establish real HTTP login/CSRF, SQL persistence/rollback/concurrency, actual scanner/provider, device or mobile UI acceptance. No production policy or evidence is seeded by this pack.
