# Admin Hiring and Employee Setup

Date: 15 October 2026.

## Root Cause and Changes

The previous Admin approvals screen used hardcoded requests and React-only status
changes. Its success messages did not represent persisted decisions. It exposed no
candidate-detail or employee-setup action.

- Admin hiring now mounts the existing API-backed HR review queue.
- Review details opens a responsive dialog with the frozen profile, organization
  references, protected document/financial/medical views, and decision history.
- Site selection uses actual sites. Custom-category placement uses the existing
  effective staff-taxonomy contract. Server validation remains authoritative.
- A successful decision closes the review and refreshes the server-backed queue;
  approved employees remain available through the Approved filter for setup.
- Approved hires link to current employee details, site assignment, and existing
  initial/additional role and web/mobile access setup.
- Department/designation are employment context, not automatic permission grants.
- Existing CSRF, role delegation, MFA checks, self-review restrictions, rowversions,
  idempotency and private evidence protections remain intact.
- Removed the fixed 17 badge from Admin navigation. Other dashboard demo metrics
  and unrelated module placeholders are outside this change.
- Unsupported approval categories cannot fabricate approvals. Leave and employee
  changes link to their existing workspaces.

## Verification

- Reproduced the local-only approval defect with a failing source assertion; the
  same assertion passes after mounting the real hiring queue.
- Authenticated browser check used the supplied synthetic Admin account and
  existing local API. Credentials are not retained in this report or source files.
- Live pending hiring queue returned zero records; approved queue returned 11.
- Candidate review opens; protected current account state loads.
- Roles/software access opens the actual access workspace. Existing delegable roles
  and Self/All/Employee/Department/Site/Team/Project scope controls load.
- Site assignment opens the existing editor with five server-provided site options,
  effective date and reason. No assignment or permission change was submitted.
- Queue layout was checked at 1440x900 and 390x844. The mobile review dialog fits
  within the viewport; no document-level horizontal overflow was observed.
- Two retained submission reads returned HTTP 500 with JSON parsing failures.
  These are server/data errors, not missing click handlers. No stored reference,
  encrypted evidence or account was repaired or fabricated.
- One later seeded approved record returned no account; real access provisioning
  remains unavailable for that record until its backend handoff is valid.
- Final TypeScript/Vite production build passed. Full ESLint passed. `npm run verify`
  stops at formatting warnings in existing/concurrent `src/layouts/AdminLayout.tsx`
  and `src/styles/admin-dashboard.css`; this is not a full verification pass.

## Acceptance Limits

No pending candidate was available for a real hiring-decision write. Positive
approval/return/reject persistence, exact retry/concurrency, initial site creation
and subsequent role/credential writes were not executed in this change. Existing
approved records were inspected read-only. The detail dialog does not substitute
mutable current data for missing frozen submission evidence.

For full acceptance, use a genuine synthetic HR draft with valid encrypted profile,
submission and document evidence, submit it through HR, then approve with an
authorized Admin and complete its explicitly approved site/access setup. Diagnose
the existing retained-submission JSON failures before expecting their frozen review
to display. Do not reseed or overwrite business history as a UI fix.

## Changed Files

- src/pages/AdminApprovalsPage.tsx
- src/modules/hr/pages/HiringApprovalsPage.tsx
- src/modules/hr/components/approvals/HiringSubmissionReview.tsx
- src/modules/hr/components/approvals/SubmittedEmploymentSummary.tsx
- src/modules/hr/components/approvals/hiring-review.css
- src/components/ui/Dialog.tsx (optional scoped class only)
- src/layouts/AdminLayout.tsx (fixed approvals badge removal only)
- src/modules/hr/README.md
- This report

Unrelated existing source changes were preserved. No commits, Git synchronization,
backend edits, migrations or database/fixture writes were performed.
