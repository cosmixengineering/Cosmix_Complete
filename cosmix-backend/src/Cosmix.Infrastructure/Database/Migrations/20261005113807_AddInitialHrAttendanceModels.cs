using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations;

/// <inheritdoc />
public partial class AddInitialHrAttendanceModels : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "attendance");

        migrationBuilder.CreateSequence(
            name: "EmployeeNumbers",
            schema: "hr");

        migrationBuilder.CreateTable(
            name: "Branches",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Branches", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Designations",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Designations", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Employees",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeNumber = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "NEXT VALUE FOR [hr].[EmployeeNumbers]"),
                Status = table.Column<int>(type: "int", nullable: false),
                FullLegalName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                FatherOrSpouseName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                IdentityDocumentType = table.Column<int>(type: "int", nullable: true),
                IdentityDocumentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                NormalizedIdentityDocumentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                IdentityDocumentExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                Gender = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                MaritalStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                PersonalPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                CompanyPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                SimIssuedOn = table.Column<DateOnly>(type: "date", nullable: true),
                SimStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                SimCreditTier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                PersonalEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                OfficialEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                NormalizedOfficialEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                JoinedOn = table.Column<DateOnly>(type: "date", nullable: true),
                ProbationEndsOn = table.Column<DateOnly>(type: "date", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Employees", x => x.Id);
                table.CheckConstraint("CK_Employees_IdentityPair", "([IdentityDocumentNumber] IS NULL AND [NormalizedIdentityDocumentNumber] IS NULL) OR ([IdentityDocumentNumber] IS NOT NULL AND [NormalizedIdentityDocumentNumber] IS NOT NULL)");
                table.CheckConstraint("CK_Employees_IdentityType", "[IdentityDocumentType] IS NULL OR [IdentityDocumentType] IN (1, 2)");
                table.CheckConstraint("CK_Employees_Number", "[EmployeeNumber] > 0");
                table.CheckConstraint("CK_Employees_OfficialEmailPair", "([OfficialEmail] IS NULL AND [NormalizedOfficialEmail] IS NULL) OR ([OfficialEmail] IS NOT NULL AND [NormalizedOfficialEmail] IS NOT NULL)");
                table.CheckConstraint("CK_Employees_ProbationDates", "[JoinedOn] IS NULL OR [ProbationEndsOn] IS NULL OR [ProbationEndsOn] >= [JoinedOn]");
                table.CheckConstraint("CK_Employees_Status", "[Status] BETWEEN 0 AND 6");
            });

        migrationBuilder.CreateTable(
            name: "EmploymentTypes",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EmploymentTypes", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Shifts",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Shifts", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "StaffCategories",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StaffCategories", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "EmployeeAddresses",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AddressType = table.Column<int>(type: "int", nullable: false),
                AddressLine1 = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                AddressLine2 = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                Province = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EmployeeAddresses", x => x.Id);
                table.CheckConstraint("CK_EmployeeAddresses_Type", "[AddressType] IN (1, 2)");
                table.ForeignKey(
                    name: "FK_EmployeeAddresses_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "EmployeeDocuments",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DocumentType = table.Column<int>(type: "int", nullable: false),
                StorageKey = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                Sha256 = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                UploadedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                IssuedOn = table.Column<DateOnly>(type: "date", nullable: true),
                ExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                ScanStatus = table.Column<int>(type: "int", nullable: false),
                VerificationStatus = table.Column<int>(type: "int", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EmployeeDocuments", x => x.Id);
                table.CheckConstraint("CK_EmployeeDocuments_Dates", "[IssuedOn] IS NULL OR [ExpiresOn] IS NULL OR [ExpiresOn] >= [IssuedOn]");
                table.CheckConstraint("CK_EmployeeDocuments_ScanStatus", "[ScanStatus] BETWEEN 0 AND 3");
                table.CheckConstraint("CK_EmployeeDocuments_Sha256", "LEN([Sha256]) = 64 AND [Sha256] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
                table.CheckConstraint("CK_EmployeeDocuments_Size", "[SizeBytes] > 0");
                table.CheckConstraint("CK_EmployeeDocuments_Type", "[DocumentType] BETWEEN 1 AND 10");
                table.CheckConstraint("CK_EmployeeDocuments_VerificationStatus", "[VerificationStatus] BETWEEN 0 AND 2");
                table.ForeignKey(
                    name: "FK_EmployeeDocuments_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "EmployeeEmergencyContacts",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                Relationship = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                AlternatePhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EmployeeEmergencyContacts", x => x.Id);
                table.ForeignKey(
                    name: "FK_EmployeeEmergencyContacts_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "EmployeeStatusChanges",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PreviousStatus = table.Column<int>(type: "int", nullable: false),
                NewStatus = table.Column<int>(type: "int", nullable: false),
                EffectiveAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                ChangedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EmployeeStatusChanges", x => x.Id);
                table.CheckConstraint("CK_EmployeeStatusChanges_EffectiveUtc", "DATEPART(TZOFFSET, [EffectiveAt]) = 0");
                table.CheckConstraint("CK_EmployeeStatusChanges_Status", "[PreviousStatus] BETWEEN 0 AND 6 AND [NewStatus] BETWEEN 0 AND 6 AND [PreviousStatus] <> [NewStatus]");
                table.ForeignKey(
                    name: "FK_EmployeeStatusChanges_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Sites",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                SupervisorEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Sites", x => x.Id);
                table.ForeignKey(
                    name: "FK_Sites_Employees_SupervisorEmployeeId",
                    column: x => x.SupervisorEmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ShiftVersions",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ShiftId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VersionNumber = table.Column<int>(type: "int", nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                StartTime = table.Column<TimeOnly>(type: "time(7)", nullable: false),
                EndTime = table.Column<TimeOnly>(type: "time(7)", nullable: false),
                EndDayOffset = table.Column<int>(type: "int", nullable: false),
                ScheduledMinutes = table.Column<int>(type: "int", nullable: false),
                PaidBreakMinutes = table.Column<int>(type: "int", nullable: false),
                UnpaidBreakMinutes = table.Column<int>(type: "int", nullable: false),
                ApplicableWeekdays = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ShiftVersions", x => x.Id);
                table.CheckConstraint("CK_ShiftVersions_ApplicableWeekdays", "[ApplicableWeekdays] BETWEEN 1 AND 127");
                table.CheckConstraint("CK_ShiftVersions_Duration", "(DATEDIFF_BIG(nanosecond, [StartTime], [EndTime]) + CAST([EndDayOffset] AS bigint) * 86400000000000) > 0 AND (DATEDIFF_BIG(nanosecond, [StartTime], [EndTime]) + CAST([EndDayOffset] AS bigint) * 86400000000000) <= 86400000000000");
                table.CheckConstraint("CK_ShiftVersions_EndDayOffset", "[EndDayOffset] IN (0, 1)");
                table.CheckConstraint("CK_ShiftVersions_Minutes", "[ScheduledMinutes] > 0 AND [ScheduledMinutes] <= (DATEDIFF_BIG(nanosecond, [StartTime], [EndTime]) + CAST([EndDayOffset] AS bigint) * 86400000000000) / 60000000000.0 AND [PaidBreakMinutes] >= 0 AND [UnpaidBreakMinutes] >= 0 AND CAST([PaidBreakMinutes] AS bigint) + [UnpaidBreakMinutes] <= (DATEDIFF_BIG(nanosecond, [StartTime], [EndTime]) + CAST([EndDayOffset] AS bigint) * 86400000000000) / 60000000000.0");
                table.CheckConstraint("CK_ShiftVersions_VersionNumber", "[VersionNumber] > 0");
                table.ForeignKey(
                    name: "FK_ShiftVersions_Shifts_ShiftId",
                    column: x => x.ShiftId,
                    principalSchema: "attendance",
                    principalTable: "Shifts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "EmployeeAssignments",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DesignationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StaffCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmploymentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ReportingManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                RecordedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EmployeeAssignments", x => x.Id);
                table.UniqueConstraint("AK_EmployeeAssignments_EmployeeId_Id", x => new { x.EmployeeId, x.Id });
                table.CheckConstraint("CK_EmployeeAssignments_Dates", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
                table.CheckConstraint("CK_EmployeeAssignments_Manager", "[ReportingManagerId] IS NULL OR [ReportingManagerId] <> [EmployeeId]");
                table.ForeignKey(
                    name: "FK_EmployeeAssignments_Branches_BranchId",
                    column: x => x.BranchId,
                    principalSchema: "hr",
                    principalTable: "Branches",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_EmployeeAssignments_Departments_DepartmentId",
                    column: x => x.DepartmentId,
                    principalSchema: "hr",
                    principalTable: "Departments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_EmployeeAssignments_Designations_DesignationId",
                    column: x => x.DesignationId,
                    principalSchema: "hr",
                    principalTable: "Designations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_EmployeeAssignments_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_EmployeeAssignments_Employees_ReportingManagerId",
                    column: x => x.ReportingManagerId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_EmployeeAssignments_EmploymentTypes_EmploymentTypeId",
                    column: x => x.EmploymentTypeId,
                    principalSchema: "hr",
                    principalTable: "EmploymentTypes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_EmployeeAssignments_Sites_SiteId",
                    column: x => x.SiteId,
                    principalSchema: "attendance",
                    principalTable: "Sites",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_EmployeeAssignments_StaffCategories_StaffCategoryId",
                    column: x => x.StaffCategoryId,
                    principalSchema: "hr",
                    principalTable: "StaffCategories",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "EmployeeShiftAssignments",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ShiftId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EmployeeShiftAssignments", x => x.Id);
                table.CheckConstraint("CK_EmployeeShiftAssignments_EffectiveDates", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
                table.ForeignKey(
                    name: "FK_EmployeeShiftAssignments_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_EmployeeShiftAssignments_Shifts_ShiftId",
                    column: x => x.ShiftId,
                    principalSchema: "attendance",
                    principalTable: "Shifts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_EmployeeShiftAssignments_Sites_SiteId",
                    column: x => x.SiteId,
                    principalSchema: "attendance",
                    principalTable: "Sites",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "SiteGeofenceVersions",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VersionNumber = table.Column<int>(type: "int", nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                Latitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: false),
                Longitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: false),
                RadiusMeters = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                MaximumAccuracyMeters = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SiteGeofenceVersions", x => x.Id);
                table.UniqueConstraint("AK_SiteGeofenceVersions_SiteId_Id", x => new { x.SiteId, x.Id });
                table.CheckConstraint("CK_SiteGeofenceVersions_Coordinates", "[Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180");
                table.CheckConstraint("CK_SiteGeofenceVersions_Distances", "[RadiusMeters] > 0 AND [MaximumAccuracyMeters] > 0");
                table.CheckConstraint("CK_SiteGeofenceVersions_VersionNumber", "[VersionNumber] > 0");
                table.ForeignKey(
                    name: "FK_SiteGeofenceVersions_Sites_SiteId",
                    column: x => x.SiteId,
                    principalSchema: "attendance",
                    principalTable: "Sites",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "RawPunchEvents",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Action = table.Column<int>(type: "int", nullable: false),
                RegisteredDeviceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_BIN2"),
                IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                RequestSha256 = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                DeviceCapturedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                IsOffline = table.Column<bool>(type: "bit", nullable: false),
                Latitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                Longitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                AccuracyMeters = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                GeofenceVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                EmployeeAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ShiftVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                VerificationAttemptReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RawPunchEvents", x => x.Id);
                table.CheckConstraint("CK_RawPunchEvents_Accuracy", "[AccuracyMeters] IS NULL OR [AccuracyMeters] >= 0");
                table.CheckConstraint("CK_RawPunchEvents_Action", "[Action] IN (1, 2)");
                table.CheckConstraint("CK_RawPunchEvents_Coordinates", "([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180)");
                table.CheckConstraint("CK_RawPunchEvents_DeviceCapturedUtc", "[DeviceCapturedAtUtc] IS NULL OR DATEPART(TZOFFSET, [DeviceCapturedAtUtc]) = 0");
                table.CheckConstraint("CK_RawPunchEvents_GeofenceSite", "[GeofenceVersionId] IS NULL OR [SiteId] IS NOT NULL");
                table.CheckConstraint("CK_RawPunchEvents_ReceivedUtc", "DATEPART(TZOFFSET, [ReceivedAtUtc]) = 0");
                table.CheckConstraint("CK_RawPunchEvents_RequestSha256", "LEN([RequestSha256]) = 64 AND [RequestSha256] NOT LIKE '%[^0-9A-F]%'");
                table.ForeignKey(
                    name: "FK_RawPunchEvents_EmployeeAssignments_EmployeeId_EmployeeAssignmentId",
                    columns: x => new { x.EmployeeId, x.EmployeeAssignmentId },
                    principalSchema: "hr",
                    principalTable: "EmployeeAssignments",
                    principalColumns: new[] { "EmployeeId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_RawPunchEvents_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_RawPunchEvents_ShiftVersions_ShiftVersionId",
                    column: x => x.ShiftVersionId,
                    principalSchema: "attendance",
                    principalTable: "ShiftVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_RawPunchEvents_SiteGeofenceVersions_SiteId_GeofenceVersionId",
                    columns: x => new { x.SiteId, x.GeofenceVersionId },
                    principalSchema: "attendance",
                    principalTable: "SiteGeofenceVersions",
                    principalColumns: new[] { "SiteId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_RawPunchEvents_Sites_SiteId",
                    column: x => x.SiteId,
                    principalSchema: "attendance",
                    principalTable: "Sites",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Branches_Code",
            schema: "hr",
            table: "Branches",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Designations_Code",
            schema: "hr",
            table: "Designations",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeAddresses_EmployeeId_AddressType",
            schema: "hr",
            table: "EmployeeAddresses",
            columns: new[] { "EmployeeId", "AddressType" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeAssignments_BranchId",
            schema: "hr",
            table: "EmployeeAssignments",
            column: "BranchId");

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeAssignments_DepartmentId_EffectiveFrom_EffectiveTo",
            schema: "hr",
            table: "EmployeeAssignments",
            columns: new[] { "DepartmentId", "EffectiveFrom", "EffectiveTo" });

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeAssignments_DesignationId",
            schema: "hr",
            table: "EmployeeAssignments",
            column: "DesignationId");

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeAssignments_EmployeeId_EffectiveFrom",
            schema: "hr",
            table: "EmployeeAssignments",
            columns: new[] { "EmployeeId", "EffectiveFrom" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeAssignments_EmploymentTypeId",
            schema: "hr",
            table: "EmployeeAssignments",
            column: "EmploymentTypeId");

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeAssignments_ReportingManagerId_EffectiveFrom_EffectiveTo",
            schema: "hr",
            table: "EmployeeAssignments",
            columns: new[] { "ReportingManagerId", "EffectiveFrom", "EffectiveTo" });

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeAssignments_SiteId_EffectiveFrom_EffectiveTo",
            schema: "hr",
            table: "EmployeeAssignments",
            columns: new[] { "SiteId", "EffectiveFrom", "EffectiveTo" });

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeAssignments_StaffCategoryId",
            schema: "hr",
            table: "EmployeeAssignments",
            column: "StaffCategoryId");

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeDocuments_EmployeeId_DocumentType",
            schema: "hr",
            table: "EmployeeDocuments",
            columns: new[] { "EmployeeId", "DocumentType" });

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeDocuments_ExpiresOn",
            schema: "hr",
            table: "EmployeeDocuments",
            column: "ExpiresOn");

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeDocuments_StorageKey",
            schema: "hr",
            table: "EmployeeDocuments",
            column: "StorageKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeEmergencyContacts_EmployeeId",
            schema: "hr",
            table: "EmployeeEmergencyContacts",
            column: "EmployeeId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Employees_EmployeeNumber",
            schema: "hr",
            table: "Employees",
            column: "EmployeeNumber",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Employees_IdentityDocumentType_NormalizedIdentityDocumentNumber",
            schema: "hr",
            table: "Employees",
            columns: new[] { "IdentityDocumentType", "NormalizedIdentityDocumentNumber" },
            unique: true,
            filter: "[IdentityDocumentType] IS NOT NULL AND [NormalizedIdentityDocumentNumber] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_Employees_NormalizedOfficialEmail",
            schema: "hr",
            table: "Employees",
            column: "NormalizedOfficialEmail",
            unique: true,
            filter: "[NormalizedOfficialEmail] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_Employees_Status_FullLegalName_Id",
            schema: "hr",
            table: "Employees",
            columns: new[] { "Status", "FullLegalName", "Id" });

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeShiftAssignments_EmployeeId_EffectiveFrom",
            schema: "attendance",
            table: "EmployeeShiftAssignments",
            columns: new[] { "EmployeeId", "EffectiveFrom" });

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeShiftAssignments_ShiftId",
            schema: "attendance",
            table: "EmployeeShiftAssignments",
            column: "ShiftId");

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeShiftAssignments_SiteId",
            schema: "attendance",
            table: "EmployeeShiftAssignments",
            column: "SiteId");

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeStatusChanges_EmployeeId_EffectiveAt_RecordedAt",
            schema: "hr",
            table: "EmployeeStatusChanges",
            columns: new[] { "EmployeeId", "EffectiveAt", "RecordedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_EmploymentTypes_Code",
            schema: "hr",
            table: "EmploymentTypes",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RawPunchEvents_EmployeeId_EmployeeAssignmentId",
            schema: "attendance",
            table: "RawPunchEvents",
            columns: new[] { "EmployeeId", "EmployeeAssignmentId" });

        migrationBuilder.CreateIndex(
            name: "IX_RawPunchEvents_EmployeeId_IdempotencyKey",
            schema: "attendance",
            table: "RawPunchEvents",
            columns: new[] { "EmployeeId", "IdempotencyKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RawPunchEvents_EmployeeId_ReceivedAtUtc",
            schema: "attendance",
            table: "RawPunchEvents",
            columns: new[] { "EmployeeId", "ReceivedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_RawPunchEvents_ShiftVersionId",
            schema: "attendance",
            table: "RawPunchEvents",
            column: "ShiftVersionId");

        migrationBuilder.CreateIndex(
            name: "IX_RawPunchEvents_SiteId_GeofenceVersionId",
            schema: "attendance",
            table: "RawPunchEvents",
            columns: new[] { "SiteId", "GeofenceVersionId" });

        migrationBuilder.CreateIndex(
            name: "IX_Shifts_Code",
            schema: "attendance",
            table: "Shifts",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ShiftVersions_ShiftId_EffectiveFrom",
            schema: "attendance",
            table: "ShiftVersions",
            columns: new[] { "ShiftId", "EffectiveFrom" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ShiftVersions_ShiftId_VersionNumber",
            schema: "attendance",
            table: "ShiftVersions",
            columns: new[] { "ShiftId", "VersionNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SiteGeofenceVersions_SiteId_EffectiveFrom",
            schema: "attendance",
            table: "SiteGeofenceVersions",
            columns: new[] { "SiteId", "EffectiveFrom" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SiteGeofenceVersions_SiteId_VersionNumber",
            schema: "attendance",
            table: "SiteGeofenceVersions",
            columns: new[] { "SiteId", "VersionNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Sites_Code",
            schema: "attendance",
            table: "Sites",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Sites_SupervisorEmployeeId",
            schema: "attendance",
            table: "Sites",
            column: "SupervisorEmployeeId");

        migrationBuilder.CreateIndex(
            name: "IX_StaffCategories_Code",
            schema: "hr",
            table: "StaffCategories",
            column: "Code",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "EmployeeAddresses",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "EmployeeDocuments",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "EmployeeEmergencyContacts",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "EmployeeShiftAssignments",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "EmployeeStatusChanges",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "RawPunchEvents",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "EmployeeAssignments",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "ShiftVersions",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "SiteGeofenceVersions",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "Branches",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "Designations",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "EmploymentTypes",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "StaffCategories",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "Shifts",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "Sites",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "Employees",
            schema: "hr");

        migrationBuilder.DropSequence(
            name: "EmployeeNumbers",
            schema: "hr");
    }
}
