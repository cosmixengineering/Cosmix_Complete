using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class TrackStatutoryEmployeePostingCoverage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StatutoryEmployeePostings",
                schema: "hr",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContributionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObligationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PostedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PayableAccountCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatutoryEmployeePostings", x => x.EntryId);
                    table.ForeignKey(
                        name: "FK_StatutoryEmployeePostings_PayrollJournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalSchema: "hr",
                        principalTable: "PayrollJournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StatutoryEmployeePostings_PayrollObligations_ObligationId",
                        column: x => x.ObligationId,
                        principalSchema: "hr",
                        principalTable: "PayrollObligations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StatutoryEmployeePostings_StatutoryContributionEntries_EntryId",
                        column: x => x.EntryId,
                        principalSchema: "hr",
                        principalTable: "StatutoryContributionEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StatutoryEmployeePostings_StatutoryContributions_ContributionId",
                        column: x => x.ContributionId,
                        principalSchema: "hr",
                        principalTable: "StatutoryContributions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryEmployeePostings_ContributionId",
                schema: "hr",
                table: "StatutoryEmployeePostings",
                column: "ContributionId");

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryEmployeePostings_JournalEntryId",
                schema: "hr",
                table: "StatutoryEmployeePostings",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryEmployeePostings_ObligationId",
                schema: "hr",
                table: "StatutoryEmployeePostings",
                column: "ObligationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StatutoryEmployeePostings",
                schema: "hr");
        }
    }
}
