# Reports

Owner: Developer 2.

Source: SRS 15.

Implemented: factual employee/date preview and bounded private CSV/XLSX/PDF export request/status/download.
The durable outbox worker reauthorizes the requester and historical scope before generation;
download rechecks original and current access. Report bytes and source manifests use private storage.

Exports require explicit worker enablement. They are factual time outputs, not approved payroll
handoff. Real bounded CSV/XLSX/PDF include generation/filter/policy-status/lock metadata.
Late/KPI/OT/pay reports, large dashboards and lock/reopen decisions remain pending.

See `docs/attendance-completion/notifications/Reports.md` for client contracts and
`docs/attendance-completion/Verification.md` for verification and deployment limits.
