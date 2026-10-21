# EmployeeDocuments domain

Source: SRS 5, API-005, SEC-007.

Employee documents, certifications, expiry and verification. File metadata belongs in SQL; private bytes belong in approved storage. Check file signatures, limits and malware scan before release. Download needs permission and employee scope.

Initial entity models and EF mappings now exist; see docs/Initial-Models.md for exact coverage. Full workflows remain to implement. Keep this domain folder free of EF Core, controllers and external SDK calls.
