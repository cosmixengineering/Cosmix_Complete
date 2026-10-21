# HR Durable Exports: Frontend Handoff

Date: 19 October 2026.

Frontend integration is implemented and build-ready against the confirmed rich-HR
export contract. The parent reports that product-backend P1/P2 review fixes are
underway and the contract is unchanged. This document records frontend work and
evidence only; it does not certify those backend fixes or runtime readiness.

**No authenticated browser acceptance was performed for this increment.**

## Exact Source Changes

All paths below are relative to `cosmix_frotned`.

| File                                                          | Change                                                                                                                                                                                                                                                                                                                                                                                                               |
| ------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `src/modules/reports/pages/HrReportsPage.tsx`                 | Keeps the paginated preview and current-page CSV/XLSX export; integrates full-filtered jobs; uses server-normalized preview filters; admits scoped report grants; resets private workspace state on session changes and observed report-cache clearing. Captures cancellation signals, prevents duplicate page exports, stabilizes report-type loading, and validates the inclusive 366-day range and export reason. |
| `src/modules/reports/api/reports-api.ts`                      | Retains the legacy `exportReport` name as an alias of the validated `exportReportPage` helper. The old binary request to the durable `/export` endpoint was removed after checking consumers with `rg`.                                                                                                                                                                                                              |
| `src/modules/reports/api/report-export-jobs.ts` (new)         | Owns validated preview metadata, immutable export intents, durable request/status/download transport, current-page binary transport, attachment checks, safe error messages, and temporary download URL cleanup.                                                                                                                                                                                                     |
| `src/modules/reports/components/HrReportExportJobs.tsx` (new) | Owns full-export format/reason entry, explicit request retries, retained accepted jobs, status polling/manual refresh, expiry display, and authenticated download actions.                                                                                                                                                                                                                                           |

The handoff adds `docs/HR-Durable-Exports-2026-10-19.md`. This documentation update
does not modify product code. The frontend increment made no shared authentication,
navigation, CSS, backend, SQL, Git, host-configuration, or automated-test-file
changes. It uses existing controls, styles, session transport, and TanStack Query.

## Confirmed Contract

Routes are relative to `/api/v1`. The backend-owned reference is
`cosmix-backend/docs/hr-srs-remediation/HR-Durable-Export-Contract-20261019.md`
in the sibling backend repository. The routes were confirmed directly with the
backend worker before client integration.

| Operation                    | Route                                       | Response and requirements                                                                                                                       |
| ---------------------------- | ------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| Preview                      | `GET /hr/reports/{reportType}` with filters | Paginated JSON. The client validates the report type, server-normalized `metadata.filter`, `returnedCount`, `totalCount`, and actual row count. |
| Current-page export          | `POST /hr/reports/{reportType}/export-page` | HTTP 200 CSV/XLSX bytes. Body `{ format, reason, filter }`; retains page/pageSize; no idempotency key.                                          |
| Full-filtered export request | `POST /hr/reports/{reportType}/export`      | HTTP 202 job JSON. Same body shape plus `Idempotency-Key`; formats `1=CSV`, `2=XLSX`, `3=PDF`. Pagination does not limit the export.            |
| Job status                   | `GET /hr/reports/exports/{id}`              | Owner-authorized job JSON under current authority.                                                                                              |
| Job download                 | `GET /hr/reports/exports/{id}/download`     | Authenticated bytes for a completed, unexpired job. No public file URL.                                                                         |

The job DTO contains `id`, `reportType`, `state`, `format`, `requestedAt`,
`generatedAt`, `downloadExpiresAt`, `failureCode`, `totalCount`, and `returnedCount`.
Generation/expiry timestamps, failure code, and counts are nullable. States are
`pending`, `running`, `completed`, and `failed`. Completed responses must contain
generation and later expiry timestamps, equal non-null total/returned counts, and
no failure code before the client accepts completion. Request responses must match
the submitted report type and format; status responses must also match the job ID.

The confirmed backend contract supplies a one-hour download expiry and explicit
failures for unavailable or incomplete output. Relevant errors include HTTP 409
`hr_report_not_ready`, HTTP 410 `hr_report_download_expired`, and HTTP 503
`hr_report_export_worker_not_enabled`. Backend source/row/output limits remain
server-owned; the client does not turn a limit failure into a partial success.

## Frontend Behavior

- Both export actions use the last successful preview's server-normalized filters,
  so date defaults remain explicit. Unsaved filter edits do not silently change an
  export. The current-page action retains pagination; the full-export intent sets
  page/pageSize to `1/1000`, which the durable endpoint ignores for dataset size.
- Reasons require 1-1000 trimmed characters and no control characters. Durable
  request payloads, filter snapshots, and UUID idempotency keys are held in memory.
  Duplicate submissions are blocked while a request is active.
- Network, response-validation, HTTP 408, and HTTP 5xx failures retain the exact
  intent for an explicit unchanged retry. Format/reason editing stays disabled
  while that outcome is uncertain. Other HTTP 4xx responses release the form for
  correction. Durable POST requests are not automatically replayed by this UI.
- Accepted jobs remain available in the mounted workspace when another format is
  requested or the preview changes. A fresh request receives a fresh key; a replay
  of an existing job ID does not add a duplicate job panel.
- Status reads use session-scoped query keys, zero inactive cache retention, and
  no query-level automatic retries or focus/reconnect refetch. Pending/running
  jobs poll every three seconds while foregrounded. Completed/failed jobs and
  status-read errors stop polling. Manual refresh is available. On a read error,
  stale job details/download controls are hidden; no timer fabricates completion.
- The page presents actions for valid scoped `hr.reports.read` and
  `hr.reports.export` grants, including self, targeted, and all-scope grants.
  Backend session/MFA, source-field permissions, historical membership, ownership,
  and download authorization remain authoritative.
- Session changes remount private workspace state. Removing an observed report
  query from the cache also resets that workspace. Outstanding actions and
  downloads abort on unmount; obsolete continuations cannot update the new
  workspace. No report data, reasons, retry keys, or job history are persisted in
  browser storage.
- Downloads require completed, unexpired jobs and use the shared authenticated
  transport. The client checks nonempty bytes, expected MIME type, the XLSX ZIP or
  PDF signature, and an 8,000,000-byte durable-output limit. Current-page files
  receive type/signature checks without this additional durable-size cap. Blob
  URLs are revoked on cancellation or after one second. "Download started" follows
  the validated browser download action; it does not claim the OS finished saving.

## Verification Evidence

| Check                                       | Result and scope                                                                                                                                                                   |
| ------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Sidecar `npm run build`                     | PASS after the final source change: TypeScript and Vite, 2443 modules transformed. Vite reported its large-chunk advisory.                                                         |
| Sidecar lint                                | PASS during the final `npm run verify` attempt.                                                                                                                                    |
| Formatting of the four changed source files | PASS with targeted Prettier checks.                                                                                                                                                |
| Sidecar full `npm run verify`               | Stopped at formatting in five unrelated files; the build was run separately and passed. This is not recorded as a full verify pass.                                                |
| Parent combined checks                      | Parent subsequently reported `npm run build` PASS, 2443 modules, and lint PASS. These are parent-reported combined results, not a claim that this documentation update reran them. |
| Controlled API and UI checks                | PASS using in-memory TypeScript/Node harnesses with controlled transport and hook behavior. No network calls or automated test files were used.                                    |

The unrelated formatting failures observed by the sidecar were
`src/layouts/AdminLayout.tsx`,
`src/modules/hr/components/hiring/SubmissionReview.tsx`,
`src/modules/hr/pages/HiringApprovalsPage.tsx`,
`src/styles/admin-dashboard.css`, and `src/styles/onboarding-stepper.css`.
This increment did not edit them or establish a later repository-wide formatting
result.

Controlled checks covered frozen same-key/payload retries after selection changes;
duplicate POST/download suppression; definitive rejection unlocking; legacy
`exportReport` delegation to `/export-page`; normalized filters and preview counts;
job identity/format binding; rejection of partial or malformed completion;
CSV/XLSX/PDF attachment handling; invalid/oversized bytes; pending/expired guards;
blob URL revocation; abort and late-response suppression; queued/completed/failed/
expired UI states; polling pause after errors; manual status refresh; multiple
accepted jobs; all seven grant kinds and invalid self-target denial; read-only
export gating; and session/cache-clear workspace reset.

## Limits and Remaining Acceptance

No authenticated browser acceptance, live API export request, worker execution,
restart/recovery exercise, or real generated-file download was performed by this
frontend sidecar. The controlled checks do not establish real browser rendering,
responsive layout, live cookies/CSRF/MFA, dispatcher durability, source-scope
reauthorization, or document contents. Backend P1/P2 review fixes and their
verification remain with the parent/backend owner; their completion is not claimed
here. The parent reports the endpoint contract remains unchanged.

Job references and uncertain retry intents exist only in the mounted workspace.
They are cleared on navigation/unmount, reload, or session reset. No job-history
listing or recovery UI was added because the confirmed contract exposes no listing
endpoint. Backend durability does not imply that this page restores earlier job
references after reload.

Preview filters are preserved, but a full export is generated from backend source
records according to the backend job contract; it is not a saved copy of preview
rows. Attachment validation checks transport type, size, and signatures, not full
XLSX/PDF structural or semantic correctness. Failed status checks require explicit
refresh, and failed/expired exports require an explicit new request as appropriate.

After backend review fixes are verified, remaining integrated acceptance is an
authorized browser run covering scoped preview and both export paths, all three
durable formats, unchanged-request retry, actual completion/download, expiry and
permission revocation, and session clearing during outstanding work. No production
readiness or payroll certification is asserted by this handoff.
