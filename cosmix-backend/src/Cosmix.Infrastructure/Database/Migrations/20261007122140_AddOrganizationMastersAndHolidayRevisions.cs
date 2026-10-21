using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationMastersAndHolidayRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HolidayCalendars_Code_EffectiveFrom",
                schema: "hr",
                table: "HolidayCalendars");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "hr",
                table: "HolidayCalendars",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                schema: "hr",
                table: "HolidayCalendars",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "SupersedesCalendarId",
                schema: "hr",
                table: "HolidayCalendars",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrganizationChanges",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuditEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Resource = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", maxLength: 524288, nullable: true),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", maxLength: 524288, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationChanges", x => x.Id);
                    table.CheckConstraint("CK_OrganizationChanges_Json", "ISJSON([AfterJson]) = 1 AND ([BeforeJson] IS NULL OR ISJSON([BeforeJson]) = 1)");
                    table.ForeignKey(
                        name: "FK_OrganizationChanges_AuditEvents_AuditEventId",
                        column: x => x.AuditEventId,
                        principalSchema: "audit",
                        principalTable: "AuditEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HolidayCalendars_Code_EffectiveFrom_Revision",
                schema: "hr",
                table: "HolidayCalendars",
                columns: new[] { "Code", "EffectiveFrom", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HolidayCalendars_SupersedesCalendarId",
                schema: "hr",
                table: "HolidayCalendars",
                column: "SupersedesCalendarId",
                unique: true,
                filter: "[SupersedesCalendarId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HolidayCalendars_Predecessor",
                schema: "hr",
                table: "HolidayCalendars",
                sql: "([Revision] = 1 AND [SupersedesCalendarId] IS NULL) OR ([Revision] > 1 AND [SupersedesCalendarId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HolidayCalendars_Revision",
                schema: "hr",
                table: "HolidayCalendars",
                sql: "[Revision] >= 1");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationChanges_AuditEventId",
                schema: "hr",
                table: "OrganizationChanges",
                column: "AuditEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationChanges_Resource_ResourceId",
                schema: "hr",
                table: "OrganizationChanges",
                columns: new[] { "Resource", "ResourceId" });

            migrationBuilder.AddForeignKey(
                name: "FK_HolidayCalendars_HolidayCalendars_SupersedesCalendarId",
                schema: "hr",
                table: "HolidayCalendars",
                column: "SupersedesCalendarId",
                principalSchema: "hr",
                principalTable: "HolidayCalendars",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                SET XACT_ABORT ON;
                IF EXISTS (SELECT 1 FROM [hr].[OrganizationChanges])
                   OR EXISTS (SELECT 1 FROM [hr].[HolidayCalendars] WHERE [Revision] > 1)
                    THROW 51002, 'Organization history exists. Review a data-preserving rollback before downgrading.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_HolidayCalendars_HolidayCalendars_SupersedesCalendarId",
                schema: "hr",
                table: "HolidayCalendars");

            migrationBuilder.DropTable(
                name: "OrganizationChanges",
                schema: "hr");

            migrationBuilder.DropIndex(
                name: "IX_HolidayCalendars_Code_EffectiveFrom_Revision",
                schema: "hr",
                table: "HolidayCalendars");

            migrationBuilder.DropIndex(
                name: "IX_HolidayCalendars_SupersedesCalendarId",
                schema: "hr",
                table: "HolidayCalendars");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HolidayCalendars_Predecessor",
                schema: "hr",
                table: "HolidayCalendars");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HolidayCalendars_Revision",
                schema: "hr",
                table: "HolidayCalendars");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "hr",
                table: "HolidayCalendars");

            migrationBuilder.DropColumn(
                name: "Revision",
                schema: "hr",
                table: "HolidayCalendars");

            migrationBuilder.DropColumn(
                name: "SupersedesCalendarId",
                schema: "hr",
                table: "HolidayCalendars");

            migrationBuilder.CreateIndex(
                name: "IX_HolidayCalendars_Code_EffectiveFrom",
                schema: "hr",
                table: "HolidayCalendars",
                columns: new[] { "Code", "EffectiveFrom" },
                unique: true);
        }
    }
}
