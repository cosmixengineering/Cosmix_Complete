using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovedPayrollCostAllocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "finance");

            migrationBuilder.CreateTable(
                name: "PayrollCostPlans",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    SupersedesId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProofReference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedByEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DecidedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DecidedByEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DecisionProofReference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollCostPlans", x => x.Id);
                    table.CheckConstraint("CK_PayrollCostPlan_Decision", "([State] = 'Draft' AND [DecidedBy] IS NULL AND [DecidedAt] IS NULL AND [DecisionProofReference] IS NULL AND [DecidedByEmployeeId] IS NULL) OR ([State] IN ('Approved','Rejected') AND [DecidedBy] IS NOT NULL AND [DecidedBy] <> [CreatedBy] AND [DecidedByEmployeeId] IS NOT NULL AND [DecidedByEmployeeId] <> [CreatedByEmployeeId] AND [DecidedAt] IS NOT NULL AND [DecidedAt] >= [CreatedAt] AND [DecisionProofReference] IS NOT NULL)");
                    table.CheckConstraint("CK_PayrollCostPlan_Revision", "[Revision] > 0");
                    table.ForeignKey(
                        name: "FK_PayrollCostPlans_PayrollCostPlans_SupersedesId",
                        column: x => x.SupersedesId,
                        principalSchema: "finance",
                        principalTable: "PayrollCostPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollCostPlans_PayrollHandoffBatches_BatchId",
                        column: x => x.BatchId,
                        principalSchema: "hr",
                        principalTable: "PayrollHandoffBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollCostPlans_BatchId",
                schema: "finance",
                table: "PayrollCostPlans",
                column: "BatchId",
                unique: true,
                filter: "[State] = 'Draft'");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollCostPlans_BatchId_Revision",
                schema: "finance",
                table: "PayrollCostPlans",
                columns: new[] { "BatchId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollCostPlans_SupersedesId",
                schema: "finance",
                table: "PayrollCostPlans",
                column: "SupersedesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollCostPlans",
                schema: "finance");
        }
    }
}
