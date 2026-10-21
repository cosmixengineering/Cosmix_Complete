# Employee self-service expansion

Owner requested this after the attendance implementation, initially with code
only and testing deferred. The owner later reauthorized builds, emulator
execution and testing on 20 October 2026. Current evidence and acceptance limits
are recorded in Verification-20261020.md. Unrelated services remain outside this
work.

The shared Employee Services hub source now includes leave and exact balances, advance
and loan requests/repayment details, resignation requests, personal profile and
assigned items, financial summaries, allowance eligibility/claims, vehicle/fuel
services, attendance correction/history and existing in-app notifications.
It also includes dated shifts, overtime requests, leave encashment and monthly
attendance facts. Shared forms prepare a visible review before mutation;
request lists include real empty/error states, range filters/pagination where
the existing contract supports them, and authorized cancellation/resubmission.
Access comes from the current server permissions; a menu tile never grants it.
No customer workflow or speculative role-specific panel is included.

All forms use existing self endpoints and server-selected employee identity.
Amounts, balances, shifts, eligibility and approvals are never fabricated.
Request submission is distinct from approval, payment or a changed employment
status. Pending/returned/rejected/cancelled states are shown separately.

Resignation lacked a mobile self endpoint. A narrow self-only backend intake
now feeds the existing protected EmployeeStatusChange snapshot and independent
HR review. Mobile can propose only Resigned and can cancel only its own pending
resignation. It cannot approve, deactivate employment or read someone else's
proposal. Existing HR administrator authorization is preserved. A new explicit
`ess.resignation.self` permission must be assigned through the normal access
workflow; code does not auto-grant it or migrate business data.

The existing vehicle API lacked self request history. A scoped GET
`/api/v1/me/vehicles/activity` now returns paged trips, fuel, maintenance and
incidents with employee filters applied before count/page. Private evidence and
calculation references stay out of these summaries. Fuel selection uses actual
approved own trips, with the server calculating mileage and claim amounts.

The subsequent history review closed two truncation gaps. New self-only GET
`/api/v1/me/advances-loans/requests` and `/agreements` page every request state
and active/repaid agreements instead of relying on the latest ten requests.
GET `/api/v1/self-service/finances/payroll-history` pages own payroll handoff
summaries instead of the latest six. It retains the same explicit financial
authority, transaction/audit and signed ledger values as the existing overview.
Generic self-read never grants payroll access. These summaries do not prove
payment. An additional attendance-history view uses the existing paged raw
punch endpoint with inclusive/exclusive date conversion and honest outcomes.

Leave history requires `attendance.leaves.self`; applying additionally reads the
actual dated shift using `attendance.rosters.self`. The app explains and gates
that dependency without widening backend authority. A separate read-only balance
panel in leave history uses only leave access, so choosing a policy and viewing
exact remaining days never requires permission to apply or read a roster.
Full-day requests use the
server's scheduled intervals; explicit rest days are listed as excluded and
unknown schedules stop submission. Encashment selects an explicit policy and
the fiscal-year boundaries returned by the balance API. No default pay
treatment, quota, salary or benefit value is invented.

Private attachments use the system document picker (PNG/JPEG/PDF) without broad
storage permission. Only user-selected evidence is read, bounded to the backend
limits, and sent to the normal scanned private-upload path. Temporary request
copies remain in the app's private cache for an exact retry and are removed on
resolution/sign-out; no face photo is part of this store. Session secrets remain
in the OS credential vault. Cache can be evicted by the OS, so missing request
content must never silently create a fresh mutation.

One unconfirmed service mutation is retained at a time. A retry reuses the same
route/body/key and signed-in account/session. Confirming the original form
closes its submit action so it cannot issue a duplicate under a new key. Body
cache retention is bounded to 24 hours; the protected reference remains after
expiration/OS eviction and requires HR reconciliation before a new request.
Temporary picker copies are removed on form completion/unmount and through
bounded cleanup of app-owned cache filenames at startup/sign-out. These are
private OS cache files, not an additional custom encryption scheme. Credentials
and retry metadata stay in Keychain/Keystore.

Source-only independent review identified and corrected the retry duplicate,
cancellation feedback, leave-treatment enum label and roster-read dependency.
Cancellation feedback labels the original leave and its separate cancellation
requests; an original Approved leave never means its cancellation was approved.
The document-picker dependency is installed and included in the successful
native build. Source checks, backend publishing, focused behavior checks,
HTTPS/authentication-boundary checks and read-only empty-scope SQL checks pass.
The updated APK was installed and the login screen exercised against the real
HTTPS preview. Actual employee data screens, native evidence selection, real
OTP sign-in and employee camera/GPS/print/replay acceptance remain pending;
the absent SMS provider is preserved as an explicit failed-closed dependency.
