# HR/payroll shared contract — 26 October 2026

This is the coordination contract for tasks 01–11 of the HR/payroll completion plan. It describes the existing handoff and the required extension boundary for the full salary engine. It does not claim that salary calculation, statutory posting or bank settlement is complete.

## Ownership

The coordinating HR/payroll implementation chat is the sole owner of shared DTO changes, EF migrations and the model snapshot for this phase. Feature workers own only their assigned files; all schema proposals return to this owner before generation. All 26 existing source migrations, including the two pending financial/compliance migrations, were applied and verified on the configured `Cosmix_Testing` target on 26 October. The additive overtime policy contract below uses existing versioned JSON and requires no additional migration. Preserve existing migrations and other chats' working changes.

HR owns approved employee, compensation, leave, attendance and correction inputs. Finance consumes the same frozen batch identity and approved component lines; it does not maintain a second salary calculation. Mobile and web are clients of this authority. Later salary-engine and Finance tasks must extend this boundary together, with an additive, explicitly versioned contract where compatibility requires it.

## Authoritative identifiers and versions

| Field | Meaning / requirement |
|---|---|
| `PayrollHandoffBatch.Id` | Existing cross-module batch identity; Finance `BatchId` must reference this exact batch. A future salary-run aggregate must map one-to-one to this identity rather than create competing totals. |
| `EmployeeId`, immutable line `Id` | Server-selected employee and frozen component identity. Client employee labels are display data. |
| `PeriodStart`, `PeriodEndExclusive` | Business-date half-open period. UTC event timestamps are separate. |
| `AttendancePeriodLockEventId` | Exact locked attendance snapshot; missing or superseded evidence blocks preparation/locking. |
| `SourceType`, `SourceId`, `SourceVersion` | Approved source trace for each earning/deduction. Do not infer a source from an amount or reuse a source twice. |
| `PolicyVersionId`, `ApprovalRequestId`, `TraceId` | Applicable approved policy, decision and retained evidence links. Null means that source type has no such reference, never permission to invent one. |
| `LinesRevision` | Revision of the frozen component collection. |
| `Version` | Opaque base64 SQL rowversion returned by current detail; send unchanged as `expectedVersion` for lock. |
| `PreviewFingerprint` | Server digest of preview evidence; prepare sends `expectedPreviewFingerprint`. A changed source requires a new preview. |

Amounts use .NET `decimal` and the existing money guards, not floating-point arithmetic. A batch has one explicit currency (current handoff: PKR). Earnings and deductions retain their signed component amounts. Totals derive from authoritative lines. Daily proration, rounding and rates come from approved effective-dated policy; missing rules block calculation. Do not treat the adjustment-only net projection as a complete employee salary. The existing component enum's base-salary support does not prove a complete base-salary producer exists.

## Lifecycle and client behavior

1. Preview is read-only and requires a locked, complete attendance month. Empty/error responses stay empty/errors.
2. Prepare sends a fresh preview fingerprint and an idempotency key. The backend creates the batch and its frozen source lines.
3. Detail returns current lines, preparer, rowversion and processing evidence. A list-row summary is not a substitute for detail.
4. Lock sends the detail's current rowversion. The preparer cannot lock their own batch; current permission, independent actor and retained approval evidence are mandatory.
5. Handoff processing records an external reference under the existing decision-actor rule. It is not payment confirmation or a journal posting. A failed request cannot change client state to processed.
6. The existing server bank-advice CSV exports frozen employee/component totals. It is not a bank-specific transfer file and must contain no invented account numbers.
7. Accrual, payment and statutory state machines remain separate from handoff state. Later tasks 18–24 must bind them to locked batch totals, actual employee contributions, balanced entries and independent payment confirmation. Current caller-supplied accounting totals and aggregate statutory allocation are known later-phase work, not accepted as the future contract.
8. After locking/posting, corrections retain the original evidence and use approved adjustment/reversal links. Never rewrite a posted employee entitlement or silently replace the source month.

## Authorization and transaction boundary

Use the existing access catalog and effective grants, current session, web MFA for management, and server-derived actor/employee/IP. Aggregate accounting/statutory APIs currently require an explicit All-scope management grant: they cannot safely return employee/site subsets until scoped repository queries are implemented. Self-service reads derive the employee from the session. Audit and business changes commit together in the existing serializable authorization transaction. Exports are audited. No default HR/Finance permission grants are introduced.

Authority is rechecked after awaited authorization and protected-storage operations and after journal persistence, before transaction completion. Expired/revoked sessions or grants must roll back business changes, audit and retry receipts together. The attendance boundary also rechecks the original resource scope when returning an idempotent result. Supplemental source/read permissions use the same transaction's final callback; checking only the initiating permission is insufficient.

Supplemental dependencies are retained in request-local actor state and revalidated against the final fresh authority snapshot. Explicit All-scope requirements remain All-scope through the final awaited session/grant check; an employee-scoped grant cannot replace an expired All grant. Scope collections are copied before I/O and survive the audit-role actor copy without being serialized into business evidence or leaking into later requests.

## Exact-source company payroll activation

Month closure requires a ready saved input revision, configured jurisdiction, retained handoff and independent company operational activation for each covered employee. Settings or a recorded legal dossier alone never activate a source. Activation is an explicit company approval, not legal certification, a complete salary calculation or a payment instruction.

The separate `attendance.payroll-activation.read`, `.request` and `.approve` permissions require explicit All scope and current web MFA; source reads additionally require payroll-input scope over current and retained resources. The request binds the server-resolved employee/month, immutable input revision, input/private-evidence hashes, calculation version, coverage fingerprint, policy version IDs and jurisdiction configuration. The maker supplies a documentary reference, SHA256, statement and reason. The reviewer cannot be the requester or the payroll beneficiary. SQL rowversion, idempotency and fresh source validation protect decisions; a changed source requires a new approval.

Protected request/decision evidence is retained in the existing approval tables. Its validated fingerprint populates the existing `MonthLockPayrollInputEntry.LegalActivationFingerprint` field; the legacy field name does not imply legal certification. Retained month reads must validate the same source, handoff and activation evidence before downstream preparation or export. No migration or default permission grant is introduced.

## User-approved attendance decisions

- Check In is employee-initiated: tap → fresh reliable GPS and server radius/roster preflight → camera → face/liveness → fresh location and final server validation → attendance result. Preflight alone never records attendance. No background geofence attendance or periodic tracking is enabled by this decision.
- Existing manual checkout remains face-free, with its own current location/server checks.
- Authorized HR/Admin configure effective-dated shift, late and half-day thresholds through explicit management permissions. No half-day start time is invented or hardcoded by this change.
- Task 06 acceptance is working dynamic configuration, as explicitly clarified by the user. Missing real company values are not an unfinished implementation task; calculations still require HR to publish those values before operational use.
- Missing checkout is an HR-review case. Unresolved attendance must block financial readiness/month closure; it must not turn into an automatic salary deduction.
- GPS coordinates are retained for 180 days. Cleanup preserves attendance timestamps, daily summaries and non-location audit evidence. A change to an active privacy notice requires fresh acceptance of the displayed version.

## Required deployment/configuration evidence

Source/build tests do not approve company rules or production data. Before live use, HR must publish the actual effective rules and privacy notice with named approvers; deployments must verify effective environment overrides (especially retention), compatible schema, worker/storage access, genuine OTP delivery and a phone camera/GPS/liveness acceptance run. Test fixtures do not substitute for those records.

## Published overtime rules and historical compatibility

Attendance policy schema 3 now accepts optional `overtime`. Its absence means new OT calculations are unavailable; it never selects nine hours or a rate implicitly. HR enters standard duty seconds (60–86400), base-salary basis, actual calendar month days or fixed divisor (1–366), a positive multiplier no greater than 100 with at most four decimal places, explicit whole/two-decimal half-up/half-even rounding, and the currently supported count-all-breaks treatment. Unworked gaps remain excluded. The roster must match the configured duty window, and completed closed duty is mandatory. Eligibility and independent OT approval remain separate requirements.

Runtime resolution uses the employee's effective published attendance policy and retains its ID plus exact overtime rules in `OvertimeDurationResult.OvertimeRules`. New financial evidence identifies `COSMIX-OT-PUBLISHED-POLICY-PKR-V2`. The amount is monthly base salary × multiplier × approved duration ÷ divisor days ÷ standard duty duration, rounded once using exact rational arithmetic. Source, approval and award digests bind these inputs. Finance consumes that immutable award rather than recalculating from a later policy.

Existing `COSMIX-OT-BASE-CALENDAR-9H-NOBREAK-PKR-V1` awards keep their original calculation. Optional null fields are omitted, preserving prior serialized source/award digests; old awards are not revalued. Approved source provenance remains required, and the repository accepts only Attendance-category versions with valid direct-HR or approved-workflow authority. No new role grants, money movement, compensation mutation or retrospective policy assignment is part of this change.
