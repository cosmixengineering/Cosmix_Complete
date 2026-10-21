using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedMobileAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_UserAccounts_Active",
                schema: "auth",
                table: "UserAccounts");

            migrationBuilder.AddColumn<bool>(
                name: "MobileAccessEnabled",
                schema: "auth",
                table: "UserAccounts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PhoneVerifiedAt",
                schema: "auth",
                table: "UserAccounts",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WebAccessEnabled",
                schema: "auth",
                table: "UserAccounts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedLoginPhoneNumber",
                schema: "hr",
                table: "EmployeeSubmissions",
                type: "varchar(16)",
                unicode: false,
                maxLength: 16,
                nullable: true,
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.AddColumn<bool>(
                name: "MobileAccessRequested",
                schema: "hr",
                table: "EmployeeSubmissions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedLoginPhoneNumber",
                schema: "hr",
                table: "Employees",
                type: "varchar(16)",
                unicode: false,
                maxLength: 16,
                nullable: true,
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.AddColumn<bool>(
                name: "MobileAccessRequested",
                schema: "hr",
                table: "Employees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AuthRateBuckets",
                schema: "auth",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false, collation: "Latin1_General_100_BIN2"),
                    WindowStartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    WindowEndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthRateBuckets", x => x.Key);
                    table.CheckConstraint("CK_AuthRateBuckets_Count", "[Count] >= 0");
                    table.CheckConstraint("CK_AuthRateBuckets_Window", "[WindowEndsAt] > [WindowStartsAt]");
                });

            migrationBuilder.CreateTable(
                name: "LoginPhoneBindings",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PhoneNumber = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ApprovedSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginPhoneBindings", x => x.Id);
                    table.UniqueConstraint("AK_LoginPhoneBindings_UserAccountId_Id", x => new { x.UserAccountId, x.Id });
                    table.CheckConstraint("CK_LoginPhoneBindings_Dates", "([VerifiedAt] IS NULL OR [VerifiedAt] >= [CreatedAt]) AND ([RevokedAt] IS NULL OR ([RevokedAt] >= [CreatedAt] AND ([VerifiedAt] IS NULL OR [RevokedAt] >= [VerifiedAt])))");
                    table.CheckConstraint("CK_LoginPhoneBindings_Phone", "DATALENGTH([PhoneNumber]) BETWEEN 3 AND 16 AND LEFT([PhoneNumber], 1) = '+' AND SUBSTRING([PhoneNumber], 2, 1) BETWEEN '1' AND '9' AND SUBSTRING([PhoneNumber], 2, 15) NOT LIKE '%[^0-9]%'");
                    table.ForeignKey(
                        name: "FK_LoginPhoneBindings_EmployeeSubmissions_ApprovedSubmissionId",
                        column: x => x.ApprovedSubmissionId,
                        principalSchema: "hr",
                        principalTable: "EmployeeSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoginPhoneBindings_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalSchema: "auth",
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MobileOtpChallenges",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PhoneBindingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Purpose = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false, collation: "Latin1_General_100_BIN2"),
                    SecurityVersion = table.Column<int>(type: "int", nullable: false),
                    DeviceHash = table.Column<string>(type: "varchar(256)", unicode: false, maxLength: 256, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Platform = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DeviceName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Verifier = table.Column<string>(type: "varchar(256)", unicode: false, maxLength: 256, nullable: false, collation: "Latin1_General_100_BIN2"),
                    IssuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    MaximumAttempts = table.Column<int>(type: "int", nullable: false),
                    FailedAttempts = table.Column<int>(type: "int", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MobileOtpChallenges", x => x.Id);
                    table.CheckConstraint("CK_MobileOtpChallenges_Attempts", "[SecurityVersion] > 0 AND [MaximumAttempts] > 0 AND [FailedAttempts] BETWEEN 0 AND [MaximumAttempts] AND ([ConsumedAt] IS NULL OR [FailedAttempts] < [MaximumAttempts])");
                    table.CheckConstraint("CK_MobileOtpChallenges_Dates", "[ExpiresAt] > [IssuedAt] AND ([ConsumedAt] IS NULL OR ([ConsumedAt] >= [IssuedAt] AND [ConsumedAt] < [ExpiresAt])) AND ([RevokedAt] IS NULL OR ([RevokedAt] >= [IssuedAt] AND ([ConsumedAt] IS NULL OR [RevokedAt] >= [ConsumedAt])))");
                    table.CheckConstraint("CK_MobileOtpChallenges_Purpose", "[Purpose] = 'MobileLogin'");
                    table.ForeignKey(
                        name: "FK_MobileOtpChallenges_LoginPhoneBindings_UserAccountId_PhoneBindingId",
                        columns: x => new { x.UserAccountId, x.PhoneBindingId },
                        principalSchema: "auth",
                        principalTable: "LoginPhoneBindings",
                        principalColumns: new[] { "UserAccountId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OtpDeliveries",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChallengeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProtectedPayload = table.Column<string>(type: "varchar(8000)", unicode: false, maxLength: 8000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ClaimedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtpDeliveries", x => x.Id);
                    table.CheckConstraint("CK_OtpDeliveries_Claim", "([Attempts] = 0 AND [ClaimedAt] IS NULL AND [LeaseUntil] IS NULL) OR ([Attempts] = 1 AND [ClaimedAt] IS NOT NULL AND [LeaseUntil] IS NOT NULL AND [ClaimedAt] >= [CreatedAt] AND [ClaimedAt] < [ExpiresAt] AND [LeaseUntil] > [ClaimedAt] AND [LeaseUntil] <= [ExpiresAt])");
                    table.CheckConstraint("CK_OtpDeliveries_Lifetime", "[ExpiresAt] > [CreatedAt]");
                    table.CheckConstraint("CK_OtpDeliveries_Payload", "([Status] IN (0,4) AND [ProtectedPayload] IS NOT NULL AND DATALENGTH([ProtectedPayload]) > 0) OR ([Status] IN (1,2,3) AND [ProtectedPayload] IS NULL)");
                    table.CheckConstraint("CK_OtpDeliveries_Status", "[Status] IN (0,1,2,3,4) AND ([Status] <> 0 OR [Attempts] = 0) AND ([Status] NOT IN (1,4) OR [Attempts] = 1)");
                    table.CheckConstraint("CK_OtpDeliveries_Submitted", "([Status] = 1 AND [SubmittedAt] IS NOT NULL AND [ClaimedAt] IS NOT NULL AND [LeaseUntil] IS NOT NULL AND [SubmittedAt] >= [ClaimedAt] AND [SubmittedAt] < [LeaseUntil] AND [SubmittedAt] < [ExpiresAt]) OR ([Status] <> 1 AND [SubmittedAt] IS NULL)");
                    table.ForeignKey(
                        name: "FK_OtpDeliveries_MobileOtpChallenges_ChallengeId",
                        column: x => x.ChallengeId,
                        principalSchema: "auth",
                        principalTable: "MobileOtpChallenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserAccounts_Active",
                schema: "auth",
                table: "UserAccounts",
                sql: "[Status] <> 3 OR (([PasswordHash] IS NOT NULL OR [PhoneVerifiedAt] IS NOT NULL) AND [ActivatedAt] IS NOT NULL AND [InitialRoleAssignmentId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserAccounts_PhoneVerified",
                schema: "auth",
                table: "UserAccounts",
                sql: "[PhoneVerifiedAt] IS NULL OR ([ActivatedAt] IS NOT NULL AND [PhoneVerifiedAt] >= [CreatedAt])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserAccounts_WebAccess",
                schema: "auth",
                table: "UserAccounts",
                sql: "[WebAccessEnabled] = 0 OR [PasswordHash] IS NOT NULL");

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

            migrationBuilder.CreateIndex(
                name: "IX_AuthRateBuckets_WindowEndsAt",
                schema: "auth",
                table: "AuthRateBuckets",
                column: "WindowEndsAt");

            migrationBuilder.CreateIndex(
                name: "IX_LoginPhoneBindings_ApprovedSubmissionId",
                schema: "auth",
                table: "LoginPhoneBindings",
                column: "ApprovedSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_LoginPhoneBindings_PhoneNumber",
                schema: "auth",
                table: "LoginPhoneBindings",
                column: "PhoneNumber",
                unique: true,
                filter: "[RevokedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LoginPhoneBindings_UserAccountId",
                schema: "auth",
                table: "LoginPhoneBindings",
                column: "UserAccountId",
                unique: true,
                filter: "[RevokedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MobileOtpChallenges_PhoneBindingId_ExpiresAt",
                schema: "auth",
                table: "MobileOtpChallenges",
                columns: new[] { "PhoneBindingId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MobileOtpChallenges_UserAccountId_IssuedAt",
                schema: "auth",
                table: "MobileOtpChallenges",
                columns: new[] { "UserAccountId", "IssuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MobileOtpChallenges_UserAccountId_PhoneBindingId",
                schema: "auth",
                table: "MobileOtpChallenges",
                columns: new[] { "UserAccountId", "PhoneBindingId" });

            migrationBuilder.CreateIndex(
                name: "IX_OtpDeliveries_ChallengeId",
                schema: "auth",
                table: "OtpDeliveries",
                column: "ChallengeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OtpDeliveries_Status_ExpiresAt_CreatedAt",
                schema: "auth",
                table: "OtpDeliveries",
                columns: new[] { "Status", "ExpiresAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OtpDeliveries_Status_LeaseUntil",
                schema: "auth",
                table: "OtpDeliveries",
                columns: new[] { "Status", "LeaseUntil" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthRateBuckets",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "OtpDeliveries",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "MobileOtpChallenges",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "LoginPhoneBindings",
                schema: "auth");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserAccounts_Active",
                schema: "auth",
                table: "UserAccounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserAccounts_PhoneVerified",
                schema: "auth",
                table: "UserAccounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserAccounts_WebAccess",
                schema: "auth",
                table: "UserAccounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeSubmissions_MobileEnrollment",
                schema: "hr",
                table: "EmployeeSubmissions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Employees_MobileEnrollment",
                schema: "hr",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "MobileAccessEnabled",
                schema: "auth",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "PhoneVerifiedAt",
                schema: "auth",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "WebAccessEnabled",
                schema: "auth",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "ApprovedLoginPhoneNumber",
                schema: "hr",
                table: "EmployeeSubmissions");

            migrationBuilder.DropColumn(
                name: "MobileAccessRequested",
                schema: "hr",
                table: "EmployeeSubmissions");

            migrationBuilder.DropColumn(
                name: "ApprovedLoginPhoneNumber",
                schema: "hr",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "MobileAccessRequested",
                schema: "hr",
                table: "Employees");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserAccounts_Active",
                schema: "auth",
                table: "UserAccounts",
                sql: "[Status] <> 3 OR ([PasswordHash] IS NOT NULL AND [ActivatedAt] IS NOT NULL AND [InitialRoleAssignmentId] IS NOT NULL)");
        }
    }
}
