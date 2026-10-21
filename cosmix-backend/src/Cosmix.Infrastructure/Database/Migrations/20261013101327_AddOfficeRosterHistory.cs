using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddOfficeRosterHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OfficeRosterAssignments",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    HolidayCalendarId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecordedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficeRosterAssignments", x => x.Id);
                    table.CheckConstraint("CK_OfficeRosterAssignments_Dates", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
                    table.ForeignKey(
                        name: "FK_OfficeRosterAssignments_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "hr",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OfficeRosterAssignments_HolidayCalendars_HolidayCalendarId",
                        column: x => x.HolidayCalendarId,
                        principalSchema: "hr",
                        principalTable: "HolidayCalendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OfficeRosterAssignments_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalSchema: "attendance",
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OfficeRosterAssignmentEnds",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfficeRosterAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficeRosterAssignmentEnds", x => x.Id);
                    table.CheckConstraint("CK_OfficeRosterAssignmentEnds_Reason", "LEN(LTRIM(RTRIM([Reason]))) > 0");
                    table.ForeignKey(
                        name: "FK_OfficeRosterAssignmentEnds_OfficeRosterAssignments_OfficeRosterAssignmentId",
                        column: x => x.OfficeRosterAssignmentId,
                        principalSchema: "attendance",
                        principalTable: "OfficeRosterAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OfficeRosterAssignmentEnds_OfficeRosterAssignmentId",
                schema: "attendance",
                table: "OfficeRosterAssignmentEnds",
                column: "OfficeRosterAssignmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OfficeRosterAssignments_BranchId_EffectiveFrom",
                schema: "attendance",
                table: "OfficeRosterAssignments",
                columns: new[] { "BranchId", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OfficeRosterAssignments_HolidayCalendarId",
                schema: "attendance",
                table: "OfficeRosterAssignments",
                column: "HolidayCalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_OfficeRosterAssignments_ShiftId",
                schema: "attendance",
                table: "OfficeRosterAssignments",
                column: "ShiftId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [attendance].[OfficeRosterAssignments])
                   OR EXISTS (SELECT 1 FROM [attendance].[OfficeRosterAssignmentEnds])
                    THROW 51000, 'Retained office roster history requires a reviewed preservation plan; downgrade refused.', 1;
                """);

            migrationBuilder.DropTable(
                name: "OfficeRosterAssignmentEnds",
                schema: "attendance");

            migrationBuilder.DropTable(
                name: "OfficeRosterAssignments",
                schema: "attendance");
        }
    }
}
