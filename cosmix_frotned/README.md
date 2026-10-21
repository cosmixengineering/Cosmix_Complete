# Cosmix Frontend

React + TypeScript web application for Cosmix Engineering. HR and Attendance are the first business workstream, not the entire system.

Folder: `cosmix_frotned`. Package: `cosmix-frontend`.

## Current scope

Global web authentication and the approved HR web workspace are implemented against the existing .NET API. All roles share `/sign-in`; account permissions come from the server. HR includes the employee directory, organization references, hiring forms/documents/submission, current employee details, change requests, salary requests and employment status requests. The subsequent approved extension adds Admin hiring approval and post-hire decision controls. There are no mock dashboards or preview business routes.

Real SMS delivery requires the selected backend provider and provisioned accounts. Local verification uses existing synthetic accounts and the backend's guarded local-testing sender on `Cosmix_Testing`, not a frontend authentication bypass. See [Global web login](docs/Global-Web-Login.md) and the [HR implementation report](docs/HR-Web-Implementation-Report-2026-10-08.md) for actual verification and remaining provider/fixture limits.

## Developer commands

Use Node.js 24 and npm.

```powershell
npm ci
dotnet dev-certs https --check --trust
npm run dev
npm run verify
```

The Windows development launcher uses an existing trusted ASP.NET localhost certificate and prints `https://localhost:5173` (or another free port). If the certificate is missing, run `dotnet dev-certs https --trust` and approve the OS trust prompt yourself. The launcher does not install certificates or change machine-wide execution policy. Start the backend separately on `https://localhost:7240`.

Use the same `localhost` hostname, not `127.0.0.1`. Another frontend port needs that exact HTTPS origin in the backend's local CORS settings. `npm run build` creates `dist`; use approved HTTPS hosting to test it. Plain HTTP preview is not a supported login environment.

## Where to work

| Responsibility                             | Folder                                |
| ------------------------------------------ | ------------------------------------- |
| Department/panel features                  | `src/modules/<module>`                |
| Reusable buttons, inputs, tables, feedback | `src/components`                      |
| Shared authenticated application layout    | `src/layouts`                         |
| Providers and routes                       | `src/app`                             |
| Session adapter and access helpers         | `src/auth`                            |
| Shared HTTP client and query configuration | `src/services`                        |
| Shared hooks, helpers and types            | `src/hooks`, `src/utils`, `src/types` |
| Configuration and constants                | `src/config`, `src/constants`         |
| Shared styles and assets                   | `src/styles`, `src/assets`            |
| Future cross-application screens           | `src/pages`                           |

Each of the 14 modules has `pages`, `components`, `api`, `hooks`, `schemas`, `types` and `utils`. Empty folders use `.gitkeep`. The public `index.ts` reserves the module's export boundary.

## What remains

Real scanner acceptance, full-fixture hiring and transfer UAT, access-provisioning UI, attendance and other business modules, and production hosting remain. SMS stays in the existing authorized testing mode for now. `/account` links to `/hr/employees` when permitted. `/hr/approvals` is the authorized hiring-review queue. See docs/HR-Approval-Completion-Report-2026-10-08.md for fresh decision tests and remaining limits.

No automated test project, deployment, database change or backend code change was made for this increment. This is not a production-ready ERP.

## SRS web continuation

The later approved delivery includes site/access setup and eighteen prepared HR/Attendance
workspaces. On 9 October, specialised calendar, weekly-roster, monthly-register,
zone-coordinate editing and report/export controls were added. Missing-service actions stay
disabled; draft review is not a saved request or calculated result. See
[current UI delivery](docs/HR-Remaining-UI-Report-2026-10-09.md) and
[full scope and backend handover](docs/HR-Full-Web-Report-2026-10-08.md).

The earlier reports and “What remains” paragraph are historical for their increment.
Scanner-clean and full hiring acceptance later passed in an isolated testing copy; office
transfer and some current-value acceptance remain fixture-dependent. Fresh authenticated
browser acceptance on 9 October was blocked by LocalDB startup failure, independently of
the passing frontend build and isolated component tests. No mock login bypass was added.

## Repository

The configured remote is [cosmixengineering/Cosmix-Frontend](https://github.com/cosmixengineering/Cosmix-Frontend). The initial architecture branch is `codex/frontend-foundation`.

```powershell
git clone --branch codex/frontend-foundation https://github.com/cosmixengineering/Cosmix-Frontend.git cosmix_frotned
cd cosmix_frotned
npm ci
```

Generated output, `node_modules`, local environment files and artifacts are excluded from Git. `.env.example` contains public configuration only.

## Guides

1. [Architecture](docs/Architecture.md)
2. [Add a feature](docs/Add-A-Feature.md)
3. [Backend integration](docs/Backend-Integration.md)
4. [Security and release](docs/Security-And-Release.md)
5. [Verification](docs/Verification.md)
