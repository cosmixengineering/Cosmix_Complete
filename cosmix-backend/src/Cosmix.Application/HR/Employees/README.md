# Employees

Owner: Developer 1.

Source: SRS 5; HR-001 to HR-008.

Employee draft, submit, approve, activate, suspend and offboard. Keep one employee identity referenced by every module. Add fields using docs/Employee-Fields.md; enforce required fields at activation, not draft save. Server-issued employee numbers, effective-dated assignments/compensation, private documents and scoped reads are required.

Status: folder prepared; business implementation is not included in this foundation.

Add feature-specific request/response DTOs, validation, a service interface/service and a repository interface here. Follow ../Departments where applicable, or Application/HR/Departments as the working read-only reference. Keep EF Core and HTTP types out of this project. Test rules before adding endpoints.
