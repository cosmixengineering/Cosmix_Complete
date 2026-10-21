# Initial HR and Attendance models

This increment adds 16 entities to the existing Department model. It provides C# models,
SQL Server mappings and a new Code First migration. It does not expose new business endpoints.

| Feature | Model classes |
|---|---|
| Organization | Designation, Branch, StaffCategory, EmploymentType |
| Employee drafts | Employee, EmployeeAddress, EmployeeEmergencyContact |
| Employee history | EmployeeAssignment, EmployeeStatusChange |
| Private files | EmployeeDocument metadata |
| Sites | Site, SiteGeofenceVersion |
| Schedules | Shift, ShiftVersion, EmployeeShiftAssignment |
| Attendance evidence | RawPunchEvent |

Entities live in Cosmix.Domain feature folders. Their configurations and the single
CosmixDbContext live in Cosmix.Infrastructure/Database. Application services, repository
implementations and HTTP workflows should be added when each real use case is implemented.
Do not expose these entities directly as response DTOs.

## Persistence decisions

- Employee drafts allow missing profile fields. SQL Server assigns a unique long employee
  number from hr.EmployeeNumbers; gaps are possible and meaningful formatting is a UI concern.
  EF rejects explicit numbers on insert and later number changes.
- Official email and identity document values have normalized, filtered unique indexes.
  A partial identity document may exist in a draft; final type/number/format completeness
  must be validated before activation. Normalization is not encryption.
- EmployeeAssignment and EmployeeShiftAssignment use start-inclusive, end-exclusive dates.
  An assignment can be closed once. Overlap and manager-cycle checks need future transactions.
- Status history records a UTC effective instant, actor and reason. It does not authorize a
  transition or update current status on its own. Account revocation remains future work.
- Shift/geofence versions are immutable and selected by their latest applicable effective
  start date. Retirement/scheduling approvals remain future work. Shifts represent same-day
  or overnight schedules of up to 24 hours; this does not cap future work-session duration.
  Days, breaks, radius and accuracy thresholds must be supplied explicitly.
- Punches store received evidence, including unverified attempts. No field claims the event
  was accepted. Future verification/assessment must fail closed and record a separate result.
  Server receipt and device timestamps are distinct UTC instants. Pakistan local business
  dates will be derived by future attendance services.
- The unique employee/idempotency-key pair prevents duplicate storage. A future use case must
  compare request fingerprints, resolve races and replay outcomes; uniqueness is not the full protocol.
- Composite foreign keys ensure a referenced geofence belongs to the recorded site and an
  employment assignment belongs to the recorded employee. Other historical applicability
  checks still belong to the application workflow.
- Referenced records use restrictive deletes; editable records have SQL rowversion tokens.
- Every SaveChanges overload rejects tracked updates/deletes to punches, shift/geofence
  versions and status history, even with automatic change detection disabled. Bulk EF SQL,
  raw SQL and privileged database operations bypass tracking; restrict database permissions
  and never use those operations for evidence/history. Repository contracts must remain append/read.

## Migration and remaining scope

AddInitialHrAttendanceModels creates 16 tables and the employee-number sequence; it leaves
InitialFoundation unchanged. It has been generated and inspected, not applied to your database.
Back up and review any target database before a deliberate migration rollout. Its Down method
drops the added tables and is destructive once they contain data.

Follow Developer-Setup.md to configure your own SQL Server database. No seed employees,
accounts, sites, shifts or policies are supplied. /health/ready still checks the Department
foundation schema; it does not certify these new tables or future workflows.

Next implement authenticated/scoped employee draft services, then approved onboarding.
Activation, full lifecycle, audit transactions, approval decisions, phone/email/identity format
validation, encryption/key management, document scanning/verification, restricted compensation,
bank/medical information, certifications, face enrollment, punch acceptance, work sessions,
summaries, corrections, zones and policy/payroll calculations remain to implement.
