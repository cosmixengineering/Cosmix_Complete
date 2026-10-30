# HR A-to-Z UAT audit — 30 October 2026

**Goal:** Verify the current HR web/backend workflows, fix reproducible defects, and produce an evidence-backed UAT readiness decision with explicit untested/blocked boundaries.

**Architecture:** Preserve the existing .NET Domain/Application/Infrastructure/API separation, React query/auth conventions, append-only attendance, independent approvals and server-authoritative policy configuration.

**Tech Stack:** .NET 10, SQL Server/LocalDB, React/TypeScript/Vite, React Native Android, IIS HTTPS.

**Spec:** The user's current A-to-Z request and accepted policies in this conversation. Existing CEO gap reports are inputs, not proof of completion.

## Global constraints

- Preserve existing work; no blanket resets, changes to real payroll, invented policies, approval impersonation or secret output.
- Use disposable, explicitly synthetic test databases for writes. Existing local testing configuration may be read and its guarded test accounts used.
- Coordinate backend builds and shared migrations. No migration without a demonstrated schema requirement.
- Task 11 stays deferred. Real parallel-month sign-off belongs to an authorized human. Mobile has employee workflows, not an Admin/SuperAdmin approval panel.
- Production activation requires the real IIS certificate/access, signing identity, SMS provider and backup destination. Missing values remain blockers, not dummy production values.

## Review focus

1. Role and record scope: self/supervisor/site/HR/Admin/SuperAdmin/Finance, denied and stale sessions.
2. Attendance: consent/device/radius/face ordering, retry and duplicate writes, overnight/missing checkout, month lock and correction.
3. Payroll: effective salaries, import, proration, independent approvals, returns, posting, payments and reconciliation integrity.
4. Web behavior: routes/export/HMR, backend errors vs fake success, stale query state, actionable configuration failures.
5. Deployment: intended database, HTTPS/health/worker responses, secret exclusions, backups and release fail-closed controls.

## Execution

- [ ] Inventory routes/controllers and runnable verification suites; map each workflow to positive/negative evidence and scope limitations.
- [ ] HR domain audit: employee lifecycle, roles/access, leave/requests, attendance/consent/corrections/month close. Reproduce any defect, add focused regression, implement minimal fix, retest.
- [ ] Payroll/Finance audit: salary/import/calculation/approval/payslips/contributions/accounting/payment/reconciliation/reports; identify actual non-payroll integration boundary without fabricating business rules.
- [ ] Web audit: verify source, imports/routes and guarded live HTTP login/permissions/refresh; inspect UI where supported and record any inability to exercise a screen.
- [ ] Run current Release backend tests plus legacy runnable verification coverage, targeted SQL scenarios, frontend verify and mobile/release checks where changed or unresolved.
- [ ] Inspect deployment scripts/configuration and probe the authorized local hosts. Run safe offline IIS/monitor checks and isolate operational blockers.
- [ ] Perform the requested security assessment through the installed security workflow, subject to its preflight; report its actual scope and outcome separately.
- [ ] Review final diff, regenerate affected deployment artifacts if runtime code changed, and publish a pass/fail/blocked coverage report with commands/results.

Execution is already authorized by the user. Investigation may run in independent domains; shared builds and changes remain coordinated by the parent agent.
