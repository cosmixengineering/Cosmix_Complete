# Console failures and overtime review — 30 October 2026

Scope: the supplied employee-profile 404, face-device 409, payroll-preview 409, and HR overtime approval discoverability. No database changes, migrations, permission grants, payroll finalization or biometric capture were performed.

## Changes

- Approved employee profiles read current section contracts rather than requiring an original hiring form. Only the explicit `employee_not_approved` response permits the draft contract; 403/404 do not trigger that fallback. Identity reads still require identity permission. Missing retained evidence remains visible, not fabricated.
- Payroll checks the existing attendance month-readiness endpoint when the user has its All-scope permission. An unlocked month shows actionable guidance without calling the incompatible handoff preview. Users without month-read permission explicitly request the preview; backend authorization and locked-history checks remain authoritative.
- Face employee-ineligibility now shows a setup-required state, employee setup link, and explicit recheck action. Camera/enrollment remains blocked. The server's 409 is a genuine unmet prerequisite, not a successful enrollment or a generic stale-version failure.
- Separate permission-gated Overtime Approvals and Allowances links are available in the HR sidebar and dashboard. Review queue, request detail and decision use existing backend endpoints.

## Live read-only verification

- Existing signed-in HR session, frontend HTTPS localhost:5173, existing API proxy.
- September 2026 payroll page displayed “Not Locked Yet”; browser error log was empty for this check. No month was finalized.
- Overtime Approvals: selected All scope, 1 October inclusive to 31 October exclusive, Pending, Load history. API returned an empty queue. No real request was approved/rejected and decision mutation acceptance is not claimed.
- The reported approved demo employee opened current basic/contact data instead of failing the whole profile with the original 404. Retained identity evidence was unavailable; this is a source-data limitation.
- Selecting that demo employee on Biometric Registration displayed setup-required guidance and disabled camera/register controls. No camera access, face capture or identity-provider acceptance was attempted.

## Remaining limits

- Employees without eligible active accounts/verified devices require the normal authorized setup workflow; the face eligibility 409 is deliberately preserved.
- Missing retained hiring/identity evidence requires a separately reviewed data correction; no history was fabricated.
- A real pending OT request and authorized decision are still needed to exercise an approval mutation in this session. Queue wiring alone is not full workflow acceptance.
- `npm run verify` passed lint but stopped on formatting in four other currently modified files: AttendanceCorrectionsHub.tsx, ReviewUi.tsx, AttendanceTrackingPage.tsx and attendance-tracking.css. Their edits were preserved.
- The separate final `npm run build` passed TypeScript and the Vite production build (2,518 modules). The existing large-bundle warning remains; this is not full release acceptance.
