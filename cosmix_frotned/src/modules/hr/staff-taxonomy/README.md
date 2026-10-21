# Staff taxonomy administration

Supported web administration of the actual `StaffTaxonomyController` and
`Application/HR/StaffCategories` contracts in the nested backend checkout.
Parent composition owns the authenticated layout, HR public index, router and navigation.

## Public Exports

Import from `src/modules/hr/staff-taxonomy/index.ts`.

| Page                          | Canonical route                          |
| ----------------------------- | ---------------------------------------- |
| `StaffTaxonomyPage`           | `/hr/staff-taxonomy`                     |
| `StaffTaxonomyRequestsPage`   | `/hr/staff-taxonomy/requests`            |
| `StaffTaxonomyNewRequestPage` | `/hr/staff-taxonomy/requests/new`        |
| `StaffTaxonomyRequestPage`    | `/hr/staff-taxonomy/requests/:requestId` |

`staffTaxonomyRoutes` contains these four absolute React Router route objects.
`staffTaxonomyPaths` and `staffTaxonomyPermissions` are also public exports.
The main page contains Current, Effective, History and Requests views. Local controls
have no links to an alternative taxonomy base. Deep-linked details validate `requestId`.

## Actual Permissions And Endpoints

All pages require a current authenticated Web account and dedicated
`hr.staff-taxonomy.read`, with `scopeKind: 7` and `targetId: null`.
The frontend also needs read access to capture the current versions for writes.

| HTTP endpoint under `/api/v1/hr/staff-taxonomy` | Backend permission          |
| ----------------------------------------------- | --------------------------- |
| `GET` root                                      | `hr.staff-taxonomy.read`    |
| `GET /effective?categoryCode=...&on=YYYY-MM-DD` | `hr.staff-taxonomy.read`    |
| `GET /history?page=...&pageSize=25`             | `hr.staff-taxonomy.read`    |
| `GET /requests?page=...&pageSize=25`            | `hr.staff-taxonomy.read`    |
| `GET /requests/{id}`                            | `hr.staff-taxonomy.read`    |
| `POST /requests`                                | `hr.staff-taxonomy.request` |
| `POST /requests/{id}/decision`                  | `hr.staff-taxonomy.review`  |
| `POST /requests/{id}/cancel`                    | `hr.staff-taxonomy.request` |

The backend additionally requires a live Web SMS-MFA session/current administrator
authorization. Review requires the permission grant's current fixed Admin/SuperAdmin
assignment, independent from the requester. That assignment and MFA are not exposed
by the session DTO; the frontend does not infer them from role labels or other grants.
Only the requester may cancel a Pending proposal. Approved/Returned/Rejected/Cancelled
requests cannot be edited or decided. Company/HeadOffice permissions are never borrowed.

## Contract And Lifetime Boundaries

- Complete structured rules use schema version 1, Office/Site location and schedule,
  explicit site-activation and outside-OT choices, and exactly five applicability/reference
  entries: Attendance, Leave, Meal, Deployment and Fuel. OFFICE/SITE confirmed constraints
  come from the actual codec. Broader categories start without inferred eligibility.
- Each required reference is an operator-selected immutable policy version ID. No reference
  catalogue endpoint exists here. The server checks category, period and availability.
  Taxonomy approval does not establish claim eligibility, rates, readiness or payment.
- Request bodies contain exact policy rowversion/revision, future dates, complete rules,
  reason and written evidence. Decisions/cancellation contain both exact rowversions,
  reason and written evidence. Authority versions are read from retained proposal evidence;
  authority configuration is deployment-owned and has no frontend write endpoint.
- Finite response schemas validate supported states, dates, exact request/policy/category
  identities, page metadata, unique rows and matching write receipts. Verified history must
  contain supported structured rules and its exact SHA-256. Invalid evidence remains visible
  without usable mappings. Root no-record responses include the controller's 204 form.
- Feature query instances, abort signals, forms, success feedback and mutation intent are
  memory-only. Authentication/grant/context changes discard them, cancel reads/writes and
  remove feature-owned cache entries. 401/403 responses revoke the active feature lifetime.
- Uncertain responses retain an owned immutable body, endpoint, expectation and idempotency
  key. Only explicit identical retries are sent. Network/408/general 5xx failures remain
  uncertain; explicit unavailable-authority 503 and definitive 4xx errors require correction
  or current-state review. No automatic write retry, browser persistence or generic JSON editor.

## Exact Owned Files

All new files are in this folder:

`index.ts`, `routes.tsx`, `paths.ts`, `pages.tsx`, `ProposalForm.tsx`,
`RequestDetail.tsx`, `MappingEditor.tsx`, `Views.tsx`, `draft.ts`, `contracts.ts`,
`access.ts`, `hooks.ts`, `session.tsx`, `lifetime.ts`, `intent.ts`, `operation.ts`,
`messages.ts`, `staff-taxonomy.css`, `README.md`.

## Verification

Temporary harness root:
`C:/Users/Huma.Khan/AppData/Local/Temp/cosmix-staff-taxonomy-web-20261013`.
No permanent test project or fixture/browser artifact was added to the repository.

Run from `cosmix_frotned`:

```powershell
node --loader file:///C:/Users/Huma.Khan/AppData/Local/Temp/cosmix-staff-taxonomy-web-20261013/loader.mjs C:/Users/Huma.Khan/AppData/Local/Temp/cosmix-staff-taxonomy-web-20261013/contracts.mjs
node --loader file:///C:/Users/Huma.Khan/AppData/Local/Temp/cosmix-staff-taxonomy-web-20261013/loader.mjs C:/Users/Huma.Khan/AppData/Local/Temp/cosmix-staff-taxonomy-web-20261013/mutations.mjs
node C:/Users/Huma.Khan/AppData/Local/Temp/cosmix-staff-taxonomy-web-20261013/browser.mjs
node ./node_modules/eslint/bin/eslint.js src/modules/hr/staff-taxonomy --max-warnings 0
node ./node_modules/prettier/bin/prettier.cjs --check src/modules/hr/staff-taxonomy
node ./node_modules/typescript/bin/tsc -p tsconfig.app.json --noEmit --incremental false --pretty false
```

Observed: 28 contract/scope regressions, 8 mutation regressions and 19 offline browser
checks passed. Missing-boundary checks were run before implementation. Generic uncertain
503 and stale success-feedback regressions were reproduced before their fixes.
Browser checks run actual components in StrictMode against synthetic HTTP fixtures with
the shared transport. They include all three independent decisions, proposal submission,
requester cancellation/retry, grant/cache revocation, late reads, history fingerprints and
desktop/narrow overflow checks. Screenshots were inspected at 1440px and 390px.

This is offline fixture verification, not authenticated SQL/provider acceptance. Real
approved authority configuration, existing grants/accounts, reviewed schema and reference
policy versions are required for live acceptance. None were provisioned or altered here.
Whole `npm run verify` belongs to parent integration. No bank workflow, backend, shared
composition/styles, SQL, scanner/SMS/mobile change, commit or push is part of this slice.

### Size-Check Review Fix

The independent review found that the initial canonical-length approximation omitted
plus signs, backticks and escaped quotes. The original oversized-plus proposal reproduced
the missing rejection before the fix. `retainedRulesLength` now counts the default
backend encoding of free-text names without replacing structural JSON quotes. Non-name
strings are constrained ASCII codes/UUIDs; canonical ordering does not change length.

The parent reran 20 size boundary assertions, 96 comparisons against the actual compiled
.NET codec, the 28 existing contracts and 19 browser checks. The reviewer independently
checked all 65,536 UTF-16 units and 16 full-rule comparisons against .NET 10.0.11; no
remaining actionable issue was found in the fix. These are memory/TEMP checks, not SQL UAT.

Encoder references: [.NET default JavaScript encoder](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Text.Encodings.Web/src/System/Text/Encodings/Web/DefaultJavaScriptEncoder.cs)
and [HTML-sensitive character exclusions](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Text.Encodings.Web/src/System/Text/Encodings/Web/AllowedBmpCodePointsBitmap.cs).
