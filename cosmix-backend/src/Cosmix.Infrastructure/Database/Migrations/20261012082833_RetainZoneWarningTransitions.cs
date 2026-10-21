using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class RetainZoneWarningTransitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LastWarning",
                schema: "attendance",
                table: "ZoneMonitoringCheckpoints",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [attendance].[ZoneMonitoringCheckpoints] WHERE [LastWarning] IS NOT NULL)
                    THROW 51000, 'Cannot downgrade retained zone warning transition state.', 1;
                """);
            migrationBuilder.DropColumn(
                name: "LastWarning",
                schema: "attendance",
                table: "ZoneMonitoringCheckpoints");
        }
    }
}
