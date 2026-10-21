# HR Management Follow-up - 19 October 2026

## Boundary

HR web is the employee-management workspace, not an employee personal leave or
claims portal. Personal APIs remain available to the separate mobile client.
This work does not implement or test that React Native app, real face/SMS
providers, Accounts settlement or payment release.

Existing authentication, cookies/CSRF, server permissions, sensitive field scopes
and immutable backend evidence remain authoritative. No mock login or production
permission bypass was added. All checks below used local code and synthetic
transport/render inputs unless explicitly stated otherwise.

## Repairs

- Attendance list and employee detail use actual scoped factual sources. Removed
  invented punch logs, duration totals, medical/contact fallbacks and fake local
  manual-punch success. Existing authorized correction/export paths are linked.
- Shift management composes the existing real shift/schedule components. Removed
  generated schedules/rates/counts and caught-error branches claiming success.
- Site details use actual site, operational and dated geofence APIs. Recorded
  punch coordinates are snapshots, not live tracking or inferred compliance.
- Biometric display uses actual authorized metadata. It does not invent an
  enrollment, confidence, successful revoke or successful provider call. Enrollment
  and revocation UI/provider acceptance remain unavailable, not simulated.
- Hiring page hook ordering is fixed without altering approval authority.
- Rich HR export feedback no longer treats total filtered records as the number
  exported from one page. Larger HR export jobs/PDF remain a separate gap; existing
  factual Attendance export jobs are not removed.
- Personal leave/encashment, attendance navigation and HR claims intake no longer
  appear as employee self-service in this web workspace. Authorized management
  queues and agreement management remain. Interest/extra-charge input is absent.
- Header bell opens the existing real `/account/notifications` workflow. Removed
  hardcoded employee alerts, fake unread counts and browser-only mark-as-read.
- Permission-gated Leave management links expose benefits, recovery work review,
  encashment review and settings. The matching sidebar section stays selected.
- Exact benefit/recovery errors distinguish missing policy, pending evidence,
  protected periods and already-allocated credit from a generic stale record.

## Management Workflows

- `/hr/attendance/leave-benefits`: authorized employee selection, retained award
  history, configured award, unused revocation and explicit source-attributed
  correction. No manual award-minute override or assumed spending order.
- `/hr/attendance/recovery-work`: pending claim queue, protected retained detail,
  exact policy/work evidence, independent review and progress. A commitment alone
  is not fulfilled work. Personal submission remains a mobile workflow.
- `/hr/leave-encashment`: Admin review queue using `attendance.leaves.review`, not
  the old nonexistent client-only encashment permission names. No amount override
  or payment action.
- `/hr/allowances`: outstation management requests/decisions, home-break/return
  review, server eligibility and retained claims. Rates and partial-day rules come
  from published policy. Missing rules produce pending/null values, not guesses.

The benefit/recovery read-denial regression is fixed: saved private responses are
hidden immediately and cleared on a failed corresponding read, including 403.
Successful reload cannot resurrect the discarded private response.

Outstation forms preserve the serialized request and idempotency key on uncertain
outcomes. Tabs/employee/record selection stay locked while the form is open;
explicit unchanged retry or discard-and-reload is available. Session/grant changes
reset the private workspace; 401/403 also clears its private query state.

Vehicle management now includes typed effective-dated policy publication,
retained versions/succession, server monthly previews and review, incident
amendments and dated trip attribution. Pending writes block tab, record and SPA
route changes; uncertain writes retain their exact payload/key until explicit
retry or discard/reconciliation. Sign-in and revoked authentication are not
trapped. Denied policy/monthly reads clear private evidence; late preview responses
cannot restore cleared state.

Leave settings now has a typed v4 publisher for base settings, encashment,
recovery, benefits and recovery fulfilment. Numeric values and governing choices
must be explicit; blank does not mean zero. Copying retained settings preserves
supported fields and rejects unknown/invalid versions. Cash eligibility and
fulfilment dependencies are validated. Publication is a new immutable version,
not modification of prior history, and successful responses must match the
submitted settings. No raw JSON editor or invented business values were added.

## Executed Checks

| Check                                  | Result                                                                                                               | Limitation                                                                                                                               |
| -------------------------------------- | -------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| Initial combined TypeScript/Vite build | PASS, existing large-chunk warning                                                                                   | Before later management additions; final build below required                                                                            |
| Hiring controlled checks               | 28 passed                                                                                                            | In-memory, not authenticated browser                                                                                                     |
| Employee attendance/biometric checks   | 32 passed                                                                                                            | Controlled responses, no provider                                                                                                        |
| Attendance list checks                 | 17 passed                                                                                                            | In-memory, no raw-punch writes                                                                                                           |
| Site checks                            | 23 passed                                                                                                            | Synthetic render/query inputs                                                                                                            |
| Report feedback checks                 | 18 passed                                                                                                            | Current-page export feedback                                                                                                             |
| Encashment/loans checks                | 39 passed                                                                                                            | Management-only rendering/contracts                                                                                                      |
| Benefit/recovery mutation cases        | 16 passed, plus controlled rendering/denial/reload checks                                                            | Synthetic responses; no actual SQL                                                                                                       |
| Outstation checks                      | 44 author checks and 17 independent parent checks passed                                                             | Exact retries, denied responses, input binding and guarded navigation; synthetic transport                                               |
| Integrated navigation                  | 21 passed                                                                                                            | Actual navigation function, controlled grants; includes legacy permission denial                                                         |
| Exact business-error messages          | 33 passed                                                                                                            | In-memory errors; authorization status remains authoritative                                                                             |
| Header and leave active navigation     | 16 passed                                                                                                            | Controlled React server rendering, no actual login                                                                                       |
| Vehicle management                     | 66 passed                                                                                                            | Controlled contracts, retries, SPA guards and denied/late response handling; new denial/navigation regressions failed before their fixes |
| Typed v4 publication                   | 27 blank-number and 39 invalid/conflicting checks passed, plus retained-field/render/response/unchanged-retry checks | In-memory; no policy was published to a database                                                                                         |
| Final combined TypeScript/Vite build   | PASS, exit 0; 2,441 modules                                                                                          | Existing large-chunk warning; not runtime acceptance                                                                                     |
| Final repository ESLint                | PASS                                                                                                                 | Ran through `npm run verify` before its formatting stage                                                                                 |

All implementation workers froze their files before the final combined build.
Scoped ESLint/Prettier and TypeScript checks passed on the corresponding frozen
packages. The finite independent source review reported no remaining actionable
P1/P2 in the corrected vehicle navigation/denial paths or v4 publication mapping.
That review was read-only, not another executed test suite.

The final `npm run verify` passed lint and stopped at formatting. This report's own
formatting was corrected afterwards. Five existing unrelated files still fail:
`AdminLayout.tsx`, `SubmissionReview.tsx`, `HiringApprovalsPage.tsx`,
`admin-dashboard.css` and `onboarding-stepper.css`. The latter also retains its
existing extra EOF blank line in `git diff --check`. No unrelated formatting was
rewritten to claim a green repository. `npm run build` was executed separately and
passed. No automated frontend test files/projects were created, following AGENTS.md.

## Live Acceptance

An attempted current-page inspection redirected to `/sign-in` and displayed a
connection error. Port 5173 was running; 7240/7481 had no API listener at inspection.
No authenticated screenshots or saved/reloaded business records are claimed.

Actual browser/mobile/SQL testing still requires a verified compatible synthetic
backend target, matching storage and approved bounded writes. The owner's no-
migration instruction is unchanged. No old setup approval was reused.

Backend implementation, exact frozen Release/standalone logs and remaining
database constraints are documented in the
[backend verification report](../../cosmix-backend/docs/hr-srs-remediation/HR-Followup-Verification-20261019.md).
