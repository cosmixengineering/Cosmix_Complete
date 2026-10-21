# HR Web Design - 8 October 2026

Status: HR-only scope and this written design approved by the PM on 8 October 2026.

## Outcome and Sources

Build the real React HR workspace against the existing .NET backend, not mock screens.
Reuse the completed global login and approved frontend folder architecture. The PM
explicitly selected HR screens only; Admin review UI comes later.

Sources: the Desktop Web Frontend Implementation and API Test Plan dated 8 October,
SRS v1.2 section 5, later PM decisions, and current backend controllers/contracts at
`70adaf7`. The backend's Organization-Masters, Employee-Hiring, Employee-Updates and
Employee-Compensation guides explain current API behavior; original SRS text does not
override later confirmed Office/Site, authentication or approval decisions.

## Screens and Delivery Order

1. Shared application frame and HR entry at `/hr/employees`: permission-aware navigation,
   account/logout, server-paginated employee directory, search, onboarding-state filter,
   loading/empty/error/denied states and New Employee action when authorized. No invented
   totals, graphs, alerts or department columns absent from the directory response.
2. Organization reference screens: departments, designations, cities, offices, staff
   categories, employment types and holidays. Show create/edit/status/history or calendar
   revision actions only with the corresponding current All-scoped grants. Sites are
   read-only here. A normal HR account may have read-only organization access.
3. Sectioned hiring form: identity, contacts, addresses, emergency contact, employment,
   proposed mobile access/SIM details, previous employment, certifications, asset evidence,
   separately authorized financial/medical sections, and standard/private medical documents.
   Field names, limits, enum values and nullability come from current backend DTOs. Preserve
   stable entry IDs in repeated form rows. Allow incomplete drafts and save each section
   sequentially using the latest employee version.
4. Submission review: backend readiness errors linked to fields/sections, selected document
   manifest, explicit submission, Pending Approval state and decision history. Returned
   hiring drafts may be corrected and resubmitted; submitted or rejected forms are not
   silently unlocked. Do not use the Admin submissions queue for HR status lists.
5. Approved employee detail: current sections, direct basic edits with reason/history,
   reviewed identity/employment/bank/medical/document requests, current versus scheduled
   values and protected history. Login phone/password/roles do not change through this UI.
6. HR requests: separate change, salary and status request lists/details, new requests,
   decisions already recorded by the backend, and cancellation only when permitted.
   Returned post-hire requests require a new corrected request; they are not mutable drafts.

## Confirmed Workflow Boundaries

- Office Staff: show and validate the office dropdown. Site Staff: HR chooses neither
  office nor site. Admin assigns/reassigns sites through its later UI.
- Employee number is server-assigned. Designation, access role and login account are distinct.
- Hiring submission is not activation, account provisioning or attendance enrollment.
- Profile photos are not face templates. No shift dropdown backed by invented lookup data.
- Admin decisions, role management, web credentials, mobile provisioning, site management
  and assignments are excluded from this HR-only delivery, even when signed in as Admin.
- Attendance, face enrollment, leave, claims, loans, notifications, reports and payroll screens
  wait for their backend workflows. React Native and backend schema changes are excluded.

## Architecture and Presentation

Keep HR pages/components/api/hooks/schemas/types/utils under `src/modules/hr`. Shared frame
belongs in `src/layouts`; reusable controls remain in `src/components`. Do not add a second
panels tree, second auth system, UI framework or global state framework.

Use a compact left navigation, restrained top bar, readable tables and sectioned forms.
Reuse the existing Cosmix logo, type and color tokens; use Lucide icons, clear labels and
accessible focus states. The first screen is the actual employee list, not a marketing
dashboard. Layout must remain usable on desktop and narrow browser widths.

Extend the existing HTTP transport narrowly for Idempotency-Key, multipart uploads and
protected binary downloads while preserving origin restrictions, cookies, CSRF and refresh
behavior. Validate actual response contracts; TanStack Query owns cancellable server reads.
Keep per-employee form state separate from server state and warn before leaving dirty forms.

## Security and Failure Behavior

- Server permissions/scopes remain authoritative. Sensitive tabs must not fetch before the
  relevant read grants exist. No client-side role-name bypass or automatic grant upgrade.
- No HR payloads, credentials, OTPs, documents or tokens in persistent browser storage or logs.
  Clear private queries/forms on logout, session change or loss of access.
- Preserve business date strings; do not shift them using browser UTC conversion.
- Keep the same key/body for an explicitly retried uncertain mutation; a new action gets a
  new key. Never silently overwrite after 409 or automatically replay unsafe requests.
- Files: existing PDF/PNG/JPEG 10 MiB limits; separate scan and verification status; private
  API reads and short-lived blob URLs. No public links, invented deletion or scanner bypass.
- Show safe validation, denied, missing, stale-version, size, throttling and unavailable
  states. Do not render raw server exceptions or claim success before the API confirms it.

## Verification and Acceptance Limits

Run `npm run verify`, browser checks on desktop/narrow viewports, keyboard/form navigation,
and actual authenticated API-backed save/read/reload flows on the approved `Cosmix_Testing`
database only. Use synthetic records; do not alter the original development database,
create real accounts, widen permissions or replace scanner/SMS configuration for testing.

Check unauthorized routes/sections, incomplete drafts, Office/Site conditions, duplicate
clicks/retries, stale versions, dirty navigation, session expiry/logout and document failures.
Use temporary verification files outside the repository; do not add test projects or artifact
directories. Report precisely which flows ran and which remain provider/fixture-blocked.
Existing minimal login fixtures are not evidence of complete hiring data or working scanning.

Source work stays local. Do not commit, push, seed additional accounts, apply migrations or
change backend behavior as part of this frontend task without separate authorization.
