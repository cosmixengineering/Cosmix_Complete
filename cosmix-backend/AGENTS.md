# Cosmix backend

Read README.md, docs/Architecture.md and docs/SRS-Module-Map.md before changing this foundation.
The source SRS is docs/requirements/HR_Attendance_Employee_Mobile_SRS_v1.2.docx.

- .NET 10, C#, SQL Server and EF Core Code First. React/React Native clients are separate.
- Maintain Domain -> no outer dependencies; Application -> Domain; Infrastructure -> Application;
  Api is the composition root. Use module-specific repositories and simple services.
- Foundation folders are NOT completed features. Do not implement placeholder success responses.
- Keep employee data scopes server-side, separate sensitive DTOs and private storage.
- Raw punches are append-only. Face verification is Check In only and must fail closed.
- No attendance machines or external messaging attendance workflow. Zones are separate from punches.
- Policy values/approval chains/provider choices pending in SRS section 19 need management confirmation.
- No secrets, real employee data, local databases or binaries in Git.
- Coordinate shared files and migrations between developers; do not rewrite applied migrations.
- Test projects were removed at the project manager's request. Do not recreate them without a request.
- Run dotnet build in Release before proposing a merge. Build success is not behavioral verification.
- Preserve unrelated changes.
