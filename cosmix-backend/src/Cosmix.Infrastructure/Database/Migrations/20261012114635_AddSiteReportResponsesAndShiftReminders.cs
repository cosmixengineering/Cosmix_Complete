using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteReportResponsesAndShiftReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MissingCheckoutReminderAfterMinutes",
                schema: "attendance",
                table: "ShiftVersions",
                type: "int",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_SiteReports_EmployeeId_Id",
                schema: "attendance",
                table: "SiteReports",
                columns: new[] { "EmployeeId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_SiteReportReviews_SiteReportId_Id",
                schema: "attendance",
                table: "SiteReportReviews",
                columns: new[] { "SiteReportId", "Id" });

            migrationBuilder.CreateTable(
                name: "SiteReportResponses",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SiteReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    ReturnedReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedByAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    PrivateEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteReportResponses", x => x.Id);
                    table.CheckConstraint("CK_SiteReportResponses_Sequence", "[Sequence] > 0");
                    table.CheckConstraint("CK_SiteReportResponses_SubmittedAtUtc_Utc", "[SubmittedAtUtc] IS NULL OR DATEPART(TZOFFSET, [SubmittedAtUtc]) = 0");
                    table.ForeignKey(
                        name: "FK_SiteReportResponses_SiteReportReviews_SiteReportId_ReturnedReviewId",
                        columns: x => new { x.SiteReportId, x.ReturnedReviewId },
                        principalSchema: "attendance",
                        principalTable: "SiteReportReviews",
                        principalColumns: new[] { "SiteReportId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SiteReportResponses_SiteReports_EmployeeId_SiteReportId",
                        columns: x => new { x.EmployeeId, x.SiteReportId },
                        principalSchema: "attendance",
                        principalTable: "SiteReports",
                        principalColumns: new[] { "EmployeeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SiteReportResponses_UserAccounts_EmployeeId_SubmittedByAccountId",
                        columns: x => new { x.EmployeeId, x.SubmittedByAccountId },
                        principalSchema: "auth",
                        principalTable: "UserAccounts",
                        principalColumns: new[] { "EmployeeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            // SQL Server compiles a migration batch before executing its preceding ADD COLUMN.
            // Defer this new-column constraint statement inside the same migration transaction.
            migrationBuilder.Sql("""
                EXEC(N'ALTER TABLE [attendance].[ShiftVersions] ADD CONSTRAINT [CK_ShiftVersions_MissingCheckoutReminderAfterMinutes] CHECK ([MissingCheckoutReminderAfterMinutes] IS NULL OR [MissingCheckoutReminderAfterMinutes] > 0)');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_SiteReportResponses_EmployeeId_SiteReportId",
                schema: "attendance",
                table: "SiteReportResponses",
                columns: new[] { "EmployeeId", "SiteReportId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteReportResponses_EmployeeId_SubmittedByAccountId",
                schema: "attendance",
                table: "SiteReportResponses",
                columns: new[] { "EmployeeId", "SubmittedByAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteReportResponses_ReturnedReviewId",
                schema: "attendance",
                table: "SiteReportResponses",
                column: "ReturnedReviewId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteReportResponses_SiteReportId_ReturnedReviewId",
                schema: "attendance",
                table: "SiteReportResponses",
                columns: new[] { "SiteReportId", "ReturnedReviewId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteReportResponses_SiteReportId_Sequence",
                schema: "attendance",
                table: "SiteReportResponses",
                columns: new[] { "SiteReportId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [attendance].[SiteReportResponses])
                    THROW 51000, 'Cannot downgrade retained site report response history.', 1;
                IF EXISTS (SELECT 1 FROM [attendance].[ShiftVersions] WHERE [MissingCheckoutReminderAfterMinutes] IS NOT NULL)
                    THROW 51000, 'Cannot downgrade configured shift reminder history.', 1;
                """);

            migrationBuilder.DropTable(
                name: "SiteReportResponses",
                schema: "attendance");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_SiteReports_EmployeeId_Id",
                schema: "attendance",
                table: "SiteReports");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_SiteReportReviews_SiteReportId_Id",
                schema: "attendance",
                table: "SiteReportReviews");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ShiftVersions_MissingCheckoutReminderAfterMinutes",
                schema: "attendance",
                table: "ShiftVersions");

            migrationBuilder.DropColumn(
                name: "MissingCheckoutReminderAfterMinutes",
                schema: "attendance",
                table: "ShiftVersions");
        }
    }
}
