using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddWebAuthenticatorRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AuthenticatorRequired",
                schema: "auth",
                table: "UserAccounts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "EnrollmentSessionId",
                schema: "auth",
                table: "MfaCredentials",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BrowserContextHash",
                schema: "auth",
                table: "MfaChallenges",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true,
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.AddColumn<Guid>(
                name: "PhoneBindingId",
                schema: "auth",
                table: "MfaChallenges",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                schema: "auth",
                table: "MfaChallenges",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MfaResetApprovals",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SecurityVersionBefore = table.Column<int>(type: "int", nullable: false),
                    SecurityVersionAfter = table.Column<int>(type: "int", nullable: false),
                    ReviewerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewerEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProofReference = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MfaResetApprovals", x => x.Id);
                    table.CheckConstraint("CK_MfaResetApprovals_Independent", "[ReviewerAccountId] <> [UserAccountId] AND [ReviewerEmployeeId] <> [EmployeeId]");
                    table.CheckConstraint("CK_MfaResetApprovals_Version", "[SecurityVersionBefore] > 0 AND [SecurityVersionAfter] = [SecurityVersionBefore] + 1");
                    table.ForeignKey(
                        name: "FK_MfaResetApprovals_UserAccounts_EmployeeId_UserAccountId",
                        columns: x => new { x.EmployeeId, x.UserAccountId },
                        principalSchema: "auth",
                        principalTable: "UserAccounts",
                        principalColumns: new[] { "EmployeeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MfaResetApprovals_UserAccounts_ReviewerEmployeeId_ReviewerAccountId",
                        columns: x => new { x.ReviewerEmployeeId, x.ReviewerAccountId },
                        principalSchema: "auth",
                        principalTable: "UserAccounts",
                        principalColumns: new[] { "EmployeeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_MfaCredentials_CurrentAuthenticator",
                schema: "auth",
                table: "MfaCredentials",
                column: "UserAccountId",
                unique: true,
                filter: "[Method] = 1 AND [ConfirmedAt] IS NOT NULL AND [RevokedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MfaChallenges_UserAccountId_PhoneBindingId",
                schema: "auth",
                table: "MfaChallenges",
                columns: new[] { "UserAccountId", "PhoneBindingId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_MfaChallenges_WebBinding",
                schema: "auth",
                table: "MfaChallenges",
                sql: "([Purpose] IS NULL AND [PhoneBindingId] IS NULL AND [BrowserContextHash] IS NULL) OR ([Purpose] IS NOT NULL AND [Purpose] = N'WebLoginTotp' AND [PhoneBindingId] IS NOT NULL AND [BrowserContextHash] IS NOT NULL AND LEN([BrowserContextHash]) = 64 AND [RegisteredDeviceId] IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_MfaResetApprovals_EmployeeId_UserAccountId",
                schema: "auth",
                table: "MfaResetApprovals",
                columns: new[] { "EmployeeId", "UserAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_MfaResetApprovals_ReviewerEmployeeId_ReviewerAccountId",
                schema: "auth",
                table: "MfaResetApprovals",
                columns: new[] { "ReviewerEmployeeId", "ReviewerAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_MfaResetApprovals_UserAccountId_SecurityVersionBefore",
                schema: "auth",
                table: "MfaResetApprovals",
                columns: new[] { "UserAccountId", "SecurityVersionBefore" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MfaChallenges_LoginPhoneBindings_UserAccountId_PhoneBindingId",
                schema: "auth",
                table: "MfaChallenges",
                columns: new[] { "UserAccountId", "PhoneBindingId" },
                principalSchema: "auth",
                principalTable: "LoginPhoneBindings",
                principalColumns: new[] { "UserAccountId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MfaChallenges_LoginPhoneBindings_UserAccountId_PhoneBindingId",
                schema: "auth",
                table: "MfaChallenges");

            migrationBuilder.DropTable(
                name: "MfaResetApprovals",
                schema: "auth");

            migrationBuilder.DropIndex(
                name: "UX_MfaCredentials_CurrentAuthenticator",
                schema: "auth",
                table: "MfaCredentials");

            migrationBuilder.DropIndex(
                name: "IX_MfaChallenges_UserAccountId_PhoneBindingId",
                schema: "auth",
                table: "MfaChallenges");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MfaChallenges_WebBinding",
                schema: "auth",
                table: "MfaChallenges");

            migrationBuilder.DropColumn(
                name: "AuthenticatorRequired",
                schema: "auth",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "EnrollmentSessionId",
                schema: "auth",
                table: "MfaCredentials");

            migrationBuilder.DropColumn(
                name: "BrowserContextHash",
                schema: "auth",
                table: "MfaChallenges");

            migrationBuilder.DropColumn(
                name: "PhoneBindingId",
                schema: "auth",
                table: "MfaChallenges");

            migrationBuilder.DropColumn(
                name: "Purpose",
                schema: "auth",
                table: "MfaChallenges");
        }
    }
}
