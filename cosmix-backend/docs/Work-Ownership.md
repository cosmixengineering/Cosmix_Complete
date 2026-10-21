# Backend work ownership

The PM owns the React web and React Native frontends. Both backend developers use this solution.
Read the supplied SRS as the requirement baseline; folders do not replace requirements.

On 12 October 2026 the user explicitly authorized completing the absent Attendance-related HR
policy work in this local checkout. That task may implement its required policy/leave integration
here while preserving existing HR behavior; no Git integration or changes to another developer's
checkout are authorized. Financial rules and decision authority still require explicit agreement.

## Developer 1

1. Start with Authentication: login, access/refresh sessions, roles/permissions, devices and MFA.
   Agree token/current-user, employee ID and scope contracts with Developer 2 first.
2. Build HR organization masters and employee draft/approval/activation with all required fields.
3. Add employee documents, assignments, compensation and controlled lifecycle changes.
4. Implement leave, policies/configuration, HR self-service and the assigned HR reports.
5. Continue approved allowance/vehicle/fuel, advances/loans and payroll handoff scope.

Own Application/Authentication and Application/HR, with corresponding Domain/HR,
Infrastructure HR repositories and Api HR/authentication controllers.
Coordinate policy calculation ownership: Developer 1 maintains versioned configuration;
Developer 2 implements attendance/OT calculations against those versions.
Own shared approval, audit and private-file contracts, with review from Developer 2.

## Developer 2

1. Start sites, shifts/rosters and unit-tested attendance contracts in parallel with authentication.
2. Build approved face enrollment/check-in verification and immutable punch handling after
   identity/device and employee lifecycle contracts are usable.
3. Add work sessions, daily summaries, corrections, zones and in-app site reports.
4. Add approved attendance-policy/OT rules, month locks and attendance reports.
5. Own notifications and durable background job infrastructure; agree shared use with Developer 1.

Own Application/Attendance, corresponding Domain/Attendance, Infrastructure Attendance
repositories/provider adapters and Api Attendance controllers.
Use the same employee master. Do not build a second employee table inside attendance.

## Shared files

Coordinate before editing Program.cs, DependencyInjection.cs, CosmixDbContext,
permissions, package versions or the migration snapshot. One migration owner per merge window.
Application interfaces are the agreement between developers; settle them before both build consumers.
Each module owner supplies backend tests, endpoint documentation and client integration examples.

Full scope in twenty days is not guaranteed by this skeleton. The PM must confirm milestones and
acceptance criteria separately. New CEO requests require written scope/date/priority decisions;
do not silently change both developers' contracts mid-task.
