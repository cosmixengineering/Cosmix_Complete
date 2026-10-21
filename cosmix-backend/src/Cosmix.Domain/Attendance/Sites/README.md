# Sites domain

Source: SRS 6.1; ATT-006, MD-003.

`Site` stores the organization reference and optional supervisor. `SiteGeofenceVersion`
stores immutable boundary coordinates and explicitly supplied radius/accuracy settings.
There are no assumed radius, accuracy, or supervisor defaults. Organization APIs remain
planned under `/api/v1/hr/sites`.

Version selection must use the latest `EffectiveFrom` not later than the attendance date
in Asia/Karachi. One version per site/start date is allowed. Version records are append-only;
retirement, backdated approvals and authorization remain future use-case work. Historical
punches keep their selected version ID. A composite FK prevents a punch from recording a
geofence belonging to another site. No provider, endpoint or geofence acceptance service
is delivered by these models.
