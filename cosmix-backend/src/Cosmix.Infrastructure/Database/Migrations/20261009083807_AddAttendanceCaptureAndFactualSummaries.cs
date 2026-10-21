using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceCaptureAndFactualSummaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_FaceVerificationAttempts_Completion",
                schema: "attendance",
                table: "FaceVerificationAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FaceVerificationAttempts_Outcome",
                schema: "attendance",
                table: "FaceVerificationAttempts");

            migrationBuilder.AlterColumn<Guid>(
                name: "PolicyVersionId",
                schema: "attendance",
                table: "WorkSessions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<DateOnly>(
                name: "BusinessDate",
                schema: "attendance",
                table: "RawPunchEvents",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HolidayCalendarId",
                schema: "attendance",
                table: "RawPunchEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SiteRosterAssignmentId",
                schema: "attendance",
                table: "RawPunchEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TemporaryShiftChangeId",
                schema: "attendance",
                table: "RawPunchEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PolicyVersionId",
                schema: "attendance",
                table: "PunchAssessments",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "ShiftVersionId",
                schema: "attendance",
                table: "DailyAttendanceSummaries",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "PolicyVersionId",
                schema: "attendance",
                table: "DailyAttendanceSummaries",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<int>(
                name: "PayableMinutes",
                schema: "attendance",
                table: "DailyAttendanceSummaries",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "OvertimeMinutes",
                schema: "attendance",
                table: "DailyAttendanceSummaries",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "LateMinutes",
                schema: "attendance",
                table: "DailyAttendanceSummaries",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "InputEvidenceReference",
                schema: "attendance",
                table: "DailyAttendanceSummaries",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RawPunchEvents_HolidayCalendarId",
                schema: "attendance",
                table: "RawPunchEvents",
                column: "HolidayCalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_RawPunchEvents_SiteRosterAssignmentId",
                schema: "attendance",
                table: "RawPunchEvents",
                column: "SiteRosterAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_RawPunchEvents_TemporaryShiftChangeId",
                schema: "attendance",
                table: "RawPunchEvents",
                column: "TemporaryShiftChangeId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FaceVerificationAttempts_Completion",
                schema: "attendance",
                table: "FaceVerificationAttempts",
                sql: "([Outcome] = 1 AND [CompletedAtUtc] IS NULL AND [LivenessPassed] IS NULL AND [MatchPassed] IS NULL AND [ResultReasonCode] IS NULL) OR ([Outcome] BETWEEN 2 AND 4 AND [CompletedAtUtc] IS NOT NULL AND [CompletedAtUtc] >= [IssuedAtUtc] AND [CompletedAtUtc] < [ExpiresAtUtc] AND [LivenessPassed] IS NOT NULL AND [MatchPassed] IS NOT NULL AND [ResultReasonCode] IS NOT NULL) OR ([Outcome] = 5 AND [CompletedAtUtc] IS NOT NULL AND [CompletedAtUtc] >= [ExpiresAtUtc] AND [LivenessPassed] IS NOT NULL AND [MatchPassed] IS NOT NULL AND [LivenessPassed] = 0 AND [MatchPassed] = 0 AND [ResultReasonCode] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FaceVerificationAttempts_Outcome",
                schema: "attendance",
                table: "FaceVerificationAttempts",
                sql: "[Outcome] BETWEEN 1 AND 5");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DailyAttendanceSummaries_PolicyOutputs",
                schema: "attendance",
                table: "DailyAttendanceSummaries",
                sql: "([PolicyVersionId] IS NULL AND [PayableMinutes] IS NULL AND [LateMinutes] IS NULL AND [OvertimeMinutes] IS NULL AND [Status] IN (1,9)) OR ([PolicyVersionId] IS NOT NULL AND [PayableMinutes] IS NOT NULL AND [LateMinutes] IS NOT NULL AND [OvertimeMinutes] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_RawPunchEvents_HolidayCalendars_HolidayCalendarId",
                schema: "attendance",
                table: "RawPunchEvents",
                column: "HolidayCalendarId",
                principalSchema: "hr",
                principalTable: "HolidayCalendars",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RawPunchEvents_ShiftPopulationAssignments_SiteRosterAssignmentId",
                schema: "attendance",
                table: "RawPunchEvents",
                column: "SiteRosterAssignmentId",
                principalSchema: "attendance",
                principalTable: "ShiftPopulationAssignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RawPunchEvents_TemporaryShiftChanges_TemporaryShiftChangeId",
                schema: "attendance",
                table: "RawPunchEvents",
                column: "TemporaryShiftChangeId",
                principalSchema: "attendance",
                principalTable: "TemporaryShiftChanges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Refuse evidence loss or invented fallback policy/pay values. Operators must preserve
            // and explicitly reconcile new attendance records before considering this downgrade.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [attendance].[RawPunchEvents] WITH (TABLOCKX,HOLDLOCK)
                    WHERE [BusinessDate] IS NOT NULL OR [HolidayCalendarId] IS NOT NULL OR
                          [SiteRosterAssignmentId] IS NOT NULL OR [TemporaryShiftChangeId] IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM [attendance].[DailyAttendanceSummaries] WITH (TABLOCKX,HOLDLOCK)
                    WHERE [InputEvidenceReference] IS NOT NULL OR [PolicyVersionId] IS NULL OR [ShiftVersionId] IS NULL OR
                          [PayableMinutes] IS NULL OR [LateMinutes] IS NULL OR [OvertimeMinutes] IS NULL)
                    OR EXISTS (SELECT 1 FROM [attendance].[WorkSessions] WITH (TABLOCKX,HOLDLOCK) WHERE [PolicyVersionId] IS NULL)
                    OR EXISTS (SELECT 1 FROM [attendance].[PunchAssessments] WITH (TABLOCKX,HOLDLOCK) WHERE [PolicyVersionId] IS NULL)
                    OR EXISTS (SELECT 1 FROM [attendance].[FaceVerificationAttempts] WITH (TABLOCKX,HOLDLOCK) WHERE [Outcome] = 5)
                    THROW 51003, 'Attendance evidence uses this migration. Preserve and reconcile it before downgrade; no policy or pay defaults will be fabricated.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_RawPunchEvents_HolidayCalendars_HolidayCalendarId",
                schema: "attendance",
                table: "RawPunchEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_RawPunchEvents_ShiftPopulationAssignments_SiteRosterAssignmentId",
                schema: "attendance",
                table: "RawPunchEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_RawPunchEvents_TemporaryShiftChanges_TemporaryShiftChangeId",
                schema: "attendance",
                table: "RawPunchEvents");

            migrationBuilder.DropIndex(
                name: "IX_RawPunchEvents_HolidayCalendarId",
                schema: "attendance",
                table: "RawPunchEvents");

            migrationBuilder.DropIndex(
                name: "IX_RawPunchEvents_SiteRosterAssignmentId",
                schema: "attendance",
                table: "RawPunchEvents");

            migrationBuilder.DropIndex(
                name: "IX_RawPunchEvents_TemporaryShiftChangeId",
                schema: "attendance",
                table: "RawPunchEvents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FaceVerificationAttempts_Completion",
                schema: "attendance",
                table: "FaceVerificationAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FaceVerificationAttempts_Outcome",
                schema: "attendance",
                table: "FaceVerificationAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DailyAttendanceSummaries_PolicyOutputs",
                schema: "attendance",
                table: "DailyAttendanceSummaries");

            migrationBuilder.DropColumn(
                name: "BusinessDate",
                schema: "attendance",
                table: "RawPunchEvents");

            migrationBuilder.DropColumn(
                name: "HolidayCalendarId",
                schema: "attendance",
                table: "RawPunchEvents");

            migrationBuilder.DropColumn(
                name: "SiteRosterAssignmentId",
                schema: "attendance",
                table: "RawPunchEvents");

            migrationBuilder.DropColumn(
                name: "TemporaryShiftChangeId",
                schema: "attendance",
                table: "RawPunchEvents");

            migrationBuilder.DropColumn(
                name: "InputEvidenceReference",
                schema: "attendance",
                table: "DailyAttendanceSummaries");

            migrationBuilder.AlterColumn<Guid>(
                name: "PolicyVersionId",
                schema: "attendance",
                table: "WorkSessions",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PolicyVersionId",
                schema: "attendance",
                table: "PunchAssessments",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ShiftVersionId",
                schema: "attendance",
                table: "DailyAttendanceSummaries",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PolicyVersionId",
                schema: "attendance",
                table: "DailyAttendanceSummaries",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PayableMinutes",
                schema: "attendance",
                table: "DailyAttendanceSummaries",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "OvertimeMinutes",
                schema: "attendance",
                table: "DailyAttendanceSummaries",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "LateMinutes",
                schema: "attendance",
                table: "DailyAttendanceSummaries",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_FaceVerificationAttempts_Completion",
                schema: "attendance",
                table: "FaceVerificationAttempts",
                sql: "([Outcome] = 1 AND [CompletedAtUtc] IS NULL AND [LivenessPassed] IS NULL AND [MatchPassed] IS NULL AND [ResultReasonCode] IS NULL) OR ([Outcome] <> 1 AND [CompletedAtUtc] IS NOT NULL AND [CompletedAtUtc] >= [IssuedAtUtc] AND [CompletedAtUtc] < [ExpiresAtUtc] AND [LivenessPassed] IS NOT NULL AND [MatchPassed] IS NOT NULL AND [ResultReasonCode] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FaceVerificationAttempts_Outcome",
                schema: "attendance",
                table: "FaceVerificationAttempts",
                sql: "[Outcome] BETWEEN 1 AND 4");
        }
    }
}
