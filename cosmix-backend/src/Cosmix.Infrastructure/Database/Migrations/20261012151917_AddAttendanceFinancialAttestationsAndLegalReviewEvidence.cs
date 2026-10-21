using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceFinancialAttestationsAndLegalReviewEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_PolicyVersions_PolicyId_Id",
                schema: "hr",
                table: "PolicyVersions",
                columns: new[] { "PolicyId", "Id" });

            migrationBuilder.CreateTable(
                name: "AttendanceLegalReviewDossiers",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourcePolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourcePolicyVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicySha256 = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    BindingSha256 = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    SupersedesId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    RecordedByAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    ProtectedEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EvidenceSha256 = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    State = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    VerificationMode = table.Column<string>(type: "varchar(48)", unicode: false, maxLength: 48, nullable: false, collation: "Latin1_General_100_BIN2")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceLegalReviewDossiers", x => x.Id);
                    table.UniqueConstraint("AK_AttendanceLegalReviewDossiers_SourcePolicyVersionId_PolicySha256_BindingSha256_Id", x => new { x.SourcePolicyVersionId, x.PolicySha256, x.BindingSha256, x.Id });
                    table.CheckConstraint("CK_AttendanceLegalReviewDossiers_BindingSha256", "LEN([BindingSha256]) = 64 AND [BindingSha256] NOT LIKE '%[^0-9A-F]%'");
                    table.CheckConstraint("CK_AttendanceLegalReviewDossiers_Dates", "YEAR([EffectiveFrom]) BETWEEN 1901 AND 9997 AND ([EffectiveTo] IS NULL OR (YEAR([EffectiveTo]) BETWEEN 1901 AND 9997 AND [EffectiveTo] > [EffectiveFrom]))");
                    table.CheckConstraint("CK_AttendanceLegalReviewDossiers_EvidenceSha256", "LEN([EvidenceSha256]) = 64 AND [EvidenceSha256] NOT LIKE '%[^0-9A-F]%'");
                    table.CheckConstraint("CK_AttendanceLegalReviewDossiers_PolicySha256", "LEN([PolicySha256]) = 64 AND [PolicySha256] NOT LIKE '%[^0-9A-F]%'");
                    table.CheckConstraint("CK_AttendanceLegalReviewDossiers_Predecessor", "[SupersedesId] IS NULL OR [SupersedesId] <> [Id]");
                    table.CheckConstraint("CK_AttendanceLegalReviewDossiers_RecordedAtUtc_Utc", "[RecordedAtUtc] IS NULL OR DATEPART(TZOFFSET, [RecordedAtUtc]) = 0");
                    table.CheckConstraint("CK_AttendanceLegalReviewDossiers_RecordOnly", "[State] = 'review_required' AND [VerificationMode] = 'recorded_not_legally_verified'");
                    table.CheckConstraint("CK_AttendanceLegalReviewDossiers_Revision", "[Revision] BETWEEN 1 AND 128 AND (([Revision] = 1 AND [SupersedesId] IS NULL) OR ([Revision] > 1 AND [SupersedesId] IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_AttendanceLegalReviewDossiers_AttendanceLegalReviewDossiers_SourcePolicyVersionId_PolicySha256_BindingSha256_SupersedesId",
                        columns: x => new { x.SourcePolicyVersionId, x.PolicySha256, x.BindingSha256, x.SupersedesId },
                        principalSchema: "attendance",
                        principalTable: "AttendanceLegalReviewDossiers",
                        principalColumns: new[] { "SourcePolicyVersionId", "PolicySha256", "BindingSha256", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceLegalReviewDossiers_PolicyVersions_SourcePolicyId_SourcePolicyVersionId",
                        columns: x => new { x.SourcePolicyId, x.SourcePolicyVersionId },
                        principalSchema: "hr",
                        principalTable: "PolicyVersions",
                        principalColumns: new[] { "PolicyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceLegalReviewDossiers_UserAccounts_RecordedByAccountId",
                        column: x => x.RecordedByAccountId,
                        principalSchema: "auth",
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendancePayrollAttestations",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    SourceRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceRevision = table.Column<int>(type: "int", nullable: false),
                    SourceFingerprint = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    PreviousAttestationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Step = table.Column<int>(type: "int", nullable: false),
                    LineFingerprint = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ExternalReceiptKey = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    PostedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    ProtectedEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EvidenceFingerprint = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendancePayrollAttestations", x => x.Id);
                    table.UniqueConstraint("AK_AttendancePayrollAttestations_EmployeeId_PeriodStart_SourceRevisionId_Id", x => new { x.EmployeeId, x.PeriodStart, x.SourceRevisionId, x.Id });
                    table.CheckConstraint("CK_AttendancePayrollAttestations_CreatedAtUtc_Utc", "[CreatedAtUtc] IS NULL OR DATEPART(TZOFFSET, [CreatedAtUtc]) = 0");
                    table.CheckConstraint("CK_AttendancePayrollAttestations_EvidenceFingerprint", "LEN([EvidenceFingerprint]) = 64 AND [EvidenceFingerprint] NOT LIKE '%[^0-9A-F]%'");
                    table.CheckConstraint("CK_AttendancePayrollAttestations_ExternalReceiptKey", "LEN([ExternalReceiptKey]) = 64 AND [ExternalReceiptKey] NOT LIKE '%[^0-9A-F]%'");
                    table.CheckConstraint("CK_AttendancePayrollAttestations_LineFingerprint", "LEN([LineFingerprint]) = 64 AND [LineFingerprint] NOT LIKE '%[^0-9A-F]%'");
                    table.CheckConstraint("CK_AttendancePayrollAttestations_OccurredAtUtc_Utc", "[OccurredAtUtc] IS NULL OR DATEPART(TZOFFSET, [OccurredAtUtc]) = 0");
                    table.CheckConstraint("CK_AttendancePayrollAttestations_Period", "DAY([PeriodStart]) = 1 AND YEAR([PeriodStart]) BETWEEN 1900 AND 9998");
                    table.CheckConstraint("CK_AttendancePayrollAttestations_PostedAtUtc_Utc", "[PostedAtUtc] IS NULL OR DATEPART(TZOFFSET, [PostedAtUtc]) = 0");
                    table.CheckConstraint("CK_AttendancePayrollAttestations_SourceFingerprint", "LEN([SourceFingerprint]) = 64 AND [SourceFingerprint] NOT LIKE '%[^0-9A-F]%'");
                    table.CheckConstraint("CK_AttendancePayrollAttestations_SourceRevision", "[SourceRevision] >= 1");
                    table.CheckConstraint("CK_AttendancePayrollAttestations_Stage", "([Status] = 'handoff' AND [Step] = 1 AND [PreviousAttestationId] IS NULL AND [PostedAtUtc] IS NULL) OR ([Status] = 'posted' AND [Step] = 2 AND [PreviousAttestationId] IS NOT NULL AND [PostedAtUtc] IS NOT NULL AND [PostedAtUtc] = [OccurredAtUtc]) OR ([Status] = 'settled' AND [Step] = 3 AND [PreviousAttestationId] IS NOT NULL AND [PostedAtUtc] IS NOT NULL AND [PostedAtUtc] <= [OccurredAtUtc])");
                    table.CheckConstraint("CK_AttendancePayrollAttestations_Time", "[OccurredAtUtc] <= [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_AttendancePayrollAttestations_AttendancePayrollAttestations_EmployeeId_PeriodStart_SourceRevisionId_PreviousAttestationId",
                        columns: x => new { x.EmployeeId, x.PeriodStart, x.SourceRevisionId, x.PreviousAttestationId },
                        principalSchema: "attendance",
                        principalTable: "AttendancePayrollAttestations",
                        principalColumns: new[] { "EmployeeId", "PeriodStart", "SourceRevisionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendancePayrollAttestations_AttendancePayrollInputRevisions_EmployeeId_PeriodStart_SourceRevisionId",
                        columns: x => new { x.EmployeeId, x.PeriodStart, x.SourceRevisionId },
                        principalSchema: "attendance",
                        principalTable: "AttendancePayrollInputRevisions",
                        principalColumns: new[] { "EmployeeId", "PeriodStart", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendancePayrollAttestations_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "hr",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendancePayrollAttestations_UserAccounts_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "auth",
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceLegalReviewDossiers_BindingSha256_Revision",
                schema: "attendance",
                table: "AttendanceLegalReviewDossiers",
                columns: new[] { "BindingSha256", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceLegalReviewDossiers_RecordedByAccountId",
                schema: "attendance",
                table: "AttendanceLegalReviewDossiers",
                column: "RecordedByAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceLegalReviewDossiers_SourcePolicyId_SourcePolicyVersionId",
                schema: "attendance",
                table: "AttendanceLegalReviewDossiers",
                columns: new[] { "SourcePolicyId", "SourcePolicyVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceLegalReviewDossiers_SourcePolicyVersionId_PolicySha256_BindingSha256_SupersedesId",
                schema: "attendance",
                table: "AttendanceLegalReviewDossiers",
                columns: new[] { "SourcePolicyVersionId", "PolicySha256", "BindingSha256", "SupersedesId" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceLegalReviewDossiers_SupersedesId",
                schema: "attendance",
                table: "AttendanceLegalReviewDossiers",
                column: "SupersedesId",
                unique: true,
                filter: "[SupersedesId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePayrollAttestations_ActorUserId",
                schema: "attendance",
                table: "AttendancePayrollAttestations",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePayrollAttestations_EmployeeId_PeriodStart_SourceRevisionId_PreviousAttestationId",
                schema: "attendance",
                table: "AttendancePayrollAttestations",
                columns: new[] { "EmployeeId", "PeriodStart", "SourceRevisionId", "PreviousAttestationId" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePayrollAttestations_EmployeeId_PeriodStart_Step",
                schema: "attendance",
                table: "AttendancePayrollAttestations",
                columns: new[] { "EmployeeId", "PeriodStart", "Step" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePayrollAttestations_ExternalReceiptKey_Step",
                schema: "attendance",
                table: "AttendancePayrollAttestations",
                columns: new[] { "ExternalReceiptKey", "Step" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [attendance].[AttendanceLegalReviewDossiers])
                   OR EXISTS (SELECT 1 FROM [attendance].[AttendancePayrollAttestations])
                    THROW 51000, 'Retained legal or financial evidence requires a reviewed preservation plan; downgrade refused.', 1;
                """);
            migrationBuilder.DropTable(
                name: "AttendanceLegalReviewDossiers",
                schema: "attendance");

            migrationBuilder.DropTable(
                name: "AttendancePayrollAttestations",
                schema: "attendance");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PolicyVersions_PolicyId_Id",
                schema: "hr",
                table: "PolicyVersions");
        }
    }
}
