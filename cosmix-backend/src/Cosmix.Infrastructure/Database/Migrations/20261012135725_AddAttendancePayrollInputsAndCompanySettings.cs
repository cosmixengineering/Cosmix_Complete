using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendancePayrollInputsAndCompanySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendancePayrollInputRevisions",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    SupersedesRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CalculationVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InputFingerprint = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ProtectedEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendancePayrollInputRevisions", x => x.Id);
                    table.UniqueConstraint("AK_AttendancePayrollInputRevisions_EmployeeId_PeriodStart_Id", x => new { x.EmployeeId, x.PeriodStart, x.Id });
                    table.CheckConstraint("CK_AttendancePayrollInputRevisions_CreatedAtUtc_Utc", "[CreatedAtUtc] IS NULL OR DATEPART(TZOFFSET, [CreatedAtUtc]) = 0");
                    table.CheckConstraint("CK_AttendancePayrollInputRevisions_Fingerprint", "LEN([InputFingerprint]) = 64 AND [InputFingerprint] NOT LIKE '%[^0-9A-F]%'");
                    table.CheckConstraint("CK_AttendancePayrollInputRevisions_Period", "DAY([PeriodStart]) = 1 AND YEAR([PeriodStart]) BETWEEN 1900 AND 9998");
                    table.CheckConstraint("CK_AttendancePayrollInputRevisions_Revision", "([Revision] = 1 AND [SupersedesRevisionId] IS NULL) OR ([Revision] > 1 AND [SupersedesRevisionId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_AttendancePayrollInputRevisions_AttendancePayrollInputRevisions_EmployeeId_PeriodStart_SupersedesRevisionId",
                        columns: x => new { x.EmployeeId, x.PeriodStart, x.SupersedesRevisionId },
                        principalSchema: "attendance",
                        principalTable: "AttendancePayrollInputRevisions",
                        principalColumns: new[] { "EmployeeId", "PeriodStart", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendancePayrollInputRevisions_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "hr",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendancePayrollInputRevisions_UserAccounts_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "auth",
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompanyProfiles",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SingletonKey = table.Column<byte>(type: "tinyint", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BusinessType = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    HeadOfficeBranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HeadOfficeCityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    JurisdictionCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyProfiles", x => x.Id);
                    table.CheckConstraint("CK_CompanyProfiles_Coordinates", "([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180)");
                    table.CheckConstraint("CK_CompanyProfiles_Jurisdiction", "[JurisdictionCode] IN ('PK-PB','PK-SD','PK-KP','PK-BA','PK-ICT')");
                    table.CheckConstraint("CK_CompanyProfiles_Revision", "[Revision] > 0");
                    table.CheckConstraint("CK_CompanyProfiles_Singleton", "[SingletonKey] = 1 AND [Id] = '0871aaf1-f14d-4dc6-a9ba-03eeb2c3047f'");
                    table.ForeignKey(
                        name: "FK_CompanyProfiles_Branches_HeadOfficeBranchId",
                        column: x => x.HeadOfficeBranchId,
                        principalSchema: "hr",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanyProfiles_Cities_HeadOfficeCityId",
                        column: x => x.HeadOfficeCityId,
                        principalSchema: "hr",
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompanySiteJurisdictionVersions",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    JurisdictionCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ConfigurationRevision = table.Column<int>(type: "int", nullable: false),
                    SupersedesVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanySiteJurisdictionVersions", x => x.Id);
                    table.CheckConstraint("CK_CompanySiteJurisdictions_Date", "[EffectiveFrom] >= '19010101' AND [EffectiveFrom] < '99980101'");
                    table.CheckConstraint("CK_CompanySiteJurisdictions_Jurisdiction", "[JurisdictionCode] IN ('PK-PB','PK-SD','PK-KP','PK-BA','PK-ICT')");
                    table.CheckConstraint("CK_CompanySiteJurisdictions_Revision", "[ConfigurationRevision] > 1");
                    table.CheckConstraint("CK_CompanySiteJurisdictions_Supersession", "[SupersedesVersionId] IS NULL OR [SupersedesVersionId] <> [Id]");
                    table.ForeignKey(
                        name: "FK_CompanySiteJurisdictionVersions_CompanyProfiles_CompanyProfileId",
                        column: x => x.CompanyProfileId,
                        principalSchema: "hr",
                        principalTable: "CompanyProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanySiteJurisdictionVersions_CompanySiteJurisdictionVersions_SupersedesVersionId",
                        column: x => x.SupersedesVersionId,
                        principalSchema: "hr",
                        principalTable: "CompanySiteJurisdictionVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanySiteJurisdictionVersions_Sites_SiteId",
                        column: x => x.SiteId,
                        principalSchema: "attendance",
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePayrollInputRevisions_ActorUserId",
                schema: "attendance",
                table: "AttendancePayrollInputRevisions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePayrollInputRevisions_EmployeeId_PeriodStart_Revision",
                schema: "attendance",
                table: "AttendancePayrollInputRevisions",
                columns: new[] { "EmployeeId", "PeriodStart", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePayrollInputRevisions_EmployeeId_PeriodStart_SupersedesRevisionId",
                schema: "attendance",
                table: "AttendancePayrollInputRevisions",
                columns: new[] { "EmployeeId", "PeriodStart", "SupersedesRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyProfiles_HeadOfficeBranchId",
                schema: "hr",
                table: "CompanyProfiles",
                column: "HeadOfficeBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyProfiles_HeadOfficeCityId",
                schema: "hr",
                table: "CompanyProfiles",
                column: "HeadOfficeCityId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyProfiles_SingletonKey",
                schema: "hr",
                table: "CompanyProfiles",
                column: "SingletonKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanySiteJurisdictionVersions_CompanyProfileId_ConfigurationRevision",
                schema: "hr",
                table: "CompanySiteJurisdictionVersions",
                columns: new[] { "CompanyProfileId", "ConfigurationRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanySiteJurisdictionVersions_SiteId_EffectiveFrom",
                schema: "hr",
                table: "CompanySiteJurisdictionVersions",
                columns: new[] { "SiteId", "EffectiveFrom" },
                unique: true,
                filter: "[SupersedesVersionId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CompanySiteJurisdictionVersions_SupersedesVersionId",
                schema: "hr",
                table: "CompanySiteJurisdictionVersions",
                column: "SupersedesVersionId",
                unique: true,
                filter: "[SupersedesVersionId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Retained financial and company history must not disappear during a rollback.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [attendance].[AttendancePayrollInputRevisions])
                   OR EXISTS (SELECT 1 FROM [hr].[CompanySiteJurisdictionVersions])
                   OR EXISTS (SELECT 1 FROM [hr].[CompanyProfiles])
                    THROW 51000, 'Cannot downgrade retained payroll inputs or company settings.', 1;
                """);

            migrationBuilder.DropTable(
                name: "AttendancePayrollInputRevisions",
                schema: "attendance");

            migrationBuilder.DropTable(
                name: "CompanySiteJurisdictionVersions",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "CompanyProfiles",
                schema: "hr");
        }
    }
}
