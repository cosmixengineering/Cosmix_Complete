using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectPoliciesAndAdminLeave : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ApprovalRequestId",
                schema: "hr",
                table: "PolicyVersions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<int>(
                name: "Authority",
                schema: "hr",
                table: "PolicyVersions",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AlterColumn<Guid>(
                name: "ApprovalRequestId",
                schema: "hr",
                table: "PolicyAssignments",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<int>(
                name: "Authority",
                schema: "hr",
                table: "PolicyAssignments",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AlterColumn<Guid>(
                name: "ApprovalRequestId",
                schema: "hr",
                table: "PolicyAssignmentEnds",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<int>(
                name: "Authority",
                schema: "hr",
                table: "PolicyAssignmentEnds",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                schema: "hr",
                table: "Policies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                schema: "hr",
                table: "LeaveTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "PolicyVersionId",
                schema: "hr",
                table: "LeaveRequests",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "PreviousRequestId",
                schema: "hr",
                table: "LeaveRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmissionKey",
                schema: "hr",
                table: "LeaveRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LeaveRequestDays",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaveRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Minutes = table.Column<int>(type: "int", nullable: false),
                    ShiftVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveRequestDays", x => x.Id);
                    table.CheckConstraint("CK_LeaveRequestDays_Interval", "[EndsAtUtc] > [StartsAtUtc] AND [Minutes] > 0 AND [Minutes] <= 1440");
                    table.ForeignKey(
                        name: "FK_LeaveRequestDays_LeaveRequests_LeaveRequestId",
                        column: x => x.LeaveRequestId,
                        principalSchema: "hr",
                        principalTable: "LeaveRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveRequestDays_ShiftVersions_ShiftVersionId",
                        column: x => x.ShiftVersionId,
                        principalSchema: "attendance",
                        principalTable: "ShiftVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // SQL Server compiles the outer batch before ADD COLUMN has executed.
            // Compile each constraint only after its authority column exists, in the same transaction.
            migrationBuilder.Sql("""
                EXEC(N'ALTER TABLE [hr].[PolicyVersions] ADD CONSTRAINT [CK_PolicyVersion_Authority] CHECK (([Authority] = 1 AND [ApprovalRequestId] IS NOT NULL) OR ([Authority] = 2 AND [ApprovalRequestId] IS NULL))');
                EXEC(N'ALTER TABLE [hr].[PolicyAssignments] ADD CONSTRAINT [CK_PolicyAssignment_Authority] CHECK (([Authority] = 1 AND [ApprovalRequestId] IS NOT NULL) OR ([Authority] = 2 AND [ApprovalRequestId] IS NULL))');
                EXEC(N'ALTER TABLE [hr].[PolicyAssignmentEnds] ADD CONSTRAINT [CK_PolicyAssignmentEnd_Authority] CHECK (([Authority] = 1 AND [ApprovalRequestId] IS NOT NULL) OR ([Authority] = 2 AND [ApprovalRequestId] IS NULL))');
                """);

            // Filter predicates also bind before this batch's ADD COLUMN statements execute.
            migrationBuilder.Sql("""
                EXEC(N'CREATE UNIQUE INDEX [IX_LeaveRequests_EmployeeId_SubmissionKey] ON [hr].[LeaveRequests] ([EmployeeId], [SubmissionKey]) WHERE [SubmissionKey] IS NOT NULL');
                EXEC(N'CREATE UNIQUE INDEX [IX_LeaveRequests_PreviousRequestId] ON [hr].[LeaveRequests] ([PreviousRequestId]) WHERE [PreviousRequestId] IS NOT NULL');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequestDays_Date_LeaveRequestId",
                schema: "hr",
                table: "LeaveRequestDays",
                columns: new[] { "Date", "LeaveRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequestDays_LeaveRequestId_Date",
                schema: "hr",
                table: "LeaveRequestDays",
                columns: new[] { "LeaveRequestId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequestDays_ShiftVersionId",
                schema: "hr",
                table: "LeaveRequestDays",
                column: "ShiftVersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveRequests_LeaveRequests_PreviousRequestId",
                schema: "hr",
                table: "LeaveRequests",
                column: "PreviousRequestId",
                principalSchema: "hr",
                principalTable: "LeaveRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [hr].[LeaveRequestDays])
                   OR EXISTS (SELECT 1 FROM [hr].[LeaveRequests] WHERE [PolicyVersionId] IS NULL OR [SubmissionKey] IS NOT NULL OR [PreviousRequestId] IS NOT NULL)
                   OR EXISTS (SELECT 1 FROM [hr].[PolicyVersions] WHERE [Authority] <> 1 OR [ApprovalRequestId] IS NULL)
                   OR EXISTS (SELECT 1 FROM [hr].[PolicyAssignments] WHERE [Authority] <> 1 OR [ApprovalRequestId] IS NULL)
                   OR EXISTS (SELECT 1 FROM [hr].[PolicyAssignmentEnds] WHERE [Authority] <> 1 OR [ApprovalRequestId] IS NULL)
                   OR EXISTS (SELECT 1 FROM [hr].[Policies] WHERE [Revision] <> 0)
                   OR EXISTS (SELECT 1 FROM [hr].[LeaveTypes] WHERE [Kind] <> 0)
                    THROW 51000, 'Cannot downgrade direct HR policy or leave history.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_LeaveRequests_LeaveRequests_PreviousRequestId",
                schema: "hr",
                table: "LeaveRequests");

            migrationBuilder.DropTable(
                name: "LeaveRequestDays",
                schema: "hr");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PolicyVersion_Authority",
                schema: "hr",
                table: "PolicyVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PolicyAssignment_Authority",
                schema: "hr",
                table: "PolicyAssignments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PolicyAssignmentEnd_Authority",
                schema: "hr",
                table: "PolicyAssignmentEnds");

            migrationBuilder.DropIndex(
                name: "IX_LeaveRequests_EmployeeId_SubmissionKey",
                schema: "hr",
                table: "LeaveRequests");

            migrationBuilder.DropIndex(
                name: "IX_LeaveRequests_PreviousRequestId",
                schema: "hr",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "Authority",
                schema: "hr",
                table: "PolicyVersions");

            migrationBuilder.DropColumn(
                name: "Authority",
                schema: "hr",
                table: "PolicyAssignments");

            migrationBuilder.DropColumn(
                name: "Authority",
                schema: "hr",
                table: "PolicyAssignmentEnds");

            migrationBuilder.DropColumn(
                name: "Revision",
                schema: "hr",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "hr",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "PreviousRequestId",
                schema: "hr",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "SubmissionKey",
                schema: "hr",
                table: "LeaveRequests");

            migrationBuilder.AlterColumn<Guid>(
                name: "ApprovalRequestId",
                schema: "hr",
                table: "PolicyVersions",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ApprovalRequestId",
                schema: "hr",
                table: "PolicyAssignments",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ApprovalRequestId",
                schema: "hr",
                table: "PolicyAssignmentEnds",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PolicyVersionId",
                schema: "hr",
                table: "LeaveRequests",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

        }
    }
}
