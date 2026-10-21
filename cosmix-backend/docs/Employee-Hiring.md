# Employee Master: Hiring and Approval APIs

Implemented backend slice, 7 October 2026. No client UI is included.
The source SRS is unchanged; the latest approved Office/Site and authentication clarifications take precedence.

## Delivered workflow

1. HR creates an empty draft. SQL Server assigns the immutable employee number. No account is created.
2. HR saves the incomplete profile, employment proposal, addresses, emergency contact and supporting sections.
3. Separately authorized users save financial/bank and medical proposals through separate endpoints.
4. HR uploads private documents, obtains a malware scan and has an authorized verifier review clean files.
5. HR puts the selected standard document IDs in the profile's `documentIds` manifest. Superseded/rejected uploads
   remain in history but can be excluded from this manifest. Medical attachments have a separate protected route flag.
6. HR checks submission validation, then submits the current employee version. Missing mandatory details, invalid masters,
   unavailable/expired selected documents and invalid references prevent submission.
7. A fixed Admin/SuperAdmin with explicit review permissions sees the pending queue and immutable submitted form.
8. Admin approves, returns with a correction reason, or rejects with a reason. A requester cannot decide their own submission.
9. Approval creates effective employment records and one dormant shared account in the same transaction. No role or logged-in session is invented.
10. Admin completes mobile enrollment and assigns approved access through the existing authentication services. Web credentials use the existing Admin-set password endpoint.

Submission changes the employee to PendingApproval, not Active. It does not create a role, login, mobile session,
salary payment, inventory transaction, attendance eligibility or site assignment.
Return unlocks the draft for HR correction and a new immutable submission version. Rejection is terminal for that
submission/record; it is not an unrecorded reopen. Earlier submissions and decisions are retained.
Approval rechecks the current masters, identity uniqueness and document state against the frozen snapshot.
Expired/unavailable/changed documents cannot be approved using stale evidence. Changes require return and resubmission.

## Office and Site rules

- `staffCategoryId` must identify an active OFFICE or SITE master.
- Office Staff may save an incomplete draft, but an active `branchId` is mandatory before submission.
- Site Staff cannot supply a branch. There is no HR `siteId` field; unknown JSON properties are rejected.
- Admin creates sites through the existing Site Master. HR never chooses a site; Admin's decision/assignment flow does.
- Department, designation, employment type, reporting manager and optional proposed shift are validated server-side.
  A reporting manager must be a different, currently active approved employee with eligible status history.
- Proposed shift is not a roster. Effective working hours, schedule assignment and face enrollment must be completed
  before attendance is enabled by later modules. No shift, age, gender, marital-status or approval policy values are invented.

## Routes and sequence

Base: `/api/v1/hr/employees`. All calls require a current web SMS-MFA session and the stated permissions.
Write requests also require browser CSRF protection and an 8-100 character `Idempotency-Key`.

| Method and suffix | Purpose |
|---|---|
| POST base, no body | Create empty draft; returns EmployeeId, employee Version and status |
| GET base | Bounded employee directory; no CNIC/contact/salary/bank/medical columns |
| GET `/{id}` | Confidential HR profile and employee status/version |
| PUT `/{id}/profile` | Save the complete replacement **profile section** of the draft |
| GET/PUT `/{id}/financial` | Separate compensation/bank/previous-salary proposal |
| GET/PUT `/{id}/medical` | Separate authorized medical proposal |
| GET `/{id}/documents?medical=false` | Standard private document metadata |
| POST `/{id}/documents` | Multipart upload; `file`, `type`, `medical`, `issuedOn`, `expiresOn`, `expectedVersion` |
| POST `/{id}/documents/{documentId}/rescan?medical=false` | Retry real scanning after configuration/outage |
| GET `/{id}/documents/{documentId}/review?medical=false` | Verifier-only clean-file download before manual verification |
| POST `/{id}/documents/{documentId}/verification?medical=false` | Audited Verified/Rejected decision |
| GET `/{id}/documents/{documentId}/download?medical=false` | Authorized download only when domain availability rules pass |
| GET `/{id}/submission-validation` | `canSubmit`, missing field paths and unavailable standard document IDs |
| POST `/{id}/submit` | Submit a locked, encrypted snapshot to the Admin queue |
| GET `/submissions` | Admin pending queue by default; bounded search/pagination/status filter |
| GET `/{id}/submissions/{submissionId}` | Exact submitted profile and standard document manifest |
| GET `/{id}/submissions/{submissionId}/financial` | Separately authorized submitted financial values |
| GET `/{id}/submissions/{submissionId}/medical` | Separately authorized submitted medical values |
| POST `/{id}/submissions/{submissionId}/decision` | Admin approve, return or reject; same-transaction effective records and dormant account |
| GET `/{id}/decisions` | HR-readable hiring decision history; latest 100 records |
| GET `/{id}/access-setup` | Fixed Admin with account-read permission retrieves the shared account ID/version and setup status |
| POST `/{id}/mobile-provisioning` | Retry-safe enrollment of the approved login phone; no SMS/session is automatically issued |
| GET `/{id}/assignments` | Current/future assignment reference rows and employee version |
| GET `/{id}/assignments/history` | Paged effective-dated assignment history |
| POST `/{id}/assignments/site` | Fixed Admin assigns/reassigns Site Staff with a reason and effective date |

Section PUT body: `{ "expectedVersion": "<employee rowversion>", "data": { ... } }`.
Submit/rescan body: `{ "expectedVersion": "<employee rowversion>" }`.
Verification body: `{ "expectedVersion": "<employee rowversion>", "decision": 1, "reason": null }`.
Decision 1 means Verified, 2 means Rejected; rejection requires a reason. This is **document verification**, not hiring approval.
Document type values: Offer 1, Contract 2, Identity 3, Degree 4, Certificate 5, License 6,
Signature 7, ThumbImpression 8, ProfilePhoto 9, Other 10. Identity type: CNIC 1 or Passport 2.

Use the latest **employee** Version from each write response for the next mutation, including file operations.
Document metadata has its own Version for evidence; do not use it as the employee ExpectedVersion.
Each mutation returns a new employee version. A 409 stale-version response requires reloading the current draft,
not silently overwriting it. Exact idempotency replay returns the original response even if the form has since changed;
reload after replay if a newer current version is needed. Reusing a key for another body/employee is rejected.

## Fields

`EmployeeProfileInput` in Swagger contains identity/CNIC/passport and dates; personal/company contacts/SIM details;
both addresses/cities/province; emergency contact; department/designation/employment category; Office/Site category;
office and reporting manager; joining/probation; optional shift proposal; explicit proposed login phone/mobile request;
previous employment; certifications; asset custody proposals; and selected document IDs.

`EmployeeFinancialInput` contains base/gross/currency/wage basis/payment method/effective date, bank name/title/number/IBAN,
and prior salaries linked to stable previous-employment EntryIds. No client-supplied bank verification status is trusted.
`EmployeeMedicalInput` contains authorized details, authorization reference and an optional **medical-classified** document ID.
Normal profiles never return these two sections. There are no password, role, approval status or server employee-number setters.
Phones use canonical international format, such as a leading `+` and country code. Monetary values use decimal precision.
Asset returns also require `returnedToAccountId`, the actual receiver's existing account reference, with the return
date and condition. Submission validation checks that the account existed by that business date, and approval
rechecks it. An invalid receiver returns `400 invalid_employee_asset_return_receiver` before the draft is locked;
it does not invent the Admin
as receiver. This records approved custody evidence, not an Inventory stock transaction.
Optional financial sections may be partial in a draft, but supplied compensation/bank/previous-salary rows must be
complete before submission. The validation endpoint returns missing financial section paths for incomplete rows.
Missing profile/financial dates should be omitted or `null` in incomplete drafts. The CLR default sentinel
`0001-01-01` returns `400 invalid_employee_date`; it is not treated as a supplied birth, employment, certificate,
asset or compensation date. This does not introduce a company minimum-age or backdating policy.

## Admin decision and account setup

Hiring decision body: `{ "expectedVersion": "<employee rowversion>", "decision": 1, "reason": null, "initialSiteId": null }`.
Hiring decision values are Approve 1, ReturnForCorrection 2, Reject 3. Confirm the enum in current OpenAPI when testing.
Return/reject require a nonblank reason and cannot include a site. An Office Staff approval cannot include a site.
Approval containing financial or medical proposals additionally requires that section's read/manage permissions.
Missing permissions never cause the section to be silently discarded. The response contains decision/account identifiers,
not private salary, medical, bank or credential values. Retrying the same key/body returns the same response.

After approval, use this sequence without repeating HR approval:

1. `GET /api/v1/hr/employees/{id}/access-setup` obtains the existing shared account ID and current account version.
2. `POST /api/v1/hr/employees/{id}/mobile-provisioning` enrolls the approved phone where mobile access was requested.
   This requires fixed Admin/SuperAdmin and existing mobile-management/delegation permissions. Repeated calls do not create another account.
3. Use `POST /api/v1/access/accounts/{accountId}/assignments` to assign a supported role and explicit scope.
   Set `configureAsInitialAccess` for initial setup; the existing service checks that the Admin may delegate every permission.
4. Where web access is approved, use `POST /api/v1/access/accounts/{accountId}/web-credentials` with email, Admin-set
   password and current account version. The password goes only to that credential endpoint, never into the hiring form or audit payload.
5. Staff use the existing mobile SMS OTP flow; web users use email/password followed by SMS OTP.
   A role/access/configuration/provider failure leaves setup incomplete and retryable, not falsely reported as a successful login.

Use `GET /api/v1/access/accounts/{accountId}` after each access mutation for the next account version.
First-owner setup and real SMS configuration remain separate deployment prerequisites, not default/demo accounts.

Site reassignment body: `{ "expectedVersion": "<employee rowversion>", "siteId": "<active site>",
"effectiveFrom": "YYYY-MM-DD", "reason": "<reviewed reason>" }`. Dates are business dates, not client timestamps.
Reassignment cannot be backdated, overlap an existing period, or overwrite a completed/scheduled period. Existing
assignments are date-grained: a replacement on the previous row's start date returns 409, as does changing an already
scheduled open assignment. Do not retry those failures by deleting old rows. A time-grained transfer workflow would
need separately agreed semantics for attendance evidence. Assignment records do not themselves grant API permissions.

Incomplete drafts are allowed. At submission, the SRS-required identity/contact/address/emergency/employment fields,
Office branch condition, verified identity document and verified profile photo must be complete. Optional background,
certificate and asset rows cannot be submitted as empty/incomplete entries. A profile photo is not face enrollment.
Father/spouse policy, controlled gender/marital choices, applicable identity expiry policy and age limits remain management decisions.

## Permissions and audit

Employee permission constants are in `EmployeePermissions`; descriptions appear in the shared access catalog.
This intake slice deliberately requires **All-scope** grants for each operation. Department/team/site-only and mobile
sessions are denied, not silently expanded. Scoped supervisor/self-service employee views remain later work.
Admin review requires the review permission on the **same current fixed Admin/SuperAdmin assignment**.
Sensitive finance, medical and document access need their own permissions in addition to the applicable profile permission.
Existing roles are not automatically upgraded. The owner-controlled fresh initialization command explicitly enumerates
the new grants; actual owner initialization is still deferred and no owner/default account was created.

Successful sensitive reads and writes persist actor, role, employee scope, timestamp, IP/device and correlation ID.
Document reads identify the document/version. Submitted profile, financial and medical reads identify the exact
immutable submission ID/version; audit metadata does not include the private section values.
Write evidence includes the request and protected before/after section
references in encrypted storage; no form bodies or private references are returned in ordinary metadata/list DTOs or logs.
Receipts contain identifiers/status only. Authorization, rowversion, database changes, audit and receipt commit together.
Encrypted file writes cannot participate in SQL rollback; reconcile orphan objects operationally before any deletion.

## Storage and scanning

Files are AES-256-GCM encrypted on the company's private server storage, not under the web root.
The development helper `src/Cosmix.Api/Initialize-DevelopmentPrivateFiles.ps1` provisions local private folders and a
non-overwritten encryption key outside Git, then sets paths using User Secrets. It does not enable a scanner or create users.
Production must provision the service identity, key backups and approved directory permissions independently.

Current upload allowlist: PDF, PNG and JPEG, up to 10 MiB; extension, declared MIME and file signature must agree.
Profile photos must be PNG/JPEG. Word/Excel, archives, HTML and executable files are not accepted by this slice.
Downloads are attachments with no-store, nosniff and sandbox headers. Unscanned/failed/rejected files are never ordinary downloads.
An authorized verifier can read **clean** files before verifying them; failed/unavailable scans cannot be manually bypassed.

Configure the real ClamAV adapter using `EmployeeFileScanner`; disabled/unknown/unavailable responses fail closed.
Read `src/Cosmix.Infrastructure/Files/README.md` for keys/rotation, local daemon settings and production scan acceptance.
No company files were sent to an external provider. Protocol tests do not prove real malware detection.

## Persistence and limits

`hr.Employees` remains the shared identity. One new `hr.EmployeeHiringDrafts` row holds nullable proposed master references
and separate protected form-section references, allowing incomplete HR work without inserting false effective assignments,
salary history, bank verification or asset custody. Submitted snapshots retain values as submitted, independent of draft edits.
The existing EmployeeSubmission, EmployeeDocument, AuditEvent and IdempotencyReceipt tables are reused.
Medical privacy classification is a new column on EmployeeDocuments, not a duplicate document subsystem.

Migration `20261007141249_AddEmployeeHiringDraftsAndDocumentPrivacy` follows the Organization migration.
Its forward SQL adds one table, one privacy column, restrictive foreign keys and checks; it drops no existing business data.
It refuses databases containing legacy employee documents until an approved privacy-classification migration is prepared.
Down is destructive to this new hiring data and is not a production rollback plan. Earlier migrations were not rewritten.

The subsequent `20261007151945_CompleteEmployeeHiringApproval` migration only makes the assignment office nullable
for Site Staff. It creates no business tables. Its Down refuses existing null-office rows instead of inventing office IDs.

## Post-hire status requests

The PM-approved flow is HR request -> a different fixed Admin/SuperAdmin decision. Supported changes are
Active -> Suspended/Resigned/Terminated, and Suspended -> Resigned/Terminated. Reactivation, retirement,
rehire, backdating and withdrawal of already approved changes are not implemented or silently inferred.
Ordinary staff HR endpoints cannot offboard an account with a current or scheduled fixed system role;
owner succession requires a separate reviewed process. Conversely, fixed Admin/SuperAdmin role grants
must reject current or future blocking employment history, including approved offboarding awaiting its date.

The following routes extend `/api/v1/hr/employees`:

| Method | Suffix | Purpose |
|---|---|---|
| GET | `/lifecycle-requests` | Paged request queue, optional state filter |
| GET | `/{id}/lifecycle` | Current effective status, employee version, next approved change |
| GET | `/{id}/lifecycle/history` | Paged immutable status history |
| GET/POST | `/{id}/lifecycle/requests` | Employee request list / submit a request |
| GET | `/{id}/lifecycle/requests/{requestId}` | Private request details, request version and decisions |
| POST | `/{id}/lifecycle/requests/{requestId}/decision` | Approve, return or reject |
| POST | `/{id}/lifecycle/requests/{requestId}/cancel` | Requester cancels a pending request |

All writes require `Idempotency-Key`. Request body: `expectedVersion`, `newStatus`, `effectiveAt`, `reason`.
`newStatus` is 3 (Suspended), 4 (Resigned) or 5 (Terminated). `effectiveAt: null` means immediately on
approval, not immediately on HR submission. An explicit ISO-8601 timestamp must still be in the future
when approved; offsets are normalized to UTC. If its time elapses during review, return the request and
submit a corrected new request. Never modify or delete the old request to retry it.

Decision body: `expectedRequestVersion`, `expectedEmployeeVersion`, `decision`, optional `reason`.
Decision values: 1 Approved, 2 Returned, 3 Rejected. Return/rejection require a reason. Cancel body:
`expectedRequestVersion`. Get current versions from request detail and employee lifecycle state.
Returned/rejected/cancelled requests retain their snapshot and decisions; a fresh request is allowed.
There can be only one pending request or one approved future change per employee. No self-approval or
approval of a status change concerning the approver's own employee identity is allowed.

Separate `hr.employees.lifecycle.read`, `.request` and `.review` permissions apply. All require current
web SMS-MFA and All scope; decisions require `.review` on the same current fixed Admin/SuperAdmin role
assignment. Merely naming a custom role Admin, or holding another unrelated fixed-role grant, is insufficient.
General profile read permission does not expose disciplinary reasons. Existing roles are not auto-upgraded.

Approval writes an immutable status change linked to the existing generic approval request/decision.
Immediate changes also update the employee/account and revoke sessions/open OTP challenges in the same
transaction. Future changes do not block login early. At their effective instant, existing authentication
checks reject non-active employment independently of the background worker. The worker polls every 30
seconds in bounded batches to reconcile cached status and revoke stored sessions; polling is not the
security boundary. History/read APIs derive effective status from history even before reconciliation.
The worker uses separate employee transactions and an employee-number cursor. Invalid records are reported
for review without repeatedly occupying the whole first batch and starving later employees. A restart begins
a fresh sweep; reprocessing an already applied change does not revoke sessions or increment versions again.
No attendance/punch deletion, payroll recalculation or automatic release of assets/loans is performed.
Attendance punch APIs remain a later module and must use the same current-employment checks.

Request snapshots and write audit evidence use private encrypted storage. List DTOs contain metadata
only; private request/history reads require lifecycle permission and are audited. Failed SQL transactions
may leave unreferenced encrypted files, as described above; there is no automatic deletion policy.

Migration `20261007164427_LinkEmployeeStatusApprovals` follows `CompleteEmployeeHiringApproval` and adds
one nullable approval reference with a restrictive FK and a filtered unique index. No new tables.
Its Down refuses to remove the column while recorded approval links exist; use forward corrections.

Post-hire salary-change requests/approval/history are now documented in [Employee Compensation](Employee-Compensation.md).
Other profile/employment amendments, reactivation/rehire, expiry notification jobs, roster/face/attendance,
actual SMS and real scanner acceptance remain separate work. This does not complete the full HR/Attendance SRS.
