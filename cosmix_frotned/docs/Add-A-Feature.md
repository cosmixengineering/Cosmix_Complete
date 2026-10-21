# Add a feature

This starter is architecture-only. Start UI or workflow implementation only after the PM requests that feature.

## Order of work

1. Confirm the approved SRS fields, permissions, record scope and backend contracts.
2. Add API DTOs in the module's `types`, with form/response schemas in `schemas`. These are not EF entities.
3. Add endpoint functions in `api`, using `src/services/api/http-client.ts`. Pass AbortSignal for cancellable reads.
4. Add query/mutation hooks in `hooks`. Include relevant user/filter context in query keys and invalidate affected data after writes.
5. Add only the requested UI in `components` and `pages`, reusing generic controls.
6. Export approved entry points through `index.ts` and register their routes in `src/app/router/AppRoutes.tsx`. There is no prebuilt navigation registry.
7. Wire real authentication and access helpers to protected routes. Build shared navigation in `src/layouts` only when requested.
8. Confirm that .NET enforces permissions and record/field scopes; frontend gates alone do not secure data.
9. Run `npm run verify` and checks appropriate to the implemented behavior. Do not add screenshots/artifact folders or a test suite without PM approval.
10. Report completed work, actual verification and remaining dependencies. Request review before integration.

## Collaboration

Keep each feature in its module. Agree ownership of auth, routing, shared components and API contracts before both developers edit them. Use feature branches and focused reviews. No folder architecture can guarantee zero conflicts.

No real employee, face, bank or salary data belongs in source files, logs or AI prompts.
