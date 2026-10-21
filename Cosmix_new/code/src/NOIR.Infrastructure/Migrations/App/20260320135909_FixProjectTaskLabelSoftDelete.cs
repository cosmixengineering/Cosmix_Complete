using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOIR.Infrastructure.Migrations.App
{
    /// <inheritdoc />
    public partial class FixProjectTaskLabelSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectTaskLabels_TaskId_LabelId_TenantId",
                table: "ProjectTaskLabels");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTaskLabels_TaskId_LabelId_TenantId",
                table: "ProjectTaskLabels",
                columns: new[] { "TaskId", "LabelId", "TenantId" },
                unique: true,
                filter: "IsDeleted = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectTaskLabels_TaskId_LabelId_TenantId",
                table: "ProjectTaskLabels");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTaskLabels_TaskId_LabelId_TenantId",
                table: "ProjectTaskLabels",
                columns: new[] { "TaskId", "LabelId", "TenantId" },
                unique: true);
        }
    }
}
