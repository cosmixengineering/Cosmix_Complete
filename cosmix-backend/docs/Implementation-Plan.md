# HR backend foundation implementation plan

Goal: create an actual Visual Studio solution, not a diagram or a completed HR product.
Basis: HR_Attendance_Employee_Mobile_SRS_v1.2.docx, sections 3, 13, 14 and 16,
and the project manager's explicit request to proceed with creation.

## Scope and sequence

This records the original foundation implementation. On 05 October 2026 the
project manager requested removal of all test projects, files and the smoke script.
Those historical verification steps are no longer part of the delivered solution;
the current workflow builds the four application projects only.

- [x] Create four net10.0 projects and test projects with inward dependencies.
- [x] Add failing HTTP foundation tests, then implement protected API bootstrap,
  problem details, correlation IDs, development OpenAPI, health and CORS.
- [x] Add a read-only Department reference feature, application service,
  module-specific repository, SQL Server EF mapping and unit of work.
- [x] Test service behavior, database mappings/repository, authentication,
  permission failures, validation, errors and environment behavior.
- [x] Add all SRS module folders, field checklist, owner guide and explicit
  not-implemented boundaries. Do not create unfinished business endpoints.
- [x] Restore, Release build, test, check package vulnerabilities and run a
  real local API smoke test. Confirm EF migration tooling.
- Delivery: review staged files and commit the verified foundation. Fast-forward
  main and develop only when remote history permits; push without force and
  verify remote refs. Git history, rather than this pre-commit plan, records delivery.

## Constraints

No full login/refresh/MFA implementation, real employee data, automatic migrations,
invented policy values, mock face verification or production-ready claims.
The Department feature is read-only reference code, not complete organization CRUD.
JWT validation alone does not implement account suspension or refresh revocation.
The API must deny access by default. Full SRS data-scope authorization is a follow-on.
SQL Server is the deployment provider; relational test substitutes do not prove
SQL Server-specific behavior. Source SRS remains unchanged.

## Review risks

Unconfigured database must fail readiness, not liveness.
Missing or invalid JWT must never become an authenticated user.
Valid identity without explicit permission must be rejected.
Unknown routes/errors must not disclose exception payloads.
Empty module folders must survive Git and say what remains to implement.
