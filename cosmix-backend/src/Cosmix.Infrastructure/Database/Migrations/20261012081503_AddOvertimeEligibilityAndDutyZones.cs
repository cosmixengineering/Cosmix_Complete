using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddOvertimeEligibilityAndDutyZones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ApprovalRequestId",
                schema: "attendance",
                table: "ZoneVersions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "DirectAuthorityEvidenceReference",
                schema: "attendance",
                table: "ZoneVersions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Longitude",
                schema: "attendance",
                table: "ZoneEvents",
                type: "decimal(10,7)",
                precision: 10,
                scale: 7,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,7)",
                oldPrecision: 10,
                oldScale: 7);

            migrationBuilder.AlterColumn<decimal>(
                name: "Latitude",
                schema: "attendance",
                table: "ZoneEvents",
                type: "decimal(10,7)",
                precision: 10,
                scale: 7,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,7)",
                oldPrecision: 10,
                oldScale: 7);

            migrationBuilder.AlterColumn<decimal>(
                name: "AccuracyMeters",
                schema: "attendance",
                table: "ZoneEvents",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LocationExpiresAtUtc",
                schema: "attendance",
                table: "ZoneEvents",
                type: "datetimeoffset(7)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationPrivateReference",
                schema: "attendance",
                table: "ZoneEvents",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ApprovalRequestId",
                schema: "attendance",
                table: "ZoneAssignments",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "DirectAuthorityEvidenceReference",
                schema: "attendance",
                table: "ZoneAssignments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RateRuleCode",
                schema: "attendance",
                table: "OvertimeRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<Guid>(
                name: "PolicyVersionId",
                schema: "attendance",
                table: "OvertimeRequests",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "RetentionPolicyVersionId",
                schema: "attendance",
                table: "DutyMonitoringWindows",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "AcceptedCheckInId",
                schema: "attendance",
                table: "DutyMonitoringWindows",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConfigurationVersionId",
                schema: "attendance",
                table: "DutyMonitoringWindows",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RegisteredDeviceId",
                schema: "attendance",
                table: "DutyMonitoringWindows",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkSessionKey",
                schema: "attendance",
                table: "DutyMonitoringWindows",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OvertimeEligibilityVersions",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Target = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    SupersedesVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ProtectedEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OvertimeEligibilityVersions", x => x.Id);
                    table.CheckConstraint("CK_OvertimeEligibilityVersions_RecordedAtUtc_Utc", "[RecordedAtUtc] IS NULL OR DATEPART(TZOFFSET, [RecordedAtUtc]) = 0");
                    table.CheckConstraint("CK_OvertimeEligibilityVersions_Revision", "([Revision] = 1 AND [SupersedesVersionId] IS NULL) OR ([Revision] > 1 AND [SupersedesVersionId] IS NOT NULL)");
                    table.CheckConstraint("CK_OvertimeEligibilityVersions_Target", "([Target] = 1 AND [EmployeeId] IS NULL) OR ([Target] = 2 AND [EmployeeId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_OvertimeEligibilityVersions_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "hr",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OvertimeEligibilityVersions_OvertimeEligibilityVersions_SupersedesVersionId",
                        column: x => x.SupersedesVersionId,
                        principalSchema: "attendance",
                        principalTable: "OvertimeEligibilityVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ZoneAssignmentEnds",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ZoneAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveToUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    AuthorityEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZoneAssignmentEnds", x => x.Id);
                    table.CheckConstraint("CK_ZoneAssignmentEnds_EffectiveToUtc_Utc", "[EffectiveToUtc] IS NULL OR DATEPART(TZOFFSET, [EffectiveToUtc]) = 0");
                    table.CheckConstraint("CK_ZoneAssignmentEnds_Future", "[EffectiveToUtc] >= [RecordedAtUtc]");
                    table.CheckConstraint("CK_ZoneAssignmentEnds_RecordedAtUtc_Utc", "[RecordedAtUtc] IS NULL OR DATEPART(TZOFFSET, [RecordedAtUtc]) = 0");
                    table.ForeignKey(
                        name: "FK_ZoneAssignmentEnds_ZoneAssignments_ZoneAssignmentId",
                        column: x => x.ZoneAssignmentId,
                        principalSchema: "attendance",
                        principalTable: "ZoneAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ZoneLocationDeletions",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ZoneEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    ReferenceSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZoneLocationDeletions", x => x.Id);
                    table.CheckConstraint("CK_ZoneLocationDeletions_DeletedAtUtc_Utc", "[DeletedAtUtc] IS NULL OR DATEPART(TZOFFSET, [DeletedAtUtc]) = 0");
                    table.CheckConstraint("CK_ZoneLocationDeletions_ExpiresAtUtc_Utc", "[ExpiresAtUtc] IS NULL OR DATEPART(TZOFFSET, [ExpiresAtUtc]) = 0");
                    table.CheckConstraint("CK_ZoneLocationDeletions_Expiry", "[DeletedAtUtc] >= [ExpiresAtUtc]");
                    table.ForeignKey(
                        name: "FK_ZoneLocationDeletions_ZoneEvents_ZoneEventId",
                        column: x => x.ZoneEventId,
                        principalSchema: "attendance",
                        principalTable: "ZoneEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ZoneMonitoringCheckpoints",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DutyMonitoringWindowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    LastPosition = table.Column<int>(type: "int", nullable: true),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LocationPrivateReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LocationExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    LiveVisibleUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZoneMonitoringCheckpoints", x => x.Id);
                    table.CheckConstraint("CK_ZoneMonitoringCheckpoints_LastReceivedAtUtc_Utc", "[LastReceivedAtUtc] IS NULL OR DATEPART(TZOFFSET, [LastReceivedAtUtc]) = 0");
                    table.CheckConstraint("CK_ZoneMonitoringCheckpoints_LiveVisibleUntilUtc_Utc", "[LiveVisibleUntilUtc] IS NULL OR DATEPART(TZOFFSET, [LiveVisibleUntilUtc]) = 0");
                    table.CheckConstraint("CK_ZoneMonitoringCheckpoints_LocationExpiresAtUtc_Utc", "[LocationExpiresAtUtc] IS NULL OR DATEPART(TZOFFSET, [LocationExpiresAtUtc]) = 0");
                    table.CheckConstraint("CK_ZoneMonitoringCheckpoints_Position", "([LastPosition] IS NULL AND [LastReceivedAtUtc] IS NULL AND [LocationId] IS NULL AND [LocationPrivateReference] IS NULL AND [LocationExpiresAtUtc] IS NULL AND [LiveVisibleUntilUtc] IS NULL) OR ([LastPosition] IS NOT NULL AND [LastPosition] BETWEEN 1 AND 3 AND [LastReceivedAtUtc] IS NOT NULL AND [LocationId] IS NOT NULL AND [LocationPrivateReference] IS NOT NULL AND [LocationExpiresAtUtc] IS NOT NULL AND [LiveVisibleUntilUtc] IS NOT NULL AND [LiveVisibleUntilUtc] > [LastReceivedAtUtc] AND [LocationExpiresAtUtc] >= [LiveVisibleUntilUtc])");
                    table.ForeignKey(
                        name: "FK_ZoneMonitoringCheckpoints_DutyMonitoringWindows_DutyMonitoringWindowId",
                        column: x => x.DutyMonitoringWindowId,
                        principalSchema: "attendance",
                        principalTable: "DutyMonitoringWindows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ZoneMonitoringConfigurationVersions",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    EffectiveFromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    EffectiveToUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    MaximumAccuracyMeters = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    WarningDistanceMeters = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    MinimumIntervalSeconds = table.Column<int>(type: "int", nullable: false),
                    RetentionDays = table.Column<int>(type: "int", nullable: false),
                    MaximumMonitoringSeconds = table.Column<int>(type: "int", nullable: false),
                    LiveVisibilitySeconds = table.Column<int>(type: "int", nullable: false),
                    VisibleNotice = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    AuthorityEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZoneMonitoringConfigurationVersions", x => x.Id);
                    table.CheckConstraint("CK_ZoneMonitoringConfigurationVersions_ApprovedAtUtc_Utc", "[ApprovedAtUtc] IS NULL OR DATEPART(TZOFFSET, [ApprovedAtUtc]) = 0");
                    table.CheckConstraint("CK_ZoneMonitoringConfigurationVersions_EffectiveFromUtc_Utc", "[EffectiveFromUtc] IS NULL OR DATEPART(TZOFFSET, [EffectiveFromUtc]) = 0");
                    table.CheckConstraint("CK_ZoneMonitoringConfigurationVersions_EffectiveToUtc_Utc", "[EffectiveToUtc] IS NULL OR DATEPART(TZOFFSET, [EffectiveToUtc]) = 0");
                    table.CheckConstraint("CK_ZoneMonitoringConfigurationVersions_Values", "[VersionNumber] > 0 AND [MaximumAccuracyMeters] > 0 AND [MaximumAccuracyMeters] <= 100000 AND [WarningDistanceMeters] BETWEEN 0 AND 100000 AND [MinimumIntervalSeconds] BETWEEN 1 AND 86400 AND [RetentionDays] BETWEEN 1 AND 3650 AND [MaximumMonitoringSeconds] BETWEEN 1 AND 172800 AND [MinimumIntervalSeconds] <= [MaximumMonitoringSeconds] AND [LiveVisibilitySeconds] BETWEEN 1 AND 86400 AND [LiveVisibilitySeconds] >= [MinimumIntervalSeconds] AND [LiveVisibilitySeconds] <= [MaximumMonitoringSeconds]");
                    table.CheckConstraint("CK_ZoneMonitoringConfigurationVersions_Window", "[EffectiveToUtc] > [EffectiveFromUtc]");
                    table.ForeignKey(
                        name: "FK_ZoneMonitoringConfigurationVersions_Sites_SiteId",
                        column: x => x.SiteId,
                        principalSchema: "attendance",
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Defer CHECK compilation until columns added earlier in this SQL batch exist.
            migrationBuilder.Sql(
                "EXEC(N'ALTER TABLE [attendance].[ZoneVersions] ADD CONSTRAINT [CK_ZoneVersions_Authority] CHECK ([ApprovalRequestId] IS NOT NULL OR [DirectAuthorityEvidenceReference] IS NOT NULL);');");

            migrationBuilder.CreateIndex(
                name: "IX_ZoneEvents_LocationExpiresAtUtc_Id",
                schema: "attendance",
                table: "ZoneEvents",
                columns: new[] { "LocationExpiresAtUtc", "Id" });

            // Defer CHECK compilation until columns added earlier in this SQL batch exist.
            migrationBuilder.Sql(
                "EXEC(N'ALTER TABLE [attendance].[ZoneEvents] ADD CONSTRAINT [CK_ZoneEvents_LocationExpiresAtUtc_Utc] CHECK ([LocationExpiresAtUtc] IS NULL OR DATEPART(TZOFFSET, [LocationExpiresAtUtc]) = 0);');");

            // Defer CHECK compilation until columns added earlier in this SQL batch exist.
            migrationBuilder.Sql(
                "EXEC(N'ALTER TABLE [attendance].[ZoneEvents] ADD CONSTRAINT [CK_ZoneEvents_LocationRetention] CHECK (([LocationPrivateReference] IS NULL AND [LocationExpiresAtUtc] IS NULL AND [Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [AccuracyMeters] IS NOT NULL) OR ([LocationPrivateReference] IS NOT NULL AND [LocationExpiresAtUtc] IS NOT NULL AND [LocationExpiresAtUtc] > [ReceivedAtUtc] AND [Latitude] IS NULL AND [Longitude] IS NULL AND [AccuracyMeters] IS NULL));');");

            // Defer CHECK compilation until columns added earlier in this SQL batch exist.
            migrationBuilder.Sql(
                "EXEC(N'ALTER TABLE [attendance].[ZoneAssignments] ADD CONSTRAINT [CK_ZoneAssignments_Authority] CHECK ([ApprovalRequestId] IS NOT NULL OR [DirectAuthorityEvidenceReference] IS NOT NULL);');");

            migrationBuilder.CreateIndex(
                name: "IX_DutyMonitoringWindows_AcceptedCheckInId",
                schema: "attendance",
                table: "DutyMonitoringWindows",
                column: "AcceptedCheckInId");

            migrationBuilder.CreateIndex(
                name: "IX_DutyMonitoringWindows_ConfigurationVersionId",
                schema: "attendance",
                table: "DutyMonitoringWindows",
                column: "ConfigurationVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_DutyMonitoringWindows_RegisteredDeviceId",
                schema: "attendance",
                table: "DutyMonitoringWindows",
                column: "RegisteredDeviceId");

            // Defer CHECK compilation until columns added earlier in this SQL batch exist.
            migrationBuilder.Sql(
                "EXEC(N'ALTER TABLE [attendance].[DutyMonitoringWindows] ADD CONSTRAINT [CK_DutyMonitoringWindows_Provenance] CHECK (([RetentionPolicyVersionId] IS NOT NULL AND [ConfigurationVersionId] IS NULL AND [AcceptedCheckInId] IS NULL AND [WorkSessionKey] IS NULL AND [RegisteredDeviceId] IS NULL) OR ([RetentionPolicyVersionId] IS NULL AND [ConfigurationVersionId] IS NOT NULL AND [AcceptedCheckInId] IS NOT NULL AND [WorkSessionKey] IS NOT NULL AND [RegisteredDeviceId] IS NOT NULL));');");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeEligibilityVersions_EmployeeId",
                schema: "attendance",
                table: "OvertimeEligibilityVersions",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeEligibilityVersions_SupersedesVersionId",
                schema: "attendance",
                table: "OvertimeEligibilityVersions",
                column: "SupersedesVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeEligibilityVersions_Target_EmployeeId_EffectiveFrom_Revision",
                schema: "attendance",
                table: "OvertimeEligibilityVersions",
                columns: new[] { "Target", "EmployeeId", "EffectiveFrom", "Revision" });

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeEligibilityVersions_Target_EmployeeId_Revision",
                schema: "attendance",
                table: "OvertimeEligibilityVersions",
                columns: new[] { "Target", "EmployeeId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ZoneAssignmentEnds_ZoneAssignmentId",
                schema: "attendance",
                table: "ZoneAssignmentEnds",
                column: "ZoneAssignmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ZoneLocationDeletions_ZoneEventId",
                schema: "attendance",
                table: "ZoneLocationDeletions",
                column: "ZoneEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ZoneMonitoringCheckpoints_DutyMonitoringWindowId",
                schema: "attendance",
                table: "ZoneMonitoringCheckpoints",
                column: "DutyMonitoringWindowId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ZoneMonitoringConfigurationVersions_SiteId_EffectiveFromUtc",
                schema: "attendance",
                table: "ZoneMonitoringConfigurationVersions",
                columns: new[] { "SiteId", "EffectiveFromUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ZoneMonitoringConfigurationVersions_SiteId_VersionNumber",
                schema: "attendance",
                table: "ZoneMonitoringConfigurationVersions",
                columns: new[] { "SiteId", "VersionNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DutyMonitoringWindows_RawPunchEvents_AcceptedCheckInId",
                schema: "attendance",
                table: "DutyMonitoringWindows",
                column: "AcceptedCheckInId",
                principalSchema: "attendance",
                principalTable: "RawPunchEvents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DutyMonitoringWindows_RegisteredDevices_RegisteredDeviceId",
                schema: "attendance",
                table: "DutyMonitoringWindows",
                column: "RegisteredDeviceId",
                principalSchema: "auth",
                principalTable: "RegisteredDevices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DutyMonitoringWindows_ZoneMonitoringConfigurationVersions_ConfigurationVersionId",
                schema: "attendance",
                table: "DutyMonitoringWindows",
                column: "ConfigurationVersionId",
                principalSchema: "attendance",
                principalTable: "ZoneMonitoringConfigurationVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Never erase direct authority, eligibility history or retained private-location
            // provenance, and never fabricate legacy policy/approval IDs or GPS coordinates.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [attendance].[OvertimeEligibilityVersions])
                    OR EXISTS (SELECT 1 FROM [attendance].[ZoneAssignmentEnds])
                    OR EXISTS (SELECT 1 FROM [attendance].[ZoneLocationDeletions])
                    OR EXISTS (SELECT 1 FROM [attendance].[ZoneMonitoringCheckpoints])
                    OR EXISTS (SELECT 1 FROM [attendance].[ZoneMonitoringConfigurationVersions])
                    THROW 51000, 'Cannot downgrade retained overtime or zone history.', 1;
                IF EXISTS (SELECT 1 FROM [attendance].[ZoneVersions]
                           WHERE [ApprovalRequestId] IS NULL OR [DirectAuthorityEvidenceReference] IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM [attendance].[ZoneAssignments]
                               WHERE [ApprovalRequestId] IS NULL OR [DirectAuthorityEvidenceReference] IS NOT NULL)
                    THROW 51000, 'Cannot downgrade direct zone authority evidence.', 1;
                IF EXISTS (SELECT 1 FROM [attendance].[ZoneEvents]
                           WHERE [Latitude] IS NULL OR [Longitude] IS NULL OR [AccuracyMeters] IS NULL
                              OR [LocationPrivateReference] IS NOT NULL OR [LocationExpiresAtUtc] IS NOT NULL)
                    THROW 51000, 'Cannot downgrade retained expiring zone location evidence.', 1;
                IF EXISTS (SELECT 1 FROM [attendance].[DutyMonitoringWindows]
                           WHERE [RetentionPolicyVersionId] IS NULL OR [ConfigurationVersionId] IS NOT NULL
                              OR [AcceptedCheckInId] IS NOT NULL OR [RegisteredDeviceId] IS NOT NULL OR [WorkSessionKey] IS NOT NULL)
                    THROW 51000, 'Cannot downgrade accepted-duty monitoring provenance.', 1;
                IF EXISTS (SELECT 1 FROM [attendance].[OvertimeRequests]
                           WHERE [PolicyVersionId] IS NULL OR [RateRuleCode] IS NULL)
                    OR EXISTS (SELECT 1 FROM [workflow].[ApprovalRequests] WHERE [RequestType] = N'attendance.overtime')
                    THROW 51000, 'Cannot downgrade retained overtime requests or decisions.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_DutyMonitoringWindows_RawPunchEvents_AcceptedCheckInId",
                schema: "attendance",
                table: "DutyMonitoringWindows");

            migrationBuilder.DropForeignKey(
                name: "FK_DutyMonitoringWindows_RegisteredDevices_RegisteredDeviceId",
                schema: "attendance",
                table: "DutyMonitoringWindows");

            migrationBuilder.DropForeignKey(
                name: "FK_DutyMonitoringWindows_ZoneMonitoringConfigurationVersions_ConfigurationVersionId",
                schema: "attendance",
                table: "DutyMonitoringWindows");

            migrationBuilder.DropTable(
                name: "OvertimeEligibilityVersions",
                schema: "attendance");

            migrationBuilder.DropTable(
                name: "ZoneAssignmentEnds",
                schema: "attendance");

            migrationBuilder.DropTable(
                name: "ZoneLocationDeletions",
                schema: "attendance");

            migrationBuilder.DropTable(
                name: "ZoneMonitoringCheckpoints",
                schema: "attendance");

            migrationBuilder.DropTable(
                name: "ZoneMonitoringConfigurationVersions",
                schema: "attendance");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ZoneVersions_Authority",
                schema: "attendance",
                table: "ZoneVersions");

            migrationBuilder.DropIndex(
                name: "IX_ZoneEvents_LocationExpiresAtUtc_Id",
                schema: "attendance",
                table: "ZoneEvents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ZoneEvents_LocationExpiresAtUtc_Utc",
                schema: "attendance",
                table: "ZoneEvents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ZoneEvents_LocationRetention",
                schema: "attendance",
                table: "ZoneEvents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ZoneAssignments_Authority",
                schema: "attendance",
                table: "ZoneAssignments");

            migrationBuilder.DropIndex(
                name: "IX_DutyMonitoringWindows_AcceptedCheckInId",
                schema: "attendance",
                table: "DutyMonitoringWindows");

            migrationBuilder.DropIndex(
                name: "IX_DutyMonitoringWindows_ConfigurationVersionId",
                schema: "attendance",
                table: "DutyMonitoringWindows");

            migrationBuilder.DropIndex(
                name: "IX_DutyMonitoringWindows_RegisteredDeviceId",
                schema: "attendance",
                table: "DutyMonitoringWindows");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DutyMonitoringWindows_Provenance",
                schema: "attendance",
                table: "DutyMonitoringWindows");

            migrationBuilder.DropColumn(
                name: "DirectAuthorityEvidenceReference",
                schema: "attendance",
                table: "ZoneVersions");

            migrationBuilder.DropColumn(
                name: "LocationExpiresAtUtc",
                schema: "attendance",
                table: "ZoneEvents");

            migrationBuilder.DropColumn(
                name: "LocationPrivateReference",
                schema: "attendance",
                table: "ZoneEvents");

            migrationBuilder.DropColumn(
                name: "DirectAuthorityEvidenceReference",
                schema: "attendance",
                table: "ZoneAssignments");

            migrationBuilder.DropColumn(
                name: "AcceptedCheckInId",
                schema: "attendance",
                table: "DutyMonitoringWindows");

            migrationBuilder.DropColumn(
                name: "ConfigurationVersionId",
                schema: "attendance",
                table: "DutyMonitoringWindows");

            migrationBuilder.DropColumn(
                name: "RegisteredDeviceId",
                schema: "attendance",
                table: "DutyMonitoringWindows");

            migrationBuilder.DropColumn(
                name: "WorkSessionKey",
                schema: "attendance",
                table: "DutyMonitoringWindows");

            migrationBuilder.AlterColumn<Guid>(
                name: "ApprovalRequestId",
                schema: "attendance",
                table: "ZoneVersions",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Longitude",
                schema: "attendance",
                table: "ZoneEvents",
                type: "decimal(10,7)",
                precision: 10,
                scale: 7,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,7)",
                oldPrecision: 10,
                oldScale: 7,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Latitude",
                schema: "attendance",
                table: "ZoneEvents",
                type: "decimal(10,7)",
                precision: 10,
                scale: 7,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,7)",
                oldPrecision: 10,
                oldScale: 7,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "AccuracyMeters",
                schema: "attendance",
                table: "ZoneEvents",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ApprovalRequestId",
                schema: "attendance",
                table: "ZoneAssignments",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RateRuleCode",
                schema: "attendance",
                table: "OvertimeRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PolicyVersionId",
                schema: "attendance",
                table: "OvertimeRequests",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "RetentionPolicyVersionId",
                schema: "attendance",
                table: "DutyMonitoringWindows",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
