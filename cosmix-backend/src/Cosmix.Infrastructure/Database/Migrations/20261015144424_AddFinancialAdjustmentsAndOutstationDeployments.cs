using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialAdjustmentsAndOutstationDeployments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendancePayrollFinancialAdjustments",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    SourceRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfirmedPostingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    PreviousAdjustmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    TargetRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviewFingerprint = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    ProtectedEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EvidenceFingerprint = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendancePayrollFinancialAdjustments", x => x.Id);
                    table.UniqueConstraint("AK_AttendancePayrollFinancialAdjustments_EmployeeId_SourceMonth_SourceRevisionId_Id", x => new { x.EmployeeId, x.SourceMonth, x.SourceRevisionId, x.Id });
                    table.CheckConstraint("CK_AttendancePayrollFinancialAdjustments_CreatedAtUtc_Utc", "[CreatedAtUtc] IS NULL OR DATEPART(TZOFFSET, [CreatedAtUtc]) = 0");
                    table.CheckConstraint("CK_AttendancePayrollFinancialAdjustments_EvidenceFingerprint", "LEN([EvidenceFingerprint]) = 64 AND [EvidenceFingerprint] NOT LIKE '%[^0-9A-F]%'");
                    table.CheckConstraint("CK_AttendancePayrollFinancialAdjustments_Months", "DAY([SourceMonth]) = 1 AND YEAR([SourceMonth]) BETWEEN 1900 AND 9998 AND DAY([TargetMonth]) = 1 AND YEAR([TargetMonth]) BETWEEN 1900 AND 9998 AND [TargetMonth] > [SourceMonth]");
                    table.CheckConstraint("CK_AttendancePayrollFinancialAdjustments_PreviewFingerprint", "LEN([PreviewFingerprint]) = 64 AND [PreviewFingerprint] NOT LIKE '%[^0-9A-F]%'");
                    table.CheckConstraint("CK_AttendancePayrollFinancialAdjustments_Revision", "([Revision] = 1 AND [PreviousAdjustmentId] IS NULL) OR ([Revision] > 1 AND [PreviousAdjustmentId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_AttendancePayrollFinancialAdjustments_AttendancePayrollAttestations_EmployeeId_SourceMonth_SourceRevisionId_ConfirmedPosting~",
                        columns: x => new { x.EmployeeId, x.SourceMonth, x.SourceRevisionId, x.ConfirmedPostingId },
                        principalSchema: "attendance",
                        principalTable: "AttendancePayrollAttestations",
                        principalColumns: new[] { "EmployeeId", "PeriodStart", "SourceRevisionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendancePayrollFinancialAdjustments_AttendancePayrollFinancialAdjustments_EmployeeId_SourceMonth_SourceRevisionId_Previous~",
                        columns: x => new { x.EmployeeId, x.SourceMonth, x.SourceRevisionId, x.PreviousAdjustmentId },
                        principalSchema: "attendance",
                        principalTable: "AttendancePayrollFinancialAdjustments",
                        principalColumns: new[] { "EmployeeId", "SourceMonth", "SourceRevisionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendancePayrollFinancialAdjustments_AttendancePayrollInputRevisions_EmployeeId_SourceMonth_SourceRevisionId",
                        columns: x => new { x.EmployeeId, x.SourceMonth, x.SourceRevisionId },
                        principalSchema: "attendance",
                        principalTable: "AttendancePayrollInputRevisions",
                        principalColumns: new[] { "EmployeeId", "PeriodStart", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendancePayrollFinancialAdjustments_AttendancePayrollInputRevisions_EmployeeId_TargetMonth_TargetRevisionId",
                        columns: x => new { x.EmployeeId, x.TargetMonth, x.TargetRevisionId },
                        principalSchema: "attendance",
                        principalTable: "AttendancePayrollInputRevisions",
                        principalColumns: new[] { "EmployeeId", "PeriodStart", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendancePayrollFinancialAdjustments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "hr",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendancePayrollFinancialAdjustments_UserAccounts_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "auth",
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OutstationDeployments",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    FinalizedClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutstationDeployments", x => x.Id);
                    table.UniqueConstraint("AK_OutstationDeployments_Id_EmployeeId", x => new { x.Id, x.EmployeeId });
                    table.CheckConstraint("CK_OutstationDeployments_Interval", "[EndsAtUtc] > [StartsAtUtc]");
                    table.CheckConstraint("CK_OutstationDeployments_Revision", "[Revision] > 0");
                    table.ForeignKey(
                        name: "FK_OutstationDeployments_AllowanceClaims_FinalizedClaimId",
                        column: x => x.FinalizedClaimId,
                        principalSchema: "hr",
                        principalTable: "AllowanceClaims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OutstationDeployments_ApprovalRequests_ApprovalRequestId",
                        column: x => x.ApprovalRequestId,
                        principalSchema: "workflow",
                        principalTable: "ApprovalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OutstationDeployments_EmployeeAssignments_EmployeeAssignmentId_EmployeeId",
                        columns: x => new { x.EmployeeAssignmentId, x.EmployeeId },
                        principalSchema: "hr",
                        principalTable: "EmployeeAssignments",
                        principalColumns: new[] { "Id", "EmployeeId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendancePayrollFinancialResolutions",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdjustmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ExternalReceiptKey = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: true, collation: "Latin1_General_100_BIN2"),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    ProtectedEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EvidenceFingerprint = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendancePayrollFinancialResolutions", x => x.Id);
                    table.CheckConstraint("CK_AttendancePayrollFinancialResolutions_CreatedAtUtc_Utc", "[CreatedAtUtc] IS NULL OR DATEPART(TZOFFSET, [CreatedAtUtc]) = 0");
                    table.CheckConstraint("CK_AttendancePayrollFinancialResolutions_EvidenceFingerprint", "[EvidenceFingerprint] IS NULL OR (LEN([EvidenceFingerprint]) = 64 AND [EvidenceFingerprint] NOT LIKE '%[^0-9A-F]%')");
                    table.CheckConstraint("CK_AttendancePayrollFinancialResolutions_ExternalReceiptKey", "[ExternalReceiptKey] IS NULL OR (LEN([ExternalReceiptKey]) = 64 AND [ExternalReceiptKey] NOT LIKE '%[^0-9A-F]%')");
                    table.CheckConstraint("CK_AttendancePayrollFinancialResolutions_OccurredAtUtc_Utc", "[OccurredAtUtc] IS NULL OR DATEPART(TZOFFSET, [OccurredAtUtc]) = 0");
                    table.CheckConstraint("CK_AttendancePayrollFinancialResolutions_Status", "([Status] = 'posted' AND [ExternalReceiptKey] IS NOT NULL) OR ([Status] = 'cancelled' AND [ExternalReceiptKey] IS NULL AND [OccurredAtUtc] = [CreatedAtUtc])");
                    table.CheckConstraint("CK_AttendancePayrollFinancialResolutions_Time", "[OccurredAtUtc] <= [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_AttendancePayrollFinancialResolutions_AttendancePayrollFinancialAdjustments_AdjustmentId",
                        column: x => x.AdjustmentId,
                        principalSchema: "attendance",
                        principalTable: "AttendancePayrollFinancialAdjustments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendancePayrollFinancialResolutions_UserAccounts_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "auth",
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeploymentHomeBreakRequests",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeploymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReturnedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SupersedesBreakId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentHomeBreakRequests", x => x.Id);
                    table.UniqueConstraint("AK_DeploymentHomeBreakRequests_Id_DeploymentId", x => new { x.Id, x.DeploymentId });
                    table.CheckConstraint("CK_DeploymentHomeBreak_Return", "[ReturnedAtUtc] IS NULL OR [ReturnedAtUtc] > [DepartedAtUtc]");
                    table.ForeignKey(
                        name: "FK_DeploymentHomeBreakRequests_ApprovalRequests_ApprovalRequestId",
                        column: x => x.ApprovalRequestId,
                        principalSchema: "workflow",
                        principalTable: "ApprovalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeploymentHomeBreakRequests_DeploymentHomeBreakRequests_SupersedesBreakId_DeploymentId",
                        columns: x => new { x.SupersedesBreakId, x.DeploymentId },
                        principalSchema: "hr",
                        principalTable: "DeploymentHomeBreakRequests",
                        principalColumns: new[] { "Id", "DeploymentId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeploymentHomeBreakRequests_OutstationDeployments_DeploymentId",
                        column: x => x.DeploymentId,
                        principalSchema: "hr",
                        principalTable: "OutstationDeployments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePayrollFinancialAdjustments_ActorUserId",
                schema: "attendance",
                table: "AttendancePayrollFinancialAdjustments",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePayrollFinancialAdjustments_EmployeeId_SourceMonth_Revision",
                schema: "attendance",
                table: "AttendancePayrollFinancialAdjustments",
                columns: new[] { "EmployeeId", "SourceMonth", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePayrollFinancialAdjustments_EmployeeId_SourceMonth_SourceRevisionId_ConfirmedPostingId",
                schema: "attendance",
                table: "AttendancePayrollFinancialAdjustments",
                columns: new[] { "EmployeeId", "SourceMonth", "SourceRevisionId", "ConfirmedPostingId" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePayrollFinancialAdjustments_EmployeeId_SourceMonth_SourceRevisionId_PreviousAdjustmentId",
                schema: "attendance",
                table: "AttendancePayrollFinancialAdjustments",
                columns: new[] { "EmployeeId", "SourceMonth", "SourceRevisionId", "PreviousAdjustmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePayrollFinancialAdjustments_EmployeeId_TargetMonth_TargetRevisionId",
                schema: "attendance",
                table: "AttendancePayrollFinancialAdjustments",
                columns: new[] { "EmployeeId", "TargetMonth", "TargetRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePayrollFinancialAdjustments_PreviousAdjustmentId",
                schema: "attendance",
                table: "AttendancePayrollFinancialAdjustments",
                column: "PreviousAdjustmentId",
                unique: true,
                filter: "[PreviousAdjustmentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePayrollFinancialAdjustments_TargetMonth_EmployeeId",
                schema: "attendance",
                table: "AttendancePayrollFinancialAdjustments",
                columns: new[] { "TargetMonth", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePayrollFinancialResolutions_ActorUserId",
                schema: "attendance",
                table: "AttendancePayrollFinancialResolutions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePayrollFinancialResolutions_AdjustmentId",
                schema: "attendance",
                table: "AttendancePayrollFinancialResolutions",
                column: "AdjustmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePayrollFinancialResolutions_ExternalReceiptKey",
                schema: "attendance",
                table: "AttendancePayrollFinancialResolutions",
                column: "ExternalReceiptKey",
                unique: true,
                filter: "[ExternalReceiptKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentHomeBreakRequests_ApprovalRequestId",
                schema: "hr",
                table: "DeploymentHomeBreakRequests",
                column: "ApprovalRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentHomeBreakRequests_DeploymentId_DepartedAtUtc",
                schema: "hr",
                table: "DeploymentHomeBreakRequests",
                columns: new[] { "DeploymentId", "DepartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentHomeBreakRequests_SupersedesBreakId_DeploymentId",
                schema: "hr",
                table: "DeploymentHomeBreakRequests",
                columns: new[] { "SupersedesBreakId", "DeploymentId" });

            migrationBuilder.CreateIndex(
                name: "IX_OutstationDeployments_ApprovalRequestId",
                schema: "hr",
                table: "OutstationDeployments",
                column: "ApprovalRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutstationDeployments_EmployeeAssignmentId_EmployeeId",
                schema: "hr",
                table: "OutstationDeployments",
                columns: new[] { "EmployeeAssignmentId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_OutstationDeployments_EmployeeId_StartsAtUtc_EndsAtUtc",
                schema: "hr",
                table: "OutstationDeployments",
                columns: new[] { "EmployeeId", "StartsAtUtc", "EndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OutstationDeployments_FinalizedClaimId",
                schema: "hr",
                table: "OutstationDeployments",
                column: "FinalizedClaimId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendancePayrollFinancialResolutions",
                schema: "attendance");

            migrationBuilder.DropTable(
                name: "DeploymentHomeBreakRequests",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "AttendancePayrollFinancialAdjustments",
                schema: "attendance");

            migrationBuilder.DropTable(
                name: "OutstationDeployments",
                schema: "hr");
        }
    }
}
