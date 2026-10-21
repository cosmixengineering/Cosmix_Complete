# Global web login: 8 October 2026

## Approved scope and baseline

Implementation follows the Desktop document
`Cosmix_Web_Frontend_Implementation_and_API_Test_Plan_2026-10-08.docx`, steps 1-2,
limited to global login/session access. Work stays in the requested combined
checkout and the existing frontend feature branch. No new worktree, commit or push.

Frontend starting revision: `426f2b9cb75ed028b4ed570aaae1017cde2f906d`.
Backend: `b74f23a32743faa18d7df11849106bf5f53d3ec1` plus the existing local
Employee Updates changes. Those backend changes were preserved, not rewritten.

## Implemented

- One React sign-in route for every authorized web role: email/password then SMS.
- OTP expiry and server-provided resend cooldown, six-digit validation, re-entered
  password for resend, duplicate-submit prevention and safe failure states.
- Forgotten-password request, SMS verification and new-password submission.
- Existing API cookie/CSRF transport, coordinated refresh, protected account route,
  real current-account view, permission/scope retention and logout.
- Cross-tab sign-in/logout notification, query-cache clearing, fresh account reads
  after identity changes, and no reuse of a consumed OTP after a later read failure.
- Trusted local HTTPS launcher; existing company logo, responsive layouts, labeled
  form controls and password visibility toggle. No placeholder business dashboards.

## Verification record

- `npm run verify`: lint, formatting, TypeScript and production build passed.
- Current backend Release build: passed, zero warnings/errors. No backend source
  changes or migrations were required for this frontend increment.
- Ten isolated in-memory checks exercised actual frontend modules: cookie/CSRF
  request contract, relative-path containment, malformed response rejection,
  single concurrent refresh, no unsafe-write replay, uncertain-refresh stop flag,
  scope retention with restricted profile, mobile-client rejection, logout failure,
  and safe ProblemDetails parsing. Their network responses were fixtures; these
  are not live successful-authentication tests. No test files/project were added.
- Real API: CSRF endpoint returned 200; HTTPS origin was explicitly allowed with
  credentials; missing-CSRF write returned 400; correctly CSRF-bound login returned
  503 with authentication not configured. No real account was created or modified.
- Real browser: sign-in and recovery navigation, service-unavailable feedback and
  password clearing after a failed request were checked. Required email validation
  and unauthenticated `/account` redirect were checked. Layouts were inspected at
  1440x900, 390x844 and 320x740; the 320px scrollbar overflow was fixed and rechecked
  (document scroll width equals client width). The existing logo rendered correctly.
  The final browser console check showed no application error/warning entries.

## Pending acceptance, not frontend bypasses

The original local API user-secrets remain unchanged. On 2026-10-08 the PM approved
a separate `Cosmix_Testing` database and a development-only in-memory SMS adapter.
Use the backend `scripts/Start-LocalTesting.ps1` launcher and its `docs/Local-Testing.md`
runbook. Three synthetic accounts and distinct passwords are held privately outside Git.

The development OTP screen shows **Show test OTP** only when the backend explicitly
reports local testing mode. The server requires localhost HTTPS, the correct test
database, a synthetic account, the issuing browser, CSRF and an active challenge.
This does not bypass password/OTP verification or expose OTPs through normal login
responses. The helper is absent from production JavaScript.

All three dummy-account HTTP logins, wrong/reused OTP rejection, browser isolation,
CSRF, role restrictions, refresh and logout passed against the actual testing API.
React HR login reached `/account`. Resend invalidation and attempt exhaustion passed.
These checks are not evidence of handset delivery or production readiness.

PM browser regression: the OTP input and local helper originally shared a React
key, allowing duplicate inputs during timer updates. Keys now have distinct
`otp-input:` and `local-otp:` prefixes. The corrected Admin challenge was checked
before and after revealing its real local code: exactly one OTP input remained.
`frontend-check@example.invalid` is an old unseeded test value, not one of the
three fixture accounts; generic login responses correctly do not issue it an OTP.

Actual Jazz integration/SMS receipt, the approved real owner, full expiry/password
recovery acceptance and production role/scope UAT remain pending. No real SMS is sent
in local testing. Sign out before switching between testing and normal API startup.

## Next approved work boundary

First finish real-account/SMS UAT when configuration is available. Then implement
permission-driven application navigation and Organization Masters screens using
the existing API. HR hiring and approvals follow the agreed frontend plan.
Mobile app, phone/device recovery administration, session-management screens,
password change for signed-in users and broader business modules are not delivered
by this login increment. The separate local testing setup is the only additional
backend/database work authorized for this testing increment.

Final review: author self-review; no independent reviewer tool was used. Automated
regression files remain absent under the repository instruction; obtain approval
for maintained regression coverage before production delivery.
