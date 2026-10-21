using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddWebSmsAuthenticationAndProtectedRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MobileOtpChallenges_Purpose",
                schema: "auth",
                table: "MobileOtpChallenges");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeSubmissions_MobileEnrollment",
                schema: "hr",
                table: "EmployeeSubmissions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Employees_MobileEnrollment",
                schema: "hr",
                table: "Employees");

            migrationBuilder.AddColumn<Guid>(
                name: "SmsOtpChallengeId",
                schema: "auth",
                table: "UserSessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SystemRole",
                schema: "auth",
                table: "Roles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_MobileOtpChallenges_UserAccountId_Id",
                schema: "auth",
                table: "MobileOtpChallenges",
                columns: new[] { "UserAccountId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_SmsOtpChallengeId",
                schema: "auth",
                table: "UserSessions",
                column: "SmsOtpChallengeId",
                unique: true,
                filter: "[SmsOtpChallengeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_UserAccountId_SmsOtpChallengeId",
                schema: "auth",
                table: "UserSessions",
                columns: new[] { "UserAccountId", "SmsOtpChallengeId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserSessions_SmsProof",
                schema: "auth",
                table: "UserSessions",
                sql: "[SmsOtpChallengeId] IS NULL OR ([Client] = 1 AND [MfaChallengeId] IS NULL AND [RegisteredDeviceId] IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_SystemRole",
                schema: "auth",
                table: "Roles",
                column: "SystemRole",
                unique: true,
                filter: "[SystemRole] <> 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Roles_SystemRole",
                schema: "auth",
                table: "Roles",
                sql: "[SystemRole] IN (0, 1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MobileOtpChallenges_Purpose",
                schema: "auth",
                table: "MobileOtpChallenges",
                sql: "([Purpose] = 'MobileLogin' AND [Platform] IN ('android','ios')) OR ([Purpose] = 'WebLogin' AND [Platform] = 'web')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeSubmissions_MobileEnrollment",
                schema: "hr",
                table: "EmployeeSubmissions",
                sql: "[MobileAccessRequested] = 0 OR [ApprovedLoginPhoneNumber] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Employees_MobileEnrollment",
                schema: "hr",
                table: "Employees",
                sql: "[MobileAccessRequested] = 0 OR [ApprovedLoginPhoneNumber] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_UserSessions_MobileOtpChallenges_UserAccountId_SmsOtpChallengeId",
                schema: "auth",
                table: "UserSessions",
                columns: new[] { "UserAccountId", "SmsOtpChallengeId" },
                principalSchema: "auth",
                principalTable: "MobileOtpChallenges",
                principalColumns: new[] { "UserAccountId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserSessions_MobileOtpChallenges_UserAccountId_SmsOtpChallengeId",
                schema: "auth",
                table: "UserSessions");

            migrationBuilder.DropIndex(
                name: "IX_UserSessions_SmsOtpChallengeId",
                schema: "auth",
                table: "UserSessions");

            migrationBuilder.DropIndex(
                name: "IX_UserSessions_UserAccountId_SmsOtpChallengeId",
                schema: "auth",
                table: "UserSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserSessions_SmsProof",
                schema: "auth",
                table: "UserSessions");

            migrationBuilder.DropIndex(
                name: "IX_Roles_SystemRole",
                schema: "auth",
                table: "Roles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Roles_SystemRole",
                schema: "auth",
                table: "Roles");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_MobileOtpChallenges_UserAccountId_Id",
                schema: "auth",
                table: "MobileOtpChallenges");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MobileOtpChallenges_Purpose",
                schema: "auth",
                table: "MobileOtpChallenges");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeSubmissions_MobileEnrollment",
                schema: "hr",
                table: "EmployeeSubmissions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Employees_MobileEnrollment",
                schema: "hr",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "SmsOtpChallengeId",
                schema: "auth",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "SystemRole",
                schema: "auth",
                table: "Roles");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MobileOtpChallenges_Purpose",
                schema: "auth",
                table: "MobileOtpChallenges",
                sql: "[Purpose] = 'MobileLogin'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeSubmissions_MobileEnrollment",
                schema: "hr",
                table: "EmployeeSubmissions",
                sql: "([MobileAccessRequested] = 0 AND [ApprovedLoginPhoneNumber] IS NULL) OR ([MobileAccessRequested] = 1 AND [ApprovedLoginPhoneNumber] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Employees_MobileEnrollment",
                schema: "hr",
                table: "Employees",
                sql: "([MobileAccessRequested] = 0 AND [ApprovedLoginPhoneNumber] IS NULL) OR ([MobileAccessRequested] = 1 AND [ApprovedLoginPhoneNumber] IS NOT NULL)");
        }
    }
}
