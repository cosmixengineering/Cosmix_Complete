using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountRecoveryAndOwnerInitialization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeOnboardingDecisions_EmployeeSubmissions_EmployeeId_SubmissionId",
                schema: "hr",
                table: "EmployeeOnboardingDecisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MobileOtpChallenges_Purpose",
                schema: "auth",
                table: "MobileOtpChallenges");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeOnboardingDecisions_EmployeeId_SubmissionId",
                schema: "hr",
                table: "EmployeeOnboardingDecisions");

            migrationBuilder.AddColumn<Guid>(
                name: "LoginPhoneChangeId",
                schema: "auth",
                table: "LoginPhoneBindings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "SubmittedByUserId",
                schema: "hr",
                table: "EmployeeSubmissions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "ExternalSubmitterReference",
                schema: "hr",
                table: "EmployeeSubmissions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Provenance",
                schema: "hr",
                table: "EmployeeSubmissions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "ActorUserId",
                schema: "hr",
                table: "EmployeeOnboardingDecisions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "ActorRoleId",
                schema: "hr",
                table: "EmployeeOnboardingDecisions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "ExternalApproverReference",
                schema: "hr",
                table: "EmployeeOnboardingDecisions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerApprovalReference",
                schema: "hr",
                table: "EmployeeOnboardingDecisions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Provenance",
                schema: "hr",
                table: "EmployeeOnboardingDecisions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_EmployeeSubmissions_EmployeeId_Id_Provenance",
                schema: "hr",
                table: "EmployeeSubmissions",
                columns: new[] { "EmployeeId", "Id", "Provenance" });

            migrationBuilder.CreateTable(
                name: "LoginPhoneChanges",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousBindingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewPhoneNumber = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedByAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedBySystemRole = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ApprovalReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SecurityVersion = table.Column<int>(type: "int", nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletionChallengeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupersededAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginPhoneChanges", x => x.Id);
                    table.UniqueConstraint("AK_LoginPhoneChanges_UserAccountId_Id", x => new { x.UserAccountId, x.Id });
                    table.CheckConstraint("CK_LoginPhoneChanges_Approval", "[ApprovedByUserId] <> [UserAccountId] AND [ApprovedBySystemRole] IN (1,2) AND [SecurityVersion] > 1 AND [ExpiresAt] > [ApprovedAt] AND [ExpiresAt] <= DATEADD(hour,24,[ApprovedAt])");
                    table.CheckConstraint("CK_LoginPhoneChanges_Completion", "([CompletedAt] IS NULL AND [CompletionChallengeId] IS NULL) OR ([CompletedAt] IS NOT NULL AND [CompletionChallengeId] IS NOT NULL AND [CompletedAt] >= [ApprovedAt] AND [CompletedAt] < [ExpiresAt] AND [SupersededAt] IS NULL)");
                    table.CheckConstraint("CK_LoginPhoneChanges_Phone", "DATALENGTH([NewPhoneNumber]) BETWEEN 3 AND 16 AND LEFT([NewPhoneNumber],1)='+' AND SUBSTRING([NewPhoneNumber],2,1) BETWEEN '1' AND '9' AND SUBSTRING([NewPhoneNumber],2,15) NOT LIKE '%[^0-9]%'");
                    table.CheckConstraint("CK_LoginPhoneChanges_Superseded", "[SupersededAt] IS NULL OR ([SupersededAt] >= [ApprovedAt] AND [CompletedAt] IS NULL)");
                    table.ForeignKey(
                        name: "FK_LoginPhoneChanges_LoginPhoneBindings_UserAccountId_PreviousBindingId",
                        columns: x => new { x.UserAccountId, x.PreviousBindingId },
                        principalSchema: "auth",
                        principalTable: "LoginPhoneBindings",
                        principalColumns: new[] { "UserAccountId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoginPhoneChanges_MobileOtpChallenges_UserAccountId_CompletionChallengeId",
                        columns: x => new { x.UserAccountId, x.CompletionChallengeId },
                        principalSchema: "auth",
                        principalTable: "MobileOtpChallenges",
                        principalColumns: new[] { "UserAccountId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoginPhoneChanges_UserRoleAssignments_ApprovedByUserId_ApprovedByAssignmentId",
                        columns: x => new { x.ApprovedByUserId, x.ApprovedByAssignmentId },
                        principalSchema: "auth",
                        principalTable: "UserRoleAssignments",
                        principalColumns: new[] { "UserAccountId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_MobileOtpChallenges_Purpose",
                schema: "auth",
                table: "MobileOtpChallenges",
                sql: "([Purpose] = 'MobileLogin' AND [Platform] IN ('android','ios')) OR ([Purpose] IN ('WebLogin','WebPasswordReset') AND [Platform] = 'web') OR ([Purpose] = 'PhoneRecovery' AND [Platform] IN ('web','android','ios'))");

            migrationBuilder.CreateIndex(
                name: "IX_LoginPhoneBindings_LoginPhoneChangeId",
                schema: "auth",
                table: "LoginPhoneBindings",
                column: "LoginPhoneChangeId",
                unique: true,
                filter: "[LoginPhoneChangeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LoginPhoneBindings_UserAccountId_LoginPhoneChangeId",
                schema: "auth",
                table: "LoginPhoneBindings",
                columns: new[] { "UserAccountId", "LoginPhoneChangeId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSubmissions_Provenance",
                schema: "hr",
                table: "EmployeeSubmissions",
                column: "Provenance",
                unique: true,
                filter: "[Provenance] = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeSubmissions_Provenance",
                schema: "hr",
                table: "EmployeeSubmissions",
                sql: "([Provenance] = 0 AND [SubmittedByUserId] IS NOT NULL AND [SubmittedByUserId] <> '00000000-0000-0000-0000-000000000000' AND [ExternalSubmitterReference] IS NULL) OR ([Provenance] = 1 AND [SubmittedByUserId] IS NULL AND [ExternalSubmitterReference] IS NOT NULL AND LEN(LTRIM(RTRIM([ExternalSubmitterReference]))) > 0 AND [Version] = 1 AND [MobileAccessRequested] = 0 AND [ApprovedLoginPhoneNumber] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeOnboardingDecisions_EmployeeId_SubmissionId_Provenance",
                schema: "hr",
                table: "EmployeeOnboardingDecisions",
                columns: new[] { "EmployeeId", "SubmissionId", "Provenance" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeOnboardingDecisions_Provenance",
                schema: "hr",
                table: "EmployeeOnboardingDecisions",
                sql: "([Provenance] = 0 AND [ActorUserId] IS NOT NULL AND [ActorUserId] <> '00000000-0000-0000-0000-000000000000' AND [ActorRoleId] IS NOT NULL AND [ActorRoleId] <> '00000000-0000-0000-0000-000000000000' AND [ExternalApproverReference] IS NULL AND [OwnerApprovalReference] IS NULL) OR ([Provenance] = 1 AND [Decision] = 1 AND [ActorUserId] IS NULL AND [ActorRoleId] IS NULL AND [ExternalApproverReference] IS NOT NULL AND LEN(LTRIM(RTRIM([ExternalApproverReference]))) > 0 AND [OwnerApprovalReference] IS NOT NULL AND LEN(LTRIM(RTRIM([OwnerApprovalReference]))) > 0)");

            migrationBuilder.CreateIndex(
                name: "IX_LoginPhoneChanges_ApprovedByUserId_ApprovedByAssignmentId",
                schema: "auth",
                table: "LoginPhoneChanges",
                columns: new[] { "ApprovedByUserId", "ApprovedByAssignmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_LoginPhoneChanges_CompletionChallengeId",
                schema: "auth",
                table: "LoginPhoneChanges",
                column: "CompletionChallengeId",
                unique: true,
                filter: "[CompletionChallengeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LoginPhoneChanges_PreviousBindingId",
                schema: "auth",
                table: "LoginPhoneChanges",
                column: "PreviousBindingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoginPhoneChanges_UserAccountId_CompletionChallengeId",
                schema: "auth",
                table: "LoginPhoneChanges",
                columns: new[] { "UserAccountId", "CompletionChallengeId" });

            migrationBuilder.CreateIndex(
                name: "IX_LoginPhoneChanges_UserAccountId_PreviousBindingId",
                schema: "auth",
                table: "LoginPhoneChanges",
                columns: new[] { "UserAccountId", "PreviousBindingId" });

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeOnboardingDecisions_EmployeeSubmissions_EmployeeId_SubmissionId_Provenance",
                schema: "hr",
                table: "EmployeeOnboardingDecisions",
                columns: new[] { "EmployeeId", "SubmissionId", "Provenance" },
                principalSchema: "hr",
                principalTable: "EmployeeSubmissions",
                principalColumns: new[] { "EmployeeId", "Id", "Provenance" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LoginPhoneBindings_LoginPhoneChanges_UserAccountId_LoginPhoneChangeId",
                schema: "auth",
                table: "LoginPhoneBindings",
                columns: new[] { "UserAccountId", "LoginPhoneChangeId" },
                principalSchema: "auth",
                principalTable: "LoginPhoneChanges",
                principalColumns: new[] { "UserAccountId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Owner/recovery provenance cannot be truthfully represented by the previous schema.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [auth].[LoginPhoneChanges])
                   OR EXISTS (SELECT 1 FROM [hr].[EmployeeSubmissions] WHERE [Provenance] = 1)
                   OR EXISTS (SELECT 1 FROM [hr].[EmployeeOnboardingDecisions] WHERE [Provenance] = 1)
                   OR EXISTS (SELECT 1 FROM [auth].[MobileOtpChallenges] WHERE [Purpose] IN ('PhoneRecovery','WebPasswordReset'))
                    THROW 51002, 'Recovery/owner evidence exists. Use a reviewed forward repair or backup restoration, not downgrade.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeOnboardingDecisions_EmployeeSubmissions_EmployeeId_SubmissionId_Provenance",
                schema: "hr",
                table: "EmployeeOnboardingDecisions");

            migrationBuilder.DropForeignKey(
                name: "FK_LoginPhoneBindings_LoginPhoneChanges_UserAccountId_LoginPhoneChangeId",
                schema: "auth",
                table: "LoginPhoneBindings");

            migrationBuilder.DropTable(
                name: "LoginPhoneChanges",
                schema: "auth");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MobileOtpChallenges_Purpose",
                schema: "auth",
                table: "MobileOtpChallenges");

            migrationBuilder.DropIndex(
                name: "IX_LoginPhoneBindings_LoginPhoneChangeId",
                schema: "auth",
                table: "LoginPhoneBindings");

            migrationBuilder.DropIndex(
                name: "IX_LoginPhoneBindings_UserAccountId_LoginPhoneChangeId",
                schema: "auth",
                table: "LoginPhoneBindings");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_EmployeeSubmissions_EmployeeId_Id_Provenance",
                schema: "hr",
                table: "EmployeeSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeSubmissions_Provenance",
                schema: "hr",
                table: "EmployeeSubmissions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeSubmissions_Provenance",
                schema: "hr",
                table: "EmployeeSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeOnboardingDecisions_EmployeeId_SubmissionId_Provenance",
                schema: "hr",
                table: "EmployeeOnboardingDecisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeOnboardingDecisions_Provenance",
                schema: "hr",
                table: "EmployeeOnboardingDecisions");

            migrationBuilder.DropColumn(
                name: "LoginPhoneChangeId",
                schema: "auth",
                table: "LoginPhoneBindings");

            migrationBuilder.DropColumn(
                name: "ExternalSubmitterReference",
                schema: "hr",
                table: "EmployeeSubmissions");

            migrationBuilder.DropColumn(
                name: "Provenance",
                schema: "hr",
                table: "EmployeeSubmissions");

            migrationBuilder.DropColumn(
                name: "ExternalApproverReference",
                schema: "hr",
                table: "EmployeeOnboardingDecisions");

            migrationBuilder.DropColumn(
                name: "OwnerApprovalReference",
                schema: "hr",
                table: "EmployeeOnboardingDecisions");

            migrationBuilder.DropColumn(
                name: "Provenance",
                schema: "hr",
                table: "EmployeeOnboardingDecisions");

            migrationBuilder.AlterColumn<Guid>(
                name: "SubmittedByUserId",
                schema: "hr",
                table: "EmployeeSubmissions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ActorUserId",
                schema: "hr",
                table: "EmployeeOnboardingDecisions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ActorRoleId",
                schema: "hr",
                table: "EmployeeOnboardingDecisions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MobileOtpChallenges_Purpose",
                schema: "auth",
                table: "MobileOtpChallenges",
                sql: "([Purpose] = 'MobileLogin' AND [Platform] IN ('android','ios')) OR ([Purpose] = 'WebLogin' AND [Platform] = 'web')");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeOnboardingDecisions_EmployeeId_SubmissionId",
                schema: "hr",
                table: "EmployeeOnboardingDecisions",
                columns: new[] { "EmployeeId", "SubmissionId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeOnboardingDecisions_EmployeeSubmissions_EmployeeId_SubmissionId",
                schema: "hr",
                table: "EmployeeOnboardingDecisions",
                columns: new[] { "EmployeeId", "SubmissionId" },
                principalSchema: "hr",
                principalTable: "EmployeeSubmissions",
                principalColumns: new[] { "EmployeeId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
