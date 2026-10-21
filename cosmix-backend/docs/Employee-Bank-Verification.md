# Employee bank verification and field policy

Bank data revisions remain immutable. Each verify/revoke transition appends an
`EmployeeBankVerification` for the exact employee and bank record; it never changes
the original account data or erases a verifier. A newly approved bank revision starts
unverified and does not inherit another bank revision's verification.

These routes require current Web MFA and an All-scope grant through the existing
employee operation. Read/status/history requires `hr.employees.read` and
`hr.employees.bank.read`. Verify/revoke and private evidence reads additionally
require `hr.employees.bank.verify`. Salary and medical grants do not grant these rights.
Hiring financial reads containing bank data now require bank-read permission;
bank-containing draft saves, removal and approval require bank-read and bank-request.
Current permissions are checked again on an idempotent retry.

| Method | Route under `/api/v1/hr/employees/{employeeId}/bank` | Result |
|---|---|---|
| GET | `/verification` | Current masked bank revision, status, exact actor/time and employee version |
| POST | `/{bankDetailsId}/verify` | Append a verified event |
| POST | `/{bankDetailsId}/revoke` | Append a revoked event |
| GET | `/{bankDetailsId}/verification-history` | Paged status/actor/time history without private proof |
| GET | `/{bankDetailsId}/verification-history/{verificationId}/evidence` | Separately authorized private reason and proof |

Both POST routes accept `Idempotency-Key` and the following exact JSON fields:
`expectedEmployeeVersion` (current Base64 SQL rowversion),
`expectedVerificationVersion` (0 before the first event), `reason` (1–1000
characters), and `proof` (1–8000 characters). Proof is a text attestation stored
encrypted with the reason, exact bank identity, status, actor, version and timestamp.
Clients cannot supply a storage path or public file URL. This slice does not upload
bank-proof attachments. The result contains `bankVerificationId` and the new
employee version. Use a new key for a new transition; repeat an uncertain request
with its original key and body.

Only the current effective bank revision can transition. Verification requires
active employment; revocation can preserve evidence for an already approved
employee after offboarding. Repeated same-state decisions, stale employee/event
versions, replaced/expired bank records and inconsistent history fail with 409.
Wrong employee/record IDs return 404. Missing permission or a non-Web/MFA session
returns 403. Current bank-update reads resolve the journal's current state while
historical account data and historical before/after snapshots remain unchanged.
Ordinary directory/profile DTOs and employee-own DTOs contain no bank details.

## Approved employee field policy

`EmployeeFieldPolicy` configuration has no invented values or numeric defaults.
An entirely empty section retains the previous completeness rules. Configuration
containing rules without `ApprovedVersion` and an effective date fails with 503.
Configured policy must be effective on the current business date; an expired or
future-only policy also fails with 503 rather than silently ignoring its rules.

The configurable fields are `ApprovedVersion`, `EffectiveFrom`, `EffectiveTo`
(exclusive), `GenderValues`, `MaritalStatusValues`, `SimStatusValues`,
`SimCreditTierValues`, `RequireGender`, `RequireMaritalStatus`,
`RequireFatherOrSpouseName`, `RequireProbationEnd`, `MinimumAgeAtJoining`,
`MaximumAgeAtJoining`, `MaximumProbationDays`, and
`RequireExpiryForIdentityTypes` (the existing identity document enum).
Controlled values are compared without case sensitivity. Supplied controlled
values must belong to configured lists. Missing required fields and approved age,
probation and type-specific expiry checks are reported by exact field paths at
submission and rechecked during approval. Incomplete drafts continue to save.
Approved identity/basic changes use the same configured controlled/required rules.

Personal/company SIM/login and emergency phones accept presentation punctuation
on input and use the existing international parser before storage. A leading `+`
is required; no unapproved country is inferred. Domain employee storage requires
canonical E.164. Login phone conflict checks include other employee selections and
live authentication phone bindings and run again at submission/approval.

The SRS supplies field labels but does not supply gender/marital/SIM controlled
lists, company age thresholds, father/spouse applicability, document-type expiry
requirements or a probation limit. These remain management decisions. OFFICE/SITE
category and branch rules are preserved until MD-012 resolves the broader taxonomy.
The configuration framework is not a completed policy publication/history UI.

The schema is prepared through the Code First model. No SQL database, user account
or external provider is changed by this implementation. SQL race/persistence and
authenticated deployment acceptance remain necessary before operational use.
