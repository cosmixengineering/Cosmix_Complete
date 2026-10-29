using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class ReturnApprovedPayrollForCorrection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollHandoffBatches_SalaryApproval",
                schema: "hr",
                table: "PayrollHandoffBatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollHandoffBatches_SalaryState",
                schema: "hr",
                table: "PayrollHandoffBatches");

            migrationBuilder.AddColumn<Guid>(
                name: "ReturnApprovalRequestId",
                schema: "hr",
                table: "PayrollHandoffBatches",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReturnedAt",
                schema: "hr",
                table: "PayrollHandoffBatches",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnedByUserId",
                schema: "hr",
                table: "PayrollHandoffBatches",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollHandoffBatches_ReturnApprovalRequestId",
                schema: "hr",
                table: "PayrollHandoffBatches",
                column: "ReturnApprovalRequestId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollHandoffBatches_Return",
                schema: "hr",
                table: "PayrollHandoffBatches",
                sql: "([ReviewState] <> 'returned' AND [ReturnApprovalRequestId] IS NULL AND [ReturnedByUserId] IS NULL AND [ReturnedAt] IS NULL) OR ([ReviewState] = 'returned' AND [CalculationVersion] = 'salary-v2' AND [ReturnApprovalRequestId] IS NOT NULL AND [ReturnedByUserId] IS NOT NULL AND [ReturnedByUserId] <> [PreparedByUserId] AND [ReturnedAt] IS NOT NULL AND [HrApprovedAt] IS NOT NULL AND [ReturnedAt] >= [HrApprovedAt] AND [LockedAt] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollHandoffBatches_SalaryApproval",
                schema: "hr",
                table: "PayrollHandoffBatches",
                sql: "[CalculationVersion] <> 'salary-v2' OR ([ReviewState] IN ('prepared','cancelled') AND [HrApprovalRequestId] IS NULL AND [HrApprovedAt] IS NULL AND [HrApprovedByUserId] IS NULL AND [LockedAt] IS NULL) OR ([ReviewState] IN ('hr_approved','rejected','returned','locked','finance_handed_off') AND [HrApprovalRequestId] IS NOT NULL AND [HrApprovedAt] IS NOT NULL AND [HrApprovedAt] >= [PreparedAt] AND [HrApprovedByUserId] IS NOT NULL AND [HrApprovedByUserId] <> [PreparedByUserId] AND (([ReviewState] IN ('hr_approved','rejected','returned') AND [LockedAt] IS NULL) OR ([ReviewState] IN ('locked','finance_handed_off') AND [LockedAt] IS NOT NULL AND [LockedAt] >= [HrApprovedAt] AND [LockedByUserId] <> [PreparedByUserId] AND [LockedByUserId] <> [HrApprovedByUserId])))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollHandoffBatches_SalaryState",
                schema: "hr",
                table: "PayrollHandoffBatches",
                sql: "[ReviewState] IN ('prepared','hr_approved','rejected','cancelled','returned','locked','finance_handed_off')");

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollHandoffBatches_ApprovalRequests_ReturnApprovalRequestId",
                schema: "hr",
                table: "PayrollHandoffBatches",
                column: "ReturnApprovalRequestId",
                principalSchema: "workflow",
                principalTable: "ApprovalRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PayrollHandoffBatches_ApprovalRequests_ReturnApprovalRequestId",
                schema: "hr",
                table: "PayrollHandoffBatches");

            migrationBuilder.DropIndex(
                name: "IX_PayrollHandoffBatches_ReturnApprovalRequestId",
                schema: "hr",
                table: "PayrollHandoffBatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollHandoffBatches_Return",
                schema: "hr",
                table: "PayrollHandoffBatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollHandoffBatches_SalaryApproval",
                schema: "hr",
                table: "PayrollHandoffBatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollHandoffBatches_SalaryState",
                schema: "hr",
                table: "PayrollHandoffBatches");

            migrationBuilder.DropColumn(
                name: "ReturnApprovalRequestId",
                schema: "hr",
                table: "PayrollHandoffBatches");

            migrationBuilder.DropColumn(
                name: "ReturnedAt",
                schema: "hr",
                table: "PayrollHandoffBatches");

            migrationBuilder.DropColumn(
                name: "ReturnedByUserId",
                schema: "hr",
                table: "PayrollHandoffBatches");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollHandoffBatches_SalaryApproval",
                schema: "hr",
                table: "PayrollHandoffBatches",
                sql: "[CalculationVersion] <> 'salary-v2' OR ([ReviewState] IN ('prepared','cancelled') AND [HrApprovalRequestId] IS NULL AND [HrApprovedAt] IS NULL AND [HrApprovedByUserId] IS NULL AND [LockedAt] IS NULL) OR ([ReviewState] IN ('hr_approved','rejected','locked','finance_handed_off') AND [HrApprovalRequestId] IS NOT NULL AND [HrApprovedAt] IS NOT NULL AND [HrApprovedAt] >= [PreparedAt] AND [HrApprovedByUserId] IS NOT NULL AND [HrApprovedByUserId] <> [PreparedByUserId] AND (([ReviewState] IN ('hr_approved','rejected') AND [LockedAt] IS NULL) OR ([ReviewState] IN ('locked','finance_handed_off') AND [LockedAt] IS NOT NULL AND [LockedAt] >= [HrApprovedAt] AND [LockedByUserId] <> [PreparedByUserId] AND [LockedByUserId] <> [HrApprovedByUserId])))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollHandoffBatches_SalaryState",
                schema: "hr",
                table: "PayrollHandoffBatches",
                sql: "[ReviewState] IN ('prepared','hr_approved','rejected','cancelled','locked','finance_handed_off')");
        }
    }
}
