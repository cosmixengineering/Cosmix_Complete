using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddOfficeCaptureValidationAndOfflineZoneClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ZoneEvents_LocationRetention",
                schema: "attendance",
                table: "ZoneEvents");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WrittenClaimExpiresAtUtc",
                schema: "attendance",
                table: "ZoneEvents",
                type: "datetimeoffset(7)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WrittenClaimPrivateReference",
                schema: "attendance",
                table: "ZoneEvents",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                schema: "attendance",
                table: "WorkSessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeAssignmentId",
                schema: "attendance",
                table: "WorkSessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HolidayCalendarId",
                schema: "attendance",
                table: "WorkSessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OfficeGeofenceVersionId",
                schema: "attendance",
                table: "WorkSessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OfficeRosterAssignmentId",
                schema: "attendance",
                table: "WorkSessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                schema: "attendance",
                table: "RawPunchEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OfficeGeofenceVersionId",
                schema: "attendance",
                table: "RawPunchEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OfficeRosterAssignmentId",
                schema: "attendance",
                table: "RawPunchEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_OfficeRosterAssignments_BranchId_Id",
                schema: "attendance",
                table: "OfficeRosterAssignments",
                columns: new[] { "BranchId", "Id" });

            migrationBuilder.CreateTable(
                name: "CaptureValidationPolicyVersions",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    SupersedesVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EffectiveFromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    PublishedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ApprovalReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SettingsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SourceFingerprint = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ProtectedEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaptureValidationPolicyVersions", x => x.Id);
                    table.UniqueConstraint("AK_CaptureValidationPolicyVersions_Id_Revision_SourceFingerprint", x => new { x.Id, x.Revision, x.SourceFingerprint });
                    table.CheckConstraint("CK_CaptureValidationPolicyVersions_EffectiveFromUtc_Utc", "[EffectiveFromUtc] IS NULL OR DATEPART(TZOFFSET, [EffectiveFromUtc]) = 0");
                    table.CheckConstraint("CK_CaptureValidationPolicyVersions_Hash", "LEN([SourceFingerprint]) = 64 AND [SourceFingerprint] NOT LIKE '%[^0-9A-F]%'");
                    table.CheckConstraint("CK_CaptureValidationPolicyVersions_PublishedAtUtc_Utc", "[PublishedAtUtc] IS NULL OR DATEPART(TZOFFSET, [PublishedAtUtc]) = 0");
                    table.CheckConstraint("CK_CaptureValidationPolicyVersions_Revision", "([Revision] = 1 AND [SupersedesVersionId] IS NULL) OR ([Revision] > 1 AND [SupersedesVersionId] IS NOT NULL)");
                    table.CheckConstraint("CK_CaptureValidationPolicyVersions_Settings", "ISJSON([SettingsJson]) = 1 AND LEN([SettingsJson]) <= 32768");
                    table.CheckConstraint("CK_CaptureValidationPolicyVersions_Window", "[EffectiveFromUtc] >= [PublishedAtUtc]");
                    table.ForeignKey(
                        name: "FK_CaptureValidationPolicyVersions_CaptureValidationPolicyVersions_SupersedesVersionId",
                        column: x => x.SupersedesVersionId,
                        principalSchema: "attendance",
                        principalTable: "CaptureValidationPolicyVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OfficeGeofenceVersions",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: false),
                    Longitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: false),
                    RadiusMeters = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    MaximumAccuracyMeters = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    JurisdictionCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ApprovalReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficeGeofenceVersions", x => x.Id);
                    table.UniqueConstraint("AK_OfficeGeofenceVersions_BranchId_Id", x => new { x.BranchId, x.Id });
                    table.CheckConstraint("CK_OfficeGeofenceVersions_Geometry", "[Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180 AND [RadiusMeters] > 0 AND [MaximumAccuracyMeters] > 0");
                    table.CheckConstraint("CK_OfficeGeofenceVersions_RecordedUtc", "DATEPART(TZOFFSET,[RecordedAtUtc]) = 0");
                    table.CheckConstraint("CK_OfficeGeofenceVersions_Version", "[VersionNumber] > 0");
                    table.ForeignKey(
                        name: "FK_OfficeGeofenceVersions_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "hr",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaptureValidationFinalEvidence",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RawPunchEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    IsOffline = table.Column<bool>(type: "bit", nullable: false),
                    RegisteredDeviceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequestSha256 = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    EvaluatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    AssessedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    CandidateOutcome = table.Column<int>(type: "int", nullable: false),
                    CandidateReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FinalOutcome = table.Column<int>(type: "int", nullable: false),
                    FinalReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PolicyVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PolicyRevision = table.Column<int>(type: "int", nullable: true),
                    SourceFingerprint = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: true, collation: "Latin1_General_100_BIN2"),
                    CodesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RelatedRawPunchIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeviceObservationCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaptureValidationFinalEvidence", x => x.Id);
                    table.CheckConstraint("CK_CaptureValidationFinalEvidence_Acceptance", "[FinalOutcome] NOT IN (2, 3) OR ([CandidateOutcome] IN (2, 3) AND [PolicyVersionId] IS NOT NULL)");
                    table.CheckConstraint("CK_CaptureValidationFinalEvidence_Action", "[Action] IN (1, 2)");
                    table.CheckConstraint("CK_CaptureValidationFinalEvidence_AssessedAtUtc_Utc", "[AssessedAtUtc] IS NULL OR DATEPART(TZOFFSET, [AssessedAtUtc]) = 0");
                    table.CheckConstraint("CK_CaptureValidationFinalEvidence_Clock", "[ReceivedAtUtc] <= [EvaluatedAtUtc] AND [EvaluatedAtUtc] <= [AssessedAtUtc]");
                    table.CheckConstraint("CK_CaptureValidationFinalEvidence_EvaluatedAtUtc_Utc", "[EvaluatedAtUtc] IS NULL OR DATEPART(TZOFFSET, [EvaluatedAtUtc]) = 0");
                    table.CheckConstraint("CK_CaptureValidationFinalEvidence_Json", "ISJSON([CodesJson]) = 1 AND ISJSON([RelatedRawPunchIdsJson]) = 1");
                    table.CheckConstraint("CK_CaptureValidationFinalEvidence_Offline", "[IsOffline] = 0 OR ([CandidateOutcome] = 1 AND [FinalOutcome] = 1)");
                    table.CheckConstraint("CK_CaptureValidationFinalEvidence_Outcome", "[CandidateOutcome] BETWEEN 1 AND 5 AND [FinalOutcome] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_CaptureValidationFinalEvidence_Policy", "([PolicyVersionId] IS NULL AND [PolicyRevision] IS NULL AND [SourceFingerprint] IS NULL) OR ([PolicyVersionId] IS NOT NULL AND [PolicyRevision] IS NOT NULL AND [SourceFingerprint] IS NOT NULL AND [PolicyRevision] > 0 AND LEN([SourceFingerprint]) = 64)");
                    table.CheckConstraint("CK_CaptureValidationFinalEvidence_ReceivedAtUtc_Utc", "[ReceivedAtUtc] IS NULL OR DATEPART(TZOFFSET, [ReceivedAtUtc]) = 0");
                    table.ForeignKey(
                        name: "FK_CaptureValidationFinalEvidence_CaptureValidationPolicyVersions_PolicyVersionId_PolicyRevision_SourceFingerprint",
                        columns: x => new { x.PolicyVersionId, x.PolicyRevision, x.SourceFingerprint },
                        principalSchema: "attendance",
                        principalTable: "CaptureValidationPolicyVersions",
                        principalColumns: new[] { "Id", "Revision", "SourceFingerprint" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaptureValidationFinalEvidence_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "hr",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaptureValidationFinalEvidence_RawPunchEvents_EmployeeId_RawPunchEventId",
                        columns: x => new { x.EmployeeId, x.RawPunchEventId },
                        principalSchema: "attendance",
                        principalTable: "RawPunchEvents",
                        principalColumns: new[] { "EmployeeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ZoneEvents_WrittenClaimExpiresAtUtc_Id",
                schema: "attendance",
                table: "ZoneEvents",
                columns: new[] { "WrittenClaimExpiresAtUtc", "Id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ZoneEvents_LocationRetention",
                schema: "attendance",
                table: "ZoneEvents",
                sql: "([LocationPrivateReference] IS NULL AND [LocationExpiresAtUtc] IS NULL AND [Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [AccuracyMeters] IS NOT NULL) OR ([LocationPrivateReference] IS NOT NULL AND [LocationExpiresAtUtc] IS NOT NULL AND [LocationExpiresAtUtc] > [ReceivedAtUtc] AND [Latitude] IS NULL AND [Longitude] IS NULL AND [AccuracyMeters] IS NULL) OR ([WrittenClaimPrivateReference] IS NOT NULL AND [WrittenClaimExpiresAtUtc] IS NOT NULL AND [LocationPrivateReference] IS NULL AND [LocationExpiresAtUtc] IS NULL AND [Latitude] IS NULL AND [Longitude] IS NULL AND [AccuracyMeters] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ZoneEvents_WrittenClaim",
                schema: "attendance",
                table: "ZoneEvents",
                sql: "([WrittenClaimPrivateReference] IS NULL AND [WrittenClaimExpiresAtUtc] IS NULL) OR ([WrittenClaimPrivateReference] IS NOT NULL AND [WrittenClaimExpiresAtUtc] IS NOT NULL AND [Source] = 3 AND [IsOffline] = 1 AND [DeviceCapturedAtUtc] IS NOT NULL AND [DeviceCapturedAtUtc] <= [ReceivedAtUtc] AND [WrittenClaimExpiresAtUtc] > [ReceivedAtUtc] AND [WrittenClaimExpiresAtUtc] > [DeviceCapturedAtUtc] AND [Latitude] IS NULL AND [Longitude] IS NULL AND [AccuracyMeters] IS NULL AND [LocationPrivateReference] IS NULL AND [LocationExpiresAtUtc] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ZoneEvents_WrittenClaimExpiresAtUtc_Utc",
                schema: "attendance",
                table: "ZoneEvents",
                sql: "[WrittenClaimExpiresAtUtc] IS NULL OR DATEPART(TZOFFSET, [WrittenClaimExpiresAtUtc]) = 0");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSessions_BranchId_OfficeGeofenceVersionId",
                schema: "attendance",
                table: "WorkSessions",
                columns: new[] { "BranchId", "OfficeGeofenceVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkSessions_BranchId_OfficeRosterAssignmentId",
                schema: "attendance",
                table: "WorkSessions",
                columns: new[] { "BranchId", "OfficeRosterAssignmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkSessions_EmployeeId_EmployeeAssignmentId",
                schema: "attendance",
                table: "WorkSessions",
                columns: new[] { "EmployeeId", "EmployeeAssignmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkSessions_HolidayCalendarId",
                schema: "attendance",
                table: "WorkSessions",
                column: "HolidayCalendarId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WorkSessions_OfficeContext",
                schema: "attendance",
                table: "WorkSessions",
                sql: "([BranchId] IS NULL AND [OfficeRosterAssignmentId] IS NULL AND [OfficeGeofenceVersionId] IS NULL) OR ([BranchId] IS NOT NULL AND [SiteId] IS NULL AND [OfficeRosterAssignmentId] IS NOT NULL AND [EmployeeAssignmentId] IS NOT NULL AND [OfficeGeofenceVersionId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_RawPunchEvents_BranchId_OfficeGeofenceVersionId",
                schema: "attendance",
                table: "RawPunchEvents",
                columns: new[] { "BranchId", "OfficeGeofenceVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_RawPunchEvents_BranchId_OfficeRosterAssignmentId",
                schema: "attendance",
                table: "RawPunchEvents",
                columns: new[] { "BranchId", "OfficeRosterAssignmentId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_RawPunchEvents_OfficeContext",
                schema: "attendance",
                table: "RawPunchEvents",
                sql: "([BranchId] IS NULL AND [OfficeRosterAssignmentId] IS NULL AND [OfficeGeofenceVersionId] IS NULL) OR ([BranchId] IS NOT NULL AND [BusinessDate] IS NOT NULL AND [SiteId] IS NULL AND [GeofenceVersionId] IS NULL AND [SiteRosterAssignmentId] IS NULL AND [TemporaryShiftChangeId] IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_CaptureValidationFinalEvidence_EmployeeId_RawPunchEventId",
                schema: "attendance",
                table: "CaptureValidationFinalEvidence",
                columns: new[] { "EmployeeId", "RawPunchEventId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaptureValidationFinalEvidence_EmployeeId_ReceivedAtUtc",
                schema: "attendance",
                table: "CaptureValidationFinalEvidence",
                columns: new[] { "EmployeeId", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CaptureValidationFinalEvidence_PolicyVersionId_PolicyRevision_SourceFingerprint",
                schema: "attendance",
                table: "CaptureValidationFinalEvidence",
                columns: new[] { "PolicyVersionId", "PolicyRevision", "SourceFingerprint" });

            migrationBuilder.CreateIndex(
                name: "IX_CaptureValidationFinalEvidence_RawPunchEventId",
                schema: "attendance",
                table: "CaptureValidationFinalEvidence",
                column: "RawPunchEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaptureValidationPolicyVersions_EffectiveFromUtc_Revision",
                schema: "attendance",
                table: "CaptureValidationPolicyVersions",
                columns: new[] { "EffectiveFromUtc", "Revision" });

            migrationBuilder.CreateIndex(
                name: "IX_CaptureValidationPolicyVersions_Revision",
                schema: "attendance",
                table: "CaptureValidationPolicyVersions",
                column: "Revision",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaptureValidationPolicyVersions_SupersedesVersionId",
                schema: "attendance",
                table: "CaptureValidationPolicyVersions",
                column: "SupersedesVersionId",
                unique: true,
                filter: "[SupersedesVersionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OfficeGeofenceVersions_BranchId_EffectiveFrom",
                schema: "attendance",
                table: "OfficeGeofenceVersions",
                columns: new[] { "BranchId", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OfficeGeofenceVersions_BranchId_VersionNumber",
                schema: "attendance",
                table: "OfficeGeofenceVersions",
                columns: new[] { "BranchId", "VersionNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RawPunchEvents_Branches_BranchId",
                schema: "attendance",
                table: "RawPunchEvents",
                column: "BranchId",
                principalSchema: "hr",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RawPunchEvents_OfficeGeofenceVersions_BranchId_OfficeGeofenceVersionId",
                schema: "attendance",
                table: "RawPunchEvents",
                columns: new[] { "BranchId", "OfficeGeofenceVersionId" },
                principalSchema: "attendance",
                principalTable: "OfficeGeofenceVersions",
                principalColumns: new[] { "BranchId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RawPunchEvents_OfficeRosterAssignments_BranchId_OfficeRosterAssignmentId",
                schema: "attendance",
                table: "RawPunchEvents",
                columns: new[] { "BranchId", "OfficeRosterAssignmentId" },
                principalSchema: "attendance",
                principalTable: "OfficeRosterAssignments",
                principalColumns: new[] { "BranchId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkSessions_Branches_BranchId",
                schema: "attendance",
                table: "WorkSessions",
                column: "BranchId",
                principalSchema: "hr",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkSessions_EmployeeAssignments_EmployeeId_EmployeeAssignmentId",
                schema: "attendance",
                table: "WorkSessions",
                columns: new[] { "EmployeeId", "EmployeeAssignmentId" },
                principalSchema: "hr",
                principalTable: "EmployeeAssignments",
                principalColumns: new[] { "EmployeeId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkSessions_HolidayCalendars_HolidayCalendarId",
                schema: "attendance",
                table: "WorkSessions",
                column: "HolidayCalendarId",
                principalSchema: "hr",
                principalTable: "HolidayCalendars",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkSessions_OfficeGeofenceVersions_BranchId_OfficeGeofenceVersionId",
                schema: "attendance",
                table: "WorkSessions",
                columns: new[] { "BranchId", "OfficeGeofenceVersionId" },
                principalSchema: "attendance",
                principalTable: "OfficeGeofenceVersions",
                principalColumns: new[] { "BranchId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkSessions_OfficeRosterAssignments_BranchId_OfficeRosterAssignmentId",
                schema: "attendance",
                table: "WorkSessions",
                columns: new[] { "BranchId", "OfficeRosterAssignmentId" },
                principalSchema: "attendance",
                principalTable: "OfficeRosterAssignments",
                principalColumns: new[] { "BranchId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("THROW 51000, 'Attendance evidence requires a reviewed preservation plan; B24 downgrade refused.', 1;");

            migrationBuilder.DropForeignKey(
                name: "FK_RawPunchEvents_Branches_BranchId",
                schema: "attendance",
                table: "RawPunchEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_RawPunchEvents_OfficeGeofenceVersions_BranchId_OfficeGeofenceVersionId",
                schema: "attendance",
                table: "RawPunchEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_RawPunchEvents_OfficeRosterAssignments_BranchId_OfficeRosterAssignmentId",
                schema: "attendance",
                table: "RawPunchEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkSessions_Branches_BranchId",
                schema: "attendance",
                table: "WorkSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkSessions_EmployeeAssignments_EmployeeId_EmployeeAssignmentId",
                schema: "attendance",
                table: "WorkSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkSessions_HolidayCalendars_HolidayCalendarId",
                schema: "attendance",
                table: "WorkSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkSessions_OfficeGeofenceVersions_BranchId_OfficeGeofenceVersionId",
                schema: "attendance",
                table: "WorkSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkSessions_OfficeRosterAssignments_BranchId_OfficeRosterAssignmentId",
                schema: "attendance",
                table: "WorkSessions");

            migrationBuilder.DropTable(
                name: "CaptureValidationFinalEvidence",
                schema: "attendance");

            migrationBuilder.DropTable(
                name: "OfficeGeofenceVersions",
                schema: "attendance");

            migrationBuilder.DropTable(
                name: "CaptureValidationPolicyVersions",
                schema: "attendance");

            migrationBuilder.DropIndex(
                name: "IX_ZoneEvents_WrittenClaimExpiresAtUtc_Id",
                schema: "attendance",
                table: "ZoneEvents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ZoneEvents_LocationRetention",
                schema: "attendance",
                table: "ZoneEvents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ZoneEvents_WrittenClaim",
                schema: "attendance",
                table: "ZoneEvents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ZoneEvents_WrittenClaimExpiresAtUtc_Utc",
                schema: "attendance",
                table: "ZoneEvents");

            migrationBuilder.DropIndex(
                name: "IX_WorkSessions_BranchId_OfficeGeofenceVersionId",
                schema: "attendance",
                table: "WorkSessions");

            migrationBuilder.DropIndex(
                name: "IX_WorkSessions_BranchId_OfficeRosterAssignmentId",
                schema: "attendance",
                table: "WorkSessions");

            migrationBuilder.DropIndex(
                name: "IX_WorkSessions_EmployeeId_EmployeeAssignmentId",
                schema: "attendance",
                table: "WorkSessions");

            migrationBuilder.DropIndex(
                name: "IX_WorkSessions_HolidayCalendarId",
                schema: "attendance",
                table: "WorkSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WorkSessions_OfficeContext",
                schema: "attendance",
                table: "WorkSessions");

            migrationBuilder.DropIndex(
                name: "IX_RawPunchEvents_BranchId_OfficeGeofenceVersionId",
                schema: "attendance",
                table: "RawPunchEvents");

            migrationBuilder.DropIndex(
                name: "IX_RawPunchEvents_BranchId_OfficeRosterAssignmentId",
                schema: "attendance",
                table: "RawPunchEvents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RawPunchEvents_OfficeContext",
                schema: "attendance",
                table: "RawPunchEvents");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_OfficeRosterAssignments_BranchId_Id",
                schema: "attendance",
                table: "OfficeRosterAssignments");

            migrationBuilder.DropColumn(
                name: "WrittenClaimExpiresAtUtc",
                schema: "attendance",
                table: "ZoneEvents");

            migrationBuilder.DropColumn(
                name: "WrittenClaimPrivateReference",
                schema: "attendance",
                table: "ZoneEvents");

            migrationBuilder.DropColumn(
                name: "BranchId",
                schema: "attendance",
                table: "WorkSessions");

            migrationBuilder.DropColumn(
                name: "EmployeeAssignmentId",
                schema: "attendance",
                table: "WorkSessions");

            migrationBuilder.DropColumn(
                name: "HolidayCalendarId",
                schema: "attendance",
                table: "WorkSessions");

            migrationBuilder.DropColumn(
                name: "OfficeGeofenceVersionId",
                schema: "attendance",
                table: "WorkSessions");

            migrationBuilder.DropColumn(
                name: "OfficeRosterAssignmentId",
                schema: "attendance",
                table: "WorkSessions");

            migrationBuilder.DropColumn(
                name: "BranchId",
                schema: "attendance",
                table: "RawPunchEvents");

            migrationBuilder.DropColumn(
                name: "OfficeGeofenceVersionId",
                schema: "attendance",
                table: "RawPunchEvents");

            migrationBuilder.DropColumn(
                name: "OfficeRosterAssignmentId",
                schema: "attendance",
                table: "RawPunchEvents");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ZoneEvents_LocationRetention",
                schema: "attendance",
                table: "ZoneEvents",
                sql: "([LocationPrivateReference] IS NULL AND [LocationExpiresAtUtc] IS NULL AND [Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [AccuracyMeters] IS NOT NULL) OR ([LocationPrivateReference] IS NOT NULL AND [LocationExpiresAtUtc] IS NOT NULL AND [LocationExpiresAtUtc] > [ReceivedAtUtc] AND [Latitude] IS NULL AND [Longitude] IS NULL AND [AccuracyMeters] IS NULL)");
        }
    }
}
