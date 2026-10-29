using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class CompleteSalaryRunAndStatutoryEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PayrollJournalEntries_SourceKey",
                schema: "hr",
                table: "PayrollJournalEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollHandoffLines_1",
                schema: "hr",
                table: "PayrollHandoffLines");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "hr",
                table: "StatutoryContributions",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "CalculationPrivateReference",
                schema: "hr",
                table: "PayrollHandoffBatches",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CalculationVersion",
                schema: "hr",
                table: "PayrollHandoffBatches",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "adjustments-v1");

            migrationBuilder.AddColumn<string>(
                name: "FrozenFingerprint",
                schema: "hr",
                table: "PayrollHandoffBatches",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HrApprovalRequestId",
                schema: "hr",
                table: "PayrollHandoffBatches",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "HrApprovedAt",
                schema: "hr",
                table: "PayrollHandoffBatches",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HrApprovedByUserId",
                schema: "hr",
                table: "PayrollHandoffBatches",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviewFingerprint",
                schema: "hr",
                table: "PayrollHandoffBatches",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewState",
                schema: "hr",
                table: "PayrollHandoffBatches",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "prepared");

            migrationBuilder.CreateTable(
                name: "PayrollSalaryComponents",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveToExclusive = table.Column<DateOnly>(type: "date", nullable: true),
                    ProrateWithBase = table.Column<bool>(type: "bit", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    SupersedesId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceImportId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollSalaryComponents", x => x.Id);
                    table.CheckConstraint("CK_PayrollSalaryComponents_Amounts", "[Amount] >= 0 AND [Revision] > 0 AND [Kind] BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_PayrollSalaryComponents_Dates", "[EffectiveToExclusive] IS NULL OR [EffectiveToExclusive] > [EffectiveFrom]");
                    table.ForeignKey(
                        name: "FK_PayrollSalaryComponents_ApprovalRequests_ApprovalRequestId",
                        column: x => x.ApprovalRequestId,
                        principalSchema: "workflow",
                        principalTable: "ApprovalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollSalaryComponents_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "hr",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollSalaryImports",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SourceSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourcePrivateReference = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    UploadedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AppliedFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AppliedPrivateReference = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SubmittedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollSalaryImports", x => x.Id);
                    table.CheckConstraint("CK_PayrollSalaryImports_Submission", "([SubmittedAt] IS NULL AND [SubmittedByUserId] IS NULL AND [AppliedFingerprint] IS NULL AND [AppliedPrivateReference] IS NULL) OR ([SubmittedAt] >= [UploadedAt] AND [SubmittedByUserId] IS NOT NULL AND [AppliedFingerprint] IS NOT NULL AND [AppliedPrivateReference] IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "PayrollSalaryPolicies",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    RulesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: false),
                    SupersedesId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollSalaryPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollSalaryPolicies_ApprovalRequests_ApprovalRequestId",
                        column: x => x.ApprovalRequestId,
                        principalSchema: "workflow",
                        principalTable: "ApprovalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StatutoryContributionEntries",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContributionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegistrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegistrationNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EmployeeContributionAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EmployerContributionAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    EvidenceSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PrivateProofReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatutoryContributionEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatutoryContributionEntries_EmployeeStatutoryRegistrations_RegistrationId",
                        column: x => x.RegistrationId,
                        principalSchema: "hr",
                        principalTable: "EmployeeStatutoryRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StatutoryContributionEntries_StatutoryContributions_ContributionId",
                        column: x => x.ContributionId,
                        principalSchema: "hr",
                        principalTable: "StatutoryContributions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollJournalEntries_SourceKey",
                schema: "hr",
                table: "PayrollJournalEntries",
                column: "SourceKey",
                unique: true,
                filter: "[SourceKey] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollHandoffLines_1",
                schema: "hr",
                table: "PayrollHandoffLines",
                sql: "[Component] BETWEEN 1 AND 12");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollHandoffBatches_HrApprovalRequestId",
                schema: "hr",
                table: "PayrollHandoffBatches",
                column: "HrApprovalRequestId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollHandoffBatches_SalaryApproval",
                schema: "hr",
                table: "PayrollHandoffBatches",
                sql: "[CalculationVersion] <> 'salary-v2' OR ([ReviewState] IN ('prepared','cancelled') AND [HrApprovalRequestId] IS NULL AND [HrApprovedAt] IS NULL AND [HrApprovedByUserId] IS NULL AND [LockedAt] IS NULL) OR ([ReviewState] IN ('hr_approved','rejected','locked','finance_handed_off') AND [HrApprovalRequestId] IS NOT NULL AND [HrApprovedAt] IS NOT NULL AND [HrApprovedAt] >= [PreparedAt] AND [HrApprovedByUserId] IS NOT NULL AND [HrApprovedByUserId] <> [PreparedByUserId] AND (([ReviewState] IN ('hr_approved','rejected') AND [LockedAt] IS NULL) OR ([ReviewState] IN ('locked','finance_handed_off') AND [LockedAt] IS NOT NULL AND [LockedAt] >= [HrApprovedAt] AND [LockedByUserId] <> [PreparedByUserId] AND [LockedByUserId] <> [HrApprovedByUserId])))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollHandoffBatches_SalaryEvidence",
                schema: "hr",
                table: "PayrollHandoffBatches",
                sql: "[CalculationVersion] <> 'salary-v2' OR ([PreviewFingerprint] IS NOT NULL AND LEN([PreviewFingerprint]) = 64 AND [FrozenFingerprint] IS NOT NULL AND LEN([FrozenFingerprint]) = 64 AND [CalculationPrivateReference] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollHandoffBatches_SalaryState",
                schema: "hr",
                table: "PayrollHandoffBatches",
                sql: "[ReviewState] IN ('prepared','hr_approved','rejected','cancelled','locked','finance_handed_off')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollHandoffBatches_SalaryVersion",
                schema: "hr",
                table: "PayrollHandoffBatches",
                sql: "[CalculationVersion] IN ('adjustments-v1','salary-v2')");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollSalaryComponents_ApprovalRequestId",
                schema: "hr",
                table: "PayrollSalaryComponents",
                column: "ApprovalRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollSalaryComponents_EmployeeId_Code_Revision",
                schema: "hr",
                table: "PayrollSalaryComponents",
                columns: new[] { "EmployeeId", "Code", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollSalaryImports_SourceSha256",
                schema: "hr",
                table: "PayrollSalaryImports",
                column: "SourceSha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollSalaryImports_UploadedAt",
                schema: "hr",
                table: "PayrollSalaryImports",
                column: "UploadedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollSalaryPolicies_ApprovalRequestId",
                schema: "hr",
                table: "PayrollSalaryPolicies",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollSalaryPolicies_Revision",
                schema: "hr",
                table: "PayrollSalaryPolicies",
                column: "Revision",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryContributionEntries_ContributionId_EmployeeId",
                schema: "hr",
                table: "StatutoryContributionEntries",
                columns: new[] { "ContributionId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryContributionEntries_EmployeeId",
                schema: "hr",
                table: "StatutoryContributionEntries",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryContributionEntries_RegistrationId",
                schema: "hr",
                table: "StatutoryContributionEntries",
                column: "RegistrationId");

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollHandoffBatches_ApprovalRequests_HrApprovalRequestId",
                schema: "hr",
                table: "PayrollHandoffBatches",
                column: "HrApprovalRequestId",
                principalSchema: "workflow",
                principalTable: "ApprovalRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PayrollHandoffBatches_ApprovalRequests_HrApprovalRequestId",
                schema: "hr",
                table: "PayrollHandoffBatches");

            migrationBuilder.DropTable(
                name: "PayrollSalaryComponents",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "PayrollSalaryImports",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "PayrollSalaryPolicies",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "StatutoryContributionEntries",
                schema: "hr");

            migrationBuilder.DropIndex(
                name: "IX_PayrollJournalEntries_SourceKey",
                schema: "hr",
                table: "PayrollJournalEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollHandoffLines_1",
                schema: "hr",
                table: "PayrollHandoffLines");

            migrationBuilder.DropIndex(
                name: "IX_PayrollHandoffBatches_HrApprovalRequestId",
                schema: "hr",
                table: "PayrollHandoffBatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollHandoffBatches_SalaryApproval",
                schema: "hr",
                table: "PayrollHandoffBatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollHandoffBatches_SalaryEvidence",
                schema: "hr",
                table: "PayrollHandoffBatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollHandoffBatches_SalaryState",
                schema: "hr",
                table: "PayrollHandoffBatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollHandoffBatches_SalaryVersion",
                schema: "hr",
                table: "PayrollHandoffBatches");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "hr",
                table: "StatutoryContributions");

            migrationBuilder.DropColumn(
                name: "CalculationPrivateReference",
                schema: "hr",
                table: "PayrollHandoffBatches");

            migrationBuilder.DropColumn(
                name: "CalculationVersion",
                schema: "hr",
                table: "PayrollHandoffBatches");

            migrationBuilder.DropColumn(
                name: "FrozenFingerprint",
                schema: "hr",
                table: "PayrollHandoffBatches");

            migrationBuilder.DropColumn(
                name: "HrApprovalRequestId",
                schema: "hr",
                table: "PayrollHandoffBatches");

            migrationBuilder.DropColumn(
                name: "HrApprovedAt",
                schema: "hr",
                table: "PayrollHandoffBatches");

            migrationBuilder.DropColumn(
                name: "HrApprovedByUserId",
                schema: "hr",
                table: "PayrollHandoffBatches");

            migrationBuilder.DropColumn(
                name: "PreviewFingerprint",
                schema: "hr",
                table: "PayrollHandoffBatches");

            migrationBuilder.DropColumn(
                name: "ReviewState",
                schema: "hr",
                table: "PayrollHandoffBatches");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollJournalEntries_SourceKey",
                schema: "hr",
                table: "PayrollJournalEntries",
                column: "SourceKey");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollHandoffLines_1",
                schema: "hr",
                table: "PayrollHandoffLines",
                sql: "[Component] BETWEEN 1 AND 8");
        }
    }
}
