# Verification

## Attendance Scheduling Package 13 October 2026

Actual shift, site-roster, circular site-boundary and Office-roster screens are
implemented in `src/modules/attendance`, with real DTOs and the existing secure
transport. Scope/schema checks passed 9+20+4 assertions; worker helpers passed 27;
isolated browser checks passed 28 plus 21 Office checks and 13 route/schema checks.
These browser harnesses used controlled synthetic transport fixtures and blocked
external network. They are not live API persistence, MFA/CSRF or SQL acceptance.
No accounts, runtime providers, permissions or React Native were changed.

`npm run verify` passed lint, formatting, TypeScript and build. The combined main
chunk remains 818.42 kB and triggers Vite's non-failing 500 kB warning. Current local
server `https://localhost:5173` returned 200 for the static root and new Office source.
Office runtime additionally needs the reviewed backend migration/current binary;
this package did not apply a migration or restart the running API. Detailed source
and remaining-work record: backend `docs/HR-Attendance-Setup-Completion-2026-10-13.md`.
Existing dated HR acceptance records below are preserved, not rerun by this package.

## HR Web increment - 8 October 2026

The current implementation and evidence are recorded in
[HR Web implementation report](HR-Web-Implementation-Report-2026-10-08.md).
The architecture-only notes below are historical, not a description of today's app.

Passed against actual `Cosmix_Testing` APIs: existing password/SMS-test-OTP login,
directory, organization reads and synthetic city/calendar writes, hiring section saves,
Office/Site clearing, direct basic changes, bank proposals, salary/status requests and
own cancellation. Pending requests did not activate salary or status changes.

Passed failure/security checks: limited-HR sensitive-read gates, approved hiring deep-link
redirect, invalid/oversized uploads, scan-failed download blocking, stale rowversion rejection,
same-key/body explicit retry after a dropped request, dirty navigation/reconnect protection,
real 404/network retry, access-cookie refresh, cancelled-refresh isolation, delayed-success
suppression after logout, two-tab logout/cache clearing and no HR payloads in browser storage.

Responsive checks ran at 1440, 390 and 320px. Existing logo loading, rendered screenshots,
mobile keyboard navigation, Escape/focus return and no body overflow were checked.
Wide tables remain horizontally scrollable within their own container on narrow screens.

`npm run verify` passed: ESLint, Prettier, TypeScript and production build. Vite reports
the existing combined application chunk above 500 kB; this is a non-failing performance
warning, not a suppressed diagnostic. No permanent automated test files were added.

Blocked/unverified: scanner-clean/verified document lifecycle and successful complete hiring
submission; identity/document changes needing approved evidence; transfer success without a
current assignment; real SMS delivery; live returned/rejected decision fixtures and completed
Admin approval/scheduled application UAT. No providers, accounts or permissions were changed
to manufacture successful acceptance. Test scripts/screenshots remain outside Git in TEMP.

## Subsequent Admin approval extension - 2026-10-08

The PM authorized Admin hiring and post-hire decision UI after the original HR-only
delivery. Fresh actual tests passed for salary/status Return, Reject and future-dated
Approve; correction-dialog dismissal, reason/self-decision restrictions, real stale409
recovery, one-decision idempotent replay after a lost committed response, immediate
current/scheduled refresh and HR approval-route/API denial. Desktop/320px screenshots
were inspected and Escape/page-error checks passed. npm run verify passed.

A fresh scanner rescan still returned Failed/unavailable. Complete hiring, private
clean-document success and assignment-backed transfers remain blocked. The combined
workflow script stopped at a bank-proposal409 after the synthetic HR employee received
a future lifecycle record; this is not a full passing suite. Use a fresh approved fixture
for remaining change-section acceptance, without rewriting scheduled/audit history.
See HR-Approval-Completion-Report-2026-10-08.md for exact evidence, fixture dates and limits.

## Historical architecture-only verification

Verified locally on 2026-10-05 after the PM clarified that no mock UI was requested.

## Passed

- `npm run verify`: ESLint, formatting, strict TypeScript compilation and production build.
- Source inspection: preview configuration, module navigation registry, placeholder module pages, sample Departments integration and designed application shell were removed.
- Module folders and their public export boundaries remain. Empty feature folders retain `.gitkeep`.
- `AppRoutes` returns `null`. `AppLayout` is an unstyled `Outlet` reserved for future approved layout work.
- The previous development server on port 5173 was stopped. No new demo server or screenshots were created.
- The production build was regenerated from the architecture-only source, replacing the old demo build.

## Cleanup completed

The local `artifacts` directory and its old screenshots, logs and browser helper were deleted on 2026-10-05 following the PM's renewed request. The initial forced deletion was blocked; standard deletion without force succeeded. A filesystem check confirmed that `artifacts` no longer exists while `package.json` and `src` remain. No application source was changed during this cleanup.

## Scope

No business UI, live backend integration, real authentication, authorization behavior or production workflows were verified. The reusable auth/API helpers remain scaffolding. Earlier browser reports for the removed UI no longer describe the current app. No test suite was added.
