using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cosmix.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class CompleteHrFoundationPartA : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LifecyclePolicyVersion",
                schema: "hr",
                table: "EmployeeStatusChanges",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovalRequestId",
                schema: "hr",
                table: "EmployeeSimAssignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EffectiveOn",
                schema: "hr",
                table: "EmployeeSimAssignments",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<Guid>(
                name: "IssueDocumentId",
                schema: "hr",
                table: "EmployeeSimAssignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                schema: "hr",
                table: "EmployeeSimAssignments",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReturnDocumentId",
                schema: "hr",
                table: "EmployeeSimAssignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReturnedToAccountId",
                schema: "hr",
                table: "EmployeeSimAssignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TransferredFromAssignmentId",
                schema: "hr",
                table: "EmployeeSimAssignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAt",
                schema: "hr",
                table: "Employees",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                schema: "hr",
                table: "Employees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "LastAppliedStatusChangeId",
                schema: "hr",
                table: "Employees",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ActiveScanAttemptId",
                schema: "hr",
                table: "EmployeeDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HistoryRevision",
                schema: "hr",
                table: "EmployeeDocuments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsScanInProgress",
                schema: "hr",
                table: "EmployeeDocuments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LifecycleChangedAt",
                schema: "hr",
                table: "EmployeeDocuments",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LifecycleChangedByUserId",
                schema: "hr",
                table: "EmployeeDocuments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LifecycleReason",
                schema: "hr",
                table: "EmployeeDocuments",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LifecycleStatus",
                schema: "hr",
                table: "EmployeeDocuments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ScanStartedAt",
                schema: "hr",
                table: "EmployeeDocuments",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ActionDocumentId",
                schema: "hr",
                table: "EmployeeCertifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovalRequestId",
                schema: "hr",
                table: "EmployeeCertifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EffectiveOn",
                schema: "hr",
                table: "EmployeeCertifications",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                schema: "hr",
                table: "EmployeeCertifications",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                schema: "hr",
                table: "EmployeeCertifications",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "hr",
                table: "EmployeeCertifications",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "SupersedesEmployeeCertificationId",
                schema: "hr",
                table: "EmployeeCertifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Permission",
                schema: "workflow",
                table: "ApprovalDelegations",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevocationReason",
                schema: "workflow",
                table: "ApprovalDelegations",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScopeKind",
                schema: "workflow",
                table: "ApprovalDelegations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetId",
                schema: "workflow",
                table: "ApprovalDelegations",
                type: "uniqueidentifier",
                nullable: true);

            // Initialize only newly introduced projections from retained facts; original dates,
            // status history, accounts, documents and evidence are never rewritten.
            var retainedProjectionSql = """
                UPDATE [hr].[EmployeeCertifications] SET [EffectiveOn] = [IssuedOn];
                UPDATE [hr].[EmployeeSimAssignments] SET [EffectiveOn] = COALESCE([ReturnedOn], [IssuedOn]);

                -- Only verified inactive history whose access projections are already fully
                -- reconciled may receive a marker. Residual legacy access remains unmarked
                -- for explicit upgrade review; this migration fabricates no revocation evidence.
                -- Active releases are deliberately excluded: matching cached Active alone does
                -- not prove that old grants/devices/face evidence were reconciled.
                UPDATE employee SET [LastAppliedStatusChangeId] = change.[Id]
                FROM [hr].[Employees] AS employee
                CROSS APPLY (
                    SELECT TOP (1) s.* FROM [hr].[EmployeeStatusChanges] AS s
                    WHERE s.[EmployeeId] = employee.[Id] AND s.[EffectiveAt] <= SYSUTCDATETIME()
                    ORDER BY s.[EffectiveAt] DESC, s.[RecordedAt] DESC, s.[Id]
                ) AS change
                INNER JOIN [workflow].[ApprovalRequests] AS request ON request.[Id] = change.[ApprovalRequestId]
                INNER JOIN [hr].[EmployeeOnboardingDecisions] AS onboarding ON onboarding.[Id] = employee.[ApprovedOnboardingDecisionId]
                    AND onboarding.[EmployeeId] = employee.[Id] AND onboarding.[Decision] = 1
                INNER JOIN [hr].[EmployeeSubmissions] AS submission ON submission.[Id] = onboarding.[SubmissionId]
                    AND submission.[EmployeeId] = employee.[Id] AND submission.[Version] = employee.[SubmittedVersion]
                    AND submission.[Provenance] = onboarding.[Provenance]
                WHERE employee.[LastAppliedStatusChangeId] IS NULL AND employee.[OnboardingStatus] = 2
                    AND employee.[Status] = change.[NewStatus] AND change.[NewStatus] IN (3,4,5,6)
                    AND request.[RequestType] = N'EmployeeStatusChange' AND request.[SubjectId] = employee.[Id]
                    AND request.[State] = 1 AND request.[ClosedAt] = change.[RecordedAt]
                    AND NOT EXISTS (SELECT 1 FROM [hr].[EmployeeStatusChanges] AS conflict
                        WHERE conflict.[EmployeeId] = employee.[Id] AND conflict.[EffectiveAt] = change.[EffectiveAt]
                            AND conflict.[Id] <> change.[Id])
                    AND (SELECT COUNT(*) FROM [workflow].[ApprovalDecisions] AS decision
                        WHERE decision.[ApprovalRequestId] = request.[Id]) = 1
                    AND EXISTS (SELECT 1 FROM [workflow].[ApprovalDecisions] AS decision
                        WHERE decision.[ApprovalRequestId] = request.[Id] AND decision.[Decision] = 1
                            AND decision.[ActorUserId] = change.[ChangedByUserId]
                            AND decision.[DecidedAt] = change.[RecordedAt])
                    AND NOT EXISTS (SELECT 1 FROM [attendance].[FaceEnrollments] AS face
                        WHERE face.[EmployeeId] = employee.[Id] AND face.[IsActive] = 1)
                    AND NOT EXISTS (SELECT 1 FROM [auth].[UserAccounts] AS account
                        WHERE account.[EmployeeId] = employee.[Id] AND (
                            account.[Status] NOT IN (4,5) OR account.[MobileAccessEnabled] = 1 OR account.[WebAccessEnabled] = 1
                            OR EXISTS (SELECT 1 FROM [auth].[RegisteredDevices] AS device
                                WHERE device.[UserAccountId] = account.[Id] AND device.[RevokedAt] IS NULL)
                            OR EXISTS (SELECT 1 FROM [auth].[UserRoleAssignments] AS assignment
                                WHERE assignment.[UserAccountId] = account.[Id] AND assignment.[RevokedAt] IS NULL)
                            OR EXISTS (SELECT 1 FROM [auth].[UserSessions] AS session
                                WHERE session.[UserAccountId] = account.[Id] AND session.[RevokedAt] IS NULL)
                            OR EXISTS (SELECT 1 FROM [auth].[MobileOtpChallenges] AS challenge
                                WHERE challenge.[UserAccountId] = account.[Id] AND challenge.[RevokedAt] IS NULL
                                    AND challenge.[ConsumedAt] IS NULL)));
                """;
            // Compile references to the new columns after ALTER ADD has executed. This is
            // required when the idempotent SQL script is sent as one SQL Server batch.
            migrationBuilder.Sql("EXEC(N'" + retainedProjectionSql.Replace("'", "''") + "');");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_EmployeeStatusChanges_Id_EmployeeId",
                schema: "hr",
                table: "EmployeeStatusChanges",
                columns: new[] { "Id", "EmployeeId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_EmployeeCertifications_Id_EmployeeId",
                schema: "hr",
                table: "EmployeeCertifications",
                columns: new[] { "Id", "EmployeeId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_EmployeeAssetCustodies_Id_EmployeeId",
                schema: "hr",
                table: "EmployeeAssetCustodies",
                columns: new[] { "Id", "EmployeeId" });

            migrationBuilder.CreateTable(
                name: "EmployeeArchiveChanges",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousArchived = table.Column<bool>(type: "bit", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    EmploymentStatus = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActorRoleCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeArchiveChanges", x => x.Id);
                    table.CheckConstraint("CK_EmployeeArchiveChanges_RecordedUtc", "DATEPART(TZOFFSET, [RecordedAt]) = 0");
                    table.CheckConstraint("CK_EmployeeArchiveChanges_State", "[PreviousArchived] <> [IsArchived] AND [EmploymentStatus] IN (4, 5, 6)");
                    table.ForeignKey(
                        name: "FK_EmployeeArchiveChanges_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "hr",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeAssetHistory",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeAssetCustodyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    EvidenceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Condition = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EffectiveAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReceiverAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TransferredFromCustodyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeAssetHistory", x => x.Id);
                    table.CheckConstraint("CK_EmployeeAssetHistory_Action", "([Action] = 1 AND [Revision] = 1 AND [ReceiverAccountId] IS NULL) OR ([Action] = 2 AND [Revision] = 2 AND [ReceiverAccountId] IS NOT NULL AND [TransferredFromCustodyId] IS NULL)");
                    table.CheckConstraint("CK_EmployeeAssetHistory_Time", "[RecordedAt] >= [EffectiveAt]");
                    table.ForeignKey(
                        name: "FK_EmployeeAssetHistory_ApprovalRequests_ApprovalRequestId",
                        column: x => x.ApprovalRequestId,
                        principalSchema: "workflow",
                        principalTable: "ApprovalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeAssetHistory_EmployeeAssetCustodies_EmployeeAssetCustodyId_EmployeeId",
                        columns: x => new { x.EmployeeAssetCustodyId, x.EmployeeId },
                        principalSchema: "hr",
                        principalTable: "EmployeeAssetCustodies",
                        principalColumns: new[] { "Id", "EmployeeId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeAssetHistory_EmployeeAssetCustodies_TransferredFromCustodyId",
                        column: x => x.TransferredFromCustodyId,
                        principalSchema: "hr",
                        principalTable: "EmployeeAssetCustodies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeAssetHistory_EmployeeDocuments_EvidenceDocumentId_EmployeeId",
                        columns: x => new { x.EvidenceDocumentId, x.EmployeeId },
                        principalSchema: "hr",
                        principalTable: "EmployeeDocuments",
                        principalColumns: new[] { "Id", "EmployeeId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeAssetHistory_UserAccounts_ReceiverAccountId",
                        column: x => x.ReceiverAccountId,
                        principalSchema: "auth",
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeBankVerifications",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeBankDetailsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    PreviousVerificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    RecordedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PrivateEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeBankVerifications", x => x.Id);
                    table.UniqueConstraint("AK_EmployeeBankVerifications_Id_EmployeeId_EmployeeBankDetailsId", x => new { x.Id, x.EmployeeId, x.EmployeeBankDetailsId });
                    table.CheckConstraint("CK_EmployeeBankVerifications_History", "([Version] = 1 AND [PreviousVerificationId] IS NULL) OR ([Version] > 1 AND [PreviousVerificationId] IS NOT NULL)");
                    table.CheckConstraint("CK_EmployeeBankVerifications_Predecessor", "[PreviousVerificationId] IS NULL OR [PreviousVerificationId] <> [Id]");
                    table.CheckConstraint("CK_EmployeeBankVerifications_Version", "[Version] > 0");
                    table.ForeignKey(
                        name: "FK_EmployeeBankVerifications_EmployeeBankDetails_EmployeeBankDetailsId_EmployeeId",
                        columns: x => new { x.EmployeeBankDetailsId, x.EmployeeId },
                        principalSchema: "hr",
                        principalTable: "EmployeeBankDetails",
                        principalColumns: new[] { "Id", "EmployeeId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeBankVerifications_EmployeeBankVerifications_PreviousVerificationId_EmployeeId_EmployeeBankDetailsId",
                        columns: x => new { x.PreviousVerificationId, x.EmployeeId, x.EmployeeBankDetailsId },
                        principalSchema: "hr",
                        principalTable: "EmployeeBankVerifications",
                        principalColumns: new[] { "Id", "EmployeeId", "EmployeeBankDetailsId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeBankVerifications_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "hr",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeDocumentHistory",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ScanAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BeforeState = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: false),
                    AfterState = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeDocumentHistory", x => x.Id);
                    table.CheckConstraint("CK_EmployeeDocumentHistory_Action", "[Action] BETWEEN 1 AND 9");
                    table.CheckConstraint("CK_EmployeeDocumentHistory_Revision", "[Revision] > 0");
                    table.ForeignKey(
                        name: "FK_EmployeeDocumentHistory_EmployeeDocuments_EmployeeDocumentId_EmployeeId",
                        columns: x => new { x.EmployeeDocumentId, x.EmployeeId },
                        principalSchema: "hr",
                        principalTable: "EmployeeDocuments",
                        principalColumns: new[] { "Id", "EmployeeId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeDocumentHistory_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "hr",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSimAssignments_ApprovalRequestId",
                schema: "hr",
                table: "EmployeeSimAssignments",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSimAssignments_IssueDocumentId_EmployeeId",
                schema: "hr",
                table: "EmployeeSimAssignments",
                columns: new[] { "IssueDocumentId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSimAssignments_PhoneNumber_EffectiveOn",
                schema: "hr",
                table: "EmployeeSimAssignments",
                columns: new[] { "PhoneNumber", "EffectiveOn" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSimAssignments_ReturnDocumentId_EmployeeId",
                schema: "hr",
                table: "EmployeeSimAssignments",
                columns: new[] { "ReturnDocumentId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSimAssignments_ReturnedToAccountId",
                schema: "hr",
                table: "EmployeeSimAssignments",
                column: "ReturnedToAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSimAssignments_SimReference_EffectiveOn",
                schema: "hr",
                table: "EmployeeSimAssignments",
                columns: new[] { "SimReference", "EffectiveOn" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSimAssignments_TransferredFromAssignmentId",
                schema: "hr",
                table: "EmployeeSimAssignments",
                column: "TransferredFromAssignmentId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeSimAssignments_Effective",
                schema: "hr",
                table: "EmployeeSimAssignments",
                sql: "[EffectiveOn] >= [IssuedOn] AND ([ReturnedOn] IS NULL OR [EffectiveOn] = [ReturnedOn])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeSimAssignments_ReturnEvidence",
                schema: "hr",
                table: "EmployeeSimAssignments",
                sql: "([ReturnDocumentId] IS NULL AND [ReturnedToAccountId] IS NULL) OR ([Status] = 3 AND [ReturnDocumentId] IS NOT NULL AND [ReturnedToAccountId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_IsArchived_CreatedAt_Id",
                schema: "hr",
                table: "Employees",
                columns: new[] { "IsArchived", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_LastAppliedStatusChangeId_Id",
                schema: "hr",
                table: "Employees",
                columns: new[] { "LastAppliedStatusChangeId", "Id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Employees_Archive",
                schema: "hr",
                table: "Employees",
                sql: "([IsArchived] = 0 AND [ArchivedAt] IS NULL) OR ([IsArchived] = 1 AND [ArchivedAt] IS NOT NULL AND [Status] IN (4, 5, 6))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Employees_ArchiveUtc",
                schema: "hr",
                table: "Employees",
                sql: "[ArchivedAt] IS NULL OR DATEPART(TZOFFSET, [ArchivedAt]) = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeDocuments_HistoryRevision",
                schema: "hr",
                table: "EmployeeDocuments",
                sql: "[HistoryRevision] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeDocuments_Lifecycle",
                schema: "hr",
                table: "EmployeeDocuments",
                sql: "([LifecycleStatus] = 0 AND [LifecycleChangedAt] IS NULL AND [LifecycleChangedByUserId] IS NULL AND [LifecycleReason] IS NULL) OR ([LifecycleStatus] BETWEEN 1 AND 3 AND [LifecycleChangedAt] IS NOT NULL AND [LifecycleChangedAt] >= [UploadedAt] AND [LifecycleChangedByUserId] IS NOT NULL AND [LifecycleReason] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeDocuments_ScanAttempt",
                schema: "hr",
                table: "EmployeeDocuments",
                sql: "([ActiveScanAttemptId] IS NULL AND [ScanStartedAt] IS NULL AND [IsScanInProgress] = 0) OR ([ActiveScanAttemptId] IS NOT NULL AND [ScanStartedAt] IS NOT NULL AND [ScanStartedAt] >= [UploadedAt])");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCertifications_ActionDocumentId_EmployeeId",
                schema: "hr",
                table: "EmployeeCertifications",
                columns: new[] { "ActionDocumentId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCertifications_ApprovalRequestId",
                schema: "hr",
                table: "EmployeeCertifications",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCertifications_SupersedesEmployeeCertificationId",
                schema: "hr",
                table: "EmployeeCertifications",
                column: "SupersedesEmployeeCertificationId",
                unique: true,
                filter: "[SupersedesEmployeeCertificationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCertifications_SupersedesEmployeeCertificationId_EmployeeId",
                schema: "hr",
                table: "EmployeeCertifications",
                columns: new[] { "SupersedesEmployeeCertificationId", "EmployeeId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeCertifications_Revision",
                schema: "hr",
                table: "EmployeeCertifications",
                sql: "[Revision] > 0 AND (([Revision] = 1 AND [SupersedesEmployeeCertificationId] IS NULL) OR ([Revision] > 1 AND [SupersedesEmployeeCertificationId] IS NOT NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeCertifications_Status",
                schema: "hr",
                table: "EmployeeCertifications",
                sql: "[Status] BETWEEN 1 AND 3 AND [EffectiveOn] >= [IssuedOn]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApprovalDelegations_RevokeReason",
                schema: "workflow",
                table: "ApprovalDelegations",
                sql: "[Permission] IS NULL OR [RevokedAt] IS NULL OR ([RevocationReason] IS NOT NULL AND LEN(LTRIM(RTRIM([RevocationReason]))) > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApprovalDelegations_Scope",
                schema: "workflow",
                table: "ApprovalDelegations",
                sql: "([Permission] IS NULL AND [ScopeKind] IS NULL AND [TargetId] IS NULL) OR ([Permission] IS NOT NULL AND [ScopeKind] IS NOT NULL AND (([ScopeKind] = 7 AND [TargetId] IS NULL) OR ([ScopeKind] BETWEEN 2 AND 6 AND [TargetId] IS NOT NULL)))");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeArchiveChanges_EmployeeId_RecordedAt_Id",
                schema: "hr",
                table: "EmployeeArchiveChanges",
                columns: new[] { "EmployeeId", "RecordedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAssetHistory_ApprovalRequestId",
                schema: "hr",
                table: "EmployeeAssetHistory",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAssetHistory_EmployeeAssetCustodyId_EmployeeId",
                schema: "hr",
                table: "EmployeeAssetHistory",
                columns: new[] { "EmployeeAssetCustodyId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAssetHistory_EmployeeAssetCustodyId_Revision",
                schema: "hr",
                table: "EmployeeAssetHistory",
                columns: new[] { "EmployeeAssetCustodyId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAssetHistory_EmployeeId_RecordedAt",
                schema: "hr",
                table: "EmployeeAssetHistory",
                columns: new[] { "EmployeeId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAssetHistory_EvidenceDocumentId_EmployeeId",
                schema: "hr",
                table: "EmployeeAssetHistory",
                columns: new[] { "EvidenceDocumentId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAssetHistory_ReceiverAccountId",
                schema: "hr",
                table: "EmployeeAssetHistory",
                column: "ReceiverAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAssetHistory_TransferredFromCustodyId",
                schema: "hr",
                table: "EmployeeAssetHistory",
                column: "TransferredFromCustodyId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeBankVerifications_EmployeeBankDetailsId_EmployeeId",
                schema: "hr",
                table: "EmployeeBankVerifications",
                columns: new[] { "EmployeeBankDetailsId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeBankVerifications_EmployeeBankDetailsId_Version",
                schema: "hr",
                table: "EmployeeBankVerifications",
                columns: new[] { "EmployeeBankDetailsId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeBankVerifications_EmployeeId",
                schema: "hr",
                table: "EmployeeBankVerifications",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeBankVerifications_PreviousVerificationId",
                schema: "hr",
                table: "EmployeeBankVerifications",
                column: "PreviousVerificationId",
                unique: true,
                filter: "[PreviousVerificationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeBankVerifications_PreviousVerificationId_EmployeeId_EmployeeBankDetailsId",
                schema: "hr",
                table: "EmployeeBankVerifications",
                columns: new[] { "PreviousVerificationId", "EmployeeId", "EmployeeBankDetailsId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocumentHistory_EmployeeDocumentId_EmployeeId",
                schema: "hr",
                table: "EmployeeDocumentHistory",
                columns: new[] { "EmployeeDocumentId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocumentHistory_EmployeeDocumentId_Revision",
                schema: "hr",
                table: "EmployeeDocumentHistory",
                columns: new[] { "EmployeeDocumentId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocumentHistory_EmployeeId_RecordedAt",
                schema: "hr",
                table: "EmployeeDocumentHistory",
                columns: new[] { "EmployeeId", "RecordedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeCertifications_ApprovalRequests_ApprovalRequestId",
                schema: "hr",
                table: "EmployeeCertifications",
                column: "ApprovalRequestId",
                principalSchema: "workflow",
                principalTable: "ApprovalRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeCertifications_EmployeeCertifications_SupersedesEmployeeCertificationId_EmployeeId",
                schema: "hr",
                table: "EmployeeCertifications",
                columns: new[] { "SupersedesEmployeeCertificationId", "EmployeeId" },
                principalSchema: "hr",
                principalTable: "EmployeeCertifications",
                principalColumns: new[] { "Id", "EmployeeId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeCertifications_EmployeeDocuments_ActionDocumentId_EmployeeId",
                schema: "hr",
                table: "EmployeeCertifications",
                columns: new[] { "ActionDocumentId", "EmployeeId" },
                principalSchema: "hr",
                principalTable: "EmployeeDocuments",
                principalColumns: new[] { "Id", "EmployeeId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_EmployeeStatusChanges_LastAppliedStatusChangeId_Id",
                schema: "hr",
                table: "Employees",
                columns: new[] { "LastAppliedStatusChangeId", "Id" },
                principalSchema: "hr",
                principalTable: "EmployeeStatusChanges",
                principalColumns: new[] { "Id", "EmployeeId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeSimAssignments_ApprovalRequests_ApprovalRequestId",
                schema: "hr",
                table: "EmployeeSimAssignments",
                column: "ApprovalRequestId",
                principalSchema: "workflow",
                principalTable: "ApprovalRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeSimAssignments_EmployeeDocuments_IssueDocumentId_EmployeeId",
                schema: "hr",
                table: "EmployeeSimAssignments",
                columns: new[] { "IssueDocumentId", "EmployeeId" },
                principalSchema: "hr",
                principalTable: "EmployeeDocuments",
                principalColumns: new[] { "Id", "EmployeeId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeSimAssignments_EmployeeDocuments_ReturnDocumentId_EmployeeId",
                schema: "hr",
                table: "EmployeeSimAssignments",
                columns: new[] { "ReturnDocumentId", "EmployeeId" },
                principalSchema: "hr",
                principalTable: "EmployeeDocuments",
                principalColumns: new[] { "Id", "EmployeeId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeSimAssignments_EmployeeSimAssignments_TransferredFromAssignmentId",
                schema: "hr",
                table: "EmployeeSimAssignments",
                column: "TransferredFromAssignmentId",
                principalSchema: "hr",
                principalTable: "EmployeeSimAssignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeSimAssignments_UserAccounts_ReturnedToAccountId",
                schema: "hr",
                table: "EmployeeSimAssignments",
                column: "ReturnedToAccountId",
                principalSchema: "auth",
                principalTable: "UserAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [hr].[EmployeeArchiveChanges])
                    OR EXISTS (SELECT 1 FROM [hr].[EmployeeAssetHistory])
                    OR EXISTS (SELECT 1 FROM [hr].[EmployeeBankVerifications])
                    OR EXISTS (SELECT 1 FROM [hr].[EmployeeDocumentHistory])
                    OR EXISTS (SELECT 1 FROM [hr].[Employees] WHERE [IsArchived] = 1)
                    OR EXISTS (SELECT 1 FROM [hr].[EmployeeDocuments] WHERE [HistoryRevision] > 0 OR [LifecycleStatus] <> 0 OR [IsScanInProgress] = 1)
                    OR EXISTS (SELECT 1 FROM [hr].[EmployeeStatusChanges] WHERE [LifecyclePolicyVersion] IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM [hr].[EmployeeCertifications] WHERE [ApprovalRequestId] IS NOT NULL OR [Reason] IS NOT NULL
                        OR [Revision] <> 1 OR [Status] <> 1 OR [SupersedesEmployeeCertificationId] IS NOT NULL OR [EffectiveOn] <> [IssuedOn])
                    OR EXISTS (SELECT 1 FROM [hr].[EmployeeSimAssignments] WHERE [ApprovalRequestId] IS NOT NULL OR [Reason] IS NOT NULL
                        OR [IssueDocumentId] IS NOT NULL OR [ReturnDocumentId] IS NOT NULL OR [TransferredFromAssignmentId] IS NOT NULL
                        OR [EffectiveOn] <> COALESCE([ReturnedOn], [IssuedOn]))
                    OR EXISTS (SELECT 1 FROM [workflow].[ApprovalDelegations] WHERE [Permission] IS NOT NULL OR [ScopeKind] IS NOT NULL)
                    THROW 51023, 'Retained Part A evidence requires a reviewed preservation plan; downgrade refused.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeCertifications_ApprovalRequests_ApprovalRequestId",
                schema: "hr",
                table: "EmployeeCertifications");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeCertifications_EmployeeCertifications_SupersedesEmployeeCertificationId_EmployeeId",
                schema: "hr",
                table: "EmployeeCertifications");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeCertifications_EmployeeDocuments_ActionDocumentId_EmployeeId",
                schema: "hr",
                table: "EmployeeCertifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_EmployeeStatusChanges_LastAppliedStatusChangeId_Id",
                schema: "hr",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeSimAssignments_ApprovalRequests_ApprovalRequestId",
                schema: "hr",
                table: "EmployeeSimAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeSimAssignments_EmployeeDocuments_IssueDocumentId_EmployeeId",
                schema: "hr",
                table: "EmployeeSimAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeSimAssignments_EmployeeDocuments_ReturnDocumentId_EmployeeId",
                schema: "hr",
                table: "EmployeeSimAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeSimAssignments_EmployeeSimAssignments_TransferredFromAssignmentId",
                schema: "hr",
                table: "EmployeeSimAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeSimAssignments_UserAccounts_ReturnedToAccountId",
                schema: "hr",
                table: "EmployeeSimAssignments");

            migrationBuilder.DropTable(
                name: "EmployeeArchiveChanges",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "EmployeeAssetHistory",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "EmployeeBankVerifications",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "EmployeeDocumentHistory",
                schema: "hr");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_EmployeeStatusChanges_Id_EmployeeId",
                schema: "hr",
                table: "EmployeeStatusChanges");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeSimAssignments_ApprovalRequestId",
                schema: "hr",
                table: "EmployeeSimAssignments");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeSimAssignments_IssueDocumentId_EmployeeId",
                schema: "hr",
                table: "EmployeeSimAssignments");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeSimAssignments_PhoneNumber_EffectiveOn",
                schema: "hr",
                table: "EmployeeSimAssignments");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeSimAssignments_ReturnDocumentId_EmployeeId",
                schema: "hr",
                table: "EmployeeSimAssignments");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeSimAssignments_ReturnedToAccountId",
                schema: "hr",
                table: "EmployeeSimAssignments");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeSimAssignments_SimReference_EffectiveOn",
                schema: "hr",
                table: "EmployeeSimAssignments");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeSimAssignments_TransferredFromAssignmentId",
                schema: "hr",
                table: "EmployeeSimAssignments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeSimAssignments_Effective",
                schema: "hr",
                table: "EmployeeSimAssignments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeSimAssignments_ReturnEvidence",
                schema: "hr",
                table: "EmployeeSimAssignments");

            migrationBuilder.DropIndex(
                name: "IX_Employees_IsArchived_CreatedAt_Id",
                schema: "hr",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_LastAppliedStatusChangeId_Id",
                schema: "hr",
                table: "Employees");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Employees_Archive",
                schema: "hr",
                table: "Employees");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Employees_ArchiveUtc",
                schema: "hr",
                table: "Employees");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeDocuments_HistoryRevision",
                schema: "hr",
                table: "EmployeeDocuments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeDocuments_Lifecycle",
                schema: "hr",
                table: "EmployeeDocuments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeDocuments_ScanAttempt",
                schema: "hr",
                table: "EmployeeDocuments");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_EmployeeCertifications_Id_EmployeeId",
                schema: "hr",
                table: "EmployeeCertifications");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeCertifications_ActionDocumentId_EmployeeId",
                schema: "hr",
                table: "EmployeeCertifications");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeCertifications_ApprovalRequestId",
                schema: "hr",
                table: "EmployeeCertifications");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeCertifications_SupersedesEmployeeCertificationId",
                schema: "hr",
                table: "EmployeeCertifications");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeCertifications_SupersedesEmployeeCertificationId_EmployeeId",
                schema: "hr",
                table: "EmployeeCertifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeCertifications_Revision",
                schema: "hr",
                table: "EmployeeCertifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeCertifications_Status",
                schema: "hr",
                table: "EmployeeCertifications");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_EmployeeAssetCustodies_Id_EmployeeId",
                schema: "hr",
                table: "EmployeeAssetCustodies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ApprovalDelegations_RevokeReason",
                schema: "workflow",
                table: "ApprovalDelegations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ApprovalDelegations_Scope",
                schema: "workflow",
                table: "ApprovalDelegations");

            migrationBuilder.DropColumn(
                name: "LifecyclePolicyVersion",
                schema: "hr",
                table: "EmployeeStatusChanges");

            migrationBuilder.DropColumn(
                name: "ApprovalRequestId",
                schema: "hr",
                table: "EmployeeSimAssignments");

            migrationBuilder.DropColumn(
                name: "EffectiveOn",
                schema: "hr",
                table: "EmployeeSimAssignments");

            migrationBuilder.DropColumn(
                name: "IssueDocumentId",
                schema: "hr",
                table: "EmployeeSimAssignments");

            migrationBuilder.DropColumn(
                name: "Reason",
                schema: "hr",
                table: "EmployeeSimAssignments");

            migrationBuilder.DropColumn(
                name: "ReturnDocumentId",
                schema: "hr",
                table: "EmployeeSimAssignments");

            migrationBuilder.DropColumn(
                name: "ReturnedToAccountId",
                schema: "hr",
                table: "EmployeeSimAssignments");

            migrationBuilder.DropColumn(
                name: "TransferredFromAssignmentId",
                schema: "hr",
                table: "EmployeeSimAssignments");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                schema: "hr",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                schema: "hr",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "LastAppliedStatusChangeId",
                schema: "hr",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ActiveScanAttemptId",
                schema: "hr",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "HistoryRevision",
                schema: "hr",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "IsScanInProgress",
                schema: "hr",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "LifecycleChangedAt",
                schema: "hr",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "LifecycleChangedByUserId",
                schema: "hr",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "LifecycleReason",
                schema: "hr",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "LifecycleStatus",
                schema: "hr",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "ScanStartedAt",
                schema: "hr",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "ActionDocumentId",
                schema: "hr",
                table: "EmployeeCertifications");

            migrationBuilder.DropColumn(
                name: "ApprovalRequestId",
                schema: "hr",
                table: "EmployeeCertifications");

            migrationBuilder.DropColumn(
                name: "EffectiveOn",
                schema: "hr",
                table: "EmployeeCertifications");

            migrationBuilder.DropColumn(
                name: "Reason",
                schema: "hr",
                table: "EmployeeCertifications");

            migrationBuilder.DropColumn(
                name: "Revision",
                schema: "hr",
                table: "EmployeeCertifications");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "hr",
                table: "EmployeeCertifications");

            migrationBuilder.DropColumn(
                name: "SupersedesEmployeeCertificationId",
                schema: "hr",
                table: "EmployeeCertifications");

            migrationBuilder.DropColumn(
                name: "Permission",
                schema: "workflow",
                table: "ApprovalDelegations");

            migrationBuilder.DropColumn(
                name: "RevocationReason",
                schema: "workflow",
                table: "ApprovalDelegations");

            migrationBuilder.DropColumn(
                name: "ScopeKind",
                schema: "workflow",
                table: "ApprovalDelegations");

            migrationBuilder.DropColumn(
                name: "TargetId",
                schema: "workflow",
                table: "ApprovalDelegations");
        }
    }
}
