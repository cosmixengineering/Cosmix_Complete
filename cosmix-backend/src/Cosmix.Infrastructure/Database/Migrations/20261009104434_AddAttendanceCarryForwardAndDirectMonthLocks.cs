using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceCarryForwardAndDirectMonthLocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ApprovalRequestId",
                schema: "attendance",
                table: "AttendancePeriodLockEvents",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "ProtectedEvidenceReference",
                schema: "attendance",
                table: "AttendancePeriodLockEvents",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AttendanceCarryForwardAdjustments",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceBusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SourcePeriodLockEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceSummaryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    SupersedesAdjustmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetMonthStart = table.Column<DateOnly>(type: "date", nullable: false),
                    TargetMonthEndExclusive = table.Column<DateOnly>(type: "date", nullable: false),
                    BeforeWorkedTicks = table.Column<long>(type: "bigint", nullable: false),
                    AfterWorkedTicks = table.Column<long>(type: "bigint", nullable: false),
                    DeltaWorkedTicks = table.Column<long>(type: "bigint", nullable: false),
                    FirstStartsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    LastEndsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    InputFingerprint = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    InterpretationFingerprint = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ProtectedEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AppliedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceCarryForwardAdjustments", x => x.Id);
                    table.UniqueConstraint("AK_AttendanceCarryForwardAdjustments_EmployeeId_SourceBusinessDate_Id", x => new { x.EmployeeId, x.SourceBusinessDate, x.Id });
                    table.CheckConstraint("CK_AttendanceCarryForwardAdjustments_AppliedAtUtc_Utc", "[AppliedAtUtc] IS NULL OR DATEPART(TZOFFSET, [AppliedAtUtc]) = 0");
                    table.CheckConstraint("CK_AttendanceCarryForwardAdjustments_Fingerprints", "LEN([InputFingerprint]) = 64 AND [InputFingerprint] NOT LIKE '%[^0-9A-F]%' AND LEN([InterpretationFingerprint]) = 64 AND [InterpretationFingerprint] NOT LIKE '%[^0-9A-F]%'");
                    table.CheckConstraint("CK_AttendanceCarryForwardAdjustments_FirstStartsAtUtc_Utc", "[FirstStartsAtUtc] IS NULL OR DATEPART(TZOFFSET, [FirstStartsAtUtc]) = 0");
                    table.CheckConstraint("CK_AttendanceCarryForwardAdjustments_LastEndsAtUtc_Utc", "[LastEndsAtUtc] IS NULL OR DATEPART(TZOFFSET, [LastEndsAtUtc]) = 0");
                    table.CheckConstraint("CK_AttendanceCarryForwardAdjustments_Month", "DAY([TargetMonthStart]) = 1 AND [TargetMonthEndExclusive] = DATEADD(month, 1, [TargetMonthStart]) AND [TargetMonthStart] > DATEFROMPARTS(YEAR([SourceBusinessDate]), MONTH([SourceBusinessDate]), 1)");
                    table.CheckConstraint("CK_AttendanceCarryForwardAdjustments_Revision", "([Revision] = 1 AND [SupersedesAdjustmentId] IS NULL) OR ([Revision] > 1 AND [SupersedesAdjustmentId] IS NOT NULL)");
                    table.CheckConstraint("CK_AttendanceCarryForwardAdjustments_Ticks", "[BeforeWorkedTicks] >= 0 AND [AfterWorkedTicks] >= 0 AND [DeltaWorkedTicks] = [AfterWorkedTicks] - [BeforeWorkedTicks]");
                    table.CheckConstraint("CK_AttendanceCarryForwardAdjustments_Window", "[LastEndsAtUtc] > [FirstStartsAtUtc]");
                    table.ForeignKey(
                        name: "FK_AttendanceCarryForwardAdjustments_ApprovalDecisions_ApprovalDecisionId",
                        column: x => x.ApprovalDecisionId,
                        principalSchema: "workflow",
                        principalTable: "ApprovalDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceCarryForwardAdjustments_AttendanceCarryForwardAdjustments_EmployeeId_SourceBusinessDate_SupersedesAdjustmentId",
                        columns: x => new { x.EmployeeId, x.SourceBusinessDate, x.SupersedesAdjustmentId },
                        principalSchema: "attendance",
                        principalTable: "AttendanceCarryForwardAdjustments",
                        principalColumns: new[] { "EmployeeId", "SourceBusinessDate", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceCarryForwardAdjustments_AttendanceCorrections_EmployeeId_CorrectionId",
                        columns: x => new { x.EmployeeId, x.CorrectionId },
                        principalSchema: "attendance",
                        principalTable: "AttendanceCorrections",
                        principalColumns: new[] { "EmployeeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceCarryForwardAdjustments_AttendancePeriodLockEvents_SourcePeriodLockEventId",
                        column: x => x.SourcePeriodLockEventId,
                        principalSchema: "attendance",
                        principalTable: "AttendancePeriodLockEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceCarryForwardAdjustments_DailyAttendanceSummaries_EmployeeId_SourceBusinessDate_SourceSummaryId",
                        columns: x => new { x.EmployeeId, x.SourceBusinessDate, x.SourceSummaryId },
                        principalSchema: "attendance",
                        principalTable: "DailyAttendanceSummaries",
                        principalColumns: new[] { "EmployeeId", "AttendanceDate", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceCarryForwardAdjustments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "hr",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Defer CHECK compilation until columns added earlier in this SQL batch exist.
            migrationBuilder.Sql(
                "EXEC(N'ALTER TABLE [attendance].[AttendancePeriodLockEvents] ADD CONSTRAINT [CK_AttendancePeriodLockEvents_Provenance] CHECK (([ApprovalRequestId] IS NOT NULL AND [ProtectedEvidenceReference] IS NULL) OR ([ApprovalRequestId] IS NULL AND [ProtectedEvidenceReference] IS NOT NULL AND [IsLocked] = 1));');");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceCarryForwardAdjustments_ApprovalDecisionId",
                schema: "attendance",
                table: "AttendanceCarryForwardAdjustments",
                column: "ApprovalDecisionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceCarryForwardAdjustments_CorrectionId",
                schema: "attendance",
                table: "AttendanceCarryForwardAdjustments",
                column: "CorrectionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceCarryForwardAdjustments_EmployeeId_CorrectionId",
                schema: "attendance",
                table: "AttendanceCarryForwardAdjustments",
                columns: new[] { "EmployeeId", "CorrectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceCarryForwardAdjustments_EmployeeId_FirstStartsAtUtc_LastEndsAtUtc",
                schema: "attendance",
                table: "AttendanceCarryForwardAdjustments",
                columns: new[] { "EmployeeId", "FirstStartsAtUtc", "LastEndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceCarryForwardAdjustments_EmployeeId_SourceBusinessDate_Revision",
                schema: "attendance",
                table: "AttendanceCarryForwardAdjustments",
                columns: new[] { "EmployeeId", "SourceBusinessDate", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceCarryForwardAdjustments_EmployeeId_SourceBusinessDate_SourceSummaryId",
                schema: "attendance",
                table: "AttendanceCarryForwardAdjustments",
                columns: new[] { "EmployeeId", "SourceBusinessDate", "SourceSummaryId" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceCarryForwardAdjustments_EmployeeId_SourceBusinessDate_SupersedesAdjustmentId",
                schema: "attendance",
                table: "AttendanceCarryForwardAdjustments",
                columns: new[] { "EmployeeId", "SourceBusinessDate", "SupersedesAdjustmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceCarryForwardAdjustments_EmployeeId_TargetMonthStart",
                schema: "attendance",
                table: "AttendanceCarryForwardAdjustments",
                columns: new[] { "EmployeeId", "TargetMonthStart" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceCarryForwardAdjustments_SourcePeriodLockEventId",
                schema: "attendance",
                table: "AttendanceCarryForwardAdjustments",
                column: "SourcePeriodLockEventId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [attendance].[AttendanceCarryForwardAdjustments])
                    THROW 51000, 'Cannot downgrade retained attendance carry-forward evidence.', 1;
                IF EXISTS (SELECT 1 FROM [workflow].[ApprovalRequests] WHERE [RequestType] = N'attendance.locked-correction')
                    THROW 51000, 'Cannot downgrade retained locked-correction requests.', 1;
                IF EXISTS (SELECT 1 FROM [attendance].[AttendancePeriodLockEvents]
                           WHERE [ProtectedEvidenceReference] IS NOT NULL OR [ApprovalRequestId] IS NULL)
                    THROW 51000, 'Cannot downgrade direct month-lock provenance.', 1;
                """);
            migrationBuilder.DropTable(
                name: "AttendanceCarryForwardAdjustments",
                schema: "attendance");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AttendancePeriodLockEvents_Provenance",
                schema: "attendance",
                table: "AttendancePeriodLockEvents");

            migrationBuilder.DropColumn(
                name: "ProtectedEvidenceReference",
                schema: "attendance",
                table: "AttendancePeriodLockEvents");

            migrationBuilder.AlterColumn<Guid>(
                name: "ApprovalRequestId",
                schema: "attendance",
                table: "AttendancePeriodLockEvents",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
