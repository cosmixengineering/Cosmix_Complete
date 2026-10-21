# Architecture

## Current scope

The project includes global web login, the approved HR workspace and the subsequent Admin hiring/post-hire approval extension. Other modules still reserve ownership rather than expose placeholder panels. No preview route or mock dashboard is present. Authentication is shared across the ERP, not duplicated by role or department.

```text
src/
  app/
    providers/         root providers
    router/            global authentication and protected account/HR routes
  assets/images/       local reusable assets
  auth/                API contracts, cookie session adapter, context and access helpers
  components/
    ui/                reusable controls
    forms/             reusable fields
    data-display/      tables and pagination
    feedback/          loading, empty and error components
    layout/            reusable page-level components
  config/              public configuration validation
  constants/           shared constants
  hooks/               reusable React hooks
  layouts/             authentication layout and responsive HR application frame
  modules/
    hr/
      api/             typed HTTP and private file transport
      components/      hiring, documents, organization and request workflows
      hooks/           permissions, queries, actions, session lifetime, dirty forms
      pages/           directory, organization, hiring, employee detail, requests
      schemas/         Zod response contracts separated by workflow
      types/           DTOs, section permissions and enum labels
      utils/           module-specific helpers
      index.ts         public export boundary
    attendance/        same folder convention
    sales/
    inventory/
    procurement/
    accounts/
    projects/
    engineering/
    field-service/
    admin-fleet/
    approvals/
    documents/
    reports/
    administration/
  pages/               global login, password recovery and current account
  services/api/        HTTP wrapper and safe errors
  services/query/      QueryClient configuration
  styles/              reusable CSS foundations
  types/               shared contracts
  utils/               shared pure helpers
```

## Panels and screens

The HR panel belongs to `src/modules/hr`; employee screens belong to `src/modules/hr/pages`. General buttons belong to `src/components`. Common application chrome belongs to `src/layouts`. Do not add a second `panels` hierarchy that duplicates the same module boundary.

The app composes approved features through each module's `index.ts`. Modules may use shared helpers but must not reach into another module's internal files. ESLint checks alias-based dependency boundaries; review relative imports for escapes and cycles too.

## HR state boundaries

- The existing React Router data router supplies dirty-navigation blocking. Authentication remains in the existing provider.
- HR query keys contain the session subject and complete endpoint/filter. Reads accept cancellation signals and validate response schemas. Private inactive queries have zero cache retention.
- Focus/reconnect do not silently replace editing forms. Explicit refresh/navigation reloads server state; successful saves refresh their own current sections. Other records stay invalidated without an automatic form-destructive refetch.
- Local form state holds unsaved changes, versions and uncertain mutation keys only in memory. No sensitive drafts are persisted in browser storage.
- `useHrLifetime` aborts outstanding actions/downloads on unmount or authentication-context changes and suppresses late continuations. Logout clears the shared query cache.
- All-scoped grants control presentation. The server still authorizes every request, reference, state transition and protected field.
- Approved employee sections use the current-value endpoints, never the original hiring snapshot. Without identity access, hiring deep links first use the existing basic-current endpoint to distinguish approved records.
- Hiring approval reads the frozen submitted profile and independently protected financial/medical snapshots. Current directory rowversion is separate from the submission revision. Standard document versions must still match the snapshot before approval is enabled. Medical file actions use permission-gated current metadata; the API does not return frozen medical document metadata, and backend approval checks remain authoritative.
- Pending request decisions require review grants and exclude requester/employee self review. Fixed Admin/SuperAdmin role enforcement stays on the server because the current session DTO exposes grants rather than role codes. Definitive HTTP 4xx errors unlock correction/reload; uncertain outcomes retain the exact payload and idempotency key. Successful decisions refresh the employee's current/scheduled views and history.

## Responsibilities

- Pages compose feature components and hooks.
- Module API functions use the shared HTTP wrapper.
- TanStack Query owns server data; local component state owns local presentation state.
- Client schemas validate inputs/responses but never replace backend validation.
- Session context owns identity only, not all company data.
- Business authorization, record scope, salary policies and EF migrations belong in .NET.
- Department administration belongs to the web application. Employee mobile self-service remains a separate React Native project.
- Build only approved workflows. No permission catalogue or new UI is implemented merely by creating folders. Hiring/change/salary/status review is now explicitly approved; full role/credential provisioning remains separate.
