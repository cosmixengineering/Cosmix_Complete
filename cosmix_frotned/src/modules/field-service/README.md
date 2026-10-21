# Field Service

This module owns Work orders, Scheduling, Service records.

Only folder boundaries are prepared. No module screens, navigation, API integrations or workflows are implemented. Confirm the SRS and decisions before creating fields or rules.

- `pages/`: route-level screens.
- `components/`: UI used only by this module.
- `api/`: this module's endpoint functions; use the shared HTTP client.
- `hooks/`: query and feature hooks.
- `schemas/`: client-side validation and server-response schemas.
- `types/`: frontend DTOs and view models, not EF entities.
- `utils/`: pure helpers specific to this module.
- `index.ts`: public entry for app composition. Internal imports stay relative.

Reuse `src/components` for general controls. Do not deep-import another module. Agree cross-module contracts with the owner. Server authorization and policy calculations remain authoritative.
