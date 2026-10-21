# Sites

Owner: Developer 2.

Source: SRS 6.1; ATT-006, MD-003.

Site master APIs are implemented under `/api/v1/hr/sites`. See `SiteMasterService` and
`SiteContracts` plus `docs/Organization-Masters.md` for contracts and limits.

Fixed Admin/SuperAdmin with paired All-scope `hr.sites.manage` maintains sites; HR can receive
All-scope `hr.sites.read`. Current web MFA, audit/idempotency and SQL rowversion are enforced.

Employee site assignment/reassignment is implemented in the HR employee module. SiteGeofenceService
now provides immutable future-dated boundary configuration, current/as-of/exact-version reads,
audit history and the employee's own current assigned-site configuration. See docs/Site-Geofences.md
and its verification record. Radius/accuracy require explicit management-approved inputs; 80 metres
is not a default. Configuration and pure geometry do not accept punches, implement zones or verify
faces. Site master changes do not assign employees, alter attendance or grant access.
Keep EF and HTTP types outside Application.
