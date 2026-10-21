# Employee Updates and Transfers

Backend-only post-hire changes, following the PM's clarification on 8 October 2026.
Hiring is not repeated. Employee/account IDs, roles, credentials and original hiring evidence remain intact.

## Which changes need approval?

| Section | Editable fields | Authority and timing |
|---|---|---|
| Basic (1) | Current/permanent address, emergency contact, personal email, marital status | Authorized HR directly saves, with reason and history |
| Identity (2) | Legal name, father/spouse, CNIC/passport type/number/expiry, DOB, gender, personal/company contact number, official contact email, identity proof | HR request; different fixed Admin approves; immediate on approval |
| Employment (3) | Department, designation, reporting manager and office | HR request; different fixed Admin approves; explicit business effective date |
| Bank (4) | Bank name, account title/number and optional IBAN | Separately authorized HR request and Admin approval; dated encrypted revision |
| Medical (5) | Medical details, authorization reference and optional private medical document | Separately authorized HR request and Admin approval; dated encrypted revision |
| Documents (6) | Approved standard-document selection | HR request and Admin approval; immediate on approval |

Basic writes cannot accept identity, bank, medical, role or employment properties. Emergency-contact
telephone numbers are basic contact details, not the employee's login identity. Phone/CNIC/legal identity
are handled in the Identity section. An official contact email is not the web login email.

**Login phone/password/email/roles are not changed here.** Changing a profile contact number does not
move SMS OTP delivery to that number. Use the existing authorized phone-recovery flow, identity review
and fresh SMS verification for that separate operation. No automatic account creation, role assignment,
session activation or mobile access is performed.

## Workflow

1. Read the appropriate current section to obtain `version` and its current values.
2. Basic: PUT a complete replacement Basic section, employee version, reason and retry key.
3. Other sections: POST one typed proposed section. Existing values stay unchanged while Pending.
4. Admin reads the protected proposal and current employee section, then supplies both current employee
   and request versions to approve (1), return (2) or reject (3). The requester and subject cannot approve.
5. Approval saves the change, before/after evidence, decision, audit and retry receipt in the same transaction.
6. A returned request is immutable. HR corrects it by submitting a **new** request with fresh versions/key.
   The requester can cancel (4) a Pending request. Closed requests cannot be reopened or decided twice.

All writes require `Idempotency-Key` (8-100 characters). An identical retry returns the original result;
changed input with the same key returns 409. Reload/review after a version conflict, never blindly retry
with a freshly fetched version. Successful writes return identifiers and employee version, not sensitive values.

## Routes

Base `/api/v1/hr/employees`. Current **web SMS-MFA** session and explicit permissions are required;
mobile bearer sessions cannot use these management APIs. Cookie writes require `X-CSRF-TOKEN`.

| Method and suffix | Use |
|---|---|
| GET `/{id}/current/{section}` | Current values, employee version and any approved future version |
| PUT `/{id}/current/basic` | Direct basic update |
| POST `/{id}/change-requests` | Request one non-Basic change |
| GET `/change-requests/{section}` | Admin/HR metadata queue |
| GET `/{id}/changes/{section}/requests` | Employee request metadata |
| GET `/{id}/changes/{section}/requests/{requestId}` | Protected proposal, versions and decisions |
| POST `/{id}/changes/{section}/requests/{requestId}/decision` | Approve/return/reject |
| POST `/{id}/changes/{section}/requests/{requestId}/cancel` | Requester cancellation |
| GET `/{id}/changes/{section}/history` | Paged immutable change metadata |
| GET `/{id}/changes/{section}/history/{changeId}` | Separately authorized before/after values |

Use the enum name or number in the route; `section` in JSON is its number. Pagination is `page`
(1-1000000), `pageSize` (1-100). Request lists accept optional `state`: Pending 0, Approved 1,
Returned 2, Rejected 3, Cancelled 4. No filter means all states, not just Pending.

Example employment request (placeholder IDs must come from real authorized master/current reads):

```json
{
  "section": 3,
  "expectedVersion": "<current employee version>",
  "data": {
    "employment": {
      "departmentId": "<department UUID>",
      "designationId": "<designation UUID>",
      "reportingManagerEmployeeId": "<approved manager employee UUID>",
      "branchId": "<office UUID, or null for Site Staff>"
    }
  },
  "effectiveFrom": "<approved business date YYYY-MM-DD>",
  "reason": "<transfer reason>"
}
```

Decision body:

```json
{
  "expectedEmployeeVersion": "<fresh employee version>",
  "expectedRequestVersion": "<request.version from detail GET>",
  "decision": 1,
  "reason": null
}
```

Return/reject require a reason. Cancel contains `expectedRequestVersion` only. The Basic PUT contains
`expectedVersion`, `data` (the Basic fields themselves, not an outer `basic` property) and `reason`.
For a non-Basic request, `data` contains exactly one matching section property; other sections are absent/null.
Unknown command properties are rejected. Swagger publishes the exact nested DTOs and length/type constraints
are enforced by the shared employee validation, not by trusting the client form.

## Effective dates and history

- Basic, Identity and Documents cannot be future-dated or backdated. Their save/approval business date is recorded.
- Employment, Bank and Medical require today or a future business date, strictly after the previous record's start.
  Same-start-day replacement and a second already-scheduled version return 409 instead of rewriting history.
- Current reads resolve today's version in `MobileAuthentication:BusinessTimeZoneId`; a future version is shown
  separately, not applied early. Timestamps record UTC instants. Invalid timezone configuration fails closed.
- An elapsed requested effective date must be returned and resubmitted. Approval never silently moves it forward.
- Transfers end the previous assignment at the new start (end-exclusive) and append its successor. Office Staff
  require an active office; Site Staff cannot select one. Staff category, employment type and existing site remain
  unchanged. Site reassignment uses the existing Admin site-assignment APIs, which share this assignment history.
- Self/indirect reporting cycles, inactive/unknown masters/managers, inconsistent overlapping history and stale
  before-values are rejected. Request and approval both revalidate references and documents.
- Bank revisions preserve prior rows; the latest started revision supersedes its predecessor. A new bank account
  is **not** marked externally verified and no payment is made. Medical values also retain earlier records.
- Original hiring snapshots are historical. Use the new current-section routes for current post-hire values.

## Documents and security

Existing private encrypted storage and scan/verification routes are reused. An Active approved employee can
receive candidate documents only with existing document-manage/verify grants plus `hr.employees.changes.request`.
Uploading/verifying evidence is not approval of a replacement document selection. No physical old file is deleted.
Identity, profile-photo and selected documents must belong to that employee, be clean, verified against the
current scan and unexpired at the effective date. Medical evidence stays under separate medical permissions.
Unavailable scanning/storage fails closed; no test scanner returns success in production.

All routes require `hr.employees.read` with server-validated All scope, not a caller-supplied employee scope.
Current limited/team/self grants fail closed until resource-scoped management queries are delivered.

| Operation/section | Additional permission |
|---|---|
| Basic write | `hr.employees.basic.manage` |
| Non-Basic request/cancel | `hr.employees.changes.request` |
| Admin decision | `hr.employees.changes.review`, tied to a current fixed Admin/SuperAdmin assignment |
| Identity read/request | `hr.employees.identity.read` / additionally `.identity.request` and `hr.employees.documents.read` |
| Bank read/request | `hr.employees.bank.read` / additionally `.bank.request` |
| Medical read/request | Existing medical read/manage and document-read grants |
| Documents read/request | Existing document read/manage grants |

Section read grants are also required for decisions and confidential history. System-role labels alone never
grant sensitive access. Existing roles are not silently upgraded; an authorized administrator must explicitly
grant the new supported permissions. The first-owner command includes the supported permissions for a future
approved owner, but this module does not create one or seed credentials.

Mutable parent/request versions protect concurrent writes. Protected proposals and before/after payloads are
stored privately; list/receipt/audit metadata exclude those values. The new append-only `hr.EmployeeChanges`
table contains journal metadata and protected references, not another employee/account/approval system.
Per-employee, per-section revisions define change order even when two writes have identical timestamps.
Identity proof is bound to the last approved Identity revision; unrelated document-selection approval does not
replace that proof. Before the first Identity revision, the original approved hiring proof is used.

## Boundaries

This slice requires approved Active employment without an outstanding future lifecycle transition. Protected
Admin/SuperAdmin employee records require a separate owner-handover flow, not ordinary HR amendments.
Reactivation/rehire, category conversion, employment-type changes, shift/roster changes, asset/SIM/certification
record lifecycles, arbitrary backdated corrections, payroll recalculation, bank verification/payment providers,
expiry notifications and a configurable approval matrix are not implemented here. Salary and site changes reuse
their existing modules. No UI is built. Real scanner/SMS and operational acceptance remain deployment work.
