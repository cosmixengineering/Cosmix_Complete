# Batch document completeness and expiry

`POST /api/v1/hr/employees/documents/completeness/batch` is a read-only JSON
report extension of the existing single-employee completeness workflow. Cookie
requests retain the existing CSRF requirement. No Idempotency-Key is required.

```json
{
  "employeeIds": ["00000000-0000-0000-0000-000000000001"],
  "expiringWithinDays": 14
}
```

Both properties are required; unknown properties are rejected. IDs must be
1-25 unique, nonempty GUIDs. The explicit expiry window must be a JSON integer
from 1-365; quoted numbers are rejected.
The service snapshots and sorts IDs before asynchronous work. It does not
discover employees or silently drop missing IDs.

The existing EmployeeOperation authorizer requires a current authorized web
MFA account and All-scope `hr.employees.read` and `hr.employees.documents.read`
grants. That gate runs before loading any selected employee. Every ID resolves
before private completeness evaluation starts. A separate final gate repeats
the current account/session and grant checks after private I/O and rejects an
actor/session change. These are the existing HR authority contracts, not new
supervisor, self-service or notification recipient authority.

## Response

`EmployeeDocumentCompletenessBatchResponse` contains:

- `metadata.generatedAtUtc`: server generation time after final authorization.
- `metadata.timeZone`, `asOf`, `reminderThrough`: the captured configured
  business timezone, one business date shared by every row, and inclusive end
  of the requested expiry window.
- `metadata.filters`: the sorted `employeeIds` and explicit `expiringWithinDays`.
- `metadata.coverage`: `explicit_selected_employees_only`.
- `metadata.documentCoverage`:
  `selected_standard_documents_and_current_or_draft_certifications`.
- `metadata.dataKind`: `factual_document_completeness`.
- `metadata.reminderStatus`: `not_scheduled_or_delivered`.
- `rows`: exactly one existing `EmployeeDocumentCompletenessResponse` per
  selected ID, in the same sorted order as the filters.

Rows retain `employeeId`, `asOf`, `reminderThrough`, `requiredDocuments`,
`unavailableSelectedDocumentIds`, `expiringDocuments`, `certifications` and
`activationBlockers`. Approved employees use the approved document selection
and current certification records; drafts use their private profile manifest.
The existing field-policy reader adds applicable published document requirements.
Selected evidence that is missing/unavailable remains visible in the existing
completeness fields; it is not confused with unavailable source storage.

## Failure and scope

Invalid selections/window return 400. Denied authority returns 403; a missing
selected employee returns 404. Private manifest/provenance conflicts retain
their existing 409 codes; unavailable policy/storage retains its existing
failure. Invalid timezone or date-window overflow returns 503. Cancellation
propagates. No failure returns a partial report or manufactures an empty row.
Preparation and final successful reads use the existing audited read operation;
an unsuccessful final gate never records a successful batch-read event.

This report does not load document bytes, issue private storage writes, schedule
or deliver reminders, include insurance/medical records, generate export files,
calculate finance or establish complete SRS reporting. Single-employee GET
remains available with its existing contract. No schema, DI or migration change
is required.

## Executed checks

On 13 October 2026, 107 isolated synthetic assertions passed against the actual
Application services, EmployeeOperation authorizer, completeness evaluator,
field-policy reader and controller metadata. Current Domain/Application source
and the owned controller were compiled in memory; the saved Domain DLL lacked
a newer attendance method, so no saved build output was replaced or copied.
The baseline reflection check first failed because the batch entry point did
not exist.

Checked cases include null/empty/duplicate/zero/26-ID selections; invalid windows
and valid 1/365 boundaries; missing JSON fields, unknown fields and quoted
numbers; initial MFA/client/current-account/permission/All-scope denial; every
ID resolving before private I/O; account/session/MFA/grant revocation after
private reads; a later employee's storage failure; unavailable published policy;
invalid timezone/date overflow; cancellation; caller-array mutation; a clock
crossing midnight; exact 25-row coverage; single-GET parity; inclusive expiry;
medical/unselected-document exclusion; applicable published requirements;
approved current certifications versus draft evidence; safe serialized output;
non-nested authorization transactions; and the exact POST route/body limit.
No failure recorded a final successful batch-read audit.

The temporary C# checks and PowerShell runner are outside the repository at
`D:/CodexTemp/cosmix-batch-completeness-20261013`. The runner creates no test
project, DLL output or cloned dependency files. Its repository, authorization,
policy and private-file boundaries are synthetic. No real SQL, provider,
account/configuration operation or HTTP host was invoked. These checks do not
establish SQL/HTTP acceptance or a full-project build; those remain with the
parent integration and independent route review.
