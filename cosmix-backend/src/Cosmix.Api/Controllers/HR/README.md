# HR controllers

HTTP binding, API version/route, permission declaration and response codes only. Inject application services, never CosmixDbContext. HR organization URLs such as sites and shifts keep the SRS /api/v1/hr path even when their service ownership is Attendance. All other routes use the corresponding SRS group with /api/v1. Business and data-scope checks belong on the server in the application/repository query path. Business endpoints are pending except the read-only Departments reference.
