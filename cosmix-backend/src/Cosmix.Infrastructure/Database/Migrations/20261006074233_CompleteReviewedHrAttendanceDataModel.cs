using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations;

/// <inheritdoc />
public partial class CompleteReviewedHrAttendanceDataModel : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // This project has no existing database. If this migration is reused against
        // earlier populated tables, require reviewed evidence conversion instead of
        // silently inventing employee approvals or document scan/verification history.
        migrationBuilder.Sql("""
            SET XACT_ABORT ON;

            IF EXISTS (SELECT 1 FROM [hr].[Employees] WHERE [Status] <> 0)
                THROW 51000, 'Migration stopped: existing non-Draft employees require a reviewed onboarding-history conversion.', 1;

            IF EXISTS (SELECT 1 FROM [hr].[EmployeeDocuments] WHERE [ScanStatus] <> 0 OR [VerificationStatus] <> 0)
                THROW 51001, 'Migration stopped: existing processed documents require a reviewed scan/verification-evidence conversion.', 1;
            """);

        migrationBuilder.EnsureSchema(
            name: "auth");

        migrationBuilder.EnsureSchema(
            name: "workflow");

        migrationBuilder.EnsureSchema(
            name: "audit");

        migrationBuilder.EnsureSchema(
            name: "communication");

        migrationBuilder.EnsureSchema(
            name: "jobs");

        migrationBuilder.EnsureSchema(
            name: "reports");

        migrationBuilder.EnsureSchema(
            name: "files");

        migrationBuilder.AddColumn<string>(
            name: "City",
            schema: "attendance",
            table: "Sites",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "CityId",
            schema: "attendance",
            table: "Sites",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ClientName",
            schema: "attendance",
            table: "Sites",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "EndsOn",
            schema: "attendance",
            table: "Sites",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ProjectId",
            schema: "attendance",
            table: "Sites",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "StartsOn",
            schema: "attendance",
            table: "Sites",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "EvidenceFileId",
            schema: "attendance",
            table: "RawPunchEvents",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ApprovedOnboardingDecisionId",
            schema: "hr",
            table: "Employees",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "DraftRevision",
            schema: "hr",
            table: "Employees",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "OnboardingStatus",
            schema: "hr",
            table: "Employees",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "SubmittedVersion",
            schema: "hr",
            table: "Employees",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ScannedAt",
            schema: "hr",
            table: "EmployeeDocuments",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ScannerVersion",
            schema: "hr",
            table: "EmployeeDocuments",
            type: "nvarchar(150)",
            maxLength: 150,
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "SignatureVerified",
            schema: "hr",
            table: "EmployeeDocuments",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "VerificationReason",
            schema: "hr",
            table: "EmployeeDocuments",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "VerifiedAt",
            schema: "hr",
            table: "EmployeeDocuments",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "VerifiedByUserId",
            schema: "hr",
            table: "EmployeeDocuments",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "CityId",
            schema: "hr",
            table: "EmployeeAddresses",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "CityId",
            schema: "hr",
            table: "Branches",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddUniqueConstraint(
            name: "AK_RawPunchEvents_EmployeeId_Id",
            schema: "attendance",
            table: "RawPunchEvents",
            columns: new[] { "EmployeeId", "Id" });

        migrationBuilder.AddUniqueConstraint(
            name: "AK_RawPunchEvents_EmployeeId_RegisteredDeviceId_IdempotencyKey_Id",
            schema: "attendance",
            table: "RawPunchEvents",
            columns: new[] { "EmployeeId", "RegisteredDeviceId", "IdempotencyKey", "Id" });

        migrationBuilder.AddUniqueConstraint(
            name: "AK_EmployeeDocuments_Id_EmployeeId",
            schema: "hr",
            table: "EmployeeDocuments",
            columns: new[] { "Id", "EmployeeId" });

        migrationBuilder.AddUniqueConstraint(
            name: "AK_EmployeeAssignments_Id_EmployeeId",
            schema: "hr",
            table: "EmployeeAssignments",
            columns: new[] { "Id", "EmployeeId" });

        migrationBuilder.CreateTable(
            name: "ApprovalDelegations",
            schema: "workflow",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DelegatorUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                DelegateUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RequestType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                EndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                AuthorizedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                RevokedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApprovalDelegations", x => x.Id);
                table.CheckConstraint("CK_ApprovalDelegations_Dates", "[EndsAt] > [StartsAt]");
                table.CheckConstraint("CK_ApprovalDelegations_Revocation", "([RevokedAt] IS NULL AND [RevokedByUserId] IS NULL) OR ([RevokedAt] IS NOT NULL AND [RevokedByUserId] IS NOT NULL)");
                table.CheckConstraint("CK_ApprovalDelegations_Users", "[DelegatorUserId] <> [DelegateUserId]");
            });

        migrationBuilder.CreateTable(
            name: "ApprovalRouteRules",
            schema: "workflow",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Version = table.Column<int>(type: "int", nullable: false),
                RequestType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ApproverPermission = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                Stage = table.Column<int>(type: "int", nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                ApprovedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                StaffCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                MinimumAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                MaximumAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                Currency = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: true),
                MinimumDurationMinutes = table.Column<int>(type: "int", nullable: true),
                MaximumDurationMinutes = table.Column<int>(type: "int", nullable: true),
                RiskCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApprovalRouteRules", x => x.Id);
                table.CheckConstraint("CK_ApprovalRouteRules_Amount", "([MinimumAmount] IS NULL OR [MinimumAmount] >= 0) AND ([MaximumAmount] IS NULL OR [MaximumAmount] >= 0) AND ([MinimumAmount] IS NULL OR [MaximumAmount] IS NULL OR [MaximumAmount] >= [MinimumAmount]) AND (([MinimumAmount] IS NULL AND [MaximumAmount] IS NULL) OR ([Currency] IS NOT NULL AND LEN([Currency]) = 3))");
                table.CheckConstraint("CK_ApprovalRouteRules_Dates", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
                table.CheckConstraint("CK_ApprovalRouteRules_Duration", "([MinimumDurationMinutes] IS NULL OR [MinimumDurationMinutes] >= 0) AND ([MaximumDurationMinutes] IS NULL OR [MaximumDurationMinutes] >= 0) AND ([MinimumDurationMinutes] IS NULL OR [MaximumDurationMinutes] IS NULL OR [MaximumDurationMinutes] >= [MinimumDurationMinutes])");
                table.CheckConstraint("CK_ApprovalRouteRules_Version", "[Version] > 0 AND [Stage] > 0");
                table.ForeignKey(
                    name: "FK_ApprovalRouteRules_Departments_DepartmentId",
                    column: x => x.DepartmentId,
                    principalSchema: "hr",
                    principalTable: "Departments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ApprovalRouteRules_Sites_SiteId",
                    column: x => x.SiteId,
                    principalSchema: "attendance",
                    principalTable: "Sites",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ApprovalRouteRules_StaffCategories_StaffCategoryId",
                    column: x => x.StaffCategoryId,
                    principalSchema: "hr",
                    principalTable: "StaffCategories",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "AttendancePeriodLocks",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                PeriodEndExclusive = table.Column<DateOnly>(type: "date", nullable: false),
                IsLocked = table.Column<bool>(type: "bit", nullable: false),
                Revision = table.Column<int>(type: "int", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AttendancePeriodLocks", x => x.Id);
                table.CheckConstraint("CK_AttendancePeriodLocks_Dates", "[PeriodEndExclusive] > [PeriodStart]");
                table.CheckConstraint("CK_AttendancePeriodLocks_Revision", "[Revision] >= 0");
            });

        migrationBuilder.CreateTable(
            name: "AuditEvents",
            schema: "audit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ActorUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ActorRoleCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                TargetType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                TargetId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                EmployeeScopeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ProtectedChangesReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                DeviceReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditEvents", x => x.Id);
                table.ForeignKey(
                    name: "FK_AuditEvents_Employees_EmployeeScopeId",
                    column: x => x.EmployeeScopeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Cities",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Province = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Cities", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "EmployeeAssetCustodies",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AssetReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                IssuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                IssueCondition = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                IssueEvidencePrivateReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                RecordedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                ReturnedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ReturnCondition = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                ReturnEvidencePrivateReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                ReturnedToUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EmployeeAssetCustodies", x => x.Id);
                table.CheckConstraint("CK_EmployeeAssetCustodies_1", "[ReturnedAt] IS NULL OR [ReturnedAt] >= [IssuedAt]");
                table.CheckConstraint("CK_EmployeeAssetCustodies_2", "([ReturnedAt] IS NULL AND [ReturnCondition] IS NULL AND [ReturnedToUserId] IS NULL AND [ReturnEvidencePrivateReference] IS NULL) OR ([ReturnedAt] IS NOT NULL AND [ReturnCondition] IS NOT NULL AND [ReturnedToUserId] IS NOT NULL)");
                table.ForeignKey(
                    name: "FK_EmployeeAssetCustodies_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "EmployeeBankDetails",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Revision = table.Column<int>(type: "int", nullable: false),
                SupersedesEmployeeBankDetailsId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                PayloadSchemaVersion = table.Column<int>(type: "int", nullable: false),
                BankNameEncryptedReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                AccountTitleEncryptedReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                AccountNumberEncryptedReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                IbanEncryptedReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                EncryptionKeyReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                MaskedAccountIdentifier = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                IsVerified = table.Column<bool>(type: "bit", nullable: false),
                VerifiedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                VerifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                RecordedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EmployeeBankDetails", x => x.Id);
                table.UniqueConstraint("AK_EmployeeBankDetails_Id_EmployeeId", x => new { x.Id, x.EmployeeId });
                table.CheckConstraint("CK_EmployeeBankDetails_1", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
                table.CheckConstraint("CK_EmployeeBankDetails_2", "([IsVerified] = 0 AND [VerifiedByUserId] IS NULL AND [VerifiedAt] IS NULL) OR ([IsVerified] = 1 AND [VerifiedByUserId] IS NOT NULL AND [VerifiedAt] IS NOT NULL)");
                table.CheckConstraint("CK_EmployeeBankDetails_3", "[PayloadSchemaVersion] > 0");
                table.CheckConstraint("CK_EmployeeBankDetails_History", "([Revision] = 1 AND [SupersedesEmployeeBankDetailsId] IS NULL) OR ([Revision] > 1 AND [SupersedesEmployeeBankDetailsId] IS NOT NULL)");
                table.CheckConstraint("CK_EmployeeBankDetails_Predecessor", "[SupersedesEmployeeBankDetailsId] IS NULL OR [SupersedesEmployeeBankDetailsId] <> [Id]");
                table.CheckConstraint("CK_EmployeeBankDetails_Revision", "[Revision] > 0");
                table.ForeignKey(
                    name: "FK_EmployeeBankDetails_EmployeeBankDetails_SupersedesEmployeeBankDetailsId_EmployeeId",
                    columns: x => new { x.SupersedesEmployeeBankDetailsId, x.EmployeeId },
                    principalSchema: "hr",
                    principalTable: "EmployeeBankDetails",
                    principalColumns: new[] { "Id", "EmployeeId" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_EmployeeBankDetails_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "EmployeeCertifications",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CertificateType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                CertificateNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Issuer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                IssuedOn = table.Column<DateOnly>(type: "date", nullable: false),
                ExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                EmployeeDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RecordedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EmployeeCertifications", x => x.Id);
                table.CheckConstraint("CK_EmployeeCertifications_1", "[ExpiresOn] IS NULL OR [ExpiresOn] >= [IssuedOn]");
                table.ForeignKey(
                    name: "FK_EmployeeCertifications_EmployeeDocuments_EmployeeDocumentId_EmployeeId",
                    columns: x => new { x.EmployeeDocumentId, x.EmployeeId },
                    principalSchema: "hr",
                    principalTable: "EmployeeDocuments",
                    principalColumns: new[] { "Id", "EmployeeId" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_EmployeeCertifications_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "EmployeeCommuteDistances",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Revision = table.Column<int>(type: "int", nullable: false),
                SupersedesEmployeeCommuteDistanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UsualHomeOfficeKm = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                RoutePrivateReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                RecordedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EmployeeCommuteDistances", x => x.Id);
                table.UniqueConstraint("AK_EmployeeCommuteDistances_Id_EmployeeId", x => new { x.Id, x.EmployeeId });
                table.CheckConstraint("CK_EmployeeCommuteDistances_1", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
                table.CheckConstraint("CK_EmployeeCommuteDistances_2", "[UsualHomeOfficeKm] >= 0");
                table.CheckConstraint("CK_EmployeeCommuteDistances_History", "([Revision] = 1 AND [SupersedesEmployeeCommuteDistanceId] IS NULL) OR ([Revision] > 1 AND [SupersedesEmployeeCommuteDistanceId] IS NOT NULL)");
                table.CheckConstraint("CK_EmployeeCommuteDistances_Predecessor", "[SupersedesEmployeeCommuteDistanceId] IS NULL OR [SupersedesEmployeeCommuteDistanceId] <> [Id]");
                table.CheckConstraint("CK_EmployeeCommuteDistances_Revision", "[Revision] > 0");
                table.ForeignKey(
                    name: "FK_EmployeeCommuteDistances_EmployeeCommuteDistances_SupersedesEmployeeCommuteDistanceId_EmployeeId",
                    columns: x => new { x.SupersedesEmployeeCommuteDistanceId, x.EmployeeId },
                    principalSchema: "hr",
                    principalTable: "EmployeeCommuteDistances",
                    principalColumns: new[] { "Id", "EmployeeId" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_EmployeeCommuteDistances_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "EmployeeMedicalRecords",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EncryptedPayloadReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                EncryptionKeyReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                EffectiveOn = table.Column<DateOnly>(type: "date", nullable: false),
                EmployeeDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RecordedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EmployeeMedicalRecords", x => x.Id);
                table.ForeignKey(
                    name: "FK_EmployeeMedicalRecords_EmployeeDocuments_EmployeeDocumentId_EmployeeId",
                    columns: x => new { x.EmployeeDocumentId, x.EmployeeId },
                    principalSchema: "hr",
                    principalTable: "EmployeeDocuments",
                    principalColumns: new[] { "Id", "EmployeeId" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_EmployeeMedicalRecords_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "EmployeeSimAssignments",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Revision = table.Column<int>(type: "int", nullable: false),
                SupersedesEmployeeSimAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                PhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                SimReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                AllowedCreditTierCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                IssuedOn = table.Column<DateOnly>(type: "date", nullable: false),
                ReturnedOn = table.Column<DateOnly>(type: "date", nullable: true),
                RecordedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EmployeeSimAssignments", x => x.Id);
                table.UniqueConstraint("AK_EmployeeSimAssignments_Id_EmployeeId", x => new { x.Id, x.EmployeeId });
                table.CheckConstraint("CK_EmployeeSimAssignments_1", "[ReturnedOn] IS NULL OR [ReturnedOn] >= [IssuedOn]");
                table.CheckConstraint("CK_EmployeeSimAssignments_2", "[Status] BETWEEN 1 AND 3");
                table.CheckConstraint("CK_EmployeeSimAssignments_3", "([Status] = 3 AND [ReturnedOn] IS NOT NULL) OR ([Status] <> 3 AND [ReturnedOn] IS NULL)");
                table.CheckConstraint("CK_EmployeeSimAssignments_History", "([Revision] = 1 AND [SupersedesEmployeeSimAssignmentId] IS NULL) OR ([Revision] > 1 AND [SupersedesEmployeeSimAssignmentId] IS NOT NULL)");
                table.CheckConstraint("CK_EmployeeSimAssignments_Predecessor", "[SupersedesEmployeeSimAssignmentId] IS NULL OR [SupersedesEmployeeSimAssignmentId] <> [Id]");
                table.CheckConstraint("CK_EmployeeSimAssignments_Revision", "[Revision] > 0");
                table.ForeignKey(
                    name: "FK_EmployeeSimAssignments_EmployeeSimAssignments_SupersedesEmployeeSimAssignmentId_EmployeeId",
                    columns: x => new { x.SupersedesEmployeeSimAssignmentId, x.EmployeeId },
                    principalSchema: "hr",
                    principalTable: "EmployeeSimAssignments",
                    principalColumns: new[] { "Id", "EmployeeId" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_EmployeeSimAssignments_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "EmployeeSubmissions",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Version = table.Column<int>(type: "int", nullable: false),
                DraftRevision = table.Column<int>(type: "int", nullable: false),
                SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                SnapshotStorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                SnapshotObjectVersion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                SnapshotHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                EncryptionKeyReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                SnapshotSchemaVersion = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EmployeeSubmissions", x => x.Id);
                table.UniqueConstraint("AK_EmployeeSubmissions_EmployeeId_Id", x => new { x.EmployeeId, x.Id });
                table.CheckConstraint("CK_EmployeeSubmissions_Versions", "[Version] > 0 AND [DraftRevision] > 0 AND [SnapshotSchemaVersion] > 0");
                table.ForeignKey(
                    name: "FK_EmployeeSubmissions_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "FaceEnrollments",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReplacesEnrollmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                EncryptedTemplateReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                EncryptionKeyReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Provider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ProviderVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                EnrolledBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                DeviceReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                EnrolledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                RevokedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                RevocationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FaceEnrollments", x => x.Id);
                table.UniqueConstraint("AK_FaceEnrollments_EmployeeId_Id", x => new { x.EmployeeId, x.Id });
                table.CheckConstraint("CK_FaceEnrollments_EnrolledAtUtc_Utc", "[EnrolledAtUtc] IS NULL OR DATEPART(TZOFFSET, [EnrolledAtUtc]) = 0");
                table.CheckConstraint("CK_FaceEnrollments_Revocation", "([IsActive] = 1 AND [RevokedAtUtc] IS NULL AND [RevokedBy] IS NULL AND [RevocationReason] IS NULL) OR ([IsActive] = 0 AND [RevokedAtUtc] IS NOT NULL AND [RevokedAtUtc] >= [EnrolledAtUtc] AND [RevokedBy] IS NOT NULL AND [RevocationReason] IS NOT NULL)");
                table.CheckConstraint("CK_FaceEnrollments_RevokedAtUtc_Utc", "[RevokedAtUtc] IS NULL OR DATEPART(TZOFFSET, [RevokedAtUtc]) = 0");
                table.ForeignKey(
                    name: "FK_FaceEnrollments_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FaceEnrollments_FaceEnrollments_EmployeeId_ReplacesEnrollmentId",
                    columns: x => new { x.EmployeeId, x.ReplacesEnrollmentId },
                    principalSchema: "attendance",
                    principalTable: "FaceEnrollments",
                    principalColumns: new[] { "EmployeeId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "HolidayCalendars",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                GovernmentNotificationReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HolidayCalendars", x => x.Id);
                table.CheckConstraint("CK_HolidayCalendars_1", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
            });

        migrationBuilder.CreateTable(
            name: "IdempotencyReceipts",
            schema: "workflow",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ActorUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Operation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                RequestSha256 = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ResultCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ProtectedResultReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IdempotencyReceipts", x => x.Id);
                table.CheckConstraint("CK_IdempotencyReceipts_Expiry", "[ExpiresAt] > [CreatedAt]");
                table.CheckConstraint("CK_IdempotencyReceipts_Hash", "LEN([RequestSha256]) = 64 AND [RequestSha256] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
                table.CheckConstraint("CK_IdempotencyReceipts_Result", "([CompletedAt] IS NULL AND [ResultCode] IS NULL AND [ProtectedResultReference] IS NULL) OR ([CompletedAt] IS NOT NULL AND [ResultCode] IS NOT NULL AND [ProtectedResultReference] IS NOT NULL AND [CompletedAt] >= [CreatedAt] AND [CompletedAt] < [ExpiresAt])");
            });

        migrationBuilder.CreateTable(
            name: "LeaveTypes",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RequiresEvidence = table.Column<bool>(type: "bit", nullable: false),
                RequiresAdvanceApproval = table.Column<bool>(type: "bit", nullable: false),
                ReducesRegularBalance = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LeaveTypes", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Notifications",
            schema: "communication",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RecipientUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Channel = table.Column<int>(type: "int", nullable: false),
                TemplateCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ProtectedPayloadReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                DeliveredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ReadAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Notifications", x => x.Id);
                table.CheckConstraint("CK_Notifications_Channel", "[Channel] BETWEEN 1 AND 4");
                table.CheckConstraint("CK_Notifications_Times", "([DeliveredAt] IS NULL OR [DeliveredAt] >= [CreatedAt]) AND ([ReadAt] IS NULL OR [ReadAt] >= [CreatedAt])");
            });

        migrationBuilder.CreateTable(
            name: "OperationalZones",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OperationalZones", x => x.Id);
                table.ForeignKey(
                    name: "FK_OperationalZones_Sites_SiteId",
                    column: x => x.SiteId,
                    principalSchema: "attendance",
                    principalTable: "Sites",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "OutboxMessages",
            schema: "jobs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MessageType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                EncryptedPayloadReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                DeduplicationKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false, collation: "Latin1_General_100_BIN2"),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                AvailableAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                State = table.Column<int>(type: "int", nullable: false),
                Attempts = table.Column<int>(type: "int", nullable: false),
                LeaseToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LeaseExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                LastFailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                table.CheckConstraint("CK_OutboxMessages_Completed", "([State] = 2 AND [CompletedAt] IS NOT NULL AND [CompletedAt] >= [CreatedAt]) OR ([State] <> 2 AND [CompletedAt] IS NULL)");
                table.CheckConstraint("CK_OutboxMessages_Lease", "([State] = 1 AND [LeaseToken] IS NOT NULL AND [LeaseExpiresAt] IS NOT NULL) OR ([State] <> 1 AND [LeaseToken] IS NULL AND [LeaseExpiresAt] IS NULL)");
                table.CheckConstraint("CK_OutboxMessages_State", "[State] BETWEEN 0 AND 3 AND [Attempts] >= 0");
            });

        migrationBuilder.CreateTable(
            name: "Permissions",
            schema: "auth",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                IsSensitive = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Permissions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Policies",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Category = table.Column<int>(type: "int", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Policies", x => x.Id);
                table.CheckConstraint("CK_Policies_1", "[Category] BETWEEN 1 AND 5");
            });

        migrationBuilder.CreateTable(
            name: "PreviousEmployments",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RoleTitle = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                StartedOn = table.Column<DateOnly>(type: "date", nullable: false),
                EndedOn = table.Column<DateOnly>(type: "date", nullable: true),
                LeavingReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                LastSalaryEncryptedReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                RecordedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PreviousEmployments", x => x.Id);
                table.CheckConstraint("CK_PreviousEmployments_1", "[EndedOn] IS NULL OR [EndedOn] >= [StartedOn]");
                table.ForeignKey(
                    name: "FK_PreviousEmployments_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Projects",
            schema: "auth",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Projects", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Roles",
            schema: "auth",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Roles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "StoredFiles",
            schema: "files",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StorageKey = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                OriginalName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                Extension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                Sha256 = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                UploadedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                EncryptionKeyReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                RetainUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                SignatureVerified = table.Column<bool>(type: "bit", nullable: false),
                ScanState = table.Column<int>(type: "int", nullable: false),
                ScannerVersion = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                ScannedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                DeletedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StoredFiles", x => x.Id);
                table.CheckConstraint("CK_StoredFiles_Deleted", "([DeletedAt] IS NULL AND [DeletedByUserId] IS NULL) OR ([DeletedAt] IS NOT NULL AND [DeletedByUserId] IS NOT NULL AND [DeletedAt] >= [UploadedAt])");
                table.CheckConstraint("CK_StoredFiles_Hash", "LEN([Sha256]) = 64 AND [Sha256] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
                table.CheckConstraint("CK_StoredFiles_Retention", "[RetainUntil] IS NULL OR [RetainUntil] > [UploadedAt]");
                table.CheckConstraint("CK_StoredFiles_Scan", "[ScanState] BETWEEN 0 AND 3 AND ([ScanState] <> 1 OR [SignatureVerified] = 1)");
                table.CheckConstraint("CK_StoredFiles_Size", "[SizeBytes] > 0");
            });

        migrationBuilder.CreateTable(
            name: "Teams",
            schema: "auth",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Teams", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Vehicles",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RegistrationNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                VehicleType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                MakeModel = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                AssetReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Vehicles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ZoneTypes",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                DisplayColor = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                EntryRule = table.Column<int>(type: "int", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ZoneTypes", x => x.Id);
                table.CheckConstraint("CK_ZoneTypes_EntryRule", "[EntryRule] BETWEEN 1 AND 3");
            });

        migrationBuilder.CreateTable(
            name: "ApprovalRequests",
            schema: "workflow",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequestType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequesterUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                SnapshotPrivateReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                Stage = table.Column<int>(type: "int", nullable: false),
                RouteRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                State = table.Column<int>(type: "int", nullable: false),
                ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApprovalRequests", x => x.Id);
                table.CheckConstraint("CK_ApprovalRequests_Closed", "([State] = 0 AND [ClosedAt] IS NULL) OR ([State] <> 0 AND [ClosedAt] IS NOT NULL AND [ClosedAt] >= [RequestedAt])");
                table.CheckConstraint("CK_ApprovalRequests_State", "[State] BETWEEN 0 AND 4 AND [Stage] > 0");
                table.ForeignKey(
                    name: "FK_ApprovalRequests_ApprovalRouteRules_RouteRuleId",
                    column: x => x.RouteRuleId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRouteRules",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Holidays",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                HolidayCalendarId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                HolidayDate = table.Column<DateOnly>(type: "date", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                NotificationPrivateReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Holidays", x => x.Id);
                table.ForeignKey(
                    name: "FK_Holidays_HolidayCalendars_HolidayCalendarId",
                    column: x => x.HolidayCalendarId,
                    principalSchema: "hr",
                    principalTable: "HolidayCalendars",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ProjectMembers",
            schema: "auth",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AssignedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectMembers", x => x.Id);
                table.CheckConstraint("CK_ProjectMembers_Dates", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
                table.ForeignKey(
                    name: "FK_ProjectMembers_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ProjectMembers_Projects_ProjectId",
                    column: x => x.ProjectId,
                    principalSchema: "auth",
                    principalTable: "Projects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "EmployeeOnboardingDecisions",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Decision = table.Column<int>(type: "int", nullable: false),
                ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ActorRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EmployeeOnboardingDecisions", x => x.Id);
                table.UniqueConstraint("AK_EmployeeOnboardingDecisions_EmployeeId_Id", x => new { x.EmployeeId, x.Id });
                table.CheckConstraint("CK_EmployeeOnboardingDecisions_Kind", "[Decision] IN (1,2,3)");
                table.CheckConstraint("CK_EmployeeOnboardingDecisions_Reason", "[Decision] = 1 OR ([Reason] IS NOT NULL AND LEN(LTRIM(RTRIM([Reason]))) > 0)");
                table.ForeignKey(
                    name: "FK_EmployeeOnboardingDecisions_EmployeeSubmissions_EmployeeId_SubmissionId",
                    columns: x => new { x.EmployeeId, x.SubmissionId },
                    principalSchema: "hr",
                    principalTable: "EmployeeSubmissions",
                    principalColumns: new[] { "EmployeeId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_EmployeeOnboardingDecisions_Roles_ActorRoleId",
                    column: x => x.ActorRoleId,
                    principalSchema: "auth",
                    principalTable: "Roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "RolePermissions",
            schema: "auth",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                GrantedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                GrantedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RevokedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RolePermissions", x => x.Id);
                table.CheckConstraint("CK_RolePermissions_Revocation", "([RevokedAt] IS NULL AND [RevokedByUserId] IS NULL) OR ([RevokedAt] IS NOT NULL AND [RevokedAt] >= [GrantedAt] AND [RevokedByUserId] IS NOT NULL)");
                table.ForeignKey(
                    name: "FK_RolePermissions_Permissions_PermissionId",
                    column: x => x.PermissionId,
                    principalSchema: "auth",
                    principalTable: "Permissions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_RolePermissions_Roles_RoleId",
                    column: x => x.RoleId,
                    principalSchema: "auth",
                    principalTable: "Roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ReportExportJobs",
            schema: "reports",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequestedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ReportType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Format = table.Column<int>(type: "int", nullable: false),
                ProtectedFilterScopeReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                RequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                PeriodFrom = table.Column<DateOnly>(type: "date", nullable: true),
                PeriodTo = table.Column<DateOnly>(type: "date", nullable: true),
                State = table.Column<int>(type: "int", nullable: false),
                StoredFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                GeneratedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                DownloadExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                SourceLockReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReportExportJobs", x => x.Id);
                table.CheckConstraint("CK_ReportExportJobs_Completed", "[State] <> 2 OR ([StoredFileId] IS NOT NULL AND [GeneratedAt] IS NOT NULL AND [DownloadExpiresAt] IS NOT NULL AND [GeneratedAt] >= [RequestedAt] AND [DownloadExpiresAt] > [GeneratedAt])");
                table.CheckConstraint("CK_ReportExportJobs_Period", "([PeriodFrom] IS NULL AND [PeriodTo] IS NULL) OR ([PeriodFrom] IS NOT NULL AND [PeriodTo] IS NOT NULL AND [PeriodTo] >= [PeriodFrom])");
                table.CheckConstraint("CK_ReportExportJobs_State", "[State] BETWEEN 0 AND 3 AND [Format] BETWEEN 1 AND 3");
                table.ForeignKey(
                    name: "FK_ReportExportJobs_StoredFiles_StoredFileId",
                    column: x => x.StoredFileId,
                    principalSchema: "files",
                    principalTable: "StoredFiles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "TeamMembers",
            schema: "auth",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AssignedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TeamMembers", x => x.Id);
                table.CheckConstraint("CK_TeamMembers_Dates", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
                table.ForeignKey(
                    name: "FK_TeamMembers_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_TeamMembers_Teams_TeamId",
                    column: x => x.TeamId,
                    principalSchema: "auth",
                    principalTable: "Teams",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "InsurancePolicies",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ProviderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                PolicyNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                CoverageStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                ExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                StoredFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RecordedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                SupersedesPolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InsurancePolicies", x => x.Id);
                table.CheckConstraint("CK_InsurancePolicies_CoverageDates", "[ExpiryDate] >= [CoverageStartDate]");
                table.CheckConstraint("CK_InsurancePolicies_Owner", "([EmployeeId] IS NOT NULL AND [EmployeeId] <> '00000000-0000-0000-0000-000000000000' AND [VehicleId] IS NULL) OR ([VehicleId] IS NOT NULL AND [VehicleId] <> '00000000-0000-0000-0000-000000000000' AND [EmployeeId] IS NULL)");
                table.CheckConstraint("CK_InsurancePolicies_Predecessor", "[SupersedesPolicyId] IS NULL OR [SupersedesPolicyId] <> [Id]");
                table.CheckConstraint("CK_InsurancePolicies_RequiredText", "LEN(LTRIM(RTRIM([ProviderName]))) > 0 AND LEN(LTRIM(RTRIM([PolicyNumber]))) > 0 AND LEN(LTRIM(RTRIM([RecordedByUserId]))) > 0");
                table.ForeignKey(
                    name: "FK_InsurancePolicies_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_InsurancePolicies_InsurancePolicies_SupersedesPolicyId",
                    column: x => x.SupersedesPolicyId,
                    principalSchema: "hr",
                    principalTable: "InsurancePolicies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_InsurancePolicies_StoredFiles_StoredFileId",
                    column: x => x.StoredFileId,
                    principalSchema: "files",
                    principalTable: "StoredFiles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_InsurancePolicies_Vehicles_VehicleId",
                    column: x => x.VehicleId,
                    principalSchema: "hr",
                    principalTable: "Vehicles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "VehicleCustodies",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                IssuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                IssueOdometerKm = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                IssueConditionPrivateReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                RecordedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                ReturnedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ReturnOdometerKm = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: true),
                ReturnConditionPrivateReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                ReturnedToUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VehicleCustodies", x => x.Id);
                table.CheckConstraint("CK_VehicleCustodies_1", "[IssueOdometerKm] >= 0");
                table.CheckConstraint("CK_VehicleCustodies_2", "[ReturnedAt] IS NULL OR ([ReturnedAt] >= [IssuedAt] AND [ReturnOdometerKm] >= [IssueOdometerKm])");
                table.CheckConstraint("CK_VehicleCustodies_3", "([ReturnedAt] IS NULL AND [ReturnOdometerKm] IS NULL AND [ReturnConditionPrivateReference] IS NULL AND [ReturnedToUserId] IS NULL) OR ([ReturnedAt] IS NOT NULL AND [ReturnOdometerKm] IS NOT NULL AND [ReturnConditionPrivateReference] IS NOT NULL AND [ReturnedToUserId] IS NOT NULL)");
                table.ForeignKey(
                    name: "FK_VehicleCustodies_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_VehicleCustodies_Vehicles_VehicleId",
                    column: x => x.VehicleId,
                    principalSchema: "hr",
                    principalTable: "Vehicles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "AdvanceLoanRequests",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Kind = table.Column<int>(type: "int", nullable: false),
                RequestedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                RequestedOn = table.Column<DateOnly>(type: "date", nullable: false),
                RequestedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AdvanceLoanRequests", x => x.Id);
                table.UniqueConstraint("AK_AdvanceLoanRequests_Id_Currency", x => new { x.Id, x.Currency });
                table.CheckConstraint("CK_AdvanceLoanRequests_1", "[RequestedAmount] > 0");
                table.CheckConstraint("CK_AdvanceLoanRequests_2", "[Kind] BETWEEN 1 AND 2");
                table.CheckConstraint("CK_AdvanceLoanRequests_3", "([SubmittedAt] IS NULL AND [ApprovalRequestId] IS NULL) OR ([SubmittedAt] IS NOT NULL AND [SubmittedAt] >= [CreatedAt] AND [ApprovalRequestId] IS NOT NULL)");
                table.CheckConstraint("CK_AdvanceLoanRequests_4", "[Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')");
                table.ForeignKey(
                    name: "FK_AdvanceLoanRequests_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_AdvanceLoanRequests_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ApprovalDecisions",
            schema: "workflow",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Decision = table.Column<int>(type: "int", nullable: false),
                ActorUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ActorRoleCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ProtectedChangeReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApprovalDecisions", x => x.Id);
                table.CheckConstraint("CK_ApprovalDecisions_Decision", "[Decision] IN (1,2,3)");
                table.CheckConstraint("CK_ApprovalDecisions_Reason", "[Decision] = 1 OR ([Reason] IS NOT NULL AND LEN(LTRIM(RTRIM([Reason]))) > 0)");
                table.ForeignKey(
                    name: "FK_ApprovalDecisions_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "AttendancePeriodLockEvents",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AttendancePeriodLockId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Revision = table.Column<int>(type: "int", nullable: false),
                IsLocked = table.Column<bool>(type: "bit", nullable: false),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                AtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AttendancePeriodLockEvents", x => x.Id);
                table.CheckConstraint("CK_AttendancePeriodLockEvents_AtUtc_Utc", "[AtUtc] IS NULL OR DATEPART(TZOFFSET, [AtUtc]) = 0");
                table.CheckConstraint("CK_AttendancePeriodLockEvents_Revision", "[Revision] > 0");
                table.ForeignKey(
                    name: "FK_AttendancePeriodLockEvents_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_AttendancePeriodLockEvents_AttendancePeriodLocks_AttendancePeriodLockId",
                    column: x => x.AttendancePeriodLockId,
                    principalSchema: "attendance",
                    principalTable: "AttendancePeriodLocks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "CompensationRecords",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Revision = table.Column<int>(type: "int", nullable: false),
                SupersedesCompensationRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                BaseAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                GrossAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                WageBasis = table.Column<int>(type: "int", nullable: false),
                PaymentMethod = table.Column<int>(type: "int", nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RecordedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CompensationRecords", x => x.Id);
                table.UniqueConstraint("AK_CompensationRecords_Id_EmployeeId", x => new { x.Id, x.EmployeeId });
                table.CheckConstraint("CK_CompensationRecords_1", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
                table.CheckConstraint("CK_CompensationRecords_2", "[BaseAmount] >= 0 AND [GrossAmount] >= [BaseAmount]");
                table.CheckConstraint("CK_CompensationRecords_3", "[WageBasis] BETWEEN 1 AND 3");
                table.CheckConstraint("CK_CompensationRecords_4", "[PaymentMethod] BETWEEN 1 AND 4");
                table.CheckConstraint("CK_CompensationRecords_5", "[Revision] > 0");
                table.CheckConstraint("CK_CompensationRecords_6", "[SupersedesCompensationRecordId] IS NULL OR [SupersedesCompensationRecordId] <> [Id]");
                table.CheckConstraint("CK_CompensationRecords_7", "[Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')");
                table.ForeignKey(
                    name: "FK_CompensationRecords_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_CompensationRecords_CompensationRecords_SupersedesCompensationRecordId_EmployeeId",
                    columns: x => new { x.SupersedesCompensationRecordId, x.EmployeeId },
                    principalSchema: "hr",
                    principalTable: "CompensationRecords",
                    principalColumns: new[] { "Id", "EmployeeId" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_CompensationRecords_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "FaceVerificationExceptions",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AlternativeMethodCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                PrivateEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                EffectiveTo = table.Column<DateOnly>(type: "date", nullable: false),
                RequestedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RequestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FaceVerificationExceptions", x => x.Id);
                table.CheckConstraint("CK_FaceVerificationExceptions_Dates", "[EffectiveTo] > [EffectiveFrom]");
                table.CheckConstraint("CK_FaceVerificationExceptions_RequestedAtUtc_Utc", "[RequestedAtUtc] IS NULL OR DATEPART(TZOFFSET, [RequestedAtUtc]) = 0");
                table.ForeignKey(
                    name: "FK_FaceVerificationExceptions_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FaceVerificationExceptions_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "PayrollHandoffBatches",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BatchReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                PreparedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                PreparedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                LinesRevision = table.Column<int>(type: "int", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                LockedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                LockedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PayrollHandoffBatches", x => x.Id);
                table.UniqueConstraint("AK_PayrollHandoffBatches_Id_Currency", x => new { x.Id, x.Currency });
                table.CheckConstraint("CK_PayrollHandoffBatches_1", "[PeriodEnd] >= [PeriodStart]");
                table.CheckConstraint("CK_PayrollHandoffBatches_2", "([LockedAt] IS NULL AND [LockedByUserId] IS NULL AND [ApprovalRequestId] IS NULL) OR ([LockedAt] IS NOT NULL AND [LockedAt] >= [PreparedAt] AND [LockedByUserId] IS NOT NULL AND [ApprovalRequestId] IS NOT NULL)");
                table.CheckConstraint("CK_PayrollHandoffBatches_3", "[Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')");
                table.CheckConstraint("CK_PayrollHandoffBatches_Revision", "[LinesRevision] >= 0");
                table.ForeignKey(
                    name: "FK_PayrollHandoffBatches_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "PolicyVersions",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VersionNumber = table.Column<int>(type: "int", nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                RulesSchemaVersion = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                RulesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: false),
                PolicyDocumentPrivateReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PublishedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PolicyVersions", x => x.Id);
                table.CheckConstraint("CK_PolicyVersions_1", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
                table.CheckConstraint("CK_PolicyVersions_2", "[VersionNumber] > 0");
                table.CheckConstraint("CK_PolicyVersions_3", "ISJSON([RulesJson]) = 1");
                table.ForeignKey(
                    name: "FK_PolicyVersions_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PolicyVersions_Policies_PolicyId",
                    column: x => x.PolicyId,
                    principalSchema: "hr",
                    principalTable: "Policies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ShiftPopulationAssignments",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ShiftId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PopulationKind = table.Column<int>(type: "int", nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                StaffCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ShiftPopulationAssignments", x => x.Id);
                table.CheckConstraint("CK_ShiftPopulationAssignments_Dates", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
                table.CheckConstraint("CK_ShiftPopulationAssignments_Population", "([PopulationKind] = 1 AND [TeamId] IS NOT NULL AND [SiteId] IS NULL AND [StaffCategoryId] IS NULL AND [DepartmentId] IS NULL) OR ([PopulationKind] = 2 AND [TeamId] IS NULL AND [SiteId] IS NOT NULL AND [StaffCategoryId] IS NULL AND [DepartmentId] IS NULL) OR ([PopulationKind] = 3 AND [TeamId] IS NULL AND [SiteId] IS NULL AND [StaffCategoryId] IS NOT NULL AND [DepartmentId] IS NULL) OR ([PopulationKind] = 4 AND [TeamId] IS NULL AND [SiteId] IS NULL AND [StaffCategoryId] IS NULL AND [DepartmentId] IS NOT NULL)");
                table.ForeignKey(
                    name: "FK_ShiftPopulationAssignments_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ShiftPopulationAssignments_Departments_DepartmentId",
                    column: x => x.DepartmentId,
                    principalSchema: "hr",
                    principalTable: "Departments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ShiftPopulationAssignments_Shifts_ShiftId",
                    column: x => x.ShiftId,
                    principalSchema: "attendance",
                    principalTable: "Shifts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ShiftPopulationAssignments_Sites_SiteId",
                    column: x => x.SiteId,
                    principalSchema: "attendance",
                    principalTable: "Sites",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ShiftPopulationAssignments_StaffCategories_StaffCategoryId",
                    column: x => x.StaffCategoryId,
                    principalSchema: "hr",
                    principalTable: "StaffCategories",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ShiftPopulationAssignments_Teams_TeamId",
                    column: x => x.TeamId,
                    principalSchema: "auth",
                    principalTable: "Teams",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "TemporaryShiftChanges",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AttendanceDate = table.Column<DateOnly>(type: "date", nullable: false),
                OriginalShiftVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReplacementShiftVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ApprovedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ApprovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                WrittenEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TemporaryShiftChanges", x => x.Id);
                table.CheckConstraint("CK_TemporaryShiftChanges_ApprovedAtUtc_Utc", "[ApprovedAtUtc] IS NULL OR DATEPART(TZOFFSET, [ApprovedAtUtc]) = 0");
                table.CheckConstraint("CK_TemporaryShiftChanges_Versions", "[OriginalShiftVersionId] <> [ReplacementShiftVersionId]");
                table.ForeignKey(
                    name: "FK_TemporaryShiftChanges_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_TemporaryShiftChanges_ShiftVersions_OriginalShiftVersionId",
                    column: x => x.OriginalShiftVersionId,
                    principalSchema: "attendance",
                    principalTable: "ShiftVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_TemporaryShiftChanges_ShiftVersions_ReplacementShiftVersionId",
                    column: x => x.ReplacementShiftVersionId,
                    principalSchema: "attendance",
                    principalTable: "ShiftVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_TemporaryShiftChanges_Sites_SiteId",
                    column: x => x.SiteId,
                    principalSchema: "attendance",
                    principalTable: "Sites",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "VehicleTrips",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Purpose = table.Column<int>(type: "int", nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                EndedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                OriginLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                DestinationLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                OdometerStartKm = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                OdometerEndKm = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                MapDistanceKm = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                RouteEvidencePrivateReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                WorkReference = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                RequestedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VehicleTrips", x => x.Id);
                table.UniqueConstraint("AK_VehicleTrips_Id_EmployeeId", x => new { x.Id, x.EmployeeId });
                table.UniqueConstraint("AK_VehicleTrips_Id_EmployeeId_VehicleId", x => new { x.Id, x.EmployeeId, x.VehicleId });
                table.CheckConstraint("CK_VehicleTrips_1", "[EndedAt] >= [StartedAt]");
                table.CheckConstraint("CK_VehicleTrips_2", "[OdometerEndKm] >= [OdometerStartKm] AND [OdometerStartKm] >= 0 AND [MapDistanceKm] >= 0");
                table.CheckConstraint("CK_VehicleTrips_3", "[Purpose] BETWEEN 1 AND 5");
                table.CheckConstraint("CK_VehicleTrips_4", "([SubmittedAt] IS NULL AND [ApprovalRequestId] IS NULL) OR ([SubmittedAt] IS NOT NULL AND [SubmittedAt] >= [CreatedAt] AND [ApprovalRequestId] IS NOT NULL)");
                table.ForeignKey(
                    name: "FK_VehicleTrips_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_VehicleTrips_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_VehicleTrips_Vehicles_VehicleId",
                    column: x => x.VehicleId,
                    principalSchema: "hr",
                    principalTable: "Vehicles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ZoneVersions",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OperationalZoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VersionNumber = table.Column<int>(type: "int", nullable: false),
                ZoneTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EntryRule = table.Column<int>(type: "int", nullable: false),
                BoundaryKind = table.Column<int>(type: "int", nullable: false),
                EffectiveFromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                EffectiveToUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                EmployeeInstruction = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                ReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                PrivateRestrictionReasonReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                ApprovedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ApprovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Latitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                Longitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                RadiusMeters = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                PolygonGeoJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ZoneVersions", x => x.Id);
                table.CheckConstraint("CK_ZoneVersions_ApprovedAtUtc_Utc", "[ApprovedAtUtc] IS NULL OR DATEPART(TZOFFSET, [ApprovedAtUtc]) = 0");
                table.CheckConstraint("CK_ZoneVersions_Boundary", "([BoundaryKind] = 1 AND [Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [RadiusMeters] IS NOT NULL AND [Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180 AND [RadiusMeters] > 0 AND [PolygonGeoJson] IS NULL) OR ([BoundaryKind] = 2 AND [Latitude] IS NULL AND [Longitude] IS NULL AND [RadiusMeters] IS NULL AND [PolygonGeoJson] IS NOT NULL AND ISJSON([PolygonGeoJson]) = 1)");
                table.CheckConstraint("CK_ZoneVersions_EffectiveFromUtc_Utc", "[EffectiveFromUtc] IS NULL OR DATEPART(TZOFFSET, [EffectiveFromUtc]) = 0");
                table.CheckConstraint("CK_ZoneVersions_EffectiveToUtc_Utc", "[EffectiveToUtc] IS NULL OR DATEPART(TZOFFSET, [EffectiveToUtc]) = 0");
                table.CheckConstraint("CK_ZoneVersions_EntryRule", "[EntryRule] BETWEEN 1 AND 3");
                table.CheckConstraint("CK_ZoneVersions_Version", "[VersionNumber] > 0");
                table.CheckConstraint("CK_ZoneVersions_Window", "[EffectiveToUtc] > [EffectiveFromUtc]");
                table.ForeignKey(
                    name: "FK_ZoneVersions_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ZoneVersions_OperationalZones_OperationalZoneId",
                    column: x => x.OperationalZoneId,
                    principalSchema: "attendance",
                    principalTable: "OperationalZones",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ZoneVersions_ZoneTypes_ZoneTypeId",
                    column: x => x.ZoneTypeId,
                    principalSchema: "attendance",
                    principalTable: "ZoneTypes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "InsuranceExpiryReminders",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                InsurancePolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RecipientUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Channel = table.Column<int>(type: "int", nullable: false),
                RemindAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                NotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CancelledByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                CancellationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InsuranceExpiryReminders", x => x.Id);
                table.CheckConstraint("CK_InsuranceExpiryReminders_Channel", "[Channel] BETWEEN 1 AND 4");
                table.CheckConstraint("CK_InsuranceExpiryReminders_RequiredText", "LEN(LTRIM(RTRIM([RecipientUserId]))) > 0 AND LEN(LTRIM(RTRIM([CreatedByUserId]))) > 0");
                table.CheckConstraint("CK_InsuranceExpiryReminders_State", "([CancelledAt] IS NULL AND [CancelledByUserId] IS NULL AND [CancellationReason] IS NULL) OR ([CancelledAt] IS NOT NULL AND [CancelledByUserId] IS NOT NULL AND [CancellationReason] IS NOT NULL AND LEN(LTRIM(RTRIM([CancelledByUserId]))) > 0 AND LEN(LTRIM(RTRIM([CancellationReason]))) > 0 AND [NotificationId] IS NULL)");
                table.CheckConstraint("CK_InsuranceExpiryReminders_Times", "[RemindAt] >= [CreatedAt] AND ([CancelledAt] IS NULL OR [CancelledAt] >= [CreatedAt])");
                table.ForeignKey(
                    name: "FK_InsuranceExpiryReminders_InsurancePolicies_InsurancePolicyId",
                    column: x => x.InsurancePolicyId,
                    principalSchema: "hr",
                    principalTable: "InsurancePolicies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_InsuranceExpiryReminders_Notifications_NotificationId",
                    column: x => x.NotificationId,
                    principalSchema: "communication",
                    principalTable: "Notifications",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "VehicleIncidents",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VehicleCustodyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                IncidentEvidencePrivateReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                ThirdPartyDetailsEncryptedReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                InsuranceClaimReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                ProposedEmployeeLiability = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                RequestedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VehicleIncidents", x => x.Id);
                table.CheckConstraint("CK_VehicleIncidents_1", "([ProposedEmployeeLiability] IS NULL AND [Currency] IS NULL) OR ([ProposedEmployeeLiability] IS NOT NULL AND [ProposedEmployeeLiability] >= 0 AND [Currency] IS NOT NULL)");
                table.CheckConstraint("CK_VehicleIncidents_2", "([SubmittedAt] IS NULL AND [ApprovalRequestId] IS NULL) OR ([SubmittedAt] IS NOT NULL AND [SubmittedAt] >= [CreatedAt] AND [ApprovalRequestId] IS NOT NULL)");
                table.CheckConstraint("CK_VehicleIncidents_3", "[Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')");
                table.ForeignKey(
                    name: "FK_VehicleIncidents_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_VehicleIncidents_VehicleCustodies_VehicleCustodyId",
                    column: x => x.VehicleCustodyId,
                    principalSchema: "hr",
                    principalTable: "VehicleCustodies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "AdvanceLoanAgreements",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AdvanceLoanRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ApprovedPrincipal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                EffectiveOn = table.Column<DateOnly>(type: "date", nullable: false),
                ApprovedTermsPrivateReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RecordedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AdvanceLoanAgreements", x => x.Id);
                table.UniqueConstraint("AK_AdvanceLoanAgreements_Id_Currency", x => new { x.Id, x.Currency });
                table.CheckConstraint("CK_AdvanceLoanAgreements_1", "[ApprovedPrincipal] > 0");
                table.CheckConstraint("CK_AdvanceLoanAgreements_2", "[Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')");
                table.ForeignKey(
                    name: "FK_AdvanceLoanAgreements_AdvanceLoanRequests_AdvanceLoanRequestId_Currency",
                    columns: x => new { x.AdvanceLoanRequestId, x.Currency },
                    principalSchema: "hr",
                    principalTable: "AdvanceLoanRequests",
                    principalColumns: new[] { "Id", "Currency" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_AdvanceLoanAgreements_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "PayrollHandoffProcessingRecords",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PayrollHandoffBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ExternalProcessingReference = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                ProtectedResultReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                ProcessedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PayrollHandoffProcessingRecords", x => x.Id);
                table.ForeignKey(
                    name: "FK_PayrollHandoffProcessingRecords_PayrollHandoffBatches_PayrollHandoffBatchId",
                    column: x => x.PayrollHandoffBatchId,
                    principalSchema: "hr",
                    principalTable: "PayrollHandoffBatches",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "DailyAttendanceSummaries",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AttendanceDate = table.Column<DateOnly>(type: "date", nullable: false),
                Revision = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                PolicyVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ShiftVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CalculationVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                InputFingerprint = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                ScheduledMinutes = table.Column<int>(type: "int", nullable: false),
                WorkedMinutes = table.Column<int>(type: "int", nullable: false),
                PayableMinutes = table.Column<int>(type: "int", nullable: false),
                LateMinutes = table.Column<int>(type: "int", nullable: false),
                OvertimeMinutes = table.Column<int>(type: "int", nullable: false),
                ExceptionCount = table.Column<int>(type: "int", nullable: false),
                CalculatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                SupersedesSummaryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DailyAttendanceSummaries", x => x.Id);
                table.UniqueConstraint("AK_DailyAttendanceSummaries_EmployeeId_AttendanceDate_Id", x => new { x.EmployeeId, x.AttendanceDate, x.Id });
                table.UniqueConstraint("AK_DailyAttendanceSummaries_EmployeeId_Id", x => new { x.EmployeeId, x.Id });
                table.CheckConstraint("CK_DailyAttendanceSummaries_CalculatedAtUtc_Utc", "[CalculatedAtUtc] IS NULL OR DATEPART(TZOFFSET, [CalculatedAtUtc]) = 0");
                table.CheckConstraint("CK_DailyAttendanceSummaries_Fingerprint", "LEN([InputFingerprint]) = 64 AND [InputFingerprint] NOT LIKE '%[^0-9A-F]%'");
                table.CheckConstraint("CK_DailyAttendanceSummaries_Minutes", "[ScheduledMinutes] >= 0 AND [WorkedMinutes] >= 0 AND [PayableMinutes] >= 0 AND [LateMinutes] >= 0 AND [OvertimeMinutes] >= 0 AND [ExceptionCount] >= 0");
                table.CheckConstraint("CK_DailyAttendanceSummaries_Revision", "[Revision] > 0");
                table.CheckConstraint("CK_DailyAttendanceSummaries_Status", "[Status] BETWEEN 1 AND 9");
                table.ForeignKey(
                    name: "FK_DailyAttendanceSummaries_DailyAttendanceSummaries_EmployeeId_AttendanceDate_SupersedesSummaryId",
                    columns: x => new { x.EmployeeId, x.AttendanceDate, x.SupersedesSummaryId },
                    principalSchema: "attendance",
                    principalTable: "DailyAttendanceSummaries",
                    principalColumns: new[] { "EmployeeId", "AttendanceDate", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_DailyAttendanceSummaries_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_DailyAttendanceSummaries_PolicyVersions_PolicyVersionId",
                    column: x => x.PolicyVersionId,
                    principalSchema: "hr",
                    principalTable: "PolicyVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_DailyAttendanceSummaries_ShiftVersions_ShiftVersionId",
                    column: x => x.ShiftVersionId,
                    principalSchema: "attendance",
                    principalTable: "ShiftVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "LeaveEncashmentRequests",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LeaveYearStart = table.Column<DateOnly>(type: "date", nullable: false),
                LeaveYearEnd = table.Column<DateOnly>(type: "date", nullable: false),
                RequestedMinutes = table.Column<int>(type: "int", nullable: false),
                AssessedKpiPercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                ProposedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                PolicyVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CalculationPrivateReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                RequestedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LeaveEncashmentRequests", x => x.Id);
                table.CheckConstraint("CK_LeaveEncashmentRequests_1", "[LeaveYearEnd] > [LeaveYearStart]");
                table.CheckConstraint("CK_LeaveEncashmentRequests_2", "[RequestedMinutes] > 0");
                table.CheckConstraint("CK_LeaveEncashmentRequests_3", "[AssessedKpiPercent] BETWEEN 0 AND 100");
                table.CheckConstraint("CK_LeaveEncashmentRequests_4", "[ProposedAmount] >= 0");
                table.CheckConstraint("CK_LeaveEncashmentRequests_5", "([SubmittedAt] IS NULL AND [ApprovalRequestId] IS NULL) OR ([SubmittedAt] IS NOT NULL AND [SubmittedAt] >= [CreatedAt] AND [ApprovalRequestId] IS NOT NULL)");
                table.CheckConstraint("CK_LeaveEncashmentRequests_6", "[Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')");
                table.ForeignKey(
                    name: "FK_LeaveEncashmentRequests_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_LeaveEncashmentRequests_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_LeaveEncashmentRequests_PolicyVersions_PolicyVersionId",
                    column: x => x.PolicyVersionId,
                    principalSchema: "hr",
                    principalTable: "PolicyVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "LeaveRequests",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LeaveTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                RequestedMinutes = table.Column<int>(type: "int", nullable: false),
                Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                ContactDuringLeave = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                RelieverEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                HandoverNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                EvidencePrivateReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                PayTreatment = table.Column<int>(type: "int", nullable: false),
                PolicyVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequestedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LeaveRequests", x => x.Id);
                table.UniqueConstraint("AK_LeaveRequests_Id_EmployeeId_LeaveTypeId", x => new { x.Id, x.EmployeeId, x.LeaveTypeId });
                table.CheckConstraint("CK_LeaveRequests_1", "[EndDate] >= [StartDate]");
                table.CheckConstraint("CK_LeaveRequests_2", "[RequestedMinutes] > 0");
                table.CheckConstraint("CK_LeaveRequests_3", "[PayTreatment] BETWEEN 0 AND 3");
                table.CheckConstraint("CK_LeaveRequests_4", "[RelieverEmployeeId] IS NULL OR [RelieverEmployeeId] <> [EmployeeId]");
                table.CheckConstraint("CK_LeaveRequests_5", "([SubmittedAt] IS NULL AND [ApprovalRequestId] IS NULL) OR ([SubmittedAt] IS NOT NULL AND [SubmittedAt] >= [CreatedAt] AND [ApprovalRequestId] IS NOT NULL)");
                table.ForeignKey(
                    name: "FK_LeaveRequests_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_LeaveRequests_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_LeaveRequests_Employees_RelieverEmployeeId",
                    column: x => x.RelieverEmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_LeaveRequests_LeaveTypes_LeaveTypeId",
                    column: x => x.LeaveTypeId,
                    principalSchema: "hr",
                    principalTable: "LeaveTypes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_LeaveRequests_PolicyVersions_PolicyVersionId",
                    column: x => x.PolicyVersionId,
                    principalSchema: "hr",
                    principalTable: "PolicyVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "PayrollHandoffLines",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PayrollHandoffBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Component = table.Column<int>(type: "int", nullable: false),
                SourceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SourceVersion = table.Column<int>(type: "int", nullable: false),
                AttendancePeriodLockEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                PolicyVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CalculationPrivateReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                TraceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PayrollHandoffLines", x => x.Id);
                table.CheckConstraint("CK_PayrollHandoffLines_1", "[Component] BETWEEN 1 AND 8");
                table.CheckConstraint("CK_PayrollHandoffLines_2", "[SourceVersion] > 0");
                table.CheckConstraint("CK_PayrollHandoffLines_3", "[Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')");
                table.ForeignKey(
                    name: "FK_PayrollHandoffLines_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PayrollHandoffLines_AttendancePeriodLockEvents_AttendancePeriodLockEventId",
                    column: x => x.AttendancePeriodLockEventId,
                    principalSchema: "attendance",
                    principalTable: "AttendancePeriodLockEvents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PayrollHandoffLines_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PayrollHandoffLines_PayrollHandoffBatches_PayrollHandoffBatchId_Currency",
                    columns: x => new { x.PayrollHandoffBatchId, x.Currency },
                    principalSchema: "hr",
                    principalTable: "PayrollHandoffBatches",
                    principalColumns: new[] { "Id", "Currency" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PayrollHandoffLines_PolicyVersions_PolicyVersionId",
                    column: x => x.PolicyVersionId,
                    principalSchema: "hr",
                    principalTable: "PolicyVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "PolicyAssignments",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PolicyVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                StaffCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                InstructionPrivateReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RecordedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PolicyAssignments", x => x.Id);
                table.CheckConstraint("CK_PolicyAssignments_1", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
                table.CheckConstraint("CK_PolicyAssignments_2", "(CASE WHEN [RoleId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [EmployeeId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [DepartmentId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [StaffCategoryId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [SiteId] IS NULL THEN 0 ELSE 1 END) = 1");
                table.ForeignKey(
                    name: "FK_PolicyAssignments_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PolicyAssignments_Departments_DepartmentId",
                    column: x => x.DepartmentId,
                    principalSchema: "hr",
                    principalTable: "Departments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PolicyAssignments_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PolicyAssignments_PolicyVersions_PolicyVersionId",
                    column: x => x.PolicyVersionId,
                    principalSchema: "hr",
                    principalTable: "PolicyVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PolicyAssignments_Roles_RoleId",
                    column: x => x.RoleId,
                    principalSchema: "auth",
                    principalTable: "Roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PolicyAssignments_Sites_SiteId",
                    column: x => x.SiteId,
                    principalSchema: "attendance",
                    principalTable: "Sites",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PolicyAssignments_StaffCategories_StaffCategoryId",
                    column: x => x.StaffCategoryId,
                    principalSchema: "hr",
                    principalTable: "StaffCategories",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "PolicyImpactAssessments",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Kind = table.Column<int>(type: "int", nullable: false),
                PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                AffectedMinutes = table.Column<int>(type: "int", nullable: false),
                ProposedMoneyImpact = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                ProposedKpiPercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                PolicyVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CalculationVersion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                ProtectedCalculationReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RecordedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PolicyImpactAssessments", x => x.Id);
                table.UniqueConstraint("AK_PolicyImpactAssessments_Id_EmployeeId", x => new { x.Id, x.EmployeeId });
                table.CheckConstraint("CK_PolicyImpactAssessments_1", "[PeriodEnd] >= [PeriodStart]");
                table.CheckConstraint("CK_PolicyImpactAssessments_2", "[AffectedMinutes] >= 0");
                table.CheckConstraint("CK_PolicyImpactAssessments_3", "[Kind] BETWEEN 1 AND 6");
                table.CheckConstraint("CK_PolicyImpactAssessments_4", "[ProposedKpiPercent] IS NULL OR [ProposedKpiPercent] BETWEEN 0 AND 100");
                table.CheckConstraint("CK_PolicyImpactAssessments_5", "([ProposedMoneyImpact] IS NULL AND [Currency] IS NULL) OR ([ProposedMoneyImpact] IS NOT NULL AND [ProposedMoneyImpact] >= 0 AND [Currency] IS NOT NULL)");
                table.CheckConstraint("CK_PolicyImpactAssessments_6", "[Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')");
                table.ForeignKey(
                    name: "FK_PolicyImpactAssessments_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PolicyImpactAssessments_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PolicyImpactAssessments_PolicyVersions_PolicyVersionId",
                    column: x => x.PolicyVersionId,
                    principalSchema: "hr",
                    principalTable: "PolicyVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "VehicleServiceRecords",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ServiceKind = table.Column<int>(type: "int", nullable: false),
                ServiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                OdometerKm = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                ApprovedOutstationKm = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                AuthorizedWorkshopReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                EvidencePrivateReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                ClaimedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                PolicyVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EligibilityCalculationPrivateReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                RequestedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VehicleServiceRecords", x => x.Id);
                table.CheckConstraint("CK_VehicleServiceRecords_1", "[OdometerKm] >= 0 AND [ApprovedOutstationKm] >= 0 AND [ClaimedAmount] >= 0");
                table.CheckConstraint("CK_VehicleServiceRecords_2", "[ServiceKind] BETWEEN 1 AND 3");
                table.CheckConstraint("CK_VehicleServiceRecords_3", "([SubmittedAt] IS NULL AND [ApprovalRequestId] IS NULL) OR ([SubmittedAt] IS NOT NULL AND [SubmittedAt] >= [CreatedAt] AND [ApprovalRequestId] IS NOT NULL)");
                table.CheckConstraint("CK_VehicleServiceRecords_4", "[Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')");
                table.ForeignKey(
                    name: "FK_VehicleServiceRecords_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_VehicleServiceRecords_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_VehicleServiceRecords_PolicyVersions_PolicyVersionId",
                    column: x => x.PolicyVersionId,
                    principalSchema: "hr",
                    principalTable: "PolicyVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_VehicleServiceRecords_Vehicles_VehicleId",
                    column: x => x.VehicleId,
                    principalSchema: "hr",
                    principalTable: "Vehicles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "WaitingTimeEvidences",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DelayedByEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                WaitingStartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                WaitingEndedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                VerifiedWaitingMinutes = table.Column<int>(type: "int", nullable: false),
                WorkPairReference = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                EvidencePrivateReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                VerifiedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                VerifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                PolicyVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WaitingTimeEvidences", x => x.Id);
                table.CheckConstraint("CK_WaitingTimeEvidences_1", "[WaitingEndedAt] > [WaitingStartedAt]");
                table.CheckConstraint("CK_WaitingTimeEvidences_2", "[VerifiedWaitingMinutes] >= 0 AND [VerifiedWaitingMinutes] <= DATEDIFF(MINUTE, [WaitingStartedAt], [WaitingEndedAt])");
                table.CheckConstraint("CK_WaitingTimeEvidences_3", "[EmployeeId] <> [DelayedByEmployeeId]");
                table.ForeignKey(
                    name: "FK_WaitingTimeEvidences_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_WaitingTimeEvidences_Employees_DelayedByEmployeeId",
                    column: x => x.DelayedByEmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_WaitingTimeEvidences_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_WaitingTimeEvidences_PolicyVersions_PolicyVersionId",
                    column: x => x.PolicyVersionId,
                    principalSchema: "hr",
                    principalTable: "PolicyVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_WaitingTimeEvidences_Sites_SiteId",
                    column: x => x.SiteId,
                    principalSchema: "attendance",
                    principalTable: "Sites",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "TemporaryShiftChangeEmployees",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TemporaryShiftChangeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TemporaryShiftChangeEmployees", x => x.Id);
                table.ForeignKey(
                    name: "FK_TemporaryShiftChangeEmployees_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_TemporaryShiftChangeEmployees_TemporaryShiftChanges_TemporaryShiftChangeId",
                    column: x => x.TemporaryShiftChangeId,
                    principalSchema: "attendance",
                    principalTable: "TemporaryShiftChanges",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "FuelClaims",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VehicleTripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeCommuteDistanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                EligibleDistanceKm = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                ExcludedCommuteKm = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                FuelLiters = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                ClaimedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                PolicyVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CalculationPrivateReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                ReceiptPrivateReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                RequestedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FuelClaims", x => x.Id);
                table.CheckConstraint("CK_FuelClaims_1", "[EligibleDistanceKm] >= 0 AND [ExcludedCommuteKm] >= 0 AND [FuelLiters] >= 0 AND [ClaimedAmount] >= 0");
                table.CheckConstraint("CK_FuelClaims_2", "([SubmittedAt] IS NULL AND [ApprovalRequestId] IS NULL) OR ([SubmittedAt] IS NOT NULL AND [SubmittedAt] >= [CreatedAt] AND [ApprovalRequestId] IS NOT NULL)");
                table.CheckConstraint("CK_FuelClaims_3", "[Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')");
                table.ForeignKey(
                    name: "FK_FuelClaims_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FuelClaims_EmployeeCommuteDistances_EmployeeCommuteDistanceId_EmployeeId",
                    columns: x => new { x.EmployeeCommuteDistanceId, x.EmployeeId },
                    principalSchema: "hr",
                    principalTable: "EmployeeCommuteDistances",
                    principalColumns: new[] { "Id", "EmployeeId" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FuelClaims_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FuelClaims_PolicyVersions_PolicyVersionId",
                    column: x => x.PolicyVersionId,
                    principalSchema: "hr",
                    principalTable: "PolicyVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FuelClaims_VehicleTrips_VehicleTripId_EmployeeId",
                    columns: x => new { x.VehicleTripId, x.EmployeeId },
                    principalSchema: "hr",
                    principalTable: "VehicleTrips",
                    principalColumns: new[] { "Id", "EmployeeId" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "VehicleMileageEntries",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VehicleTripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                ApprovedBusinessKm = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                ExcludedCommuteKm = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                OutstationKm = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                MonthlyAdjustmentKm = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                CalculationPrivateReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                PolicyVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                RecordedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VehicleMileageEntries", x => x.Id);
                table.CheckConstraint("CK_VehicleMileageEntries_1", "[ApprovedBusinessKm] >= 0 AND [ExcludedCommuteKm] >= 0 AND [OutstationKm] >= 0 AND [OutstationKm] <= [ApprovedBusinessKm]");
                table.ForeignKey(
                    name: "FK_VehicleMileageEntries_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_VehicleMileageEntries_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_VehicleMileageEntries_PolicyVersions_PolicyVersionId",
                    column: x => x.PolicyVersionId,
                    principalSchema: "hr",
                    principalTable: "PolicyVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_VehicleMileageEntries_VehicleTrips_VehicleTripId_EmployeeId_VehicleId",
                    columns: x => new { x.VehicleTripId, x.EmployeeId, x.VehicleId },
                    principalSchema: "hr",
                    principalTable: "VehicleTrips",
                    principalColumns: new[] { "Id", "EmployeeId", "VehicleId" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_VehicleMileageEntries_Vehicles_VehicleId",
                    column: x => x.VehicleId,
                    principalSchema: "hr",
                    principalTable: "Vehicles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ZoneAssignments",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ZoneVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PopulationKind = table.Column<int>(type: "int", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsEmployeeOverride = table.Column<bool>(type: "bit", nullable: false),
                EffectiveFromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                EffectiveToUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ApprovedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ApprovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ZoneAssignments", x => x.Id);
                table.UniqueConstraint("AK_ZoneAssignments_ZoneVersionId_Id", x => new { x.ZoneVersionId, x.Id });
                table.CheckConstraint("CK_ZoneAssignments_ApprovedAtUtc_Utc", "[ApprovedAtUtc] IS NULL OR DATEPART(TZOFFSET, [ApprovedAtUtc]) = 0");
                table.CheckConstraint("CK_ZoneAssignments_EffectiveFromUtc_Utc", "[EffectiveFromUtc] IS NULL OR DATEPART(TZOFFSET, [EffectiveFromUtc]) = 0");
                table.CheckConstraint("CK_ZoneAssignments_EffectiveToUtc_Utc", "[EffectiveToUtc] IS NULL OR DATEPART(TZOFFSET, [EffectiveToUtc]) = 0");
                table.CheckConstraint("CK_ZoneAssignments_Override", "[IsEmployeeOverride] = 0 OR [PopulationKind] = 1");
                table.CheckConstraint("CK_ZoneAssignments_Population", "([PopulationKind] = 1 AND [EmployeeId] IS NOT NULL AND [RoleId] IS NULL AND [TeamId] IS NULL AND [SiteId] IS NULL) OR ([PopulationKind] = 2 AND [EmployeeId] IS NULL AND [RoleId] IS NOT NULL AND [TeamId] IS NULL AND [SiteId] IS NULL) OR ([PopulationKind] = 3 AND [EmployeeId] IS NULL AND [RoleId] IS NULL AND [TeamId] IS NOT NULL AND [SiteId] IS NULL) OR ([PopulationKind] = 4 AND [EmployeeId] IS NULL AND [RoleId] IS NULL AND [TeamId] IS NULL AND [SiteId] IS NOT NULL)");
                table.CheckConstraint("CK_ZoneAssignments_Window", "[EffectiveToUtc] > [EffectiveFromUtc]");
                table.ForeignKey(
                    name: "FK_ZoneAssignments_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ZoneAssignments_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ZoneAssignments_Roles_RoleId",
                    column: x => x.RoleId,
                    principalSchema: "auth",
                    principalTable: "Roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ZoneAssignments_Sites_SiteId",
                    column: x => x.SiteId,
                    principalSchema: "attendance",
                    principalTable: "Sites",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ZoneAssignments_Teams_TeamId",
                    column: x => x.TeamId,
                    principalSchema: "auth",
                    principalTable: "Teams",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ZoneAssignments_ZoneVersions_ZoneVersionId",
                    column: x => x.ZoneVersionId,
                    principalSchema: "attendance",
                    principalTable: "ZoneVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ZoneExceptionRequests",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ZoneVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StartsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                EndsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                RequestedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RequestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TaskReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ZoneExceptionRequests", x => x.Id);
                table.CheckConstraint("CK_ZoneExceptionRequests_EndsAtUtc_Utc", "[EndsAtUtc] IS NULL OR DATEPART(TZOFFSET, [EndsAtUtc]) = 0");
                table.CheckConstraint("CK_ZoneExceptionRequests_RequestedAtUtc_Utc", "[RequestedAtUtc] IS NULL OR DATEPART(TZOFFSET, [RequestedAtUtc]) = 0");
                table.CheckConstraint("CK_ZoneExceptionRequests_StartsAtUtc_Utc", "[StartsAtUtc] IS NULL OR DATEPART(TZOFFSET, [StartsAtUtc]) = 0");
                table.CheckConstraint("CK_ZoneExceptionRequests_Window", "[EndsAtUtc] > [StartsAtUtc]");
                table.ForeignKey(
                    name: "FK_ZoneExceptionRequests_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ZoneExceptionRequests_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ZoneExceptionRequests_ZoneVersions_ZoneVersionId",
                    column: x => x.ZoneVersionId,
                    principalSchema: "attendance",
                    principalTable: "ZoneVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "RepaymentScheduleEntries",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AdvanceLoanAgreementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Sequence = table.Column<int>(type: "int", nullable: false),
                DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                PrincipalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                OtherApprovedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RepaymentScheduleEntries", x => x.Id);
                table.UniqueConstraint("AK_RepaymentScheduleEntries_Id_AdvanceLoanAgreementId_Currency", x => new { x.Id, x.AdvanceLoanAgreementId, x.Currency });
                table.CheckConstraint("CK_RepaymentScheduleEntries_1", "[Sequence] > 0");
                table.CheckConstraint("CK_RepaymentScheduleEntries_2", "[PrincipalAmount] >= 0 AND [OtherApprovedAmount] >= 0 AND ([PrincipalAmount] + [OtherApprovedAmount]) > 0");
                table.CheckConstraint("CK_RepaymentScheduleEntries_3", "[Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')");
                table.ForeignKey(
                    name: "FK_RepaymentScheduleEntries_AdvanceLoanAgreements_AdvanceLoanAgreementId_Currency",
                    columns: x => new { x.AdvanceLoanAgreementId, x.Currency },
                    principalSchema: "hr",
                    principalTable: "AdvanceLoanAgreements",
                    principalColumns: new[] { "Id", "Currency" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "AttendanceCalculationLines",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DailyAttendanceSummaryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Sequence = table.Column<int>(type: "int", nullable: false),
                RuleCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Explanation = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                InputQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                ResultQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                UnitCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                MonetaryAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                Currency = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AttendanceCalculationLines", x => x.Id);
                table.CheckConstraint("CK_AttendanceCalculationLines_Currency", "([MonetaryAmount] IS NULL AND [Currency] IS NULL) OR ([MonetaryAmount] IS NOT NULL AND [Currency] IS NOT NULL AND LEN([Currency]) = 3)");
                table.CheckConstraint("CK_AttendanceCalculationLines_Sequence", "[Sequence] > 0");
                table.ForeignKey(
                    name: "FK_AttendanceCalculationLines_DailyAttendanceSummaries_DailyAttendanceSummaryId",
                    column: x => x.DailyAttendanceSummaryId,
                    principalSchema: "attendance",
                    principalTable: "DailyAttendanceSummaries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "AttendanceCorrections",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AttendanceDate = table.Column<DateOnly>(type: "date", nullable: false),
                Kind = table.Column<int>(type: "int", nullable: false),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequestedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RequestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                PrivateEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                OriginalTimeUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                RequestedTimeUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                RequestedEndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                OriginalStatus = table.Column<int>(type: "int", nullable: true),
                RequestedStatus = table.Column<int>(type: "int", nullable: true),
                RawPunchEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                OriginalSummaryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                AdjustmentPeriodLockId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AttendanceCorrections", x => x.Id);
                table.UniqueConstraint("AK_AttendanceCorrections_EmployeeId_Id", x => new { x.EmployeeId, x.Id });
                table.CheckConstraint("CK_AttendanceCorrections_Kind", "[Kind] BETWEEN 1 AND 4");
                table.CheckConstraint("CK_AttendanceCorrections_OriginalTimeUtc_Utc", "[OriginalTimeUtc] IS NULL OR DATEPART(TZOFFSET, [OriginalTimeUtc]) = 0");
                table.CheckConstraint("CK_AttendanceCorrections_RequestedAtUtc_Utc", "[RequestedAtUtc] IS NULL OR DATEPART(TZOFFSET, [RequestedAtUtc]) = 0");
                table.CheckConstraint("CK_AttendanceCorrections_RequestedEndUtc_Utc", "[RequestedEndUtc] IS NULL OR DATEPART(TZOFFSET, [RequestedEndUtc]) = 0");
                table.CheckConstraint("CK_AttendanceCorrections_RequestedTimeUtc_Utc", "[RequestedTimeUtc] IS NULL OR DATEPART(TZOFFSET, [RequestedTimeUtc]) = 0");
                table.CheckConstraint("CK_AttendanceCorrections_Status", "([OriginalStatus] IS NULL OR [OriginalStatus] BETWEEN 1 AND 9) AND ([RequestedStatus] IS NULL OR [RequestedStatus] BETWEEN 1 AND 9)");
                table.CheckConstraint("CK_AttendanceCorrections_TypedChange", "([Kind] = 3 AND [RequestedStatus] IS NOT NULL AND [RequestedTimeUtc] IS NULL AND [RequestedEndUtc] IS NULL) OR ([Kind] IN (1,2) AND [RequestedTimeUtc] IS NOT NULL AND [RequestedEndUtc] IS NULL AND [RequestedStatus] IS NULL) OR ([Kind] = 4 AND [RequestedTimeUtc] IS NOT NULL AND [RequestedEndUtc] IS NOT NULL AND [RequestedEndUtc] > [RequestedTimeUtc] AND [RequestedStatus] IS NULL)");
                table.ForeignKey(
                    name: "FK_AttendanceCorrections_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_AttendanceCorrections_AttendancePeriodLocks_AdjustmentPeriodLockId",
                    column: x => x.AdjustmentPeriodLockId,
                    principalSchema: "attendance",
                    principalTable: "AttendancePeriodLocks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_AttendanceCorrections_DailyAttendanceSummaries_EmployeeId_AttendanceDate_OriginalSummaryId",
                    columns: x => new { x.EmployeeId, x.AttendanceDate, x.OriginalSummaryId },
                    principalSchema: "attendance",
                    principalTable: "DailyAttendanceSummaries",
                    principalColumns: new[] { "EmployeeId", "AttendanceDate", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_AttendanceCorrections_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_AttendanceCorrections_RawPunchEvents_EmployeeId_RawPunchEventId",
                    columns: x => new { x.EmployeeId, x.RawPunchEventId },
                    principalSchema: "attendance",
                    principalTable: "RawPunchEvents",
                    principalColumns: new[] { "EmployeeId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "AttendancePeriodSummaries",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AttendancePeriodLockEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DailyAttendanceSummaryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AttendanceDate = table.Column<DateOnly>(type: "date", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AttendancePeriodSummaries", x => x.Id);
                table.ForeignKey(
                    name: "FK_AttendancePeriodSummaries_AttendancePeriodLockEvents_AttendancePeriodLockEventId",
                    column: x => x.AttendancePeriodLockEventId,
                    principalSchema: "attendance",
                    principalTable: "AttendancePeriodLockEvents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_AttendancePeriodSummaries_DailyAttendanceSummaries_EmployeeId_AttendanceDate_DailyAttendanceSummaryId",
                    columns: x => new { x.EmployeeId, x.AttendanceDate, x.DailyAttendanceSummaryId },
                    principalSchema: "attendance",
                    principalTable: "DailyAttendanceSummaries",
                    principalColumns: new[] { "EmployeeId", "AttendanceDate", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "LeaveCancellationRequests",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LeaveRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequestedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LeaveCancellationRequests", x => x.Id);
                table.CheckConstraint("CK_LeaveCancellationRequests_1", "([SubmittedAt] IS NULL AND [ApprovalRequestId] IS NULL) OR ([SubmittedAt] IS NOT NULL AND [SubmittedAt] >= [CreatedAt] AND [ApprovalRequestId] IS NOT NULL)");
                table.ForeignKey(
                    name: "FK_LeaveCancellationRequests_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_LeaveCancellationRequests_LeaveRequests_LeaveRequestId",
                    column: x => x.LeaveRequestId,
                    principalSchema: "hr",
                    principalTable: "LeaveRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "LeaveLedgerEntries",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LeaveTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LeaveYearStart = table.Column<DateOnly>(type: "date", nullable: false),
                LeaveYearEnd = table.Column<DateOnly>(type: "date", nullable: false),
                EntryType = table.Column<int>(type: "int", nullable: false),
                MinutesDelta = table.Column<int>(type: "int", nullable: false),
                LeaveRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ReversesEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                PolicyVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                RecordedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LeaveLedgerEntries", x => x.Id);
                table.UniqueConstraint("AK_LeaveLedgerEntries_Id_EmployeeId_LeaveTypeId_LeaveYearStart_LeaveYearEnd", x => new { x.Id, x.EmployeeId, x.LeaveTypeId, x.LeaveYearStart, x.LeaveYearEnd });
                table.CheckConstraint("CK_LeaveLedgerEntries_1", "[LeaveYearEnd] > [LeaveYearStart]");
                table.CheckConstraint("CK_LeaveLedgerEntries_2", "[MinutesDelta] <> 0");
                table.CheckConstraint("CK_LeaveLedgerEntries_3", "[EntryType] BETWEEN 1 AND 6");
                table.CheckConstraint("CK_LeaveLedgerEntries_4", "[ReversesEntryId] IS NULL OR [ReversesEntryId] <> [Id]");
                table.ForeignKey(
                    name: "FK_LeaveLedgerEntries_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_LeaveLedgerEntries_LeaveLedgerEntries_ReversesEntryId_EmployeeId_LeaveTypeId_LeaveYearStart_LeaveYearEnd",
                    columns: x => new { x.ReversesEntryId, x.EmployeeId, x.LeaveTypeId, x.LeaveYearStart, x.LeaveYearEnd },
                    principalSchema: "hr",
                    principalTable: "LeaveLedgerEntries",
                    principalColumns: new[] { "Id", "EmployeeId", "LeaveTypeId", "LeaveYearStart", "LeaveYearEnd" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_LeaveLedgerEntries_LeaveRequests_LeaveRequestId_EmployeeId_LeaveTypeId",
                    columns: x => new { x.LeaveRequestId, x.EmployeeId, x.LeaveTypeId },
                    principalSchema: "hr",
                    principalTable: "LeaveRequests",
                    principalColumns: new[] { "Id", "EmployeeId", "LeaveTypeId" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_LeaveLedgerEntries_LeaveTypes_LeaveTypeId",
                    column: x => x.LeaveTypeId,
                    principalSchema: "hr",
                    principalTable: "LeaveTypes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_LeaveLedgerEntries_PolicyVersions_PolicyVersionId",
                    column: x => x.PolicyVersionId,
                    principalSchema: "hr",
                    principalTable: "PolicyVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "LeaveRecoveryCommitments",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LeaveRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CommitmentDate = table.Column<DateOnly>(type: "date", nullable: false),
                CommittedMinutes = table.Column<int>(type: "int", nullable: false),
                BeforeLeave = table.Column<bool>(type: "bit", nullable: false),
                PolicyVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RecordedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LeaveRecoveryCommitments", x => x.Id);
                table.CheckConstraint("CK_LeaveRecoveryCommitments_1", "[CommittedMinutes] > 0");
                table.ForeignKey(
                    name: "FK_LeaveRecoveryCommitments_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_LeaveRecoveryCommitments_LeaveRequests_LeaveRequestId",
                    column: x => x.LeaveRequestId,
                    principalSchema: "hr",
                    principalTable: "LeaveRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_LeaveRecoveryCommitments_PolicyVersions_PolicyVersionId",
                    column: x => x.PolicyVersionId,
                    principalSchema: "hr",
                    principalTable: "PolicyVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "PolicyAssignmentEnds",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PolicyAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EffectiveTo = table.Column<DateOnly>(type: "date", nullable: false),
                Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RecordedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PolicyAssignmentEnds", x => x.Id);
                table.CheckConstraint("CK_PolicyAssignmentEnds_Reason", "LEN(LTRIM(RTRIM([Reason]))) > 0");
                table.ForeignKey(
                    name: "FK_PolicyAssignmentEnds_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PolicyAssignmentEnds_PolicyAssignments_PolicyAssignmentId",
                    column: x => x.PolicyAssignmentId,
                    principalSchema: "hr",
                    principalTable: "PolicyAssignments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "DisciplinaryCases",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReasonCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                ProtectedEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                PolicyImpactAssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OpenedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                OpenedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                State = table.Column<int>(type: "int", nullable: false),
                ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ApprovedOutcomePrivateReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DisciplinaryCases", x => x.Id);
                table.CheckConstraint("CK_DisciplinaryCases_1", "[State] BETWEEN 0 AND 2");
                table.CheckConstraint("CK_DisciplinaryCases_2", "[ClosedAt] IS NULL OR [ClosedAt] >= [OpenedAt]");
                table.CheckConstraint("CK_DisciplinaryCases_3", "([State] <> 2 AND [ClosedAt] IS NULL AND [ApprovedOutcomePrivateReference] IS NULL) OR ([State] = 2 AND [ClosedAt] IS NOT NULL AND [ApprovedOutcomePrivateReference] IS NOT NULL)");
                table.ForeignKey(
                    name: "FK_DisciplinaryCases_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_DisciplinaryCases_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_DisciplinaryCases_PolicyImpactAssessments_PolicyImpactAssessmentId_EmployeeId",
                    columns: x => new { x.PolicyImpactAssessmentId, x.EmployeeId },
                    principalSchema: "hr",
                    principalTable: "PolicyImpactAssessments",
                    principalColumns: new[] { "Id", "EmployeeId" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "DutyMonitoringWindows",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ZoneAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StartsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                EndsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                NoticeShownAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                RetentionPolicyVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DutyReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DutyMonitoringWindows", x => x.Id);
                table.UniqueConstraint("AK_DutyMonitoringWindows_EmployeeId_ZoneAssignmentId_Id", x => new { x.EmployeeId, x.ZoneAssignmentId, x.Id });
                table.CheckConstraint("CK_DutyMonitoringWindows_EndsAtUtc_Utc", "[EndsAtUtc] IS NULL OR DATEPART(TZOFFSET, [EndsAtUtc]) = 0");
                table.CheckConstraint("CK_DutyMonitoringWindows_NoticeShownAtUtc_Utc", "[NoticeShownAtUtc] IS NULL OR DATEPART(TZOFFSET, [NoticeShownAtUtc]) = 0");
                table.CheckConstraint("CK_DutyMonitoringWindows_StartsAtUtc_Utc", "[StartsAtUtc] IS NULL OR DATEPART(TZOFFSET, [StartsAtUtc]) = 0");
                table.CheckConstraint("CK_DutyMonitoringWindows_Window", "[EndsAtUtc] > [StartsAtUtc] AND [NoticeShownAtUtc] <= [StartsAtUtc]");
                table.ForeignKey(
                    name: "FK_DutyMonitoringWindows_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_DutyMonitoringWindows_PolicyVersions_RetentionPolicyVersionId",
                    column: x => x.RetentionPolicyVersionId,
                    principalSchema: "hr",
                    principalTable: "PolicyVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_DutyMonitoringWindows_ZoneAssignments_ZoneAssignmentId",
                    column: x => x.ZoneAssignmentId,
                    principalSchema: "attendance",
                    principalTable: "ZoneAssignments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "RepaymentLedgerEntries",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AdvanceLoanAgreementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RepaymentScheduleEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Kind = table.Column<int>(type: "int", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                ValueDate = table.Column<DateOnly>(type: "date", nullable: false),
                SourceReference = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ReversesEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RecordedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RepaymentLedgerEntries", x => x.Id);
                table.UniqueConstraint("AK_RepaymentLedgerEntries_Id_AdvanceLoanAgreementId_Currency", x => new { x.Id, x.AdvanceLoanAgreementId, x.Currency });
                table.CheckConstraint("CK_RepaymentLedgerEntries_1", "[Amount] <> 0");
                table.CheckConstraint("CK_RepaymentLedgerEntries_2", "[Kind] BETWEEN 1 AND 4");
                table.CheckConstraint("CK_RepaymentLedgerEntries_3", "[ReversesEntryId] IS NULL OR [ReversesEntryId] <> [Id]");
                table.CheckConstraint("CK_RepaymentLedgerEntries_4", "([Kind] IN (1, 2) AND [Amount] > 0) OR ([Kind] = 3 AND [Amount] < 0) OR [Kind] = 4");
                table.CheckConstraint("CK_RepaymentLedgerEntries_5", "[Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')");
                table.CheckConstraint("CK_RepaymentLedgerEntries_ReversalSource", "[Kind] <> 3 OR [ReversesEntryId] IS NOT NULL");
                table.ForeignKey(
                    name: "FK_RepaymentLedgerEntries_AdvanceLoanAgreements_AdvanceLoanAgreementId_Currency",
                    columns: x => new { x.AdvanceLoanAgreementId, x.Currency },
                    principalSchema: "hr",
                    principalTable: "AdvanceLoanAgreements",
                    principalColumns: new[] { "Id", "Currency" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_RepaymentLedgerEntries_RepaymentLedgerEntries_ReversesEntryId_AdvanceLoanAgreementId_Currency",
                    columns: x => new { x.ReversesEntryId, x.AdvanceLoanAgreementId, x.Currency },
                    principalSchema: "hr",
                    principalTable: "RepaymentLedgerEntries",
                    principalColumns: new[] { "Id", "AdvanceLoanAgreementId", "Currency" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_RepaymentLedgerEntries_RepaymentScheduleEntries_RepaymentScheduleEntryId_AdvanceLoanAgreementId_Currency",
                    columns: x => new { x.RepaymentScheduleEntryId, x.AdvanceLoanAgreementId, x.Currency },
                    principalSchema: "hr",
                    principalTable: "RepaymentScheduleEntries",
                    principalColumns: new[] { "Id", "AdvanceLoanAgreementId", "Currency" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "WorkSessions",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SessionKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Revision = table.Column<int>(type: "int", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AttendanceDate = table.Column<DateOnly>(type: "date", nullable: false),
                StartsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                EndsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                CheckInPunchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CheckOutPunchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CorrectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ShiftVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PolicyVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CalculatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                EndsContinuousDuty = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WorkSessions", x => x.Id);
                table.UniqueConstraint("AK_WorkSessions_EmployeeId_Id", x => new { x.EmployeeId, x.Id });
                table.UniqueConstraint("AK_WorkSessions_Id_EmployeeId", x => new { x.Id, x.EmployeeId });
                table.CheckConstraint("CK_WorkSessions_CalculatedAtUtc_Utc", "[CalculatedAtUtc] IS NULL OR DATEPART(TZOFFSET, [CalculatedAtUtc]) = 0");
                table.CheckConstraint("CK_WorkSessions_CheckOut", "[CheckOutPunchId] IS NULL OR [EndsAtUtc] IS NOT NULL");
                table.CheckConstraint("CK_WorkSessions_ContinuousDuty", "[EndsContinuousDuty] = 0 OR [EndsAtUtc] IS NOT NULL");
                table.CheckConstraint("CK_WorkSessions_EndsAtUtc_Utc", "[EndsAtUtc] IS NULL OR DATEPART(TZOFFSET, [EndsAtUtc]) = 0");
                table.CheckConstraint("CK_WorkSessions_Evidence", "[CorrectionId] IS NOT NULL OR ([CheckInPunchId] IS NOT NULL AND ([EndsAtUtc] IS NULL OR [CheckOutPunchId] IS NOT NULL))");
                table.CheckConstraint("CK_WorkSessions_Revision", "[Revision] > 0");
                table.CheckConstraint("CK_WorkSessions_StartsAtUtc_Utc", "[StartsAtUtc] IS NULL OR DATEPART(TZOFFSET, [StartsAtUtc]) = 0");
                table.CheckConstraint("CK_WorkSessions_Window", "[EndsAtUtc] IS NULL OR [EndsAtUtc] > [StartsAtUtc]");
                table.ForeignKey(
                    name: "FK_WorkSessions_AttendanceCorrections_EmployeeId_CorrectionId",
                    columns: x => new { x.EmployeeId, x.CorrectionId },
                    principalSchema: "attendance",
                    principalTable: "AttendanceCorrections",
                    principalColumns: new[] { "EmployeeId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_WorkSessions_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_WorkSessions_PolicyVersions_PolicyVersionId",
                    column: x => x.PolicyVersionId,
                    principalSchema: "hr",
                    principalTable: "PolicyVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_WorkSessions_RawPunchEvents_EmployeeId_CheckInPunchId",
                    columns: x => new { x.EmployeeId, x.CheckInPunchId },
                    principalSchema: "attendance",
                    principalTable: "RawPunchEvents",
                    principalColumns: new[] { "EmployeeId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_WorkSessions_RawPunchEvents_EmployeeId_CheckOutPunchId",
                    columns: x => new { x.EmployeeId, x.CheckOutPunchId },
                    principalSchema: "attendance",
                    principalTable: "RawPunchEvents",
                    principalColumns: new[] { "EmployeeId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_WorkSessions_ShiftVersions_ShiftVersionId",
                    column: x => x.ShiftVersionId,
                    principalSchema: "attendance",
                    principalTable: "ShiftVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_WorkSessions_Sites_SiteId",
                    column: x => x.SiteId,
                    principalSchema: "attendance",
                    principalTable: "Sites",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "AllowanceClaims",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Kind = table.Column<int>(type: "int", nullable: false),
                EmployeeAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DeploymentCategoryCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                DeploymentApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                WorkSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                EligibilityPeriodStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                EligibilityPeriodEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                EligibilityKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                ClaimedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                PayableDate = table.Column<DateOnly>(type: "date", nullable: false),
                PayrollPeriodStart = table.Column<DateOnly>(type: "date", nullable: true),
                PolicyVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CalculationPrivateReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                EvidencePrivateReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                RequestedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AllowanceClaims", x => x.Id);
                table.CheckConstraint("CK_AllowanceClaims_1", "[EligibilityPeriodEnd] > [EligibilityPeriodStart]");
                table.CheckConstraint("CK_AllowanceClaims_2", "[Kind] BETWEEN 1 AND 6");
                table.CheckConstraint("CK_AllowanceClaims_3", "[ClaimedAmount] >= 0");
                table.CheckConstraint("CK_AllowanceClaims_4", "([SubmittedAt] IS NULL AND [ApprovalRequestId] IS NULL) OR ([SubmittedAt] IS NOT NULL AND [SubmittedAt] >= [CreatedAt] AND [ApprovalRequestId] IS NOT NULL)");
                table.CheckConstraint("CK_AllowanceClaims_5", "[Kind] <> 5 OR [DeploymentApprovalRequestId] IS NOT NULL");
                table.CheckConstraint("CK_AllowanceClaims_6", "[Kind] NOT BETWEEN 1 AND 4 OR [WorkSessionId] IS NOT NULL");
                table.CheckConstraint("CK_AllowanceClaims_7", "[Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')");
                table.ForeignKey(
                    name: "FK_AllowanceClaims_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_AllowanceClaims_ApprovalRequests_DeploymentApprovalRequestId",
                    column: x => x.DeploymentApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_AllowanceClaims_EmployeeAssignments_EmployeeAssignmentId_EmployeeId",
                    columns: x => new { x.EmployeeAssignmentId, x.EmployeeId },
                    principalSchema: "hr",
                    principalTable: "EmployeeAssignments",
                    principalColumns: new[] { "Id", "EmployeeId" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_AllowanceClaims_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_AllowanceClaims_PolicyVersions_PolicyVersionId",
                    column: x => x.PolicyVersionId,
                    principalSchema: "hr",
                    principalTable: "PolicyVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_AllowanceClaims_WorkSessions_WorkSessionId_EmployeeId",
                    columns: x => new { x.WorkSessionId, x.EmployeeId },
                    principalSchema: "attendance",
                    principalTable: "WorkSessions",
                    principalColumns: new[] { "Id", "EmployeeId" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "AttendanceExceptions",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AttendanceDate = table.Column<DateOnly>(type: "date", nullable: false),
                Kind = table.Column<int>(type: "int", nullable: false),
                Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                DetectedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                RawPunchEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                WorkSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                DailyAttendanceSummaryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AttendanceExceptions", x => x.Id);
                table.CheckConstraint("CK_AttendanceExceptions_DetectedAtUtc_Utc", "[DetectedAtUtc] IS NULL OR DATEPART(TZOFFSET, [DetectedAtUtc]) = 0");
                table.CheckConstraint("CK_AttendanceExceptions_Kind", "[Kind] BETWEEN 1 AND 10");
                table.ForeignKey(
                    name: "FK_AttendanceExceptions_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_AttendanceExceptions_DailyAttendanceSummaries_EmployeeId_DailyAttendanceSummaryId",
                    columns: x => new { x.EmployeeId, x.DailyAttendanceSummaryId },
                    principalSchema: "attendance",
                    principalTable: "DailyAttendanceSummaries",
                    principalColumns: new[] { "EmployeeId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_AttendanceExceptions_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_AttendanceExceptions_RawPunchEvents_EmployeeId_RawPunchEventId",
                    columns: x => new { x.EmployeeId, x.RawPunchEventId },
                    principalSchema: "attendance",
                    principalTable: "RawPunchEvents",
                    principalColumns: new[] { "EmployeeId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_AttendanceExceptions_WorkSessions_EmployeeId_WorkSessionId",
                    columns: x => new { x.EmployeeId, x.WorkSessionId },
                    principalSchema: "attendance",
                    principalTable: "WorkSessions",
                    principalColumns: new[] { "EmployeeId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "AttendanceSummarySessions",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DailyAttendanceSummaryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                WorkSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AllocatedMinutes = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AttendanceSummarySessions", x => x.Id);
                table.CheckConstraint("CK_AttendanceSummarySessions_Minutes", "[AllocatedMinutes] >= 0");
                table.ForeignKey(
                    name: "FK_AttendanceSummarySessions_DailyAttendanceSummaries_EmployeeId_DailyAttendanceSummaryId",
                    columns: x => new { x.EmployeeId, x.DailyAttendanceSummaryId },
                    principalSchema: "attendance",
                    principalTable: "DailyAttendanceSummaries",
                    principalColumns: new[] { "EmployeeId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_AttendanceSummarySessions_WorkSessions_EmployeeId_WorkSessionId",
                    columns: x => new { x.EmployeeId, x.WorkSessionId },
                    principalSchema: "attendance",
                    principalTable: "WorkSessions",
                    principalColumns: new[] { "EmployeeId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "OvertimeRequests",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                WorkSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StartsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                EndsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                RequestedMinutes = table.Column<int>(type: "int", nullable: false),
                PolicyVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RateRuleCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                PrivateEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                RequestedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RequestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                JobReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                BulkSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OvertimeRequests", x => x.Id);
                table.CheckConstraint("CK_OvertimeRequests_EndsAtUtc_Utc", "[EndsAtUtc] IS NULL OR DATEPART(TZOFFSET, [EndsAtUtc]) = 0");
                table.CheckConstraint("CK_OvertimeRequests_Minutes", "[RequestedMinutes] > 0 AND CAST([RequestedMinutes] AS bigint) * 600000000 <= DATEDIFF_BIG(microsecond, [StartsAtUtc], [EndsAtUtc]) * 10");
                table.CheckConstraint("CK_OvertimeRequests_RequestedAtUtc_Utc", "[RequestedAtUtc] IS NULL OR DATEPART(TZOFFSET, [RequestedAtUtc]) = 0");
                table.CheckConstraint("CK_OvertimeRequests_StartsAtUtc_Utc", "[StartsAtUtc] IS NULL OR DATEPART(TZOFFSET, [StartsAtUtc]) = 0");
                table.CheckConstraint("CK_OvertimeRequests_Window", "[EndsAtUtc] > [StartsAtUtc]");
                table.ForeignKey(
                    name: "FK_OvertimeRequests_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_OvertimeRequests_PolicyVersions_PolicyVersionId",
                    column: x => x.PolicyVersionId,
                    principalSchema: "hr",
                    principalTable: "PolicyVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_OvertimeRequests_Sites_SiteId",
                    column: x => x.SiteId,
                    principalSchema: "attendance",
                    principalTable: "Sites",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_OvertimeRequests_WorkSessions_EmployeeId_WorkSessionId",
                    columns: x => new { x.EmployeeId, x.WorkSessionId },
                    principalSchema: "attendance",
                    principalTable: "WorkSessions",
                    principalColumns: new[] { "EmployeeId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "AllowanceSettlementRecords",
            schema: "hr",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AllowanceClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                State = table.Column<int>(type: "int", nullable: false),
                PayrollHandoffLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ExternalPaymentReference = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                RecordedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AllowanceSettlementRecords", x => x.Id);
                table.CheckConstraint("CK_AllowanceSettlementRecords_1", "[State] BETWEEN 0 AND 2");
                table.CheckConstraint("CK_AllowanceSettlementRecords_2", "[State] <> 1 OR [PayrollHandoffLineId] IS NOT NULL");
                table.CheckConstraint("CK_AllowanceSettlementRecords_3", "[State] <> 2 OR [ExternalPaymentReference] IS NOT NULL");
                table.ForeignKey(
                    name: "FK_AllowanceSettlementRecords_AllowanceClaims_AllowanceClaimId",
                    column: x => x.AllowanceClaimId,
                    principalSchema: "hr",
                    principalTable: "AllowanceClaims",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_AllowanceSettlementRecords_PayrollHandoffLines_PayrollHandoffLineId",
                    column: x => x.PayrollHandoffLineId,
                    principalSchema: "hr",
                    principalTable: "PayrollHandoffLines",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "AccountSetupInvitations",
            schema: "auth",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ApprovalDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TokenHash = table.Column<string>(type: "varchar(256)", unicode: false, maxLength: 256, nullable: false, collation: "Latin1_General_100_BIN2"),
                Channel = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                ProtectedDestinationReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                IssuedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                IssuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ConsumedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                DeliveryStatus = table.Column<int>(type: "int", nullable: false),
                DeliveryAttempts = table.Column<int>(type: "int", nullable: false),
                LastDeliveryAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                NextDeliveryAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                LastDeliveryFailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AccountSetupInvitations", x => x.Id);
                table.UniqueConstraint("AK_AccountSetupInvitations_UserAccountId_Id", x => new { x.UserAccountId, x.Id });
                table.CheckConstraint("CK_AccountSetupInvitations_Dates", "[ExpiresAt] > [IssuedAt] AND ([ConsumedAt] IS NULL OR ([ConsumedAt] >= [IssuedAt] AND [ConsumedAt] < [ExpiresAt])) AND ([RevokedAt] IS NULL OR [RevokedAt] >= [IssuedAt])");
                table.CheckConstraint("CK_AccountSetupInvitations_Delivery", "[DeliveryStatus] BETWEEN 0 AND 2 AND [DeliveryAttempts] >= 0");
                table.ForeignKey(
                    name: "FK_AccountSetupInvitations_EmployeeOnboardingDecisions_ApprovalDecisionId",
                    column: x => x.ApprovalDecisionId,
                    principalSchema: "hr",
                    principalTable: "EmployeeOnboardingDecisions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "FaceVerificationAttempts",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FaceEnrollmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RegisteredDeviceKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RegisteredDeviceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_BIN2"),
                PunchIdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                ChallengeSha256 = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                Provider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ProviderVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                IssuedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                Outcome = table.Column<int>(type: "int", nullable: false),
                LivenessPassed = table.Column<bool>(type: "bit", nullable: true),
                MatchPassed = table.Column<bool>(type: "bit", nullable: true),
                ResultReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                ConsumedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                ConsumedByPunchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FaceVerificationAttempts", x => x.Id);
                table.UniqueConstraint("AK_FaceVerificationAttempts_EmployeeId_Id", x => new { x.EmployeeId, x.Id });
                table.CheckConstraint("CK_FaceVerificationAttempts_CompletedAtUtc_Utc", "[CompletedAtUtc] IS NULL OR DATEPART(TZOFFSET, [CompletedAtUtc]) = 0");
                table.CheckConstraint("CK_FaceVerificationAttempts_Completion", "([Outcome] = 1 AND [CompletedAtUtc] IS NULL AND [LivenessPassed] IS NULL AND [MatchPassed] IS NULL AND [ResultReasonCode] IS NULL) OR ([Outcome] <> 1 AND [CompletedAtUtc] IS NOT NULL AND [CompletedAtUtc] >= [IssuedAtUtc] AND [CompletedAtUtc] < [ExpiresAtUtc] AND [LivenessPassed] IS NOT NULL AND [MatchPassed] IS NOT NULL AND [ResultReasonCode] IS NOT NULL)");
                table.CheckConstraint("CK_FaceVerificationAttempts_ConsumedAtUtc_Utc", "[ConsumedAtUtc] IS NULL OR DATEPART(TZOFFSET, [ConsumedAtUtc]) = 0");
                table.CheckConstraint("CK_FaceVerificationAttempts_Consumption", "([ConsumedAtUtc] IS NULL AND [ConsumedByPunchId] IS NULL) OR ([ConsumedAtUtc] IS NOT NULL AND [ConsumedByPunchId] IS NOT NULL AND [Outcome] = 2 AND [ConsumedAtUtc] >= [CompletedAtUtc] AND [ConsumedAtUtc] < [ExpiresAtUtc])");
                table.CheckConstraint("CK_FaceVerificationAttempts_Digest", "LEN([ChallengeSha256]) = 64 AND [ChallengeSha256] NOT LIKE '%[^0-9A-F]%'");
                table.CheckConstraint("CK_FaceVerificationAttempts_ExpiresAtUtc_Utc", "[ExpiresAtUtc] IS NULL OR DATEPART(TZOFFSET, [ExpiresAtUtc]) = 0");
                table.CheckConstraint("CK_FaceVerificationAttempts_IssuedAtUtc_Utc", "[IssuedAtUtc] IS NULL OR DATEPART(TZOFFSET, [IssuedAtUtc]) = 0");
                table.CheckConstraint("CK_FaceVerificationAttempts_Outcome", "[Outcome] BETWEEN 1 AND 4");
                table.CheckConstraint("CK_FaceVerificationAttempts_Passed", "[Outcome] <> 2 OR ([LivenessPassed] = 1 AND [MatchPassed] = 1)");
                table.CheckConstraint("CK_FaceVerificationAttempts_Window", "[ExpiresAtUtc] > [IssuedAtUtc]");
                table.ForeignKey(
                    name: "FK_FaceVerificationAttempts_FaceEnrollments_EmployeeId_FaceEnrollmentId",
                    columns: x => new { x.EmployeeId, x.FaceEnrollmentId },
                    principalSchema: "attendance",
                    principalTable: "FaceEnrollments",
                    principalColumns: new[] { "EmployeeId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FaceVerificationAttempts_RawPunchEvents_EmployeeId_RegisteredDeviceId_PunchIdempotencyKey_ConsumedByPunchId",
                    columns: x => new { x.EmployeeId, x.RegisteredDeviceId, x.PunchIdempotencyKey, x.ConsumedByPunchId },
                    principalSchema: "attendance",
                    principalTable: "RawPunchEvents",
                    principalColumns: new[] { "EmployeeId", "RegisteredDeviceId", "IdempotencyKey", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "PunchAssessments",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RawPunchEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Revision = table.Column<int>(type: "int", nullable: false),
                Outcome = table.Column<int>(type: "int", nullable: false),
                ReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                PolicyVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CalculationVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                AssessedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                FaceVerificationAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                GeofenceDistanceMeters = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                LocationReliable = table.Column<bool>(type: "bit", nullable: true),
                InsideGeofence = table.Column<bool>(type: "bit", nullable: true),
                DeviceRiskCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PunchAssessments", x => x.Id);
                table.CheckConstraint("CK_PunchAssessments_AssessedAtUtc_Utc", "[AssessedAtUtc] IS NULL OR DATEPART(TZOFFSET, [AssessedAtUtc]) = 0");
                table.CheckConstraint("CK_PunchAssessments_Distance", "[GeofenceDistanceMeters] IS NULL OR [GeofenceDistanceMeters] >= 0");
                table.CheckConstraint("CK_PunchAssessments_Outcome", "[Outcome] BETWEEN 1 AND 5");
                table.CheckConstraint("CK_PunchAssessments_Revision", "[Revision] > 0");
                table.ForeignKey(
                    name: "FK_PunchAssessments_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PunchAssessments_FaceVerificationAttempts_EmployeeId_FaceVerificationAttemptId",
                    columns: x => new { x.EmployeeId, x.FaceVerificationAttemptId },
                    principalSchema: "attendance",
                    principalTable: "FaceVerificationAttempts",
                    principalColumns: new[] { "EmployeeId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PunchAssessments_PolicyVersions_PolicyVersionId",
                    column: x => x.PolicyVersionId,
                    principalSchema: "hr",
                    principalTable: "PolicyVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PunchAssessments_RawPunchEvents_EmployeeId_RawPunchEventId",
                    columns: x => new { x.EmployeeId, x.RawPunchEventId },
                    principalSchema: "attendance",
                    principalTable: "RawPunchEvents",
                    principalColumns: new[] { "EmployeeId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "MfaChallenges",
            schema: "auth",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RegisteredDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                VerifiedCredentialId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SecurityVersion = table.Column<int>(type: "int", nullable: false),
                ChallengeHash = table.Column<string>(type: "varchar(256)", unicode: false, maxLength: 256, nullable: false, collation: "Latin1_General_100_BIN2"),
                IssuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                VerifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ConsumedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                FailedAttempts = table.Column<int>(type: "int", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MfaChallenges", x => x.Id);
                table.UniqueConstraint("AK_MfaChallenges_UserAccountId_Id", x => new { x.UserAccountId, x.Id });
                table.CheckConstraint("CK_MfaChallenges_Counters", "[SecurityVersion] > 0 AND [FailedAttempts] >= 0");
                table.CheckConstraint("CK_MfaChallenges_Lifetime", "[ExpiresAt] > [IssuedAt] AND ([VerifiedAt] IS NULL OR ([VerifiedAt] >= [IssuedAt] AND [VerifiedAt] < [ExpiresAt])) AND ([ConsumedAt] IS NULL OR ([VerifiedAt] IS NOT NULL AND [ConsumedAt] >= [VerifiedAt] AND [ConsumedAt] < [ExpiresAt]))");
                table.CheckConstraint("CK_MfaChallenges_Verified", "([VerifiedAt] IS NULL AND [VerifiedCredentialId] IS NULL) OR ([VerifiedAt] IS NOT NULL AND [VerifiedCredentialId] IS NOT NULL)");
            });

        migrationBuilder.CreateTable(
            name: "MfaCredentials",
            schema: "auth",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Method = table.Column<int>(type: "int", nullable: false),
                ProtectedSecretReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                RecoveryCodeHash = table.Column<string>(type: "varchar(256)", unicode: false, maxLength: 256, nullable: true, collation: "Latin1_General_100_BIN2"),
                EnrolledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ConfirmedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                LastAcceptedCounter = table.Column<long>(type: "bigint", nullable: true),
                RecoveryCodeConsumedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MfaCredentials", x => x.Id);
                table.UniqueConstraint("AK_MfaCredentials_UserAccountId_Id", x => new { x.UserAccountId, x.Id });
                table.CheckConstraint("CK_MfaCredentials_Counter", "[LastAcceptedCounter] IS NULL OR ([Method] = 1 AND [LastAcceptedCounter] >= 0)");
                table.CheckConstraint("CK_MfaCredentials_Dates", "([ConfirmedAt] IS NULL OR [ConfirmedAt] >= [EnrolledAt]) AND ([RevokedAt] IS NULL OR [RevokedAt] >= [EnrolledAt]) AND ([RecoveryCodeConsumedAt] IS NULL OR ([Method] = 2 AND [ConfirmedAt] IS NOT NULL AND [RecoveryCodeConsumedAt] >= [ConfirmedAt]))");
                table.CheckConstraint("CK_MfaCredentials_Secret", "([Method] = 2 AND [RecoveryCodeHash] IS NOT NULL AND [ProtectedSecretReference] IS NULL) OR ([Method] IN (1,3) AND [ProtectedSecretReference] IS NOT NULL AND [RecoveryCodeHash] IS NULL)");
            });

        migrationBuilder.CreateTable(
            name: "PasswordResetChallenges",
            schema: "auth",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SecurityVersion = table.Column<int>(type: "int", nullable: false),
                TokenHash = table.Column<string>(type: "varchar(256)", unicode: false, maxLength: 256, nullable: false, collation: "Latin1_General_100_BIN2"),
                Channel = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                ProtectedDestinationReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                IssuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ConsumedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                FailedVerificationCount = table.Column<int>(type: "int", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PasswordResetChallenges", x => x.Id);
                table.CheckConstraint("CK_PasswordResetChallenges_Counters", "[SecurityVersion] > 0 AND [FailedVerificationCount] >= 0");
                table.CheckConstraint("CK_PasswordResetChallenges_Dates", "[ExpiresAt] > [IssuedAt] AND ([ConsumedAt] IS NULL OR ([ConsumedAt] >= [IssuedAt] AND [ConsumedAt] < [ExpiresAt])) AND ([RevokedAt] IS NULL OR [RevokedAt] >= [IssuedAt])");
            });

        migrationBuilder.CreateTable(
            name: "RefreshTokens",
            schema: "auth",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TokenHash = table.Column<string>(type: "varchar(256)", unicode: false, maxLength: 256, nullable: false, collation: "Latin1_General_100_BIN2"),
                IssuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ConsumedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ReplacedByTokenId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                table.UniqueConstraint("AK_RefreshTokens_UserSessionId_Id", x => new { x.UserSessionId, x.Id });
                table.CheckConstraint("CK_RefreshTokens_Lifetime", "[ExpiresAt] > [IssuedAt] AND ([ConsumedAt] IS NULL OR ([ConsumedAt] >= [IssuedAt] AND [ConsumedAt] < [ExpiresAt]))");
                table.CheckConstraint("CK_RefreshTokens_Rotation", "([ConsumedAt] IS NULL AND [ReplacedByTokenId] IS NULL) OR ([ConsumedAt] IS NOT NULL AND [ReplacedByTokenId] IS NOT NULL AND [ReplacedByTokenId] <> [Id])");
                table.ForeignKey(
                    name: "FK_RefreshTokens_RefreshTokens_UserSessionId_ReplacedByTokenId",
                    columns: x => new { x.UserSessionId, x.ReplacedByTokenId },
                    principalSchema: "auth",
                    principalTable: "RefreshTokens",
                    principalColumns: new[] { "UserSessionId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "RegisteredDevices",
            schema: "auth",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DeviceIdentifierHash = table.Column<string>(type: "varchar(256)", unicode: false, maxLength: 256, nullable: false, collation: "Latin1_General_100_BIN2"),
                Platform = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                DisplayName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                RegisteredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                VerifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                LastSeenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                RevocationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                IntegrityAssessmentCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                IntegrityAssessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RegisteredDevices", x => x.Id);
                table.UniqueConstraint("AK_RegisteredDevices_UserAccountId_Id", x => new { x.UserAccountId, x.Id });
                table.CheckConstraint("CK_RegisteredDevices_Dates", "([VerifiedAt] IS NULL OR [VerifiedAt] >= [RegisteredAt]) AND ([LastSeenAt] IS NULL OR [LastSeenAt] >= [RegisteredAt]) AND ([RevokedAt] IS NULL OR [RevokedAt] >= [RegisteredAt])");
            });

        migrationBuilder.CreateTable(
            name: "SiteReportReviews",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SiteReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Sequence = table.Column<int>(type: "int", nullable: false),
                Outcome = table.Column<int>(type: "int", nullable: false),
                Reviewer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ReviewerRole = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SiteReportReviews", x => x.Id);
                table.CheckConstraint("CK_SiteReportReviews_Outcome", "[Outcome] BETWEEN 1 AND 4");
                table.CheckConstraint("CK_SiteReportReviews_ReviewedAtUtc_Utc", "[ReviewedAtUtc] IS NULL OR DATEPART(TZOFFSET, [ReviewedAtUtc]) = 0");
                table.CheckConstraint("CK_SiteReportReviews_Sequence", "[Sequence] > 0");
                table.ForeignKey(
                    name: "FK_SiteReportReviews_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "SiteReports",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Kind = table.Column<int>(type: "int", nullable: false),
                TaskReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                RegisteredDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                DeviceCapturedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                Latitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                Longitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                AccuracyMeters = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                IsOffline = table.Column<bool>(type: "bit", nullable: false),
                PrivateEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SiteReports", x => x.Id);
                table.CheckConstraint("CK_SiteReports_Coordinates", "(([Latitude] IS NULL AND [Longitude] IS NULL AND [AccuracyMeters] IS NULL) OR ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180 AND ([AccuracyMeters] IS NULL OR [AccuracyMeters] >= 0)))");
                table.CheckConstraint("CK_SiteReports_DeviceCapturedAtUtc_Utc", "[DeviceCapturedAtUtc] IS NULL OR DATEPART(TZOFFSET, [DeviceCapturedAtUtc]) = 0");
                table.CheckConstraint("CK_SiteReports_Kind", "[Kind] BETWEEN 1 AND 5");
                table.CheckConstraint("CK_SiteReports_ReceivedAtUtc_Utc", "[ReceivedAtUtc] IS NULL OR DATEPART(TZOFFSET, [ReceivedAtUtc]) = 0");
                table.ForeignKey(
                    name: "FK_SiteReports_EmployeeAssignments_EmployeeId_EmployeeAssignmentId",
                    columns: x => new { x.EmployeeId, x.EmployeeAssignmentId },
                    principalSchema: "hr",
                    principalTable: "EmployeeAssignments",
                    principalColumns: new[] { "EmployeeId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_SiteReports_RegisteredDevices_UserAccountId_RegisteredDeviceId",
                    columns: x => new { x.UserAccountId, x.RegisteredDeviceId },
                    principalSchema: "auth",
                    principalTable: "RegisteredDevices",
                    principalColumns: new[] { "UserAccountId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_SiteReports_Sites_SiteId",
                    column: x => x.SiteId,
                    principalSchema: "attendance",
                    principalTable: "Sites",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "UserAccessScopes",
            schema: "auth",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserRoleAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Kind = table.Column<int>(type: "int", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserAccessScopes", x => x.Id);
                table.UniqueConstraint("AK_UserAccessScopes_UserRoleAssignmentId_Id", x => new { x.UserRoleAssignmentId, x.Id });
                table.CheckConstraint("CK_UserAccessScopes_Target", "([Kind] IN (1,2) AND [EmployeeId] IS NOT NULL AND [DepartmentId] IS NULL AND [TeamId] IS NULL AND [SiteId] IS NULL AND [ProjectId] IS NULL) OR ([Kind] = 3 AND [EmployeeId] IS NULL AND [DepartmentId] IS NOT NULL AND [TeamId] IS NULL AND [SiteId] IS NULL AND [ProjectId] IS NULL) OR ([Kind] = 4 AND [EmployeeId] IS NULL AND [DepartmentId] IS NULL AND [TeamId] IS NOT NULL AND [SiteId] IS NULL AND [ProjectId] IS NULL) OR ([Kind] = 5 AND [EmployeeId] IS NULL AND [DepartmentId] IS NULL AND [TeamId] IS NULL AND [SiteId] IS NOT NULL AND [ProjectId] IS NULL) OR ([Kind] = 6 AND [EmployeeId] IS NULL AND [DepartmentId] IS NULL AND [TeamId] IS NULL AND [SiteId] IS NULL AND [ProjectId] IS NOT NULL) OR ([Kind] = 7 AND [EmployeeId] IS NULL AND [DepartmentId] IS NULL AND [TeamId] IS NULL AND [SiteId] IS NULL AND [ProjectId] IS NULL)");
                table.ForeignKey(
                    name: "FK_UserAccessScopes_Departments_DepartmentId",
                    column: x => x.DepartmentId,
                    principalSchema: "hr",
                    principalTable: "Departments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_UserAccessScopes_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_UserAccessScopes_Projects_ProjectId",
                    column: x => x.ProjectId,
                    principalSchema: "auth",
                    principalTable: "Projects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_UserAccessScopes_Sites_SiteId",
                    column: x => x.SiteId,
                    principalSchema: "attendance",
                    principalTable: "Sites",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_UserAccessScopes_Teams_TeamId",
                    column: x => x.TeamId,
                    principalSchema: "auth",
                    principalTable: "Teams",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "UserAccounts",
            schema: "auth",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ApprovalDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LoginName = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                NormalizedLoginName = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                PasswordHash = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                PasswordHashFormat = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                PasswordChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                Status = table.Column<int>(type: "int", nullable: false),
                InitialRoleAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                InitialAccessScopeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CurrentSetupInvitationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                MfaRequired = table.Column<bool>(type: "bit", nullable: false),
                SecurityVersion = table.Column<int>(type: "int", nullable: false),
                FailedLoginCount = table.Column<int>(type: "int", nullable: false),
                LockedUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ActivatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                StatusChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                StatusReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserAccounts", x => x.Id);
                table.UniqueConstraint("AK_UserAccounts_EmployeeId_Id", x => new { x.EmployeeId, x.Id });
                table.CheckConstraint("CK_UserAccounts_Active", "[Status] <> 3 OR ([PasswordHash] IS NOT NULL AND [ActivatedAt] IS NOT NULL AND [InitialRoleAssignmentId] IS NOT NULL)");
                table.CheckConstraint("CK_UserAccounts_Counters", "[SecurityVersion] > 0 AND [FailedLoginCount] >= 0");
                table.CheckConstraint("CK_UserAccounts_Credentials", "([PasswordHash] IS NULL AND [PasswordHashFormat] IS NULL AND [PasswordChangedAt] IS NULL) OR ([PasswordHash] IS NOT NULL AND [PasswordHashFormat] IS NOT NULL AND [PasswordChangedAt] IS NOT NULL)");
                table.CheckConstraint("CK_UserAccounts_InitialAccess", "([InitialRoleAssignmentId] IS NULL AND [InitialAccessScopeId] IS NULL) OR ([InitialRoleAssignmentId] IS NOT NULL AND [InitialAccessScopeId] IS NOT NULL)");
                table.CheckConstraint("CK_UserAccounts_Invitation", "[Status] <> 2 OR ([CurrentSetupInvitationId] IS NOT NULL AND [InitialAccessScopeId] IS NOT NULL)");
                table.CheckConstraint("CK_UserAccounts_Status", "[Status] BETWEEN 0 AND 5");
                table.ForeignKey(
                    name: "FK_UserAccounts_AccountSetupInvitations_Id_CurrentSetupInvitationId",
                    columns: x => new { x.Id, x.CurrentSetupInvitationId },
                    principalSchema: "auth",
                    principalTable: "AccountSetupInvitations",
                    principalColumns: new[] { "UserAccountId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_UserAccounts_EmployeeOnboardingDecisions_EmployeeId_ApprovalDecisionId",
                    columns: x => new { x.EmployeeId, x.ApprovalDecisionId },
                    principalSchema: "hr",
                    principalTable: "EmployeeOnboardingDecisions",
                    principalColumns: new[] { "EmployeeId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_UserAccounts_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "hr",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_UserAccounts_UserAccessScopes_InitialRoleAssignmentId_InitialAccessScopeId",
                    columns: x => new { x.InitialRoleAssignmentId, x.InitialAccessScopeId },
                    principalSchema: "auth",
                    principalTable: "UserAccessScopes",
                    principalColumns: new[] { "UserRoleAssignmentId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "UserRoleAssignments",
            schema: "auth",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AssignedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EffectiveFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                EffectiveTo = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                RevokedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RevocationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserRoleAssignments", x => x.Id);
                table.UniqueConstraint("AK_UserRoleAssignments_UserAccountId_Id", x => new { x.UserAccountId, x.Id });
                table.CheckConstraint("CK_UserRoleAssignments_Dates", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
                table.CheckConstraint("CK_UserRoleAssignments_Revocation", "([RevokedAt] IS NULL AND [RevokedByUserId] IS NULL AND [RevocationReason] IS NULL) OR ([RevokedAt] IS NOT NULL AND [RevokedByUserId] IS NOT NULL AND [RevocationReason] IS NOT NULL)");
                table.ForeignKey(
                    name: "FK_UserRoleAssignments_Roles_RoleId",
                    column: x => x.RoleId,
                    principalSchema: "auth",
                    principalTable: "Roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_UserRoleAssignments_UserAccounts_EmployeeId_UserAccountId",
                    columns: x => new { x.EmployeeId, x.UserAccountId },
                    principalSchema: "auth",
                    principalTable: "UserAccounts",
                    principalColumns: new[] { "EmployeeId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "UserSessions",
            schema: "auth",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Client = table.Column<int>(type: "int", nullable: false),
                RegisteredDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                MfaChallengeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RefreshTokenFamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SecurityVersion = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                RevocationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserSessions", x => x.Id);
                table.CheckConstraint("CK_UserSessions_Client", "[Client] IN (1,2) AND ([Client] <> 2 OR [RegisteredDeviceId] IS NOT NULL)");
                table.CheckConstraint("CK_UserSessions_Lifetime", "[ExpiresAt] > [CreatedAt] AND ([RevokedAt] IS NULL OR [RevokedAt] >= [CreatedAt])");
                table.CheckConstraint("CK_UserSessions_Version", "[SecurityVersion] > 0");
                table.ForeignKey(
                    name: "FK_UserSessions_MfaChallenges_UserAccountId_MfaChallengeId",
                    columns: x => new { x.UserAccountId, x.MfaChallengeId },
                    principalSchema: "auth",
                    principalTable: "MfaChallenges",
                    principalColumns: new[] { "UserAccountId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_UserSessions_RegisteredDevices_UserAccountId_RegisteredDeviceId",
                    columns: x => new { x.UserAccountId, x.RegisteredDeviceId },
                    principalSchema: "auth",
                    principalTable: "RegisteredDevices",
                    principalColumns: new[] { "UserAccountId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_UserSessions_UserAccounts_UserAccountId",
                    column: x => x.UserAccountId,
                    principalSchema: "auth",
                    principalTable: "UserAccounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ZoneEvents",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DutyMonitoringWindowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ZoneAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ZoneVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Kind = table.Column<int>(type: "int", nullable: false),
                Source = table.Column<int>(type: "int", nullable: false),
                RegisteredDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                DeviceCapturedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                Latitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: false),
                Longitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: false),
                AccuracyMeters = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                IsOffline = table.Column<bool>(type: "bit", nullable: false),
                ShownInstruction = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ZoneEvents", x => x.Id);
                table.CheckConstraint("CK_ZoneEvents_Coordinates", "(([Latitude] IS NULL AND [Longitude] IS NULL AND [AccuracyMeters] IS NULL) OR ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180 AND ([AccuracyMeters] IS NULL OR [AccuracyMeters] >= 0)))");
                table.CheckConstraint("CK_ZoneEvents_DeviceCapturedAtUtc_Utc", "[DeviceCapturedAtUtc] IS NULL OR DATEPART(TZOFFSET, [DeviceCapturedAtUtc]) = 0");
                table.CheckConstraint("CK_ZoneEvents_Kind", "[Kind] BETWEEN 1 AND 5");
                table.CheckConstraint("CK_ZoneEvents_ReceivedAtUtc_Utc", "[ReceivedAtUtc] IS NULL OR DATEPART(TZOFFSET, [ReceivedAtUtc]) = 0");
                table.CheckConstraint("CK_ZoneEvents_Source", "[Source] BETWEEN 1 AND 3");
                table.ForeignKey(
                    name: "FK_ZoneEvents_DutyMonitoringWindows_EmployeeId_ZoneAssignmentId_DutyMonitoringWindowId",
                    columns: x => new { x.EmployeeId, x.ZoneAssignmentId, x.DutyMonitoringWindowId },
                    principalSchema: "attendance",
                    principalTable: "DutyMonitoringWindows",
                    principalColumns: new[] { "EmployeeId", "ZoneAssignmentId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ZoneEvents_RegisteredDevices_UserAccountId_RegisteredDeviceId",
                    columns: x => new { x.UserAccountId, x.RegisteredDeviceId },
                    principalSchema: "auth",
                    principalTable: "RegisteredDevices",
                    principalColumns: new[] { "UserAccountId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ZoneEvents_UserAccounts_EmployeeId_UserAccountId",
                    columns: x => new { x.EmployeeId, x.UserAccountId },
                    principalSchema: "auth",
                    principalTable: "UserAccounts",
                    principalColumns: new[] { "EmployeeId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ZoneEvents_ZoneAssignments_ZoneVersionId_ZoneAssignmentId",
                    columns: x => new { x.ZoneVersionId, x.ZoneAssignmentId },
                    principalSchema: "attendance",
                    principalTable: "ZoneAssignments",
                    principalColumns: new[] { "ZoneVersionId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ZoneEvents_ZoneVersions_ZoneVersionId",
                    column: x => x.ZoneVersionId,
                    principalSchema: "attendance",
                    principalTable: "ZoneVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ZoneEventResponses",
            schema: "attendance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ZoneEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Kind = table.Column<int>(type: "int", nullable: false),
                Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                AtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                PrivateEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ZoneEventResponses", x => x.Id);
                table.CheckConstraint("CK_ZoneEventResponses_AtUtc_Utc", "[AtUtc] IS NULL OR DATEPART(TZOFFSET, [AtUtc]) = 0");
                table.CheckConstraint("CK_ZoneEventResponses_Kind", "[Kind] BETWEEN 1 AND 3");
                table.CheckConstraint("CK_ZoneEventResponses_Reason", "[Kind] = 1 OR [Reason] IS NOT NULL");
                table.ForeignKey(
                    name: "FK_ZoneEventResponses_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalSchema: "workflow",
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ZoneEventResponses_ZoneEvents_ZoneEventId",
                    column: x => x.ZoneEventId,
                    principalSchema: "attendance",
                    principalTable: "ZoneEvents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Sites_CityId",
            schema: "attendance",
            table: "Sites",
            column: "CityId");

        migrationBuilder.CreateIndex(
            name: "IX_Sites_ProjectId",
            schema: "attendance",
            table: "Sites",
            column: "ProjectId");

        migrationBuilder.AddCheckConstraint(
            name: "CK_Sites_Dates",
            schema: "attendance",
            table: "Sites",
            sql: "[EndsOn] IS NULL OR ([StartsOn] IS NOT NULL AND [EndsOn] >= [StartsOn])");

        migrationBuilder.CreateIndex(
            name: "IX_RawPunchEvents_EvidenceFileId",
            schema: "attendance",
            table: "RawPunchEvents",
            column: "EvidenceFileId");

        migrationBuilder.CreateIndex(
            name: "IX_Employees_Id_ApprovedOnboardingDecisionId",
            schema: "hr",
            table: "Employees",
            columns: new[] { "Id", "ApprovedOnboardingDecisionId" });

        migrationBuilder.CreateIndex(
            name: "IX_Employees_OnboardingStatus_CreatedAt_Id",
            schema: "hr",
            table: "Employees",
            columns: new[] { "OnboardingStatus", "CreatedAt", "Id" });

        migrationBuilder.AddCheckConstraint(
            name: "CK_Employees_OnboardingApproval",
            schema: "hr",
            table: "Employees",
            sql: "([OnboardingStatus] = 2 AND [ApprovedOnboardingDecisionId] IS NOT NULL) OR ([OnboardingStatus] <> 2 AND [ApprovedOnboardingDecisionId] IS NULL)");

        migrationBuilder.AddCheckConstraint(
            name: "CK_Employees_OnboardingStatus",
            schema: "hr",
            table: "Employees",
            sql: "[OnboardingStatus] BETWEEN 0 AND 4");

        migrationBuilder.AddCheckConstraint(
            name: "CK_Employees_SubmittedVersion",
            schema: "hr",
            table: "Employees",
            sql: "[SubmittedVersion] >= 0 AND [DraftRevision] > 0");

        migrationBuilder.AddCheckConstraint(
            name: "CK_EmployeeDocuments_ScanEvidence",
            schema: "hr",
            table: "EmployeeDocuments",
            sql: "([ScanStatus] = 0 AND [ScannedAt] IS NULL) OR ([ScanStatus] <> 0 AND [ScannedAt] IS NOT NULL AND [ScannedAt] >= [UploadedAt] AND [ScannerVersion] IS NOT NULL AND ([ScanStatus] <> 1 OR [SignatureVerified] = 1))");

        migrationBuilder.AddCheckConstraint(
            name: "CK_EmployeeDocuments_VerificationEvidence",
            schema: "hr",
            table: "EmployeeDocuments",
            sql: "([VerificationStatus] = 0 AND [VerifiedAt] IS NULL AND [VerifiedByUserId] IS NULL) OR ([VerificationStatus] <> 0 AND [VerifiedAt] IS NOT NULL AND [VerifiedAt] >= [UploadedAt] AND [VerifiedByUserId] IS NOT NULL AND ([VerificationStatus] <> 2 OR [VerificationReason] IS NOT NULL))");

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeAddresses_CityId",
            schema: "hr",
            table: "EmployeeAddresses",
            column: "CityId");

        migrationBuilder.CreateIndex(
            name: "IX_Branches_CityId",
            schema: "hr",
            table: "Branches",
            column: "CityId");

        migrationBuilder.CreateIndex(
            name: "IX_AccountSetupInvitations_ApprovalDecisionId",
            schema: "auth",
            table: "AccountSetupInvitations",
            column: "ApprovalDecisionId");

        migrationBuilder.CreateIndex(
            name: "IX_AccountSetupInvitations_DeliveryStatus_NextDeliveryAttemptAt",
            schema: "auth",
            table: "AccountSetupInvitations",
            columns: new[] { "DeliveryStatus", "NextDeliveryAttemptAt" });

        migrationBuilder.CreateIndex(
            name: "IX_AccountSetupInvitations_TokenHash",
            schema: "auth",
            table: "AccountSetupInvitations",
            column: "TokenHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AccountSetupInvitations_UserAccountId_IssuedAt",
            schema: "auth",
            table: "AccountSetupInvitations",
            columns: new[] { "UserAccountId", "IssuedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_AdvanceLoanAgreements_AdvanceLoanRequestId",
            schema: "hr",
            table: "AdvanceLoanAgreements",
            column: "AdvanceLoanRequestId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AdvanceLoanAgreements_AdvanceLoanRequestId_Currency",
            schema: "hr",
            table: "AdvanceLoanAgreements",
            columns: new[] { "AdvanceLoanRequestId", "Currency" });

        migrationBuilder.CreateIndex(
            name: "IX_AdvanceLoanAgreements_ApprovalRequestId",
            schema: "hr",
            table: "AdvanceLoanAgreements",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_AdvanceLoanRequests_ApprovalRequestId",
            schema: "hr",
            table: "AdvanceLoanRequests",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_AdvanceLoanRequests_EmployeeId",
            schema: "hr",
            table: "AdvanceLoanRequests",
            column: "EmployeeId");

        migrationBuilder.CreateIndex(
            name: "IX_AllowanceClaims_ApprovalRequestId",
            schema: "hr",
            table: "AllowanceClaims",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_AllowanceClaims_DeploymentApprovalRequestId",
            schema: "hr",
            table: "AllowanceClaims",
            column: "DeploymentApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_AllowanceClaims_EmployeeAssignmentId_EmployeeId",
            schema: "hr",
            table: "AllowanceClaims",
            columns: new[] { "EmployeeAssignmentId", "EmployeeId" });

        migrationBuilder.CreateIndex(
            name: "IX_AllowanceClaims_EmployeeId_Kind_EligibilityKey",
            schema: "hr",
            table: "AllowanceClaims",
            columns: new[] { "EmployeeId", "Kind", "EligibilityKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AllowanceClaims_PolicyVersionId",
            schema: "hr",
            table: "AllowanceClaims",
            column: "PolicyVersionId");

        migrationBuilder.CreateIndex(
            name: "IX_AllowanceClaims_WorkSessionId_EmployeeId",
            schema: "hr",
            table: "AllowanceClaims",
            columns: new[] { "WorkSessionId", "EmployeeId" });

        migrationBuilder.CreateIndex(
            name: "IX_AllowanceSettlementRecords_AllowanceClaimId_IdempotencyKey",
            schema: "hr",
            table: "AllowanceSettlementRecords",
            columns: new[] { "AllowanceClaimId", "IdempotencyKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AllowanceSettlementRecords_PayrollHandoffLineId",
            schema: "hr",
            table: "AllowanceSettlementRecords",
            column: "PayrollHandoffLineId");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalDecisions_ApprovalRequestId",
            schema: "workflow",
            table: "ApprovalDecisions",
            column: "ApprovalRequestId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalDelegations_DelegateUserId_RequestType_StartsAt",
            schema: "workflow",
            table: "ApprovalDelegations",
            columns: new[] { "DelegateUserId", "RequestType", "StartsAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalRequests_RequestType_SubjectId_Stage",
            schema: "workflow",
            table: "ApprovalRequests",
            columns: new[] { "RequestType", "SubjectId", "Stage" });

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalRequests_RouteRuleId",
            schema: "workflow",
            table: "ApprovalRequests",
            column: "RouteRuleId");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalRequests_State_RequestType_RequestedAt",
            schema: "workflow",
            table: "ApprovalRequests",
            columns: new[] { "State", "RequestType", "RequestedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalRouteRules_Code_Version",
            schema: "workflow",
            table: "ApprovalRouteRules",
            columns: new[] { "Code", "Version" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalRouteRules_DepartmentId",
            schema: "workflow",
            table: "ApprovalRouteRules",
            column: "DepartmentId");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalRouteRules_RequestType_EffectiveFrom_EffectiveTo",
            schema: "workflow",
            table: "ApprovalRouteRules",
            columns: new[] { "RequestType", "EffectiveFrom", "EffectiveTo" });

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalRouteRules_SiteId",
            schema: "workflow",
            table: "ApprovalRouteRules",
            column: "SiteId");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalRouteRules_StaffCategoryId",
            schema: "workflow",
            table: "ApprovalRouteRules",
            column: "StaffCategoryId");

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceCalculationLines_DailyAttendanceSummaryId_Sequence",
            schema: "attendance",
            table: "AttendanceCalculationLines",
            columns: new[] { "DailyAttendanceSummaryId", "Sequence" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceCorrections_AdjustmentPeriodLockId",
            schema: "attendance",
            table: "AttendanceCorrections",
            column: "AdjustmentPeriodLockId");

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceCorrections_ApprovalRequestId",
            schema: "attendance",
            table: "AttendanceCorrections",
            column: "ApprovalRequestId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceCorrections_EmployeeId_AttendanceDate",
            schema: "attendance",
            table: "AttendanceCorrections",
            columns: new[] { "EmployeeId", "AttendanceDate" });

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceCorrections_EmployeeId_AttendanceDate_OriginalSummaryId",
            schema: "attendance",
            table: "AttendanceCorrections",
            columns: new[] { "EmployeeId", "AttendanceDate", "OriginalSummaryId" });

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceCorrections_EmployeeId_RawPunchEventId",
            schema: "attendance",
            table: "AttendanceCorrections",
            columns: new[] { "EmployeeId", "RawPunchEventId" });

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceExceptions_ApprovalRequestId",
            schema: "attendance",
            table: "AttendanceExceptions",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceExceptions_EmployeeId_AttendanceDate",
            schema: "attendance",
            table: "AttendanceExceptions",
            columns: new[] { "EmployeeId", "AttendanceDate" });

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceExceptions_EmployeeId_DailyAttendanceSummaryId",
            schema: "attendance",
            table: "AttendanceExceptions",
            columns: new[] { "EmployeeId", "DailyAttendanceSummaryId" });

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceExceptions_EmployeeId_RawPunchEventId",
            schema: "attendance",
            table: "AttendanceExceptions",
            columns: new[] { "EmployeeId", "RawPunchEventId" });

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceExceptions_EmployeeId_WorkSessionId",
            schema: "attendance",
            table: "AttendanceExceptions",
            columns: new[] { "EmployeeId", "WorkSessionId" });

        migrationBuilder.CreateIndex(
            name: "IX_AttendancePeriodLockEvents_ApprovalRequestId",
            schema: "attendance",
            table: "AttendancePeriodLockEvents",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_AttendancePeriodLockEvents_AttendancePeriodLockId_Revision",
            schema: "attendance",
            table: "AttendancePeriodLockEvents",
            columns: new[] { "AttendancePeriodLockId", "Revision" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AttendancePeriodLocks_PeriodStart_PeriodEndExclusive",
            schema: "attendance",
            table: "AttendancePeriodLocks",
            columns: new[] { "PeriodStart", "PeriodEndExclusive" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AttendancePeriodSummaries_AttendancePeriodLockEventId_EmployeeId_AttendanceDate",
            schema: "attendance",
            table: "AttendancePeriodSummaries",
            columns: new[] { "AttendancePeriodLockEventId", "EmployeeId", "AttendanceDate" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AttendancePeriodSummaries_EmployeeId_AttendanceDate_DailyAttendanceSummaryId",
            schema: "attendance",
            table: "AttendancePeriodSummaries",
            columns: new[] { "EmployeeId", "AttendanceDate", "DailyAttendanceSummaryId" });

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceSummarySessions_DailyAttendanceSummaryId_WorkSessionId",
            schema: "attendance",
            table: "AttendanceSummarySessions",
            columns: new[] { "DailyAttendanceSummaryId", "WorkSessionId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceSummarySessions_EmployeeId_DailyAttendanceSummaryId",
            schema: "attendance",
            table: "AttendanceSummarySessions",
            columns: new[] { "EmployeeId", "DailyAttendanceSummaryId" });

        migrationBuilder.CreateIndex(
            name: "IX_AttendanceSummarySessions_EmployeeId_WorkSessionId",
            schema: "attendance",
            table: "AttendanceSummarySessions",
            columns: new[] { "EmployeeId", "WorkSessionId" });

        migrationBuilder.CreateIndex(
            name: "IX_AuditEvents_ActorUserId_OccurredAt",
            schema: "audit",
            table: "AuditEvents",
            columns: new[] { "ActorUserId", "OccurredAt" });

        migrationBuilder.CreateIndex(
            name: "IX_AuditEvents_CorrelationId",
            schema: "audit",
            table: "AuditEvents",
            column: "CorrelationId");

        migrationBuilder.CreateIndex(
            name: "IX_AuditEvents_EmployeeScopeId",
            schema: "audit",
            table: "AuditEvents",
            column: "EmployeeScopeId");

        migrationBuilder.CreateIndex(
            name: "IX_AuditEvents_TargetType_TargetId_OccurredAt",
            schema: "audit",
            table: "AuditEvents",
            columns: new[] { "TargetType", "TargetId", "OccurredAt" });

        migrationBuilder.CreateIndex(
            name: "IX_Cities_Code",
            schema: "hr",
            table: "Cities",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CompensationRecords_ApprovalRequestId",
            schema: "hr",
            table: "CompensationRecords",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_CompensationRecords_EmployeeId_EffectiveFrom_Revision",
            schema: "hr",
            table: "CompensationRecords",
            columns: new[] { "EmployeeId", "EffectiveFrom", "Revision" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CompensationRecords_SupersedesCompensationRecordId_EmployeeId",
            schema: "hr",
            table: "CompensationRecords",
            columns: new[] { "SupersedesCompensationRecordId", "EmployeeId" });

        migrationBuilder.CreateIndex(
            name: "IX_DailyAttendanceSummaries_EmployeeId_AttendanceDate_Revision",
            schema: "attendance",
            table: "DailyAttendanceSummaries",
            columns: new[] { "EmployeeId", "AttendanceDate", "Revision" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_DailyAttendanceSummaries_EmployeeId_AttendanceDate_SupersedesSummaryId",
            schema: "attendance",
            table: "DailyAttendanceSummaries",
            columns: new[] { "EmployeeId", "AttendanceDate", "SupersedesSummaryId" });

        migrationBuilder.CreateIndex(
            name: "IX_DailyAttendanceSummaries_PolicyVersionId",
            schema: "attendance",
            table: "DailyAttendanceSummaries",
            column: "PolicyVersionId");

        migrationBuilder.CreateIndex(
            name: "IX_DailyAttendanceSummaries_ShiftVersionId",
            schema: "attendance",
            table: "DailyAttendanceSummaries",
            column: "ShiftVersionId");

        migrationBuilder.CreateIndex(
            name: "IX_DisciplinaryCases_ApprovalRequestId",
            schema: "hr",
            table: "DisciplinaryCases",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_DisciplinaryCases_EmployeeId",
            schema: "hr",
            table: "DisciplinaryCases",
            column: "EmployeeId");

        migrationBuilder.CreateIndex(
            name: "IX_DisciplinaryCases_PolicyImpactAssessmentId_EmployeeId",
            schema: "hr",
            table: "DisciplinaryCases",
            columns: new[] { "PolicyImpactAssessmentId", "EmployeeId" });

        migrationBuilder.CreateIndex(
            name: "IX_DutyMonitoringWindows_EmployeeId_StartsAtUtc",
            schema: "attendance",
            table: "DutyMonitoringWindows",
            columns: new[] { "EmployeeId", "StartsAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_DutyMonitoringWindows_RetentionPolicyVersionId",
            schema: "attendance",
            table: "DutyMonitoringWindows",
            column: "RetentionPolicyVersionId");

        migrationBuilder.CreateIndex(
            name: "IX_DutyMonitoringWindows_ZoneAssignmentId",
            schema: "attendance",
            table: "DutyMonitoringWindows",
            column: "ZoneAssignmentId");

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeAssetCustodies_AssetReference",
            schema: "hr",
            table: "EmployeeAssetCustodies",
            column: "AssetReference",
            unique: true,
            filter: "[ReturnedAt] IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeAssetCustodies_EmployeeId_IssuedAt",
            schema: "hr",
            table: "EmployeeAssetCustodies",
            columns: new[] { "EmployeeId", "IssuedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeBankDetails_EmployeeId",
            schema: "hr",
            table: "EmployeeBankDetails",
            column: "EmployeeId");

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeBankDetails_SupersedesEmployeeBankDetailsId",
            schema: "hr",
            table: "EmployeeBankDetails",
            column: "SupersedesEmployeeBankDetailsId",
            unique: true,
            filter: "[SupersedesEmployeeBankDetailsId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeBankDetails_SupersedesEmployeeBankDetailsId_EmployeeId",
            schema: "hr",
            table: "EmployeeBankDetails",
            columns: new[] { "SupersedesEmployeeBankDetailsId", "EmployeeId" });

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeCertifications_EmployeeDocumentId_EmployeeId",
            schema: "hr",
            table: "EmployeeCertifications",
            columns: new[] { "EmployeeDocumentId", "EmployeeId" });

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeCertifications_EmployeeId_ExpiresOn",
            schema: "hr",
            table: "EmployeeCertifications",
            columns: new[] { "EmployeeId", "ExpiresOn" });

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeCommuteDistances_EmployeeId",
            schema: "hr",
            table: "EmployeeCommuteDistances",
            column: "EmployeeId");

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeCommuteDistances_SupersedesEmployeeCommuteDistanceId",
            schema: "hr",
            table: "EmployeeCommuteDistances",
            column: "SupersedesEmployeeCommuteDistanceId",
            unique: true,
            filter: "[SupersedesEmployeeCommuteDistanceId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeCommuteDistances_SupersedesEmployeeCommuteDistanceId_EmployeeId",
            schema: "hr",
            table: "EmployeeCommuteDistances",
            columns: new[] { "SupersedesEmployeeCommuteDistanceId", "EmployeeId" });

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeMedicalRecords_EmployeeDocumentId_EmployeeId",
            schema: "hr",
            table: "EmployeeMedicalRecords",
            columns: new[] { "EmployeeDocumentId", "EmployeeId" });

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeMedicalRecords_EmployeeId",
            schema: "hr",
            table: "EmployeeMedicalRecords",
            column: "EmployeeId");

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeOnboardingDecisions_ActorRoleId",
            schema: "hr",
            table: "EmployeeOnboardingDecisions",
            column: "ActorRoleId");

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeOnboardingDecisions_EmployeeId_DecidedAt",
            schema: "hr",
            table: "EmployeeOnboardingDecisions",
            columns: new[] { "EmployeeId", "DecidedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeOnboardingDecisions_EmployeeId_SubmissionId",
            schema: "hr",
            table: "EmployeeOnboardingDecisions",
            columns: new[] { "EmployeeId", "SubmissionId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeOnboardingDecisions_SubmissionId",
            schema: "hr",
            table: "EmployeeOnboardingDecisions",
            column: "SubmissionId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeSimAssignments_EmployeeId",
            schema: "hr",
            table: "EmployeeSimAssignments",
            column: "EmployeeId");

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeSimAssignments_SupersedesEmployeeSimAssignmentId",
            schema: "hr",
            table: "EmployeeSimAssignments",
            column: "SupersedesEmployeeSimAssignmentId",
            unique: true,
            filter: "[SupersedesEmployeeSimAssignmentId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeSimAssignments_SupersedesEmployeeSimAssignmentId_EmployeeId",
            schema: "hr",
            table: "EmployeeSimAssignments",
            columns: new[] { "SupersedesEmployeeSimAssignmentId", "EmployeeId" });

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeSubmissions_EmployeeId_Version",
            schema: "hr",
            table: "EmployeeSubmissions",
            columns: new[] { "EmployeeId", "Version" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeSubmissions_SubmittedAt_Id",
            schema: "hr",
            table: "EmployeeSubmissions",
            columns: new[] { "SubmittedAt", "Id" });

        migrationBuilder.CreateIndex(
            name: "IX_FaceEnrollments_EmployeeId",
            schema: "attendance",
            table: "FaceEnrollments",
            column: "EmployeeId",
            unique: true,
            filter: "[IsActive] = 1");

        migrationBuilder.CreateIndex(
            name: "IX_FaceEnrollments_EmployeeId_ReplacesEnrollmentId",
            schema: "attendance",
            table: "FaceEnrollments",
            columns: new[] { "EmployeeId", "ReplacesEnrollmentId" });

        migrationBuilder.CreateIndex(
            name: "IX_FaceVerificationAttempts_ChallengeSha256",
            schema: "attendance",
            table: "FaceVerificationAttempts",
            column: "ChallengeSha256",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_FaceVerificationAttempts_ConsumedByPunchId",
            schema: "attendance",
            table: "FaceVerificationAttempts",
            column: "ConsumedByPunchId",
            unique: true,
            filter: "[ConsumedByPunchId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_FaceVerificationAttempts_EmployeeId_FaceEnrollmentId",
            schema: "attendance",
            table: "FaceVerificationAttempts",
            columns: new[] { "EmployeeId", "FaceEnrollmentId" });

        migrationBuilder.CreateIndex(
            name: "IX_FaceVerificationAttempts_EmployeeId_PunchIdempotencyKey_IssuedAtUtc",
            schema: "attendance",
            table: "FaceVerificationAttempts",
            columns: new[] { "EmployeeId", "PunchIdempotencyKey", "IssuedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_FaceVerificationAttempts_EmployeeId_RegisteredDeviceId_PunchIdempotencyKey_ConsumedByPunchId",
            schema: "attendance",
            table: "FaceVerificationAttempts",
            columns: new[] { "EmployeeId", "RegisteredDeviceId", "PunchIdempotencyKey", "ConsumedByPunchId" });

        migrationBuilder.CreateIndex(
            name: "IX_FaceVerificationAttempts_EmployeeId_UserAccountId",
            schema: "attendance",
            table: "FaceVerificationAttempts",
            columns: new[] { "EmployeeId", "UserAccountId" });

        migrationBuilder.CreateIndex(
            name: "IX_FaceVerificationAttempts_UserAccountId_RegisteredDeviceKey",
            schema: "attendance",
            table: "FaceVerificationAttempts",
            columns: new[] { "UserAccountId", "RegisteredDeviceKey" });

        migrationBuilder.CreateIndex(
            name: "IX_FaceVerificationExceptions_ApprovalRequestId",
            schema: "attendance",
            table: "FaceVerificationExceptions",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_FaceVerificationExceptions_EmployeeId_EffectiveFrom",
            schema: "attendance",
            table: "FaceVerificationExceptions",
            columns: new[] { "EmployeeId", "EffectiveFrom" });

        migrationBuilder.CreateIndex(
            name: "IX_FuelClaims_ApprovalRequestId",
            schema: "hr",
            table: "FuelClaims",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_FuelClaims_EmployeeCommuteDistanceId_EmployeeId",
            schema: "hr",
            table: "FuelClaims",
            columns: new[] { "EmployeeCommuteDistanceId", "EmployeeId" });

        migrationBuilder.CreateIndex(
            name: "IX_FuelClaims_EmployeeId",
            schema: "hr",
            table: "FuelClaims",
            column: "EmployeeId");

        migrationBuilder.CreateIndex(
            name: "IX_FuelClaims_PolicyVersionId",
            schema: "hr",
            table: "FuelClaims",
            column: "PolicyVersionId");

        migrationBuilder.CreateIndex(
            name: "IX_FuelClaims_VehicleTripId_EmployeeId",
            schema: "hr",
            table: "FuelClaims",
            columns: new[] { "VehicleTripId", "EmployeeId" });

        migrationBuilder.CreateIndex(
            name: "IX_HolidayCalendars_Code_EffectiveFrom",
            schema: "hr",
            table: "HolidayCalendars",
            columns: new[] { "Code", "EffectiveFrom" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Holidays_HolidayCalendarId_HolidayDate",
            schema: "hr",
            table: "Holidays",
            columns: new[] { "HolidayCalendarId", "HolidayDate" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_IdempotencyReceipts_ActorUserId_Operation_Key",
            schema: "workflow",
            table: "IdempotencyReceipts",
            columns: new[] { "ActorUserId", "Operation", "Key" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_IdempotencyReceipts_ExpiresAt",
            schema: "workflow",
            table: "IdempotencyReceipts",
            column: "ExpiresAt");

        migrationBuilder.CreateIndex(
            name: "IX_InsuranceExpiryReminders_InsurancePolicyId_RecipientUserId_Channel_RemindAt",
            schema: "hr",
            table: "InsuranceExpiryReminders",
            columns: new[] { "InsurancePolicyId", "RecipientUserId", "Channel", "RemindAt" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_InsuranceExpiryReminders_NotificationId",
            schema: "hr",
            table: "InsuranceExpiryReminders",
            column: "NotificationId",
            unique: true,
            filter: "[NotificationId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_InsuranceExpiryReminders_RemindAt",
            schema: "hr",
            table: "InsuranceExpiryReminders",
            column: "RemindAt",
            filter: "[NotificationId] IS NULL AND [CancelledAt] IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_InsurancePolicies_EmployeeId_ExpiryDate",
            schema: "hr",
            table: "InsurancePolicies",
            columns: new[] { "EmployeeId", "ExpiryDate" },
            filter: "[EmployeeId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_InsurancePolicies_ExpiryDate",
            schema: "hr",
            table: "InsurancePolicies",
            column: "ExpiryDate");

        migrationBuilder.CreateIndex(
            name: "IX_InsurancePolicies_StoredFileId",
            schema: "hr",
            table: "InsurancePolicies",
            column: "StoredFileId");

        migrationBuilder.CreateIndex(
            name: "IX_InsurancePolicies_SupersedesPolicyId",
            schema: "hr",
            table: "InsurancePolicies",
            column: "SupersedesPolicyId",
            unique: true,
            filter: "[SupersedesPolicyId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_InsurancePolicies_VehicleId_ExpiryDate",
            schema: "hr",
            table: "InsurancePolicies",
            columns: new[] { "VehicleId", "ExpiryDate" },
            filter: "[VehicleId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_LeaveCancellationRequests_ApprovalRequestId",
            schema: "hr",
            table: "LeaveCancellationRequests",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_LeaveCancellationRequests_LeaveRequestId",
            schema: "hr",
            table: "LeaveCancellationRequests",
            column: "LeaveRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_LeaveEncashmentRequests_ApprovalRequestId",
            schema: "hr",
            table: "LeaveEncashmentRequests",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_LeaveEncashmentRequests_EmployeeId",
            schema: "hr",
            table: "LeaveEncashmentRequests",
            column: "EmployeeId");

        migrationBuilder.CreateIndex(
            name: "IX_LeaveEncashmentRequests_PolicyVersionId",
            schema: "hr",
            table: "LeaveEncashmentRequests",
            column: "PolicyVersionId");

        migrationBuilder.CreateIndex(
            name: "IX_LeaveLedgerEntries_EmployeeId_IdempotencyKey",
            schema: "hr",
            table: "LeaveLedgerEntries",
            columns: new[] { "EmployeeId", "IdempotencyKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_LeaveLedgerEntries_EmployeeId_LeaveTypeId_LeaveYearStart",
            schema: "hr",
            table: "LeaveLedgerEntries",
            columns: new[] { "EmployeeId", "LeaveTypeId", "LeaveYearStart" });

        migrationBuilder.CreateIndex(
            name: "IX_LeaveLedgerEntries_LeaveRequestId_EmployeeId_LeaveTypeId",
            schema: "hr",
            table: "LeaveLedgerEntries",
            columns: new[] { "LeaveRequestId", "EmployeeId", "LeaveTypeId" });

        migrationBuilder.CreateIndex(
            name: "IX_LeaveLedgerEntries_LeaveTypeId",
            schema: "hr",
            table: "LeaveLedgerEntries",
            column: "LeaveTypeId");

        migrationBuilder.CreateIndex(
            name: "IX_LeaveLedgerEntries_PolicyVersionId",
            schema: "hr",
            table: "LeaveLedgerEntries",
            column: "PolicyVersionId");

        migrationBuilder.CreateIndex(
            name: "IX_LeaveLedgerEntries_ReversesEntryId_EmployeeId_LeaveTypeId_LeaveYearStart_LeaveYearEnd",
            schema: "hr",
            table: "LeaveLedgerEntries",
            columns: new[] { "ReversesEntryId", "EmployeeId", "LeaveTypeId", "LeaveYearStart", "LeaveYearEnd" });

        migrationBuilder.CreateIndex(
            name: "IX_LeaveRecoveryCommitments_ApprovalRequestId",
            schema: "hr",
            table: "LeaveRecoveryCommitments",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_LeaveRecoveryCommitments_LeaveRequestId",
            schema: "hr",
            table: "LeaveRecoveryCommitments",
            column: "LeaveRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_LeaveRecoveryCommitments_PolicyVersionId",
            schema: "hr",
            table: "LeaveRecoveryCommitments",
            column: "PolicyVersionId");

        migrationBuilder.CreateIndex(
            name: "IX_LeaveRequests_ApprovalRequestId",
            schema: "hr",
            table: "LeaveRequests",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_LeaveRequests_EmployeeId",
            schema: "hr",
            table: "LeaveRequests",
            column: "EmployeeId");

        migrationBuilder.CreateIndex(
            name: "IX_LeaveRequests_LeaveTypeId",
            schema: "hr",
            table: "LeaveRequests",
            column: "LeaveTypeId");

        migrationBuilder.CreateIndex(
            name: "IX_LeaveRequests_PolicyVersionId",
            schema: "hr",
            table: "LeaveRequests",
            column: "PolicyVersionId");

        migrationBuilder.CreateIndex(
            name: "IX_LeaveRequests_RelieverEmployeeId",
            schema: "hr",
            table: "LeaveRequests",
            column: "RelieverEmployeeId");

        migrationBuilder.CreateIndex(
            name: "IX_LeaveTypes_Code",
            schema: "hr",
            table: "LeaveTypes",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MfaChallenges_ChallengeHash",
            schema: "auth",
            table: "MfaChallenges",
            column: "ChallengeHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MfaChallenges_UserAccountId_IssuedAt",
            schema: "auth",
            table: "MfaChallenges",
            columns: new[] { "UserAccountId", "IssuedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_MfaChallenges_UserAccountId_RegisteredDeviceId",
            schema: "auth",
            table: "MfaChallenges",
            columns: new[] { "UserAccountId", "RegisteredDeviceId" });

        migrationBuilder.CreateIndex(
            name: "IX_MfaChallenges_UserAccountId_VerifiedCredentialId",
            schema: "auth",
            table: "MfaChallenges",
            columns: new[] { "UserAccountId", "VerifiedCredentialId" });

        migrationBuilder.CreateIndex(
            name: "IX_MfaCredentials_RecoveryCodeHash",
            schema: "auth",
            table: "MfaCredentials",
            column: "RecoveryCodeHash",
            unique: true,
            filter: "[RecoveryCodeHash] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_MfaCredentials_UserAccountId_RevokedAt",
            schema: "auth",
            table: "MfaCredentials",
            columns: new[] { "UserAccountId", "RevokedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_RecipientUserId_ReadAt_CreatedAt",
            schema: "communication",
            table: "Notifications",
            columns: new[] { "RecipientUserId", "ReadAt", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_OperationalZones_Code",
            schema: "attendance",
            table: "OperationalZones",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_OperationalZones_SiteId",
            schema: "attendance",
            table: "OperationalZones",
            column: "SiteId");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_MessageType_DeduplicationKey",
            schema: "jobs",
            table: "OutboxMessages",
            columns: new[] { "MessageType", "DeduplicationKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_State_AvailableAt_LeaseExpiresAt",
            schema: "jobs",
            table: "OutboxMessages",
            columns: new[] { "State", "AvailableAt", "LeaseExpiresAt" });

        migrationBuilder.CreateIndex(
            name: "IX_OvertimeRequests_ApprovalRequestId",
            schema: "attendance",
            table: "OvertimeRequests",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_OvertimeRequests_BulkSubmissionId",
            schema: "attendance",
            table: "OvertimeRequests",
            column: "BulkSubmissionId");

        migrationBuilder.CreateIndex(
            name: "IX_OvertimeRequests_EmployeeId_StartsAtUtc",
            schema: "attendance",
            table: "OvertimeRequests",
            columns: new[] { "EmployeeId", "StartsAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_OvertimeRequests_EmployeeId_WorkSessionId",
            schema: "attendance",
            table: "OvertimeRequests",
            columns: new[] { "EmployeeId", "WorkSessionId" });

        migrationBuilder.CreateIndex(
            name: "IX_OvertimeRequests_PolicyVersionId",
            schema: "attendance",
            table: "OvertimeRequests",
            column: "PolicyVersionId");

        migrationBuilder.CreateIndex(
            name: "IX_OvertimeRequests_SiteId",
            schema: "attendance",
            table: "OvertimeRequests",
            column: "SiteId");

        migrationBuilder.CreateIndex(
            name: "IX_PasswordResetChallenges_TokenHash",
            schema: "auth",
            table: "PasswordResetChallenges",
            column: "TokenHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PasswordResetChallenges_UserAccountId_IssuedAt",
            schema: "auth",
            table: "PasswordResetChallenges",
            columns: new[] { "UserAccountId", "IssuedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_PayrollHandoffBatches_ApprovalRequestId",
            schema: "hr",
            table: "PayrollHandoffBatches",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_PayrollHandoffBatches_BatchReference",
            schema: "hr",
            table: "PayrollHandoffBatches",
            column: "BatchReference",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PayrollHandoffLines_ApprovalRequestId",
            schema: "hr",
            table: "PayrollHandoffLines",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_PayrollHandoffLines_AttendancePeriodLockEventId",
            schema: "hr",
            table: "PayrollHandoffLines",
            column: "AttendancePeriodLockEventId");

        migrationBuilder.CreateIndex(
            name: "IX_PayrollHandoffLines_EmployeeId",
            schema: "hr",
            table: "PayrollHandoffLines",
            column: "EmployeeId");

        migrationBuilder.CreateIndex(
            name: "IX_PayrollHandoffLines_PayrollHandoffBatchId_Currency",
            schema: "hr",
            table: "PayrollHandoffLines",
            columns: new[] { "PayrollHandoffBatchId", "Currency" });

        migrationBuilder.CreateIndex(
            name: "IX_PayrollHandoffLines_PayrollHandoffBatchId_EmployeeId_SourceType_SourceId_SourceVersion_Component",
            schema: "hr",
            table: "PayrollHandoffLines",
            columns: new[] { "PayrollHandoffBatchId", "EmployeeId", "SourceType", "SourceId", "SourceVersion", "Component" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PayrollHandoffLines_PolicyVersionId",
            schema: "hr",
            table: "PayrollHandoffLines",
            column: "PolicyVersionId");

        migrationBuilder.CreateIndex(
            name: "IX_PayrollHandoffLines_TraceId",
            schema: "hr",
            table: "PayrollHandoffLines",
            column: "TraceId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PayrollHandoffProcessingRecords_PayrollHandoffBatchId_ExternalProcessingReference",
            schema: "hr",
            table: "PayrollHandoffProcessingRecords",
            columns: new[] { "PayrollHandoffBatchId", "ExternalProcessingReference" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Permissions_Code",
            schema: "auth",
            table: "Permissions",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Policies_Code",
            schema: "hr",
            table: "Policies",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PolicyAssignmentEnds_ApprovalRequestId",
            schema: "hr",
            table: "PolicyAssignmentEnds",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_PolicyAssignmentEnds_PolicyAssignmentId",
            schema: "hr",
            table: "PolicyAssignmentEnds",
            column: "PolicyAssignmentId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PolicyAssignments_ApprovalRequestId",
            schema: "hr",
            table: "PolicyAssignments",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_PolicyAssignments_DepartmentId",
            schema: "hr",
            table: "PolicyAssignments",
            column: "DepartmentId");

        migrationBuilder.CreateIndex(
            name: "IX_PolicyAssignments_EmployeeId",
            schema: "hr",
            table: "PolicyAssignments",
            column: "EmployeeId");

        migrationBuilder.CreateIndex(
            name: "IX_PolicyAssignments_PolicyVersionId",
            schema: "hr",
            table: "PolicyAssignments",
            column: "PolicyVersionId");

        migrationBuilder.CreateIndex(
            name: "IX_PolicyAssignments_RoleId",
            schema: "hr",
            table: "PolicyAssignments",
            column: "RoleId");

        migrationBuilder.CreateIndex(
            name: "IX_PolicyAssignments_SiteId",
            schema: "hr",
            table: "PolicyAssignments",
            column: "SiteId");

        migrationBuilder.CreateIndex(
            name: "IX_PolicyAssignments_StaffCategoryId",
            schema: "hr",
            table: "PolicyAssignments",
            column: "StaffCategoryId");

        migrationBuilder.CreateIndex(
            name: "IX_PolicyImpactAssessments_ApprovalRequestId",
            schema: "hr",
            table: "PolicyImpactAssessments",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_PolicyImpactAssessments_EmployeeId",
            schema: "hr",
            table: "PolicyImpactAssessments",
            column: "EmployeeId");

        migrationBuilder.CreateIndex(
            name: "IX_PolicyImpactAssessments_PolicyVersionId",
            schema: "hr",
            table: "PolicyImpactAssessments",
            column: "PolicyVersionId");

        migrationBuilder.CreateIndex(
            name: "IX_PolicyVersions_ApprovalRequestId",
            schema: "hr",
            table: "PolicyVersions",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_PolicyVersions_PolicyId_VersionNumber",
            schema: "hr",
            table: "PolicyVersions",
            columns: new[] { "PolicyId", "VersionNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PreviousEmployments_EmployeeId",
            schema: "hr",
            table: "PreviousEmployments",
            column: "EmployeeId");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectMembers_EmployeeId_EffectiveFrom_EffectiveTo",
            schema: "auth",
            table: "ProjectMembers",
            columns: new[] { "EmployeeId", "EffectiveFrom", "EffectiveTo" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectMembers_ProjectId_EmployeeId_EffectiveFrom",
            schema: "auth",
            table: "ProjectMembers",
            columns: new[] { "ProjectId", "EmployeeId", "EffectiveFrom" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Projects_Code",
            schema: "auth",
            table: "Projects",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PunchAssessments_EmployeeId_FaceVerificationAttemptId",
            schema: "attendance",
            table: "PunchAssessments",
            columns: new[] { "EmployeeId", "FaceVerificationAttemptId" });

        migrationBuilder.CreateIndex(
            name: "IX_PunchAssessments_EmployeeId_RawPunchEventId",
            schema: "attendance",
            table: "PunchAssessments",
            columns: new[] { "EmployeeId", "RawPunchEventId" });

        migrationBuilder.CreateIndex(
            name: "IX_PunchAssessments_PolicyVersionId",
            schema: "attendance",
            table: "PunchAssessments",
            column: "PolicyVersionId");

        migrationBuilder.CreateIndex(
            name: "IX_PunchAssessments_RawPunchEventId_Revision",
            schema: "attendance",
            table: "PunchAssessments",
            columns: new[] { "RawPunchEventId", "Revision" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RefreshTokens_ReplacedByTokenId",
            schema: "auth",
            table: "RefreshTokens",
            column: "ReplacedByTokenId",
            unique: true,
            filter: "[ReplacedByTokenId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_RefreshTokens_TokenHash",
            schema: "auth",
            table: "RefreshTokens",
            column: "TokenHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RefreshTokens_UserSessionId_ExpiresAt",
            schema: "auth",
            table: "RefreshTokens",
            columns: new[] { "UserSessionId", "ExpiresAt" });

        migrationBuilder.CreateIndex(
            name: "IX_RefreshTokens_UserSessionId_ReplacedByTokenId",
            schema: "auth",
            table: "RefreshTokens",
            columns: new[] { "UserSessionId", "ReplacedByTokenId" });

        migrationBuilder.CreateIndex(
            name: "IX_RegisteredDevices_UserAccountId_DeviceIdentifierHash",
            schema: "auth",
            table: "RegisteredDevices",
            columns: new[] { "UserAccountId", "DeviceIdentifierHash" },
            unique: true,
            filter: "[RevokedAt] IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_RepaymentLedgerEntries_AdvanceLoanAgreementId_Currency",
            schema: "hr",
            table: "RepaymentLedgerEntries",
            columns: new[] { "AdvanceLoanAgreementId", "Currency" });

        migrationBuilder.CreateIndex(
            name: "IX_RepaymentLedgerEntries_AdvanceLoanAgreementId_IdempotencyKey",
            schema: "hr",
            table: "RepaymentLedgerEntries",
            columns: new[] { "AdvanceLoanAgreementId", "IdempotencyKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RepaymentLedgerEntries_RepaymentScheduleEntryId_AdvanceLoanAgreementId_Currency",
            schema: "hr",
            table: "RepaymentLedgerEntries",
            columns: new[] { "RepaymentScheduleEntryId", "AdvanceLoanAgreementId", "Currency" });

        migrationBuilder.CreateIndex(
            name: "IX_RepaymentLedgerEntries_ReversesEntryId_AdvanceLoanAgreementId_Currency",
            schema: "hr",
            table: "RepaymentLedgerEntries",
            columns: new[] { "ReversesEntryId", "AdvanceLoanAgreementId", "Currency" });

        migrationBuilder.CreateIndex(
            name: "IX_RepaymentScheduleEntries_AdvanceLoanAgreementId_Currency",
            schema: "hr",
            table: "RepaymentScheduleEntries",
            columns: new[] { "AdvanceLoanAgreementId", "Currency" });

        migrationBuilder.CreateIndex(
            name: "IX_RepaymentScheduleEntries_AdvanceLoanAgreementId_Sequence",
            schema: "hr",
            table: "RepaymentScheduleEntries",
            columns: new[] { "AdvanceLoanAgreementId", "Sequence" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ReportExportJobs_RequestedByUserId_RequestedAt",
            schema: "reports",
            table: "ReportExportJobs",
            columns: new[] { "RequestedByUserId", "RequestedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ReportExportJobs_State",
            schema: "reports",
            table: "ReportExportJobs",
            column: "State");

        migrationBuilder.CreateIndex(
            name: "IX_ReportExportJobs_StoredFileId",
            schema: "reports",
            table: "ReportExportJobs",
            column: "StoredFileId");

        migrationBuilder.CreateIndex(
            name: "IX_RolePermissions_PermissionId",
            schema: "auth",
            table: "RolePermissions",
            column: "PermissionId");

        migrationBuilder.CreateIndex(
            name: "IX_RolePermissions_RoleId_PermissionId",
            schema: "auth",
            table: "RolePermissions",
            columns: new[] { "RoleId", "PermissionId" },
            unique: true,
            filter: "[RevokedAt] IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_Roles_Code",
            schema: "auth",
            table: "Roles",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ShiftPopulationAssignments_ApprovalRequestId",
            schema: "attendance",
            table: "ShiftPopulationAssignments",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_ShiftPopulationAssignments_DepartmentId_EffectiveFrom",
            schema: "attendance",
            table: "ShiftPopulationAssignments",
            columns: new[] { "DepartmentId", "EffectiveFrom" });

        migrationBuilder.CreateIndex(
            name: "IX_ShiftPopulationAssignments_ShiftId",
            schema: "attendance",
            table: "ShiftPopulationAssignments",
            column: "ShiftId");

        migrationBuilder.CreateIndex(
            name: "IX_ShiftPopulationAssignments_SiteId_EffectiveFrom",
            schema: "attendance",
            table: "ShiftPopulationAssignments",
            columns: new[] { "SiteId", "EffectiveFrom" });

        migrationBuilder.CreateIndex(
            name: "IX_ShiftPopulationAssignments_StaffCategoryId_EffectiveFrom",
            schema: "attendance",
            table: "ShiftPopulationAssignments",
            columns: new[] { "StaffCategoryId", "EffectiveFrom" });

        migrationBuilder.CreateIndex(
            name: "IX_ShiftPopulationAssignments_TeamId_EffectiveFrom",
            schema: "attendance",
            table: "ShiftPopulationAssignments",
            columns: new[] { "TeamId", "EffectiveFrom" });

        migrationBuilder.CreateIndex(
            name: "IX_SiteReportReviews_ApprovalRequestId",
            schema: "attendance",
            table: "SiteReportReviews",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_SiteReportReviews_SiteReportId_Sequence",
            schema: "attendance",
            table: "SiteReportReviews",
            columns: new[] { "SiteReportId", "Sequence" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SiteReports_EmployeeId_EmployeeAssignmentId",
            schema: "attendance",
            table: "SiteReports",
            columns: new[] { "EmployeeId", "EmployeeAssignmentId" });

        migrationBuilder.CreateIndex(
            name: "IX_SiteReports_EmployeeId_IdempotencyKey",
            schema: "attendance",
            table: "SiteReports",
            columns: new[] { "EmployeeId", "IdempotencyKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SiteReports_EmployeeId_UserAccountId",
            schema: "attendance",
            table: "SiteReports",
            columns: new[] { "EmployeeId", "UserAccountId" });

        migrationBuilder.CreateIndex(
            name: "IX_SiteReports_SiteId_ReceivedAtUtc",
            schema: "attendance",
            table: "SiteReports",
            columns: new[] { "SiteId", "ReceivedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_SiteReports_UserAccountId_RegisteredDeviceId",
            schema: "attendance",
            table: "SiteReports",
            columns: new[] { "UserAccountId", "RegisteredDeviceId" });

        migrationBuilder.CreateIndex(
            name: "IX_StoredFiles_ScanState_UploadedAt",
            schema: "files",
            table: "StoredFiles",
            columns: new[] { "ScanState", "UploadedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_StoredFiles_StorageKey",
            schema: "files",
            table: "StoredFiles",
            column: "StorageKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TeamMembers_EmployeeId_EffectiveFrom_EffectiveTo",
            schema: "auth",
            table: "TeamMembers",
            columns: new[] { "EmployeeId", "EffectiveFrom", "EffectiveTo" });

        migrationBuilder.CreateIndex(
            name: "IX_TeamMembers_TeamId_EmployeeId_EffectiveFrom",
            schema: "auth",
            table: "TeamMembers",
            columns: new[] { "TeamId", "EmployeeId", "EffectiveFrom" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Teams_Code",
            schema: "auth",
            table: "Teams",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TemporaryShiftChangeEmployees_EmployeeId",
            schema: "attendance",
            table: "TemporaryShiftChangeEmployees",
            column: "EmployeeId");

        migrationBuilder.CreateIndex(
            name: "IX_TemporaryShiftChangeEmployees_TemporaryShiftChangeId_EmployeeId",
            schema: "attendance",
            table: "TemporaryShiftChangeEmployees",
            columns: new[] { "TemporaryShiftChangeId", "EmployeeId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TemporaryShiftChanges_ApprovalRequestId",
            schema: "attendance",
            table: "TemporaryShiftChanges",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_TemporaryShiftChanges_OriginalShiftVersionId",
            schema: "attendance",
            table: "TemporaryShiftChanges",
            column: "OriginalShiftVersionId");

        migrationBuilder.CreateIndex(
            name: "IX_TemporaryShiftChanges_ReplacementShiftVersionId",
            schema: "attendance",
            table: "TemporaryShiftChanges",
            column: "ReplacementShiftVersionId");

        migrationBuilder.CreateIndex(
            name: "IX_TemporaryShiftChanges_SiteId_AttendanceDate",
            schema: "attendance",
            table: "TemporaryShiftChanges",
            columns: new[] { "SiteId", "AttendanceDate" });

        migrationBuilder.CreateIndex(
            name: "IX_UserAccessScopes_DepartmentId",
            schema: "auth",
            table: "UserAccessScopes",
            column: "DepartmentId");

        migrationBuilder.CreateIndex(
            name: "IX_UserAccessScopes_EmployeeId",
            schema: "auth",
            table: "UserAccessScopes",
            column: "EmployeeId");

        migrationBuilder.CreateIndex(
            name: "IX_UserAccessScopes_ProjectId",
            schema: "auth",
            table: "UserAccessScopes",
            column: "ProjectId");

        migrationBuilder.CreateIndex(
            name: "IX_UserAccessScopes_SiteId",
            schema: "auth",
            table: "UserAccessScopes",
            column: "SiteId");

        migrationBuilder.CreateIndex(
            name: "IX_UserAccessScopes_TeamId",
            schema: "auth",
            table: "UserAccessScopes",
            column: "TeamId");

        migrationBuilder.CreateIndex(
            name: "IX_UserAccessScopes_UserRoleAssignmentId_DepartmentId",
            schema: "auth",
            table: "UserAccessScopes",
            columns: new[] { "UserRoleAssignmentId", "DepartmentId" },
            unique: true,
            filter: "[DepartmentId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_UserAccessScopes_UserRoleAssignmentId_Kind",
            schema: "auth",
            table: "UserAccessScopes",
            columns: new[] { "UserRoleAssignmentId", "Kind" },
            unique: true,
            filter: "[Kind] = 7");

        migrationBuilder.CreateIndex(
            name: "IX_UserAccessScopes_UserRoleAssignmentId_Kind_EmployeeId",
            schema: "auth",
            table: "UserAccessScopes",
            columns: new[] { "UserRoleAssignmentId", "Kind", "EmployeeId" },
            unique: true,
            filter: "[EmployeeId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_UserAccessScopes_UserRoleAssignmentId_ProjectId",
            schema: "auth",
            table: "UserAccessScopes",
            columns: new[] { "UserRoleAssignmentId", "ProjectId" },
            unique: true,
            filter: "[ProjectId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_UserAccessScopes_UserRoleAssignmentId_SiteId",
            schema: "auth",
            table: "UserAccessScopes",
            columns: new[] { "UserRoleAssignmentId", "SiteId" },
            unique: true,
            filter: "[SiteId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_UserAccessScopes_UserRoleAssignmentId_TeamId",
            schema: "auth",
            table: "UserAccessScopes",
            columns: new[] { "UserRoleAssignmentId", "TeamId" },
            unique: true,
            filter: "[TeamId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_UserAccounts_EmployeeId",
            schema: "auth",
            table: "UserAccounts",
            column: "EmployeeId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserAccounts_EmployeeId_ApprovalDecisionId",
            schema: "auth",
            table: "UserAccounts",
            columns: new[] { "EmployeeId", "ApprovalDecisionId" });

        migrationBuilder.CreateIndex(
            name: "IX_UserAccounts_Id_CurrentSetupInvitationId",
            schema: "auth",
            table: "UserAccounts",
            columns: new[] { "Id", "CurrentSetupInvitationId" });

        migrationBuilder.CreateIndex(
            name: "IX_UserAccounts_Id_InitialRoleAssignmentId",
            schema: "auth",
            table: "UserAccounts",
            columns: new[] { "Id", "InitialRoleAssignmentId" });

        migrationBuilder.CreateIndex(
            name: "IX_UserAccounts_InitialRoleAssignmentId_InitialAccessScopeId",
            schema: "auth",
            table: "UserAccounts",
            columns: new[] { "InitialRoleAssignmentId", "InitialAccessScopeId" });

        migrationBuilder.CreateIndex(
            name: "IX_UserAccounts_NormalizedLoginName",
            schema: "auth",
            table: "UserAccounts",
            column: "NormalizedLoginName",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserAccounts_Status_Id",
            schema: "auth",
            table: "UserAccounts",
            columns: new[] { "Status", "Id" });

        migrationBuilder.CreateIndex(
            name: "IX_UserRoleAssignments_EmployeeId_UserAccountId",
            schema: "auth",
            table: "UserRoleAssignments",
            columns: new[] { "EmployeeId", "UserAccountId" });

        migrationBuilder.CreateIndex(
            name: "IX_UserRoleAssignments_RoleId",
            schema: "auth",
            table: "UserRoleAssignments",
            column: "RoleId");

        migrationBuilder.CreateIndex(
            name: "IX_UserRoleAssignments_UserAccountId_EffectiveFrom_EffectiveTo",
            schema: "auth",
            table: "UserRoleAssignments",
            columns: new[] { "UserAccountId", "EffectiveFrom", "EffectiveTo" });

        migrationBuilder.CreateIndex(
            name: "IX_UserRoleAssignments_UserAccountId_RoleId_EffectiveFrom",
            schema: "auth",
            table: "UserRoleAssignments",
            columns: new[] { "UserAccountId", "RoleId", "EffectiveFrom" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserSessions_MfaChallengeId",
            schema: "auth",
            table: "UserSessions",
            column: "MfaChallengeId",
            unique: true,
            filter: "[MfaChallengeId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_UserSessions_RefreshTokenFamilyId",
            schema: "auth",
            table: "UserSessions",
            column: "RefreshTokenFamilyId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserSessions_UserAccountId_MfaChallengeId",
            schema: "auth",
            table: "UserSessions",
            columns: new[] { "UserAccountId", "MfaChallengeId" });

        migrationBuilder.CreateIndex(
            name: "IX_UserSessions_UserAccountId_RegisteredDeviceId",
            schema: "auth",
            table: "UserSessions",
            columns: new[] { "UserAccountId", "RegisteredDeviceId" });

        migrationBuilder.CreateIndex(
            name: "IX_UserSessions_UserAccountId_RevokedAt_ExpiresAt",
            schema: "auth",
            table: "UserSessions",
            columns: new[] { "UserAccountId", "RevokedAt", "ExpiresAt" });

        migrationBuilder.CreateIndex(
            name: "IX_VehicleCustodies_EmployeeId",
            schema: "hr",
            table: "VehicleCustodies",
            column: "EmployeeId");

        migrationBuilder.CreateIndex(
            name: "IX_VehicleCustodies_VehicleId",
            schema: "hr",
            table: "VehicleCustodies",
            column: "VehicleId",
            unique: true,
            filter: "[ReturnedAt] IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_VehicleIncidents_ApprovalRequestId",
            schema: "hr",
            table: "VehicleIncidents",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_VehicleIncidents_VehicleCustodyId",
            schema: "hr",
            table: "VehicleIncidents",
            column: "VehicleCustodyId");

        migrationBuilder.CreateIndex(
            name: "IX_VehicleMileageEntries_ApprovalRequestId",
            schema: "hr",
            table: "VehicleMileageEntries",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_VehicleMileageEntries_EmployeeId_BusinessDate",
            schema: "hr",
            table: "VehicleMileageEntries",
            columns: new[] { "EmployeeId", "BusinessDate" });

        migrationBuilder.CreateIndex(
            name: "IX_VehicleMileageEntries_PolicyVersionId",
            schema: "hr",
            table: "VehicleMileageEntries",
            column: "PolicyVersionId");

        migrationBuilder.CreateIndex(
            name: "IX_VehicleMileageEntries_VehicleId_IdempotencyKey",
            schema: "hr",
            table: "VehicleMileageEntries",
            columns: new[] { "VehicleId", "IdempotencyKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_VehicleMileageEntries_VehicleTripId_EmployeeId_VehicleId",
            schema: "hr",
            table: "VehicleMileageEntries",
            columns: new[] { "VehicleTripId", "EmployeeId", "VehicleId" });

        migrationBuilder.CreateIndex(
            name: "IX_Vehicles_RegistrationNumber",
            schema: "hr",
            table: "Vehicles",
            column: "RegistrationNumber",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_VehicleServiceRecords_ApprovalRequestId",
            schema: "hr",
            table: "VehicleServiceRecords",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_VehicleServiceRecords_EmployeeId",
            schema: "hr",
            table: "VehicleServiceRecords",
            column: "EmployeeId");

        migrationBuilder.CreateIndex(
            name: "IX_VehicleServiceRecords_PolicyVersionId",
            schema: "hr",
            table: "VehicleServiceRecords",
            column: "PolicyVersionId");

        migrationBuilder.CreateIndex(
            name: "IX_VehicleServiceRecords_VehicleId",
            schema: "hr",
            table: "VehicleServiceRecords",
            column: "VehicleId");

        migrationBuilder.CreateIndex(
            name: "IX_VehicleTrips_ApprovalRequestId",
            schema: "hr",
            table: "VehicleTrips",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_VehicleTrips_EmployeeId",
            schema: "hr",
            table: "VehicleTrips",
            column: "EmployeeId");

        migrationBuilder.CreateIndex(
            name: "IX_VehicleTrips_VehicleId",
            schema: "hr",
            table: "VehicleTrips",
            column: "VehicleId");

        migrationBuilder.CreateIndex(
            name: "IX_WaitingTimeEvidences_ApprovalRequestId",
            schema: "hr",
            table: "WaitingTimeEvidences",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_WaitingTimeEvidences_DelayedByEmployeeId",
            schema: "hr",
            table: "WaitingTimeEvidences",
            column: "DelayedByEmployeeId");

        migrationBuilder.CreateIndex(
            name: "IX_WaitingTimeEvidences_EmployeeId",
            schema: "hr",
            table: "WaitingTimeEvidences",
            column: "EmployeeId");

        migrationBuilder.CreateIndex(
            name: "IX_WaitingTimeEvidences_PolicyVersionId",
            schema: "hr",
            table: "WaitingTimeEvidences",
            column: "PolicyVersionId");

        migrationBuilder.CreateIndex(
            name: "IX_WaitingTimeEvidences_SiteId",
            schema: "hr",
            table: "WaitingTimeEvidences",
            column: "SiteId");

        migrationBuilder.CreateIndex(
            name: "IX_WorkSessions_EmployeeId_AttendanceDate",
            schema: "attendance",
            table: "WorkSessions",
            columns: new[] { "EmployeeId", "AttendanceDate" });

        migrationBuilder.CreateIndex(
            name: "IX_WorkSessions_EmployeeId_CheckInPunchId",
            schema: "attendance",
            table: "WorkSessions",
            columns: new[] { "EmployeeId", "CheckInPunchId" });

        migrationBuilder.CreateIndex(
            name: "IX_WorkSessions_EmployeeId_CheckOutPunchId",
            schema: "attendance",
            table: "WorkSessions",
            columns: new[] { "EmployeeId", "CheckOutPunchId" });

        migrationBuilder.CreateIndex(
            name: "IX_WorkSessions_EmployeeId_CorrectionId",
            schema: "attendance",
            table: "WorkSessions",
            columns: new[] { "EmployeeId", "CorrectionId" });

        migrationBuilder.CreateIndex(
            name: "IX_WorkSessions_PolicyVersionId",
            schema: "attendance",
            table: "WorkSessions",
            column: "PolicyVersionId");

        migrationBuilder.CreateIndex(
            name: "IX_WorkSessions_SessionKey_Revision",
            schema: "attendance",
            table: "WorkSessions",
            columns: new[] { "SessionKey", "Revision" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_WorkSessions_ShiftVersionId",
            schema: "attendance",
            table: "WorkSessions",
            column: "ShiftVersionId");

        migrationBuilder.CreateIndex(
            name: "IX_WorkSessions_SiteId",
            schema: "attendance",
            table: "WorkSessions",
            column: "SiteId");

        migrationBuilder.CreateIndex(
            name: "IX_ZoneAssignments_ApprovalRequestId",
            schema: "attendance",
            table: "ZoneAssignments",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_ZoneAssignments_EmployeeId_EffectiveFromUtc",
            schema: "attendance",
            table: "ZoneAssignments",
            columns: new[] { "EmployeeId", "EffectiveFromUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_ZoneAssignments_RoleId_EffectiveFromUtc",
            schema: "attendance",
            table: "ZoneAssignments",
            columns: new[] { "RoleId", "EffectiveFromUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_ZoneAssignments_SiteId_EffectiveFromUtc",
            schema: "attendance",
            table: "ZoneAssignments",
            columns: new[] { "SiteId", "EffectiveFromUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_ZoneAssignments_TeamId_EffectiveFromUtc",
            schema: "attendance",
            table: "ZoneAssignments",
            columns: new[] { "TeamId", "EffectiveFromUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_ZoneEventResponses_ApprovalRequestId",
            schema: "attendance",
            table: "ZoneEventResponses",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_ZoneEventResponses_ZoneEventId_AtUtc",
            schema: "attendance",
            table: "ZoneEventResponses",
            columns: new[] { "ZoneEventId", "AtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_ZoneEvents_EmployeeId_IdempotencyKey",
            schema: "attendance",
            table: "ZoneEvents",
            columns: new[] { "EmployeeId", "IdempotencyKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ZoneEvents_EmployeeId_ReceivedAtUtc",
            schema: "attendance",
            table: "ZoneEvents",
            columns: new[] { "EmployeeId", "ReceivedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_ZoneEvents_EmployeeId_UserAccountId",
            schema: "attendance",
            table: "ZoneEvents",
            columns: new[] { "EmployeeId", "UserAccountId" });

        migrationBuilder.CreateIndex(
            name: "IX_ZoneEvents_EmployeeId_ZoneAssignmentId_DutyMonitoringWindowId",
            schema: "attendance",
            table: "ZoneEvents",
            columns: new[] { "EmployeeId", "ZoneAssignmentId", "DutyMonitoringWindowId" });

        migrationBuilder.CreateIndex(
            name: "IX_ZoneEvents_UserAccountId_RegisteredDeviceId",
            schema: "attendance",
            table: "ZoneEvents",
            columns: new[] { "UserAccountId", "RegisteredDeviceId" });

        migrationBuilder.CreateIndex(
            name: "IX_ZoneEvents_ZoneVersionId_ZoneAssignmentId",
            schema: "attendance",
            table: "ZoneEvents",
            columns: new[] { "ZoneVersionId", "ZoneAssignmentId" });

        migrationBuilder.CreateIndex(
            name: "IX_ZoneExceptionRequests_ApprovalRequestId",
            schema: "attendance",
            table: "ZoneExceptionRequests",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_ZoneExceptionRequests_EmployeeId_StartsAtUtc",
            schema: "attendance",
            table: "ZoneExceptionRequests",
            columns: new[] { "EmployeeId", "StartsAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_ZoneExceptionRequests_ZoneVersionId",
            schema: "attendance",
            table: "ZoneExceptionRequests",
            column: "ZoneVersionId");

        migrationBuilder.CreateIndex(
            name: "IX_ZoneTypes_Code",
            schema: "attendance",
            table: "ZoneTypes",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ZoneVersions_ApprovalRequestId",
            schema: "attendance",
            table: "ZoneVersions",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_ZoneVersions_OperationalZoneId_EffectiveFromUtc",
            schema: "attendance",
            table: "ZoneVersions",
            columns: new[] { "OperationalZoneId", "EffectiveFromUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_ZoneVersions_OperationalZoneId_VersionNumber",
            schema: "attendance",
            table: "ZoneVersions",
            columns: new[] { "OperationalZoneId", "VersionNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ZoneVersions_ZoneTypeId",
            schema: "attendance",
            table: "ZoneVersions",
            column: "ZoneTypeId");

        migrationBuilder.AddForeignKey(
            name: "FK_Branches_Cities_CityId",
            schema: "hr",
            table: "Branches",
            column: "CityId",
            principalSchema: "hr",
            principalTable: "Cities",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_EmployeeAddresses_Cities_CityId",
            schema: "hr",
            table: "EmployeeAddresses",
            column: "CityId",
            principalSchema: "hr",
            principalTable: "Cities",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_Employees_EmployeeOnboardingDecisions_Id_ApprovedOnboardingDecisionId",
            schema: "hr",
            table: "Employees",
            columns: new[] { "Id", "ApprovedOnboardingDecisionId" },
            principalSchema: "hr",
            principalTable: "EmployeeOnboardingDecisions",
            principalColumns: new[] { "EmployeeId", "Id" },
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_RawPunchEvents_StoredFiles_EvidenceFileId",
            schema: "attendance",
            table: "RawPunchEvents",
            column: "EvidenceFileId",
            principalSchema: "files",
            principalTable: "StoredFiles",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_Sites_Cities_CityId",
            schema: "attendance",
            table: "Sites",
            column: "CityId",
            principalSchema: "hr",
            principalTable: "Cities",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_Sites_Projects_ProjectId",
            schema: "attendance",
            table: "Sites",
            column: "ProjectId",
            principalSchema: "auth",
            principalTable: "Projects",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_AccountSetupInvitations_UserAccounts_UserAccountId",
            schema: "auth",
            table: "AccountSetupInvitations",
            column: "UserAccountId",
            principalSchema: "auth",
            principalTable: "UserAccounts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_FaceVerificationAttempts_RegisteredDevices_UserAccountId_RegisteredDeviceKey",
            schema: "attendance",
            table: "FaceVerificationAttempts",
            columns: new[] { "UserAccountId", "RegisteredDeviceKey" },
            principalSchema: "auth",
            principalTable: "RegisteredDevices",
            principalColumns: new[] { "UserAccountId", "Id" },
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_FaceVerificationAttempts_UserAccounts_EmployeeId_UserAccountId",
            schema: "attendance",
            table: "FaceVerificationAttempts",
            columns: new[] { "EmployeeId", "UserAccountId" },
            principalSchema: "auth",
            principalTable: "UserAccounts",
            principalColumns: new[] { "EmployeeId", "Id" },
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_MfaChallenges_MfaCredentials_UserAccountId_VerifiedCredentialId",
            schema: "auth",
            table: "MfaChallenges",
            columns: new[] { "UserAccountId", "VerifiedCredentialId" },
            principalSchema: "auth",
            principalTable: "MfaCredentials",
            principalColumns: new[] { "UserAccountId", "Id" },
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_MfaChallenges_RegisteredDevices_UserAccountId_RegisteredDeviceId",
            schema: "auth",
            table: "MfaChallenges",
            columns: new[] { "UserAccountId", "RegisteredDeviceId" },
            principalSchema: "auth",
            principalTable: "RegisteredDevices",
            principalColumns: new[] { "UserAccountId", "Id" },
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_MfaChallenges_UserAccounts_UserAccountId",
            schema: "auth",
            table: "MfaChallenges",
            column: "UserAccountId",
            principalSchema: "auth",
            principalTable: "UserAccounts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_MfaCredentials_UserAccounts_UserAccountId",
            schema: "auth",
            table: "MfaCredentials",
            column: "UserAccountId",
            principalSchema: "auth",
            principalTable: "UserAccounts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_PasswordResetChallenges_UserAccounts_UserAccountId",
            schema: "auth",
            table: "PasswordResetChallenges",
            column: "UserAccountId",
            principalSchema: "auth",
            principalTable: "UserAccounts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_RefreshTokens_UserSessions_UserSessionId",
            schema: "auth",
            table: "RefreshTokens",
            column: "UserSessionId",
            principalSchema: "auth",
            principalTable: "UserSessions",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_RegisteredDevices_UserAccounts_UserAccountId",
            schema: "auth",
            table: "RegisteredDevices",
            column: "UserAccountId",
            principalSchema: "auth",
            principalTable: "UserAccounts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_SiteReportReviews_SiteReports_SiteReportId",
            schema: "attendance",
            table: "SiteReportReviews",
            column: "SiteReportId",
            principalSchema: "attendance",
            principalTable: "SiteReports",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_SiteReports_UserAccounts_EmployeeId_UserAccountId",
            schema: "attendance",
            table: "SiteReports",
            columns: new[] { "EmployeeId", "UserAccountId" },
            principalSchema: "auth",
            principalTable: "UserAccounts",
            principalColumns: new[] { "EmployeeId", "Id" },
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_UserAccessScopes_UserRoleAssignments_UserRoleAssignmentId",
            schema: "auth",
            table: "UserAccessScopes",
            column: "UserRoleAssignmentId",
            principalSchema: "auth",
            principalTable: "UserRoleAssignments",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_UserAccounts_UserRoleAssignments_Id_InitialRoleAssignmentId",
            schema: "auth",
            table: "UserAccounts",
            columns: new[] { "Id", "InitialRoleAssignmentId" },
            principalSchema: "auth",
            principalTable: "UserRoleAssignments",
            principalColumns: new[] { "UserAccountId", "Id" },
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Branches_Cities_CityId",
            schema: "hr",
            table: "Branches");

        migrationBuilder.DropForeignKey(
            name: "FK_EmployeeAddresses_Cities_CityId",
            schema: "hr",
            table: "EmployeeAddresses");

        migrationBuilder.DropForeignKey(
            name: "FK_Employees_EmployeeOnboardingDecisions_Id_ApprovedOnboardingDecisionId",
            schema: "hr",
            table: "Employees");

        migrationBuilder.DropForeignKey(
            name: "FK_RawPunchEvents_StoredFiles_EvidenceFileId",
            schema: "attendance",
            table: "RawPunchEvents");

        migrationBuilder.DropForeignKey(
            name: "FK_Sites_Cities_CityId",
            schema: "attendance",
            table: "Sites");

        migrationBuilder.DropForeignKey(
            name: "FK_Sites_Projects_ProjectId",
            schema: "attendance",
            table: "Sites");

        migrationBuilder.DropForeignKey(
            name: "FK_AccountSetupInvitations_EmployeeOnboardingDecisions_ApprovalDecisionId",
            schema: "auth",
            table: "AccountSetupInvitations");

        migrationBuilder.DropForeignKey(
            name: "FK_UserAccounts_EmployeeOnboardingDecisions_EmployeeId_ApprovalDecisionId",
            schema: "auth",
            table: "UserAccounts");

        migrationBuilder.DropForeignKey(
            name: "FK_AccountSetupInvitations_UserAccounts_UserAccountId",
            schema: "auth",
            table: "AccountSetupInvitations");

        migrationBuilder.DropForeignKey(
            name: "FK_UserRoleAssignments_UserAccounts_EmployeeId_UserAccountId",
            schema: "auth",
            table: "UserRoleAssignments");

        migrationBuilder.DropTable(
            name: "AllowanceSettlementRecords",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "ApprovalDecisions",
            schema: "workflow");

        migrationBuilder.DropTable(
            name: "ApprovalDelegations",
            schema: "workflow");

        migrationBuilder.DropTable(
            name: "AttendanceCalculationLines",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "AttendanceExceptions",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "AttendancePeriodSummaries",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "AttendanceSummarySessions",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "AuditEvents",
            schema: "audit");

        migrationBuilder.DropTable(
            name: "Cities",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "CompensationRecords",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "DisciplinaryCases",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "EmployeeAssetCustodies",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "EmployeeBankDetails",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "EmployeeCertifications",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "EmployeeMedicalRecords",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "EmployeeSimAssignments",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "FaceVerificationExceptions",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "FuelClaims",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "Holidays",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "IdempotencyReceipts",
            schema: "workflow");

        migrationBuilder.DropTable(
            name: "InsuranceExpiryReminders",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "LeaveCancellationRequests",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "LeaveEncashmentRequests",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "LeaveLedgerEntries",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "LeaveRecoveryCommitments",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "OutboxMessages",
            schema: "jobs");

        migrationBuilder.DropTable(
            name: "OvertimeRequests",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "PasswordResetChallenges",
            schema: "auth");

        migrationBuilder.DropTable(
            name: "PayrollHandoffProcessingRecords",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "PolicyAssignmentEnds",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "PreviousEmployments",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "ProjectMembers",
            schema: "auth");

        migrationBuilder.DropTable(
            name: "PunchAssessments",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "RefreshTokens",
            schema: "auth");

        migrationBuilder.DropTable(
            name: "RepaymentLedgerEntries",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "ReportExportJobs",
            schema: "reports");

        migrationBuilder.DropTable(
            name: "RolePermissions",
            schema: "auth");

        migrationBuilder.DropTable(
            name: "ShiftPopulationAssignments",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "SiteReportReviews",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "TeamMembers",
            schema: "auth");

        migrationBuilder.DropTable(
            name: "TemporaryShiftChangeEmployees",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "VehicleIncidents",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "VehicleMileageEntries",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "VehicleServiceRecords",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "WaitingTimeEvidences",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "ZoneEventResponses",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "ZoneExceptionRequests",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "AllowanceClaims",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "PayrollHandoffLines",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "PolicyImpactAssessments",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "EmployeeCommuteDistances",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "HolidayCalendars",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "InsurancePolicies",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "Notifications",
            schema: "communication");

        migrationBuilder.DropTable(
            name: "LeaveRequests",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "PolicyAssignments",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "FaceVerificationAttempts",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "UserSessions",
            schema: "auth");

        migrationBuilder.DropTable(
            name: "RepaymentScheduleEntries",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "Permissions",
            schema: "auth");

        migrationBuilder.DropTable(
            name: "SiteReports",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "TemporaryShiftChanges",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "VehicleCustodies",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "VehicleTrips",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "ZoneEvents",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "WorkSessions",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "AttendancePeriodLockEvents",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "PayrollHandoffBatches",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "StoredFiles",
            schema: "files");

        migrationBuilder.DropTable(
            name: "LeaveTypes",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "FaceEnrollments",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "MfaChallenges",
            schema: "auth");

        migrationBuilder.DropTable(
            name: "AdvanceLoanAgreements",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "Vehicles",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "DutyMonitoringWindows",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "AttendanceCorrections",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "MfaCredentials",
            schema: "auth");

        migrationBuilder.DropTable(
            name: "RegisteredDevices",
            schema: "auth");

        migrationBuilder.DropTable(
            name: "AdvanceLoanRequests",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "ZoneAssignments",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "AttendancePeriodLocks",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "DailyAttendanceSummaries",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "ZoneVersions",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "PolicyVersions",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "OperationalZones",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "ZoneTypes",
            schema: "attendance");

        migrationBuilder.DropTable(
            name: "ApprovalRequests",
            schema: "workflow");

        migrationBuilder.DropTable(
            name: "Policies",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "ApprovalRouteRules",
            schema: "workflow");

        migrationBuilder.DropTable(
            name: "EmployeeOnboardingDecisions",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "EmployeeSubmissions",
            schema: "hr");

        migrationBuilder.DropTable(
            name: "UserAccounts",
            schema: "auth");

        migrationBuilder.DropTable(
            name: "AccountSetupInvitations",
            schema: "auth");

        migrationBuilder.DropTable(
            name: "UserAccessScopes",
            schema: "auth");

        migrationBuilder.DropTable(
            name: "Projects",
            schema: "auth");

        migrationBuilder.DropTable(
            name: "Teams",
            schema: "auth");

        migrationBuilder.DropTable(
            name: "UserRoleAssignments",
            schema: "auth");

        migrationBuilder.DropTable(
            name: "Roles",
            schema: "auth");

        migrationBuilder.DropIndex(
            name: "IX_Sites_CityId",
            schema: "attendance",
            table: "Sites");

        migrationBuilder.DropIndex(
            name: "IX_Sites_ProjectId",
            schema: "attendance",
            table: "Sites");

        migrationBuilder.DropCheckConstraint(
            name: "CK_Sites_Dates",
            schema: "attendance",
            table: "Sites");

        migrationBuilder.DropUniqueConstraint(
            name: "AK_RawPunchEvents_EmployeeId_Id",
            schema: "attendance",
            table: "RawPunchEvents");

        migrationBuilder.DropUniqueConstraint(
            name: "AK_RawPunchEvents_EmployeeId_RegisteredDeviceId_IdempotencyKey_Id",
            schema: "attendance",
            table: "RawPunchEvents");

        migrationBuilder.DropIndex(
            name: "IX_RawPunchEvents_EvidenceFileId",
            schema: "attendance",
            table: "RawPunchEvents");

        migrationBuilder.DropIndex(
            name: "IX_Employees_Id_ApprovedOnboardingDecisionId",
            schema: "hr",
            table: "Employees");

        migrationBuilder.DropIndex(
            name: "IX_Employees_OnboardingStatus_CreatedAt_Id",
            schema: "hr",
            table: "Employees");

        migrationBuilder.DropCheckConstraint(
            name: "CK_Employees_OnboardingApproval",
            schema: "hr",
            table: "Employees");

        migrationBuilder.DropCheckConstraint(
            name: "CK_Employees_OnboardingStatus",
            schema: "hr",
            table: "Employees");

        migrationBuilder.DropCheckConstraint(
            name: "CK_Employees_SubmittedVersion",
            schema: "hr",
            table: "Employees");

        migrationBuilder.DropUniqueConstraint(
            name: "AK_EmployeeDocuments_Id_EmployeeId",
            schema: "hr",
            table: "EmployeeDocuments");

        migrationBuilder.DropCheckConstraint(
            name: "CK_EmployeeDocuments_ScanEvidence",
            schema: "hr",
            table: "EmployeeDocuments");

        migrationBuilder.DropCheckConstraint(
            name: "CK_EmployeeDocuments_VerificationEvidence",
            schema: "hr",
            table: "EmployeeDocuments");

        migrationBuilder.DropUniqueConstraint(
            name: "AK_EmployeeAssignments_Id_EmployeeId",
            schema: "hr",
            table: "EmployeeAssignments");

        migrationBuilder.DropIndex(
            name: "IX_EmployeeAddresses_CityId",
            schema: "hr",
            table: "EmployeeAddresses");

        migrationBuilder.DropIndex(
            name: "IX_Branches_CityId",
            schema: "hr",
            table: "Branches");

        migrationBuilder.DropColumn(
            name: "City",
            schema: "attendance",
            table: "Sites");

        migrationBuilder.DropColumn(
            name: "CityId",
            schema: "attendance",
            table: "Sites");

        migrationBuilder.DropColumn(
            name: "ClientName",
            schema: "attendance",
            table: "Sites");

        migrationBuilder.DropColumn(
            name: "EndsOn",
            schema: "attendance",
            table: "Sites");

        migrationBuilder.DropColumn(
            name: "ProjectId",
            schema: "attendance",
            table: "Sites");

        migrationBuilder.DropColumn(
            name: "StartsOn",
            schema: "attendance",
            table: "Sites");

        migrationBuilder.DropColumn(
            name: "EvidenceFileId",
            schema: "attendance",
            table: "RawPunchEvents");

        migrationBuilder.DropColumn(
            name: "ApprovedOnboardingDecisionId",
            schema: "hr",
            table: "Employees");

        migrationBuilder.DropColumn(
            name: "DraftRevision",
            schema: "hr",
            table: "Employees");

        migrationBuilder.DropColumn(
            name: "OnboardingStatus",
            schema: "hr",
            table: "Employees");

        migrationBuilder.DropColumn(
            name: "SubmittedVersion",
            schema: "hr",
            table: "Employees");

        migrationBuilder.DropColumn(
            name: "ScannedAt",
            schema: "hr",
            table: "EmployeeDocuments");

        migrationBuilder.DropColumn(
            name: "ScannerVersion",
            schema: "hr",
            table: "EmployeeDocuments");

        migrationBuilder.DropColumn(
            name: "SignatureVerified",
            schema: "hr",
            table: "EmployeeDocuments");

        migrationBuilder.DropColumn(
            name: "VerificationReason",
            schema: "hr",
            table: "EmployeeDocuments");

        migrationBuilder.DropColumn(
            name: "VerifiedAt",
            schema: "hr",
            table: "EmployeeDocuments");

        migrationBuilder.DropColumn(
            name: "VerifiedByUserId",
            schema: "hr",
            table: "EmployeeDocuments");

        migrationBuilder.DropColumn(
            name: "CityId",
            schema: "hr",
            table: "EmployeeAddresses");

        migrationBuilder.DropColumn(
            name: "CityId",
            schema: "hr",
            table: "Branches");
    }
}
