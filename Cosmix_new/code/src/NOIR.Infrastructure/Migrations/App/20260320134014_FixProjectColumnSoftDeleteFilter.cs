using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOIR.Infrastructure.Migrations.App
{
    /// <inheritdoc />
    public partial class FixProjectColumnSoftDeleteFilter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectColumns_TenantId_ProjectId_Name",
                table: "ProjectColumns");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectColumns_TenantId_ProjectId_Name",
                table: "ProjectColumns",
                columns: new[] { "TenantId", "ProjectId", "Name" },
                unique: true,
                filter: "IsDeleted = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectColumns_TenantId_ProjectId_Name",
                table: "ProjectColumns");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectColumns_TenantId_ProjectId_Name",
                table: "ProjectColumns",
                columns: new[] { "TenantId", "ProjectId", "Name" },
                unique: true);
        }
    }
}
