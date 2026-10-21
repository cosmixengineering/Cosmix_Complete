using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectSiteSchedulingAndTemporaryAmendments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ApprovalRequestId",
                schema: "attendance",
                table: "TemporaryShiftChanges",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "ApprovalRequestId",
                schema: "attendance",
                table: "ShiftPopulationAssignments",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "HolidayCalendarId",
                schema: "attendance",
                table: "ShiftPopulationAssignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ApprovalRequestId",
                schema: "attendance",
                table: "ShiftPopulationAssignmentEnds",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.CreateTable(
                name: "TemporaryShiftChangeAmendments",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalChangeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReplacementChangeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecordedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    WrittenEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemporaryShiftChangeAmendments", x => x.Id);
                    table.CheckConstraint("CK_TemporaryShiftChangeAmendments_DifferentChanges", "[ReplacementChangeId] IS NULL OR [OriginalChangeId] <> [ReplacementChangeId]");
                    table.CheckConstraint("CK_TemporaryShiftChangeAmendments_Evidence", "LEN(LTRIM(RTRIM([RecordedBy]))) > 0 AND LEN(LTRIM(RTRIM([WrittenEvidenceReference]))) > 0 AND LEN(LTRIM(RTRIM([Reason]))) > 0");
                    table.CheckConstraint("CK_TemporaryShiftChangeAmendments_RecordedAtUtc_Utc", "[RecordedAtUtc] IS NULL OR DATEPART(TZOFFSET, [RecordedAtUtc]) = 0");
                    table.ForeignKey(
                        name: "FK_TemporaryShiftChangeAmendments_TemporaryShiftChanges_OriginalChangeId",
                        column: x => x.OriginalChangeId,
                        principalSchema: "attendance",
                        principalTable: "TemporaryShiftChanges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TemporaryShiftChangeAmendments_TemporaryShiftChanges_ReplacementChangeId",
                        column: x => x.ReplacementChangeId,
                        principalSchema: "attendance",
                        principalTable: "TemporaryShiftChanges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftPopulationAssignments_HolidayCalendarId",
                schema: "attendance",
                table: "ShiftPopulationAssignments",
                column: "HolidayCalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_TemporaryShiftChangeAmendments_OriginalChangeId",
                schema: "attendance",
                table: "TemporaryShiftChangeAmendments",
                column: "OriginalChangeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TemporaryShiftChangeAmendments_ReplacementChangeId",
                schema: "attendance",
                table: "TemporaryShiftChangeAmendments",
                column: "ReplacementChangeId",
                unique: true,
                filter: "[ReplacementChangeId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftPopulationAssignments_HolidayCalendars_HolidayCalendarId",
                schema: "attendance",
                table: "ShiftPopulationAssignments",
                column: "HolidayCalendarId",
                principalSchema: "hr",
                principalTable: "HolidayCalendars",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A rollback must never erase direct scheduling evidence or fabricate an approval.
            // Hold write locks until EF's migration transaction completes the schema rollback.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [attendance].[TemporaryShiftChangeAmendments] WITH (TABLOCKX, HOLDLOCK))
                   OR EXISTS (SELECT 1 FROM [attendance].[TemporaryShiftChanges] WITH (TABLOCKX, HOLDLOCK)
                              WHERE [ApprovalRequestId] IS NULL)
                   OR EXISTS (SELECT 1 FROM [attendance].[ShiftPopulationAssignments] WITH (TABLOCKX, HOLDLOCK)
                              WHERE [ApprovalRequestId] IS NULL OR [HolidayCalendarId] IS NOT NULL)
                   OR EXISTS (SELECT 1 FROM [attendance].[ShiftPopulationAssignmentEnds] WITH (TABLOCKX, HOLDLOCK)
                              WHERE [ApprovalRequestId] IS NULL)
                    THROW 51002, 'Direct site scheduling evidence exists. Use an explicitly reviewed forward migration; rollback is refused.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftPopulationAssignments_HolidayCalendars_HolidayCalendarId",
                schema: "attendance",
                table: "ShiftPopulationAssignments");

            migrationBuilder.DropTable(
                name: "TemporaryShiftChangeAmendments",
                schema: "attendance");

            migrationBuilder.DropIndex(
                name: "IX_ShiftPopulationAssignments_HolidayCalendarId",
                schema: "attendance",
                table: "ShiftPopulationAssignments");

            migrationBuilder.DropColumn(
                name: "HolidayCalendarId",
                schema: "attendance",
                table: "ShiftPopulationAssignments");

            migrationBuilder.AlterColumn<Guid>(
                name: "ApprovalRequestId",
                schema: "attendance",
                table: "TemporaryShiftChanges",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ApprovalRequestId",
                schema: "attendance",
                table: "ShiftPopulationAssignments",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ApprovalRequestId",
                schema: "attendance",
                table: "ShiftPopulationAssignmentEnds",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
