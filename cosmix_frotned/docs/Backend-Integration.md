# Backend integration

## Web authentication contract

The .NET backend already implements shared web authentication. The React app uses
its Secure, HttpOnly, SameSite=Strict cookies. It does not request, decode or store
access/refresh tokens, and it does not use the mobile bearer login flow.

All paths below are relative to `/api/v1`:

| Operation                 | Method and route                 | Body                           |
| ------------------------- | -------------------------------- | ------------------------------ |
| Browser and CSRF setup    | GET `/auth/web/csrf`             | None                           |
| Request login             | POST `/auth/web/login`           | email, password                |
| Verify SMS                | POST `/auth/web/otp/verify`      | challengeId, code              |
| Resend login SMS          | POST `/auth/web/otp/resend`      | challengeId, email, password   |
| Restore identity          | GET `/me`, GET `/me/permissions` | None                           |
| Refresh session           | POST `/auth/web/refresh`         | None; browser cookies          |
| Sign out                  | POST `/auth/web/logout`          | None                           |
| Request password recovery | POST `/auth/web/password/forgot` | email                          |
| Resend recovery SMS       | POST `/auth/web/password/resend` | challengeId, email             |
| Reset password            | POST `/auth/web/password/reset`  | challengeId, code, newPassword |

`202` returns a challenge, not proof of account existence, SMS delivery or login.
Only successful verification plus server session reads produce account access.
Resend asks for the password again; the previous password is cleared.
Recovery password policy remains server-controlled; the UI does not invent a
configurable minimum. An initially unactivated account may need the existing
audited Admin initial-password reset instead of ordinary SMS recovery.

The API's enums currently serialize numerically: Web client = 1. Full permission
grants retain role-assignment ID, scope kind and target ID. `PermissionGate` is only
a presentation helper, never authorization. `/me` forbidden with a successful
permissions read displays restricted identity, not a fabricated user.

## Local HTTPS

`npm run dev` runs `scripts/start-dev.ps1` on Windows. It requires an existing,
trusted ASP.NET localhost development certificate. The temporary password-protected
PFX is outside Git in a current-user-only temp folder; its password is passed only
to the development-server process. Normal exit removes the export. A forced process
kill may leave the protected export until OS/user temp cleanup.

The browser calls `https://localhost:7240/api/v1` by default in development. The API
already permits `https://localhost:5173` with credentials. If Vite selects another
port, explicitly configure that exact allowed origin outside source control before
using login. The frontend does not change backend settings, apply migrations or
start the API. Run it separately from the backend solution.

`.env.example` contains only public endpoint configuration. Never put credentials,
signing keys, database connections or provider secrets in `VITE_` variables.
Both browser and API must use trusted HTTPS and the same hostname spelling.
Do not disable TLS checks, weaken cookies or use wildcard credentialed CORS.

On non-Windows systems, run Vite with `COSMIX_DEV_HTTPS_PFX` and
`COSMIX_DEV_HTTPS_PASSWORD` supplied privately for an approved local certificate.
The checked-in launcher targets this project's Windows development machines.

## Request and session behavior

- `request()` confines URLs to the configured API, sends credentials, rejects
  redirects, disables response caching and uses a 30-second timeout.
- Unsafe requests obtain fresh CSRF first. Only simultaneous CSRF reads are shared;
  Web Locks coordinate browser-cookie initialization across same-origin tabs.
- Protected GET requests can refresh and retry once. Web Locks serialize rotation
  across tabs; a permissions probe detects rotation completed by another tab.
- Unsafe requests are never automatically replayed. A failed or uncertain refresh
  requires fresh login. Browsers without Web Locks also require fresh login when
  the access cookie expires.
- Local storage contains at most the non-sensitive
  `cosmix.refresh-requires-sign-in=1` stop flag, not credentials or session data.
  It prevents reloads/other tabs replaying an uncertain refresh. It cannot grant
  access; only fresh successful OTP verification clears it. If storage is blocked,
  automatic refresh fails closed.
- Logout is confirmed by the server before success is shown. BroadcastChannel
  informs other same-origin tabs. Session changes clear QueryClient data and
  invalidate pending session reads. Every protected API remains server-enforced.
- Error handling maps status/code safely; raw exceptions, provider messages,
  passwords and OTPs are never rendered or logged by this frontend.

The same transport now supports explicit `Idempotency-Key`, `FormData` uploads
(browser-generated multipart boundaries) and authenticated binary downloads.
HR writes never auto-retry. An explicit retry of an uncertain action retains the
same body/key; editing that uncertain payload is blocked until the result is checked.
Rowversions remain opaque server values. Files have no public URLs; short-lived
blob URLs are revoked, and downloads are cancelled on session/unmount changes.

## HR contracts

The implementation targets backend `70adaf7`. All HR routes use `/api/v1/hr`.
See the [implementation report](HR-Web-Implementation-Report-2026-10-08.md) for the
complete integrated endpoint inventory and the [module guide](../src/modules/hr/README.md)
for route/ownership boundaries.

- Organization masters and holidays use their distinct DTOs and history/revision endpoints.
- Hiring profile, financial, medical and document sections use separate grants and sequential employee versions.
- Post-hire basic details update directly with an audit reason. Identity, employment, bank, medical and document changes are approval requests.
- Salary and employment status use separate request contracts. Pending requests do not change current values.
- Sites are read-only in HR; there are no Admin decisions, role-management or account-provisioning calls in this workspace.
- Scanner failure remains unavailable evidence. The frontend never marks documents clean or bypasses submission requirements.

Do not add business permissions, policies, tables or fake API routes in React.

## Admin approval extension - 8 October 2026

The PM subsequently authorized Admin review for HR hiring and post-hire requests.
The existing cookie, CSRF and idempotency transport is reused.

- Hiring queue: GET /api/v1/hr/employees/submissions with page/pageSize/search/status.
- Frozen hiring review: GET /api/v1/hr/employees/{id}/submissions/{submissionId} and
  separately protected /financial and /medical sections.
- Hiring decision: POST /api/v1/hr/employees/{id}/submissions/{submissionId}/decision
  with expectedVersion, decision, reason and optional initialSiteId. Decisions are
  1 Approve, 2 Return for correction, 3 Reject. expectedVersion is the current employee
  rowversion, not the frozen submission's numeric revision.
- Current access state: GET /api/v1/hr/employees/{id}/access-setup under its own grants.
  The frontend displays this state without creating assignments or credentials.
- Change/salary/status request decisions append /decision to their existing detail
  endpoints. Bodies contain expectedEmployeeVersion, expectedRequestVersion, decision
  and reason. Reasons are required for Return and Reject.
- Permission grants control visible actions; the API additionally enforces fixed
  system-role membership and prevents requester/employee self review. /me does not
  expose role codes, so the frontend does not fabricate a role check.
- Private document review requires documents.verify; ordinary download requires the
  matching read permission. Standard frozen versions must match current metadata.
  Medical snapshot responses omit frozen document metadata, so medical file actions
  show protected current metadata and approval still validates immutable evidence.

See HR-Approval-Completion-Report-2026-10-08.md for actual runtime evidence and remaining
scanner/full-fixture limits. Do not infer successful hiring from an empty approval queue.
