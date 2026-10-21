using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class CorrectReviewedModelLifecycles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Do not fabricate revision/approval/payment evidence for an already used model.
            // Draft-only claims are compatible; processed claims need a reviewed conversion.
            migrationBuilder.Sql("""
                SET XACT_ABORT ON;

                IF EXISTS (SELECT 1 FROM [hr].[AllowanceClaims] WHERE [SubmittedAt] IS NOT NULL OR [ApprovalRequestId] IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM [hr].[AllowanceSettlementRecords])
                    THROW 51002, 'Migration stopped: existing submitted allowance claims or settlements require a reviewed submission/payment-history conversion.', 1;
                """);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TerminatedAt",
                schema: "auth",
                table: "TeamMembers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TerminatedByUserId",
                schema: "auth",
                table: "TeamMembers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "TerminatedEffectiveTo",
                schema: "auth",
                table: "TeamMembers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "TerminationBusinessDate",
                schema: "auth",
                table: "TeamMembers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TerminationReason",
                schema: "auth",
                table: "TeamMembers",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TerminatedAt",
                schema: "auth",
                table: "ProjectMembers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TerminatedByUserId",
                schema: "auth",
                table: "ProjectMembers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "TerminatedEffectiveTo",
                schema: "auth",
                table: "ProjectMembers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "TerminationBusinessDate",
                schema: "auth",
                table: "ProjectMembers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TerminationReason",
                schema: "auth",
                table: "ProjectMembers",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasScanConflict",
                schema: "hr",
                table: "EmployeeDocuments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VerifiedScanAt",
                schema: "hr",
                table: "EmployeeDocuments",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubmissionVersion",
                schema: "hr",
                table: "AllowanceSettlementRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSettledAt",
                schema: "hr",
                table: "AllowanceClaims",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SettlementState",
                schema: "hr",
                table: "AllowanceClaims",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SubmissionVersion",
                schema: "hr",
                table: "AllowanceClaims",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AllowanceClaimSubmissions",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllowanceClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotPrivateReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ClaimedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PolicyVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CalculationPrivateReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EvidencePrivateReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PayableDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PayrollPeriodStart = table.Column<DateOnly>(type: "date", nullable: true),
                    CorrectionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SubmittedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllowanceClaimSubmissions", x => x.Id);
                    table.UniqueConstraint("AK_AllowanceClaimSubmissions_AllowanceClaimId_Version", x => new { x.AllowanceClaimId, x.Version });
                    table.CheckConstraint("CK_AllowanceClaimSubmissions_Amount", "[ClaimedAmount] >= 0");
                    table.CheckConstraint("CK_AllowanceClaimSubmissions_Correction", "([Version] = 1 AND [CorrectionReason] IS NULL) OR ([Version] > 1 AND [CorrectionReason] IS NOT NULL AND LEN(LTRIM(RTRIM([CorrectionReason]))) > 0)");
                    table.CheckConstraint("CK_AllowanceClaimSubmissions_Version", "[Version] > 0");
                    table.ForeignKey(
                        name: "FK_AllowanceClaimSubmissions_AllowanceClaims_AllowanceClaimId",
                        column: x => x.AllowanceClaimId,
                        principalSchema: "hr",
                        principalTable: "AllowanceClaims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AllowanceClaimSubmissions_ApprovalRequests_ApprovalRequestId",
                        column: x => x.ApprovalRequestId,
                        principalSchema: "workflow",
                        principalTable: "ApprovalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AllowanceClaimSubmissions_PolicyVersions_PolicyVersionId",
                        column: x => x.PolicyVersionId,
                        principalSchema: "hr",
                        principalTable: "PolicyVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShiftPopulationAssignmentEnds",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShiftPopulationAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftPopulationAssignmentEnds", x => x.Id);
                    table.CheckConstraint("CK_ShiftPopulationAssignmentEnds_Reason", "LEN(LTRIM(RTRIM([Reason]))) > 0");
                    table.ForeignKey(
                        name: "FK_ShiftPopulationAssignmentEnds_ApprovalRequests_ApprovalRequestId",
                        column: x => x.ApprovalRequestId,
                        principalSchema: "workflow",
                        principalTable: "ApprovalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftPopulationAssignmentEnds_ShiftPopulationAssignments_ShiftPopulationAssignmentId",
                        column: x => x.ShiftPopulationAssignmentId,
                        principalSchema: "attendance",
                        principalTable: "ShiftPopulationAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_TeamMembers_Termination",
                schema: "auth",
                table: "TeamMembers",
                sql: "([TerminatedEffectiveTo] IS NULL AND [TerminationReason] IS NULL AND [TerminatedByUserId] IS NULL AND [TerminatedAt] IS NULL AND [TerminationBusinessDate] IS NULL) OR ([TerminatedEffectiveTo] IS NOT NULL AND [TerminationReason] IS NOT NULL AND [TerminatedByUserId] IS NOT NULL AND [TerminatedAt] IS NOT NULL AND [TerminationBusinessDate] IS NOT NULL AND LEN(LTRIM(RTRIM([TerminationReason]))) > 0 AND [TerminatedByUserId] <> '00000000-0000-0000-0000-000000000000' AND [TerminatedEffectiveTo] > [EffectiveFrom] AND [TerminatedEffectiveTo] >= [TerminationBusinessDate] AND ([EffectiveTo] IS NULL OR ([TerminatedEffectiveTo] <= [EffectiveTo] AND [EffectiveTo] > [TerminationBusinessDate])))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectMembers_Termination",
                schema: "auth",
                table: "ProjectMembers",
                sql: "([TerminatedEffectiveTo] IS NULL AND [TerminationReason] IS NULL AND [TerminatedByUserId] IS NULL AND [TerminatedAt] IS NULL AND [TerminationBusinessDate] IS NULL) OR ([TerminatedEffectiveTo] IS NOT NULL AND [TerminationReason] IS NOT NULL AND [TerminatedByUserId] IS NOT NULL AND [TerminatedAt] IS NOT NULL AND [TerminationBusinessDate] IS NOT NULL AND LEN(LTRIM(RTRIM([TerminationReason]))) > 0 AND [TerminatedByUserId] <> '00000000-0000-0000-0000-000000000000' AND [TerminatedEffectiveTo] > [EffectiveFrom] AND [TerminatedEffectiveTo] >= [TerminationBusinessDate] AND ([EffectiveTo] IS NULL OR ([TerminatedEffectiveTo] <= [EffectiveTo] AND [EffectiveTo] > [TerminationBusinessDate])))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeDocuments_ScanConflict",
                schema: "hr",
                table: "EmployeeDocuments",
                sql: "[HasScanConflict] = 0 OR [ScanStatus] <> 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeDocuments_VerifiedScan",
                schema: "hr",
                table: "EmployeeDocuments",
                sql: "[VerifiedScanAt] IS NULL OR ([VerifiedAt] IS NOT NULL AND [ScannedAt] IS NOT NULL AND [VerifiedScanAt] >= [UploadedAt] AND [VerifiedScanAt] <= [VerifiedAt] AND [VerifiedScanAt] <= [ScannedAt])");

            migrationBuilder.CreateIndex(
                name: "IX_AllowanceSettlementRecords_AllowanceClaimId_State",
                schema: "hr",
                table: "AllowanceSettlementRecords",
                columns: new[] { "AllowanceClaimId", "State" },
                unique: true,
                filter: "[State] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_AllowanceSettlementRecords_AllowanceClaimId_SubmissionVersion",
                schema: "hr",
                table: "AllowanceSettlementRecords",
                columns: new[] { "AllowanceClaimId", "SubmissionVersion" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AllowanceSettlementRecords_Submission",
                schema: "hr",
                table: "AllowanceSettlementRecords",
                sql: "[SubmissionVersion] > 0 AND [State] BETWEEN 1 AND 2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AllowanceClaims_Settlement",
                schema: "hr",
                table: "AllowanceClaims",
                sql: "([SettlementState] = 0 AND [LastSettledAt] IS NULL) OR ([SettlementState] BETWEEN 1 AND 2 AND [LastSettledAt] IS NOT NULL AND [SubmittedAt] IS NOT NULL AND [LastSettledAt] >= [SubmittedAt])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AllowanceClaims_SubmissionVersion",
                schema: "hr",
                table: "AllowanceClaims",
                sql: "([SubmissionVersion] = 0 AND [SubmittedAt] IS NULL) OR ([SubmissionVersion] > 0 AND [SubmittedAt] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_AllowanceClaimSubmissions_AllowanceClaimId_SnapshotPrivateReference",
                schema: "hr",
                table: "AllowanceClaimSubmissions",
                columns: new[] { "AllowanceClaimId", "SnapshotPrivateReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AllowanceClaimSubmissions_ApprovalRequestId",
                schema: "hr",
                table: "AllowanceClaimSubmissions",
                column: "ApprovalRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AllowanceClaimSubmissions_PolicyVersionId",
                schema: "hr",
                table: "AllowanceClaimSubmissions",
                column: "PolicyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftPopulationAssignmentEnds_ApprovalRequestId",
                schema: "attendance",
                table: "ShiftPopulationAssignmentEnds",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftPopulationAssignmentEnds_ShiftPopulationAssignmentId",
                schema: "attendance",
                table: "ShiftPopulationAssignmentEnds",
                column: "ShiftPopulationAssignmentId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AllowanceSettlementRecords_AllowanceClaimSubmissions_AllowanceClaimId_SubmissionVersion",
                schema: "hr",
                table: "AllowanceSettlementRecords",
                columns: new[] { "AllowanceClaimId", "SubmissionVersion" },
                principalSchema: "hr",
                principalTable: "AllowanceClaimSubmissions",
                principalColumns: new[] { "AllowanceClaimId", "Version" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AllowanceSettlementRecords_AllowanceClaimSubmissions_AllowanceClaimId_SubmissionVersion",
                schema: "hr",
                table: "AllowanceSettlementRecords");

            migrationBuilder.DropTable(
                name: "AllowanceClaimSubmissions",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "ShiftPopulationAssignmentEnds",
                schema: "attendance");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TeamMembers_Termination",
                schema: "auth",
                table: "TeamMembers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectMembers_Termination",
                schema: "auth",
                table: "ProjectMembers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeDocuments_ScanConflict",
                schema: "hr",
                table: "EmployeeDocuments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeDocuments_VerifiedScan",
                schema: "hr",
                table: "EmployeeDocuments");

            migrationBuilder.DropIndex(
                name: "IX_AllowanceSettlementRecords_AllowanceClaimId_State",
                schema: "hr",
                table: "AllowanceSettlementRecords");

            migrationBuilder.DropIndex(
                name: "IX_AllowanceSettlementRecords_AllowanceClaimId_SubmissionVersion",
                schema: "hr",
                table: "AllowanceSettlementRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AllowanceSettlementRecords_Submission",
                schema: "hr",
                table: "AllowanceSettlementRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AllowanceClaims_Settlement",
                schema: "hr",
                table: "AllowanceClaims");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AllowanceClaims_SubmissionVersion",
                schema: "hr",
                table: "AllowanceClaims");

            migrationBuilder.DropColumn(
                name: "TerminatedAt",
                schema: "auth",
                table: "TeamMembers");

            migrationBuilder.DropColumn(
                name: "TerminatedByUserId",
                schema: "auth",
                table: "TeamMembers");

            migrationBuilder.DropColumn(
                name: "TerminatedEffectiveTo",
                schema: "auth",
                table: "TeamMembers");

            migrationBuilder.DropColumn(
                name: "TerminationBusinessDate",
                schema: "auth",
                table: "TeamMembers");

            migrationBuilder.DropColumn(
                name: "TerminationReason",
                schema: "auth",
                table: "TeamMembers");

            migrationBuilder.DropColumn(
                name: "TerminatedAt",
                schema: "auth",
                table: "ProjectMembers");

            migrationBuilder.DropColumn(
                name: "TerminatedByUserId",
                schema: "auth",
                table: "ProjectMembers");

            migrationBuilder.DropColumn(
                name: "TerminatedEffectiveTo",
                schema: "auth",
                table: "ProjectMembers");

            migrationBuilder.DropColumn(
                name: "TerminationBusinessDate",
                schema: "auth",
                table: "ProjectMembers");

            migrationBuilder.DropColumn(
                name: "TerminationReason",
                schema: "auth",
                table: "ProjectMembers");

            migrationBuilder.DropColumn(
                name: "HasScanConflict",
                schema: "hr",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "VerifiedScanAt",
                schema: "hr",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "SubmissionVersion",
                schema: "hr",
                table: "AllowanceSettlementRecords");

            migrationBuilder.DropColumn(
                name: "LastSettledAt",
                schema: "hr",
                table: "AllowanceClaims");

            migrationBuilder.DropColumn(
                name: "SettlementState",
                schema: "hr",
                table: "AllowanceClaims");

            migrationBuilder.DropColumn(
                name: "SubmissionVersion",
                schema: "hr",
                table: "AllowanceClaims");
        }
    }
}
