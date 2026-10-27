# Approvals

This module owns the existing approval route and delegation workspace.

Route publication supports allowance, fuel, vehicle maintenance, leave encashment, advance, loan and policy exception. It requires a current web MFA session with explicit All-scope `approvals.routes.manage` and the selected `approvals.stages.N.review` permission. The server validates current account eligibility and authority. No role name alone grants publication access.

Route history additionally requires `approvals.routes.read`; publication-only actors can reach the form without reading history. Delegations retain their own permission and scope checks. Fixed decisions and independent reviewer rules in the consuming workflows remain authoritative; this screen does not configure arbitrary leave, attendance or payroll approval chains.

- `pages/`: route-level screens.
- `components/`: UI used only by this module.
- `api/`: this module's endpoint functions; use the shared HTTP client.
- `hooks/`: query and feature hooks.
- `schemas/`: client-side validation and server-response schemas.
- `types/`: frontend DTOs and view models, not EF entities.
- `utils/`: pure helpers specific to this module.
- `index.ts`: public entry for app composition. Internal imports stay relative.

Reuse `src/components` for general controls. Do not deep-import another module. Agree cross-module contracts with the owner. Server authorization and policy calculations remain authoritative.
