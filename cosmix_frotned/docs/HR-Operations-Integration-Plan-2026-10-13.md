# HR Operations Web Integration Plan

> Execute the remaining integrations in the already approved HR full-web design and
> delivery plan continuously, with disjoint feature ownership and final independent review.

**Goal:** Replace supported prepared HR flows with genuine current backend integration.
**Spec:** `docs/HR-Full-Web-Delivery-Plan-2026-10-08.md`, approved SRS v1.2 and later confirmed
workflow decisions; the PM explicitly requested all seven non-mobile gaps on13October.
**Architecture:** Existing Attendance module, secure shared transport/authentication,
strict actual DTO schemas, scope-qualified presentation and server-authoritative decisions.
No new backend business interface is authorized merely by a prepared frontend form.

## Constraints

- Preserve login/testing OTP, all source history and dirty working trees. No commit/push.
- React Native, production activation, account/permission creation, SQL application and
  provider/scanner/SMS configuration changes are excluded from this integration package.
- Use actual controllers/DTOs; no fabricated responses, guessed APIs or browser pay engines.
- Self views use their genuine own-data APIs, not HR All-scope endpoints.
- Never reopen a locked month, infer absence from missing evidence, or turn empty results
  into approved zero liability. Office OT stays ineligible. Zones are Operational/Red only.
- Private drafts and query data remain memory-only and reset with authentication/resource
  changes. Exact uncertain mutation intent/key is retained; stale state requires re-review.
- Temporary isolated tests only. Browser fixture checks are not real SQL/provider acceptance.

## Tasks And Ownership

1. Review/daily/correction/OT slice: `src/modules/attendance/operations/review/**`.
   Read actual summary/audit/correction/exception/carry-forward/OT contracts. Implement
   the authorized management and own flows, validations, source histories, stale/retry
   controls and permission denials. Return a feature-local `index.ts` and actual page names.
2. Leave/policy slice: `operations/leave-policy/**`. Actual ordinary leave intake/balance/
   preview/returned correction/cancellation/Admin review and supported effective policy/
   settings publication/history. No benefit/encashment/recovery or unsupported policy API.
3. Field slice: `operations/field/**`. Actual Operational/Red configuration/events/review,
   site report intake/response/evidence/HR decisions and own notifications. No web GPS,
   background tracking, provider activation or automatic discipline/pay adjustments.
4. Parent reports/closure/input slice: `operations/reports/**`. Genuine explicit selected-
   employee factual register/dashboard and CSV/XLSX/PDF jobs/private downloads; global
   month preview/detail/finalize only when server Ready. Supported payroll-input reads/
   immutable revision preparation stay separate from posting/payment authority.
5. Parent composition: Attendance public exports, protected routes, sidebar labels and
   old supported-workspace redirects. Preserve unsupported prepared views as unavailable,
   not allegedly completed modules. Shared schemas/transport remain single-owner.
6. Verify/review/handover: failed-then-passed contract/helper checks, offline desktop/narrow
   interaction checks, `npm run verify`, fresh independent integration review, dated exact
   endpoint/file/status report and Desktop copy. Do not count offline fixtures as live UAT.

## Review Focus

- A grant for one permission cannot lend its scope to another. Exact unknown historical
  membership remains server-authorized, not inferred from a role name.
- Delayed responses after logout/account/employee/period/grant changes must not render or
  write old private data. Uncertain retries must not pick up refreshed tokens/payloads.
- Unknown response states, incomplete employee/date matrices, mixed resources and wrong
  export content/format must fail validation. Factual time is not payable time.
- Global month actions require All-scoped grants and exact current preview/version.
  Financial/legal/provider blockers remain explicit; no false ready state or reopening.
- Own requests and management review remain separate. No requester/beneficiary self-decision,
  scanner bypass, implicit evidence authority or unsupported mutation is introduced.

## Backend Decisions Still Open

Office physical capture/boundaries require the distinct source/provenance design. KPI,
waiting/studying, benefits/encashment/recovery, meal/vehicle/loan lifecycles need the
specific PM decisions requested in chat. The selected face PoC stays inactive because
the actual model/deployment are unavailable. New backend subsystems need their written
design/plan review; this package does not interpret the frontend approval as those decisions.

## Confirmed Authority And Configuration Update

The PM clarified on 13 October that Company and Head Office configuration belongs only to
SuperAdmin. Attendance, salary, overtime and deduction policy management belongs to
Admin with assigned permissions; this does not replace existing scoped HR scheduling,
geofence, correction or overtime case-review authority. The bounded backend configuration
checks must enforce that distinction, including replay, rather than trusting frontend labels.

New leave settings use a versioned contract with explicit quotas, service threshold and
annual start month/day. Retained v1 settings keep their original published interpretation.
Changing a quota-year boundary must not erase recorded utilization or cancellation history;
unsupported transition reconciliation remains blocked only for the affected policy choice.

Only genuinely missing business decisions are tracked in the single authoritative
`cosmix-backend/docs/attendance-completion/Pending-Policy-Sheet-2026-10-13.md`. The existing
approved workflows continue. No unconfirmed KPI, penalty, benefit or payment is activated.
