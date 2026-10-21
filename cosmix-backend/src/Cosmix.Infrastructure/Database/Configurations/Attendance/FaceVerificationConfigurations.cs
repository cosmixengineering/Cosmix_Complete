using Cosmix.Domain.Attendance.FaceVerification;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Attendance;

public sealed class FaceEnrollmentConfiguration : IEntityTypeConfiguration<FaceEnrollment>
{
    public void Configure(EntityTypeBuilder<FaceEnrollment> b)
    {
        AttendanceMapping.Record(b, "FaceEnrollments", ("Revocation", "([IsActive] = 1 AND [RevokedAtUtc] IS NULL AND [RevokedBy] IS NULL AND [RevocationReason] IS NULL) OR ([IsActive] = 0 AND [RevokedAtUtc] IS NOT NULL AND [RevokedAtUtc] >= [EnrolledAtUtc] AND [RevokedBy] IS NOT NULL AND [RevocationReason] IS NOT NULL)"));
        AttendanceMapping.Strings(b, ("EncryptedTemplateReference", 500), ("EncryptionKeyReference", 200), ("Provider", 100), ("ProviderVersion", 100),
            ("EnrolledBy", 200), ("DeviceReference", 200), ("Reason", 1000), ("CorrelationId", 100), ("RevokedBy", 200), ("RevocationReason", 1000));
        b.HasIndex(x => x.EmployeeId).IsUnique().HasFilter("[IsActive] = 1");
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FaceEnrollment>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.ReplacesEnrollmentId }).HasPrincipalKey(x => new { x.EmployeeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FaceVerificationAttemptConfiguration : IEntityTypeConfiguration<FaceVerificationAttempt>
{
    public void Configure(EntityTypeBuilder<FaceVerificationAttempt> b)
    {
        AttendanceMapping.Record(b, "FaceVerificationAttempts", ("Window", "[ExpiresAtUtc] > [IssuedAtUtc]"), ("Outcome", "[Outcome] BETWEEN 1 AND 5"),
            ("Digest", "LEN([ChallengeSha256]) = 64 AND [ChallengeSha256] NOT LIKE '%[^0-9A-F]%'"),
            ("Completion", "([Outcome] = 1 AND [CompletedAtUtc] IS NULL AND [LivenessPassed] IS NULL AND [MatchPassed] IS NULL AND [ResultReasonCode] IS NULL) OR ([Outcome] BETWEEN 2 AND 4 AND [CompletedAtUtc] IS NOT NULL AND [CompletedAtUtc] >= [IssuedAtUtc] AND [CompletedAtUtc] < [ExpiresAtUtc] AND [LivenessPassed] IS NOT NULL AND [MatchPassed] IS NOT NULL AND [ResultReasonCode] IS NOT NULL) OR ([Outcome] = 5 AND [CompletedAtUtc] IS NOT NULL AND [CompletedAtUtc] >= [ExpiresAtUtc] AND [LivenessPassed] IS NOT NULL AND [MatchPassed] IS NOT NULL AND [LivenessPassed] = 0 AND [MatchPassed] = 0 AND [ResultReasonCode] IS NOT NULL)"),
            ("Passed", "[Outcome] <> 2 OR ([LivenessPassed] = 1 AND [MatchPassed] = 1)"),
            ("Consumption", "([ConsumedAtUtc] IS NULL AND [ConsumedByPunchId] IS NULL) OR ([ConsumedAtUtc] IS NOT NULL AND [ConsumedByPunchId] IS NOT NULL AND [Outcome] = 2 AND [ConsumedAtUtc] >= [CompletedAtUtc] AND [ConsumedAtUtc] < [ExpiresAtUtc])"));
        AttendanceMapping.Strings(b, ("RegisteredDeviceId", 200), ("PunchIdempotencyKey", 100), ("Provider", 100), ("ProviderVersion", 100), ("CorrelationId", 100), ("ResultReasonCode", 100));
        b.Property(x => x.ChallengeSha256).HasMaxLength(64).IsFixedLength().IsUnicode(false).UseCollation("Latin1_General_100_BIN2");
        b.Property(x => x.RegisteredDeviceId).UseCollation("Latin1_General_100_BIN2"); b.Property(x => x.PunchIdempotencyKey).UseCollation("Latin1_General_100_BIN2");
        b.HasIndex(x => x.ChallengeSha256).IsUnique(); b.HasIndex(x => x.ConsumedByPunchId).IsUnique().HasFilter("[ConsumedByPunchId] IS NOT NULL");
        b.HasIndex(x => new { x.EmployeeId, x.PunchIdempotencyKey, x.IssuedAtUtc });
        b.HasOne(x => x.Enrollment).WithMany().HasForeignKey(x => new { x.EmployeeId, x.FaceEnrollmentId }).HasPrincipalKey(x => new { x.EmployeeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.UserAccountId }).HasPrincipalKey(x => new { x.EmployeeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Device).WithMany().HasForeignKey(x => new { x.UserAccountId, x.RegisteredDeviceKey }).HasPrincipalKey(x => new { x.UserAccountId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<RawPunchEvent>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.RegisteredDeviceId, x.PunchIdempotencyKey, x.ConsumedByPunchId })
            .HasPrincipalKey(x => new { x.EmployeeId, x.RegisteredDeviceId, x.IdempotencyKey, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FaceVerificationExceptionConfiguration : IEntityTypeConfiguration<FaceVerificationException>
{
    public void Configure(EntityTypeBuilder<FaceVerificationException> b)
    {
        AttendanceMapping.Record(b, "FaceVerificationExceptions", ("Dates", "[EffectiveTo] > [EffectiveFrom]"));
        AttendanceMapping.Strings(b, ("AlternativeMethodCode", 100), ("PrivateEvidenceReference", 500), ("RequestedBy", 200));
        b.HasIndex(x => new { x.EmployeeId, x.EffectiveFrom });
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApprovalRequest>().WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
