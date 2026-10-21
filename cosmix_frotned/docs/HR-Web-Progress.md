# Execution ledger - plan: docs/HR-Web-Implementation-Plan.md

Approved scope: docs/HR-Web-Design.md. Started 8 October 2026.

- Preflight: Tasks 2-5 consume Task 1 authenticated HTTP/access/query helpers; preserve a
  single transport and All-scoped presentation checks (backend AccessScopeKind.All = 7).
- Preflight: Task 4 reuses Task 3 field/document controls but never the original hiring
  snapshot as the current post-hire values.
- Ruling: work in the existing requested frontend checkout on codex/frontend-foundation,
  preserving uncommitted login work; no worktree, commit or push. User explicitly requested
  this project and local-only work. Verification scripts remain outside Git in TEMP.
- Ruling: inline sequential execution with a fresh final agent review. Provider/fixture
  gaps are documented; no grants, accounts, scanner settings or backend changes to hide gaps.
- Baseline: frontend verification passed in preparation; local-testing API confirms
  enabled=true, database=Cosmix_Testing. Backend uses existing guarded test sender.
- Task 1: foundation/directory implemented. Browser check failed on absent Employees heading
  before implementation, then passed with actual API records, HR financial denial and 390px
  overflow check. npm run verify passes. Additional session/retry checks continue in Task 6.
- Task 2: implemented. Missing heading RED became GREEN. Actual HR read-only navigation,
  Admin synthetic city create/edit/history, identical idempotent replay and stale-version
  rejection passed. npm run verify passed. Additional edge cases continue in Task 6.
- Ruling: use the shared typed hrGet/hrWrite transport rather than add identical per-module
  wrappers; contracts remain separated by workflow. Cost: transport call sites live in hooks.
- Task 3: implemented. Missing hiring route RED became GREEN. Actual synthetic draft
  creation/profile save/reload, Office-to-Site branch clearing, dirty navigation cancellation,
  forbidden private prefetch, invalid file rejection and scanner-failed document download
  blocking passed. Release frontend build passed. Scanner-clean/verified submission is blocked
  by the unconfigured scanner, not bypassed. Remaining edge checks continue in Task 6.
- Ruling: retain proposed shift read-only because no authorized lookup contract exists.
  Asset return recipient accepts a validated UUID reference, not invented account options;
  backend validates its authority. A searchable recipient lookup awaits backend support.
- Ruling: use the existing React Router data-router provider to support unsaved-navigation
  blocking while retaining the existing route and authentication components.
- Task 4: implemented. Missing detail route RED became GREEN; current-section navigation,
  sensitive deep-link denial without fetch and npm run verify passed. Actual post-hire
  mutation checks require a complete approved synthetic employment fixture; inspecting this
  availability, without creating accounts or bypassing approved hiring evidence.
- Ruling: post-hire office selection follows the existing assignment's non-null branch;
  approved Office assignments require one and Site assignments forbid one in the backend.
  If employment is absent, transfer editing is blocked, not inferred from hiring snapshots.
- Task 5: implemented. Missing requests route RED became GREEN. Actual Admin-account
  salary/status list reads and navigation passed; no Admin decisions in the HR interface.
  npm run verify passed. Full request mutation acceptance remains fixture-dependent.
- Task 6: implemented and available local checks completed. Whole-module fresh-agent review,
  responsive/browser/error/session verification and documentation completed. Initial login
  probes encountered unavailable test OTP; later actual logins passed without rate-limit,
  provider or permission changes. See final report for tests that remain provider/fixture-blocked.
- Updated fixture evidence: the existing synthetic HR login fixture exposes a basic section
  but no current employment assignment. Actual basic save/current reload and bank, salary,
  status proposal/detail/cancellation passed. No approvals or account changes were performed.
- Review fixes: cancel actions/downloads with session lifetime; suppress detached callbacks;
  stop reconnect from remounting dirty forms; protect organization navigation/unload; refetch
  current details after save; gate approved hiring deep links before identity reads; correct
  business-code mappings and readiness anchors. The bounded reviewer recheck confirmed these
  paths and identified one cancellation/refresh race, subsequently fixed and reproduced GREEN
  against an actual 401/refresh. This was a focused review, not a full security certification.
- Final verification: npm run verify passed. Actual browser tests covered unchanged retry keys,
  stale409/no overwrite, cross-tab logout/cache clearing, late financial response suppression,
  refresh and cancelled refresh, responsive widths, keyboard navigation and existing login.
- Ruling: no redundant per-feature transport wrappers or generic employee mutation endpoint.
  Current/basic is used as the permitted approved-state check when an identity grant is absent;
  only the backend's exact employee_not_approved response allows draft-profile loading.
- Remaining acceptance limits: scanner-clean evidence and complete submission, assignment-
  backed transfers, protected evidence changes, real SMS and approval/returned/scheduled UAT.
  Backend/mobile remain unchanged; no commit, push, account creation or provider reconfiguration.
