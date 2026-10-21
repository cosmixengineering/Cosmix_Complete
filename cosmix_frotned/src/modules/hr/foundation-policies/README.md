# HR Foundation Policies

This feature administers the actual Fields, Lifecycle and Records contracts in
`FoundationPoliciesController` and `Application/HR/FoundationPolicies`. It owns only
this directory. Application composition remains with the parent.

Files: `access.ts`, `contracts.ts`, `draft.ts`, `hooks.ts`, `inputs.ts`, `index.ts`,
`pages.tsx`, `routes.tsx`, `FoundationPoliciesWorkspace.tsx`, `PolicyView.tsx`,
`ProposalForm.tsx`, `RequestDetail.tsx`, `RulesEditor.tsx`,
`foundation-policies.css` and this `README.md`.

## Public Boundary

Import `index.ts`. Runtime exports are `FoundationPoliciesWorkspace`,
`FoundationPoliciesPage`, `foundationPolicyRoutes`, `foundationPolicyPermissions`,
`canFoundationPolicy` and `policyKinds`. Type exports are `FoundationPolicyKind`,
`FoundationPolicyCurrent`, `FoundationPolicyDetail`, `FoundationPolicyRules` and
`FoundationPolicyView`.

`FoundationPoliciesWorkspace` accepts `kind: 'Fields' | 'Lifecycle' | 'Records'`,
optional `requestId`, optional `initialView: 'current' | 'history' | 'requests'` and
optional `newProposal`. `FoundationPoliciesPage` derives the family/request from
route parameters and accepts optional `requests` and `newProposal` booleans.

Mount `foundationPolicyRoutes` under the existing authenticated layout:

- `/hr/foundation-policies` (Fields by default)
- `/hr/foundation-policies/:kind`
- `/hr/foundation-policies/:kind/requests`
- `/hr/foundation-policies/:kind/requests/new`
- `/hr/foundation-policies/:kind/requests/:requestId`

Family route segments are exactly `fields`, `lifecycle` and `records`. Unknown
families and invalid UUIDs render unavailable without issuing policy reads.

## Access And Transport

The exact independent grants are `hr.foundation-policies.read`,
`hr.foundation-policies.request` and `hr.foundation-policies.review`. Every needed
grant must have `scopeKind: 7` and `targetId: null`; reads are required for writes.
Authenticated web access is required. Company, Head Office, employee, attendance
and other permission grants confer no policy authority. Fixed current
Admin/SuperAdmin assignment and MFA are enforced by the server. The requester
cannot approve, return or reject their own proposal; cancellation is requester-only.

The shared `request` transport preserves cookie credentials, CSRF, origin bounds
and idempotency. All calls are under `/api/v1/hr/foundation-policies/{kind}`:

- `GET /current` or `/current?date=YYYY-MM-DD`
- `GET /history?page={page}&pageSize=25`
- `GET /requests?page={page}&pageSize=25` with an optional exact state name
- `GET /requests/{requestId}`
- `POST /requests`
- `POST /requests/{requestId}/decision`
- `POST /requests/{requestId}/cancel`

The frontend sends exact enum names for document/identity types and write
decisions. Request and decision view states are numeric, matching the actual
default MVC serialization. No raw JSON policy editor or seeded policy is included.
Controlled vocabularies and document rows are structured inputs with stable row
IDs. Required booleans and Records review mode require an explicit choice.

## State And Concurrency

Private queries have unique component epochs and zero inactive retention. User,
employee identity, grant contents, authentication state and family/request route
changes remount the private workspace; cleanup aborts reads/writes and removes
its cache. Local drafts and mutation receipts remain memory-only.

The first write re-reads and checks current policy/request versions and the
publication count. Publication checks the latest retained version and pending
queue. Uncertain outcomes freeze the exact body, endpoint, key and result
binding for an explicit identical retry. Definitive stale/denied/unavailable
failures require reload and review. No write retries automatically.

Policy interval dates remain date-only strings: start inclusive, end exclusive.
Current server business date comes from `/current`; no browser timezone is used
to infer it. Evidence timestamps display explicitly in UTC. As-of views cannot
originate new proposals. Unknown states or inconsistent responses fail validation.

## Verification

Temporary verification source lives outside the repository in Windows TEMP.
The feature has no permanent test file/project. Checks run on 13 October 2026:

```powershell
node C:/Users/Huma.Khan/AppData/Local/Temp/cosmix-foundation-policies-check.cjs --types
node C:/Users/Huma.Khan/AppData/Local/Temp/cosmix-foundation-browser/browser-check.mjs
node node_modules/eslint/bin/eslint.js src/modules/hr/foundation-policies --max-warnings 0
node node_modules/prettier/bin/prettier.cjs --check src/modules/hr/foundation-policies
```

All 13 behavioral checks and 8 temporary browser checks passed, together with
feature-local TypeScript, ESLint and formatting checks. The behavioral checks
cover exact scopes, self-review, context changes, typed
rules/enums, date bounds, frozen retries and resource-bound mutation results.
Temporary browser checks use the actual React module/shared HTTP client with
synthetic intercepted API/CSRF responses, including StrictMode and desktop/mobile
layout inspection. They do not prove live authenticated HTTP, SQL races, private
storage or approval authority configuration. Whole-project verify/build belongs
to the parent central run. No account/provider/SQL/backend/bank verification work
is part of this feature.
