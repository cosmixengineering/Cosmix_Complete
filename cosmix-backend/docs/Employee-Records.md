# Post-hire assets, SIMs and certifications

A05 adds post-hire operational requests, independent fixed Admin review, retained custody/evidence and factual current/history APIs. Existing hiring intake remains separate. A selected-document change does not renew or reinstate a certificate.

All routes require current web MFA and the current All-scope employee read permission plus the selected family's permission. The nine independent permissions are `hr.employees.{assets|sims|certifications}.{read|request|review}`. A review grant also requires the same current fixed Admin/SuperAdmin role assignment; a requester cannot review their own request. Family read does not grant bank, medical, identity-document download or other family access.

No operational approval authority is inferred from deployment defaults. Requests and decisions require `EmployeeRecordPolicy` with an explicit approved version, effective period and `ApprovedReviewMode = "FixedAdminReview"`. Empty/invalid/out-of-date configuration returns `employee_record_policy_not_configured` (503). SIM issue/transfer additionally requires approved, effective `EmployeeFieldPolicy.SimCreditTierValues`; an absent list returns 503, and a value outside it returns 400. These configuration records must come from an approved management decision. The code seeds no version, authority or tier. Requests retain the operational policy version and SIM credit-policy version and conflict if either changes before review.

## Routes

The common prefix is `/api/v1/hr/employees`. Page queries accept `page` (1–1,000,000), `pageSize` (1–100); request queues optionally accept `state`. Results use the existing `PagedResult` shape.

| Method | Route | Result |
|---|---|---|
| GET | `/{id}/assets` | Open asset custody, optionally at an earlier UTC `asOf` instant |
| GET | `/{id}/assets/history` | Retained issue/return custody rows |
| GET | `/{id}/assets/{custodyId}/history` | Immutable operational issue/return events with evidence IDs and transfer source |
| GET | `/{id}/sims` | Non-returned current successor tips, optionally at a historical business `asOf` date |
| GET | `/{id}/sims/history` | All issue/status/return/transfer rows |
| GET | `/{id}/certifications` | Current successor tips including suspended/revoked records and factual expiry |
| GET | `/{id}/certifications/history` | Retained certificate/license revisions and attachments |
| GET | `/record-requests/{kind}` | Authorized family request queue |
| GET | `/{id}/records/{kind}/requests` | Employee family request history |
| GET | `/{id}/records/{kind}/requests/{requestId}` | Frozen proposal and retained decisions |
| POST | `/{id}/records/{kind}/requests` | Submit an operational proposal |
| POST | `/{id}/records/{kind}/requests/{requestId}/decision` | Approve, return or reject using a different fixed Admin |
| POST | `/{id}/records/{kind}/requests/{requestId}/cancel` | Requester cancels a pending proposal |

`kind` is `Asset`, `Sim` or `Certification`. Write routes require `Idempotency-Key` (8–100 printable key characters from the existing audited employee-operation contract). Request/decision responses contain `RecordRequestId`, `EmployeeRecordId` and, where applicable, `DecisionId`. They contain no private object/storage references.

A proposal requires `ExpectedEmployeeVersion`, `Action`, `ExpectedCurrentRecordId` (null only for Issue), UTC `EffectiveAt`, printable `Reason`, and exactly one typed `Asset`, `Sim` or `Certification` input. Decisions require expected employee/request rowversions, decision and reason. Transfers also require the receiving employee's expected rowversion. Actual operations must occur on the current configured business date, at or before server time; arbitrary backdating and future scheduling are rejected. A proposal carried across a business-date boundary must be cancelled/returned and resubmitted with current evidence. New issues, renewals/replacements and transfers require active, unarchived employment without a scheduled lifecycle change; return, suspension and revocation remain available for retaining offboarding facts.

Asset issue requires reference, condition and the employee's evidence document ID. Return requires the exact open custody ID, return condition, evidence and a real active receiving account. Transfer retains the source return and creates a new issue for a different approved, active employee, with separate receiving evidence and condition. The filtered unique SQL index on active `AssetReference` is preserved. Transfer saves the source close/history before adding the receiving issue within the same outer transaction; a later failure rolls back both. The owned write guard rejects deletion, changed issue metadata, repeat return and an unpaired return. Operational events retain revision, effective time, condition, document ID, actor, reason, receiver, approval and transfer linkage. Legacy hiring rows keep their original facts; no historical operational actor/evidence is fabricated.

SIM issue requires reference, explicit international phone, approved credit tier and evidence. Phone numbers use the shared E.164 normalizer. Return appends a same-employee successor with original issue details and issue evidence plus return date/document/receiver. Transfer appends that source return and creates a new receiving assignment linked to it. Global reference/phone tip checks run in the serializable authorization transaction; suspended SIMs cannot be silently reactivated through transfer. Parent-owned hiring validation also checks these current tips, so hiring cannot bypass the operational guard.

Certification issue/renew/replace requires type, number, issuer, issue/expiry dates and exact verified Certificate/License evidence with matching dates. Renewal requires an active predecessor with a known expiry, later validity and new evidence. Replacement keeps type/issuer/number/issue/expiry unchanged and changes only attachment evidence; it cannot grant longer validity. Suspension/revocation append a new status/evidence revision and retain the underlying certificate attachment. Revocation is terminal; renewal/replacement does not reinstate a suspension. Each predecessor has at most one successor; old certificates and attachments remain in history. Expiry is derived on the configured business date (`ExpiresOn < asOf`); the expiry date itself remains inclusive, matching existing document rules.

Evidence inputs are document GUIDs. The server requires exact employee ownership, Standard privacy, a permitted document type, current clean/signature/verification state and valid dates, then reads the server-held private file reference and verifies byte length/SHA-256. It repeats validation on approval. Cross-employee, medical/identity evidence, corrupt/unavailable files and arbitrary client storage-reference fields fail. Evidence bytes and storage locations are never returned by these APIs; downloads retain the separate document permissions.

## Integration and verification limits

Business changes, generic approvals/decisions, audit evidence and compact idempotency receipts use the existing `EmployeeOperation` serializable transaction. Reads also record authorized audit access. Record-only retries recheck current permission/MFA; old receipts do not grant new authority. Certificate completeness uses the latest applicable successor in the document module rather than all historical certificates.

The factual current/history projections are available for Part G consumers. This slice does not claim a dispatched notification, delivered reminder, generated report or external provider success. Actual notification recipients/rules and report integration remain Part G work.

Standalone verification source is [VerifyEmployeeRecords.cs](employee-records/VerifyEmployeeRecords.cs). It exercises the real services/operation/pure domain/model with synthetic repositories, authentication grants and private bytes in TEMP. No test project is added to the solution. The recorded checks do not prove actual SQL transaction rollback/range locks, authenticated HTTP/CSRF, deployed private storage/scanner, migrations on an existing database or production acceptance. Parent-owned schema generation/backfills and real acceptance remain separate; no SQL is applied by A05.
