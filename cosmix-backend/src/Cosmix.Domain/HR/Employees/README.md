# Employees domain

Source: SRS 5; HR-001 to HR-008.

Employee draft, submit, approve, activate, suspend and offboard. Keep one employee identity referenced by every module. Add fields using docs/Employee-Fields.md; enforce required fields at activation, not draft save. Server-issued employee numbers, effective-dated assignments/compensation, private documents and scoped reads are required.

Initial entity models and EF mappings now exist; see docs/Initial-Models.md for exact coverage. Full workflows remain to implement. Keep this domain folder free of EF Core, controllers and external SDK calls.
