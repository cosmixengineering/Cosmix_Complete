using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddComplianceWorkstreams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeStatutoryRegistrations",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scheme = table.Column<int>(type: "int", nullable: false),
                    RegistrationNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RegisteredAt = table.Column<DateOnly>(type: "date", nullable: false),
                    RecordedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeStatutoryRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeStatutoryRegistrations_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "hr",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LocationNoticePolicies",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    NoticeText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PublishedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationNoticePolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayrollObligations",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TotalGrossAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalNetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    FinalizedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FinalizedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollObligations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollObligations_PayrollHandoffBatches_BatchId",
                        column: x => x.BatchId,
                        principalSchema: "hr",
                        principalTable: "PayrollHandoffBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PunchLocationRedactions",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PunchEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RedactedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RetentionPolicyDays = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PunchLocationRedactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PunchLocationRedactions_RawPunchEvents_PunchEventId",
                        column: x => x.PunchEventId,
                        principalSchema: "attendance",
                        principalTable: "RawPunchEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StatutoryContributions",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scheme = table.Column<int>(type: "int", nullable: false),
                    ContributionMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    EmployeeContributionAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EmployerContributionAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    PayableAccountCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EmployerRegistrationNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PostedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PostedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatutoryContributions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeLocationConsents",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoticePolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoticePolicyVersion = table.Column<int>(type: "int", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeviceMetadata = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeLocationConsents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeLocationConsents_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "hr",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeLocationConsents_LocationNoticePolicies_NoticePolicyId",
                        column: x => x.NoticePolicyId,
                        principalSchema: "attendance",
                        principalTable: "LocationNoticePolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollJournalEntries",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObligationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    DebitAccountCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreditAccountCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PostedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollJournalEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollJournalEntries_PayrollObligations_ObligationId",
                        column: x => x.ObligationId,
                        principalSchema: "hr",
                        principalTable: "PayrollObligations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalaryPayments",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObligationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccrualJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    PaymentReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PaymentJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ConfirmedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalaryPayments_PayrollJournalEntries_AccrualJournalEntryId",
                        column: x => x.AccrualJournalEntryId,
                        principalSchema: "hr",
                        principalTable: "PayrollJournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalaryPayments_PayrollJournalEntries_PaymentJournalEntryId",
                        column: x => x.PaymentJournalEntryId,
                        principalSchema: "hr",
                        principalTable: "PayrollJournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalaryPayments_PayrollObligations_ObligationId",
                        column: x => x.ObligationId,
                        principalSchema: "hr",
                        principalTable: "PayrollObligations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLocationConsents_EmployeeId_NoticePolicyVersion",
                schema: "attendance",
                table: "EmployeeLocationConsents",
                columns: new[] { "EmployeeId", "NoticePolicyVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLocationConsents_NoticePolicyId",
                schema: "attendance",
                table: "EmployeeLocationConsents",
                column: "NoticePolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeStatutoryRegistrations_EmployeeId_Scheme",
                schema: "hr",
                table: "EmployeeStatutoryRegistrations",
                columns: new[] { "EmployeeId", "Scheme" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LocationNoticePolicies_IsActive",
                schema: "attendance",
                table: "LocationNoticePolicies",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_LocationNoticePolicies_Version",
                schema: "attendance",
                table: "LocationNoticePolicies",
                column: "Version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollJournalEntries_Kind",
                schema: "hr",
                table: "PayrollJournalEntries",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollJournalEntries_ObligationId",
                schema: "hr",
                table: "PayrollJournalEntries",
                column: "ObligationId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollObligations_BatchId",
                schema: "hr",
                table: "PayrollObligations",
                column: "BatchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollObligations_State",
                schema: "hr",
                table: "PayrollObligations",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_PunchLocationRedactions_PunchEventId",
                schema: "attendance",
                table: "PunchLocationRedactions",
                column: "PunchEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PunchLocationRedactions_RedactedAtUtc",
                schema: "attendance",
                table: "PunchLocationRedactions",
                column: "RedactedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryPayments_AccrualJournalEntryId",
                schema: "hr",
                table: "SalaryPayments",
                column: "AccrualJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryPayments_ObligationId",
                schema: "hr",
                table: "SalaryPayments",
                column: "ObligationId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryPayments_PaymentJournalEntryId",
                schema: "hr",
                table: "SalaryPayments",
                column: "PaymentJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryPayments_State",
                schema: "hr",
                table: "SalaryPayments",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryContributions_Scheme_ContributionMonth",
                schema: "hr",
                table: "StatutoryContributions",
                columns: new[] { "Scheme", "ContributionMonth" });

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryContributions_State",
                schema: "hr",
                table: "StatutoryContributions",
                column: "State");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeLocationConsents",
                schema: "attendance");

            migrationBuilder.DropTable(
                name: "EmployeeStatutoryRegistrations",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "PunchLocationRedactions",
                schema: "attendance");

            migrationBuilder.DropTable(
                name: "SalaryPayments",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "StatutoryContributions",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "LocationNoticePolicies",
                schema: "attendance");

            migrationBuilder.DropTable(
                name: "PayrollJournalEntries",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "PayrollObligations",
                schema: "hr");
        }
    }
}
