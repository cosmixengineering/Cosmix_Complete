# Partial readiness completion plan

> **For agentic workers:** Use the assigned independent work areas; preserve other working changes. Verify each implementation before reporting it complete.

**Goal:** Close demonstrable software gaps in tasks 3, 6, 7, 8, 9 and 10 and distinguish executable readiness from unavailable company acceptance.

**Architecture:** Keep existing layered payroll/Finance contracts, dynamic company policy administration and native Android network security. IIS serves the frontend and API from one HTTPS site. No fabricated company inputs or disabled security gates.

**Tech stack:** .NET 10, SQL Server, React Native/Android, React/Vite, PowerShell, Node tests.

**Spec:** User's twelve-gap review and approved IIS origin https://192.168.1.20; docs/Product-Readiness-20261030.md.

## Constraints and review focus

- Real salaries, policy approvals, bank acceptance and human sign-off are not test fixtures.
- Mobile remains employee-facing; no Admin/SuperAdmin approval panel.
- TLS validation, hostname checks, pinning and independent approvals remain enforced.
- No access to the IIS Windows host, production signing identity or SMS provider is assumed.
- Test wrong CA/pins, stale generated resources, unauthorized readiness access, incomplete effective dates and attendance failure paths.

## Implementation

- [x] Finance/readiness: inspected payroll-only ledger boundary; corrected salary/roster/permission/whole-month posting-account coverage, with regression and SQL checks. Non-payroll Finance modules are still absent.
- [x] Android: explicit private IPv4/CA trust implemented; 8 tests, source verification, native policy/resource compilation and remote Android debug compile/lint passed. Actual company signing/TLS material remains unavailable.
- [x] Deployment/CI: guarded stopped-site installer and strict monitoring checks tested and packaged; mobile PR/remote checks passed. Actual IIS activation/off-site monitoring remains unavailable.
- [x] Attendance: actual service tests and consent fail-closed fix complete; owned emulator could not boot online and was stopped. Physical/SQL-backed employee acceptance remains unverified.
- [x] Integration: 684 backend regressions passed; r8 frozen frontend/backend package verified, offline SQL included, final API rebuilt/restarted and health checked. Detailed boundaries and evidence recorded in docs/Partial-Tasks-Verification-20261030.md.

## External execution dependencies

IIS certificate and Windows access/config, company release signing key, production SMS, actual policies/mappings and an available employee/device remain required for actual activation. Historical HBL comparison stays deferred. No live month/sign-off is inferred from synthetic verification.
