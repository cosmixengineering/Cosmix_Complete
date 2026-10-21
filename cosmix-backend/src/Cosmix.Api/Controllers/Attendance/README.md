# Attendance controllers

HTTP binding, API version/route, permission declaration and response codes only. Inject application
services, never CosmixDbContext. HR organization URLs such as sites and shifts keep the SRS
`/api/v1/hr` path even when their service ownership is Attendance. All other routes use the
corresponding SRS group with `/api/v1`. Business and data-scope checks belong in Application/repositories.

`SitesController` implements Site master list/detail/create/edit/status/history, with fixed Admin
write control and separately permitted HR reads. Employee assignment/geofences and other Attendance
business endpoints remain pending. See `docs/Organization-Masters.md` for the approved scope.
