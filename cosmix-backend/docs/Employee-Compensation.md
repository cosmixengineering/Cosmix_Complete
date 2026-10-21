# Employee Salary Change APIs

Backend scope: approved employee salary changes, using the existing employee identity,
`CompensationRecord`, `ApprovalRequest`, `ApprovalDecision`, private storage and audit records.
The PM continued the proposed HR request -> different authorized Admin approval design.
This is not a payroll engine, bank-payment integration, or the remaining profile-amendment module.
Recorded checks and acceptance limits are in [verification](Employee-Compensation-Verification.md).

## Workflow

1. A user with explicit salary read permission retrieves the employee's current salary and version.
2. HR with salary request permission submits the proposed base/gross amount, currency, wage basis,
   payment method, effective date and reason. The current salary is not changed by submission.
3. A different fixed Admin/SuperAdmin with salary review and read permission reviews the frozen
   before/proposed values, then approves, returns or rejects. The subject cannot decide their own salary.
4. Approval appends a new compensation record linked to the request and decision. A failed transaction
   rolls back the salary, decision, audit and retry receipt together. Earlier salary rows are never edited.
5. Current/as-of reads resolve the approved version for the requested business date. A future approval
   is shown as scheduled; it is not returned as today's effective salary.

Return, rejection and requester cancellation retain their evidence without adding compensation.
For correction after return, HR submits a fresh request with a new retry key and current versions;
the earlier request is not rewritten. There is no withdrawal/edit of an already approved salary version.

## API sequence

Base: `/api/v1/hr/employees`. All routes require a current **web SMS-MFA** session.
Cookie-authenticated writes also require `X-CSRF-TOKEN` and an 8-100 character `Idempotency-Key`.

| Method and suffix | Purpose |
|---|---|
| GET `/{id}/compensation?asOf=YYYY-MM-DD` | Effective salary on a business date, next scheduled record, latest record ID and employee version |
| GET `/{id}/compensation/history` | Paged immutable salary versions with resolved effective end dates |
| GET `/compensation-requests` | Paged cross-employee request queue; `state=0` selects Pending |
| GET `/{id}/compensation/requests` | Paged request metadata for one employee |
| POST `/{id}/compensation/requests` | Submit a protected salary-change proposal |
| GET `/{id}/compensation/requests/{requestId}` | Frozen before/proposed values, request version and decisions |
| POST `/{id}/compensation/requests/{requestId}/decision` | Fixed Admin approve, return or reject |
| POST `/{id}/compensation/requests/{requestId}/cancel` | Requester cancels a pending request |

Queue/list queries accept `page` (1-1000000) and `pageSize` (1-100). Omitting `state` includes all states.
States: Pending 0, Approved 1, Returned 2, Rejected 3, Cancelled 4.
The ordinary employee directory and general profile do not include salary values.

Submit body:

```json
{
  "expectedVersion": "<employeeVersion from compensation GET>",
  "expectedCompensationId": "<latestRecordId from compensation GET>",
  "baseAmount": 60000.00,
  "grossAmount": 65000.00,
  "currency": "PKR",
  "wageBasis": 1,
  "paymentMethod": 1,
  "effectiveFrom": "<approved business date YYYY-MM-DD>",
  "reason": "<reason for the salary change>"
}
```

`expectedCompensationId` must be JSON `null` only when no salary record exists.
The first post-hire salary can be approved through this same flow if the hiring form omitted compensation.
Wage basis: Monthly 1, Daily 2, Hourly 3. Payment method: Bank 1, Cheque 2, Cash 3, OtherApproved 4.
Amounts are required, nonnegative, maximum SQL `decimal(18,2)`, with at most two fractional digits;
gross cannot be below base. Currency is exactly three uppercase ASCII letters, matching the existing model.
This validates the currency format, not an exchange rate or an approved-currency catalog.
Unknown JSON properties are rejected. Account numbers, credentials and arbitrary approval actors are not inputs.

Decision body:

```json
{
  "expectedRequestVersion": "<request.version from detail GET>",
  "expectedEmployeeVersion": "<employeeVersion from compensation GET>",
  "decision": 1,
  "reason": null
}
```

Decisions: Approve 1, Return 2, Reject 3. Return/reject require a reason.
Cancel body contains only `expectedRequestVersion`.
Writes return employee version and relevant request/decision/compensation identifiers, not salary data.
An exact retry returns the original result; a reused key with changed input returns 409.
After a stale-version conflict, reload and review; never silently overwrite or remove old history.

## Effective dates and safeguards

- `asOf` defaults to today in the configured `MobileAuthentication:BusinessTimeZoneId`. Missing/invalid
  configuration fails closed. Effective salary dates are business dates; evidence timestamps are UTC.
- Request and approval require approved Active employment and no current/future blocking status change.
  Historical read access remains available to authorized users after employment ends.
- The effective date must be today or later, no earlier than joining, and strictly later than the
  last salary version's start date. Same-start-day changes and backdated changes are rejected.
- A date that passes while waiting for approval requires return and a new request, not silently changing its date.
- Only one pending salary request and one approved future salary version are supported per employee.
  The pending request captures the exact preceding salary; approval revalidates it.
- For an open-ended preceding record, its **resolved** end is the next version's start. Its stored end
  remains unchanged. `recordedEffectiveTo` exposes the stored evidence separately from `effectiveTo`.
- An explicitly recorded finite end cannot be overlapped. Gaps stay gaps. An expired newer version never
  causes an older salary to become effective again. Ambiguous/invalid history returns a review conflict.
- There is no background salary activation job: reads resolve dated versions directly. Later payroll must
  consume that same dated history together with eligible employment and approved payroll-period rules.

## Permissions and persistence

Separate sensitive, web-only, All-scope grants:

- `hr.employees.compensation.read`
- `hr.employees.compensation.request`
- `hr.employees.compensation.review`

The review grant must be on the same current fixed Admin/SuperAdmin assignment used for the decision.
Custom role names do not confer fixed-role authority. Hiring financial/profile permissions do not implicitly
grant these salary endpoints; salary permissions do not grant bank/medical records. Existing assigned roles
are not automatically upgraded. Fresh owner initialization explicitly lists the new grants; it does not create an owner.

Requests and approval before/after evidence are encrypted in the existing private store. Lists and retry
receipts contain identifiers/status only. Sensitive reads are audited; salaries and protected object references
are not returned in ordinary employee DTOs or logs. Failed SQL writes may leave encrypted orphan objects;
the existing private-storage reconciliation and backup requirements apply.

No entity, table, context, migration or automatic database update is added for this slice. Authorization,
versions, salary writes and audit/receipt saves use the existing serializable administration transaction.

## Remaining separate work

Profile/contact/bank/medical amendments, department/designation/manager/office changes, configurable multistage
approval matrices, delegated approvals, retroactive adjustments, payroll calculation/payment and notifications
are not delivered by these endpoints. This bounded single-stage flow does not complete APR-001 for the whole ERP.
Later mobile own-salary views need their own self-scope contract; this web-admin slice never grants mobile management access.
Real owner/SMS/scanner and production acceptance prerequisites remain unchanged.
